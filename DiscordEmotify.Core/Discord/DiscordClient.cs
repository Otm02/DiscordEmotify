using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exceptions;
using DiscordChatExporter.Core.Utils;
using DiscordChatExporter.Core.Utils.Extensions;
using Gress;
using JsonExtensions.Http;
using JsonExtensions.Reading;

namespace DiscordChatExporter.Core.Discord;

public class DiscordClient(
    string token,
    RateLimitPreference rateLimitPreference = RateLimitPreference.RespectAll
)
{
    private readonly Uri _baseUri = new("https://discord.com/api/v10/", UriKind.Absolute);
    private TokenKind? _resolvedTokenKind;

    private async ValueTask<HttpResponseMessage> GetResponseAsync(
        string url,
        TokenKind tokenKind,
        CancellationToken cancellationToken = default
    )
    {
        return await Http.ResponseResiliencePipeline.ExecuteAsync(
            async innerCancellationToken =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, url));

                // Don't validate because the token can have special characters
                // https://github.com/Tyrrrz/DiscordChatExporter/issues/828
                request.Headers.TryAddWithoutValidation(
                    "Authorization",
                    tokenKind == TokenKind.Bot ? $"Bot {token}" : token
                );

                var response = await Http.Client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    innerCancellationToken
                );

                // Discord has advisory rate limits (communicated via response headers), but they are typically
                // way stricter than the actual rate limits enforced by the server.
                // The user may choose to ignore the advisory rate limits and only retry on hard rate limits,
                // if they want to prioritize speed over compliance (and safety of their account/bot).
                // https://github.com/Tyrrrz/DiscordChatExporter/issues/1021
                if (rateLimitPreference.IsRespectedFor(tokenKind))
                {
                    var remainingRequestCount = response
                        .Headers.TryGetValue("X-RateLimit-Remaining")
                        ?.Pipe(s => int.Parse(s, CultureInfo.InvariantCulture));

                    var resetAfterDelay = response
                        .Headers.TryGetValue("X-RateLimit-Reset-After")
                        ?.Pipe(s => double.Parse(s, CultureInfo.InvariantCulture))
                        .Pipe(TimeSpan.FromSeconds);

                    // If this was the last request available before hitting the rate limit,
                    // wait out the reset time so that future requests can succeed.
                    // This may add an unnecessary delay in case the user doesn't intend to
                    // make any more requests, but implementing a smarter solution would
                    // require properly keeping track of Discord's global/per-route/per-resource
                    // rate limits and that's just way too much effort.
                    // https://discord.com/developers/docs/topics/rate-limits
                    if (remainingRequestCount <= 0 && resetAfterDelay is not null)
                    {
                        var delay =
                            // Adding a small buffer to the reset time reduces the chance of getting
                            // rate limited again, because it allows for more requests to be released.
                            (resetAfterDelay.Value + TimeSpan.FromSeconds(1))
                            // Sometimes Discord returns an absurdly high value for the reset time, which
                            // is not actually enforced by the server. So we cap it at a reasonable value.
                            .Clamp(TimeSpan.Zero, TimeSpan.FromSeconds(60));

                        await Task.Delay(delay, innerCancellationToken);
                    }
                }

                return response;
            },
            cancellationToken
        );
    }

    private async ValueTask<TokenKind> ResolveTokenKindAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_resolvedTokenKind is TokenKind cached)
            return cached;

        async ValueTask<bool> IsAuthorizedAsync(TokenKind kind)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(_baseUri, "users/@me")
            );
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                kind == TokenKind.Bot ? $"Bot {token}" : token
            );

            using var response = await Http.Client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }

        if (await IsAuthorizedAsync(TokenKind.Bot))
        {
            _resolvedTokenKind = TokenKind.Bot;
            return TokenKind.Bot;
        }

        if (await IsAuthorizedAsync(TokenKind.User))
        {
            _resolvedTokenKind = TokenKind.User;
            return TokenKind.User;
        }

        throw new DiscordChatExporterException("Authentication token is invalid.", true);
    }

    public async ValueTask AddReactionAsync(
        Snowflake channelId,
        Snowflake messageId,
        Emoji emoji,
        CancellationToken cancellationToken = default
    )
    {
        var tokenKind = await ResolveTokenKindAsync(cancellationToken);

        var reactionName = emoji.Id is not null
            // Custom emoji: name:id
            ? emoji.Name + ':' + emoji.Id
            // Standard emoji: prefer actual Unicode if available
            : (EmojiIndex.TryGetName(emoji.Name) ?? emoji.Name);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = new Uri(
                _baseUri,
                $"channels/{channelId}/messages/{messageId}/reactions/{Uri.EscapeDataString(reactionName)}/@me"
            );

            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                tokenKind == TokenKind.Bot ? $"Bot {token}" : token
            );

            using var response = await Http.Client.SendAsync(request, cancellationToken);

            // Hard limit handling
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                TimeSpan? retryAfter =
                    response.Headers.RetryAfter?.Delta
                    ?? response
                        .Headers.TryGetValue("X-RateLimit-Reset-After")
                        ?.Pipe(s => double.Parse(s, CultureInfo.InvariantCulture))
                        .Pipe(TimeSpan.FromSeconds);

                if (retryAfter is null)
                {
                    try
                    {
                        var body = await response.Content.ReadAsJsonAsync(cancellationToken);
                        if (body.TryGetProperty("retry_after", out var retryProp))
                            retryAfter = TimeSpan.FromSeconds(retryProp.GetDouble());
                    }
                    catch
                    {
                        // ignore parse errors
                    }
                }

                retryAfter ??= TimeSpan.FromSeconds(1);
                var wait = (retryAfter.Value + TimeSpan.FromMilliseconds(200)).Clamp(
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(60)
                );
                await Task.Delay(wait, cancellationToken);
                continue;
            }

            // Advisory limit handling
            if (rateLimitPreference.IsRespectedFor(tokenKind))
            {
                var remainingRequestCount = response
                    .Headers.TryGetValue("X-RateLimit-Remaining")
                    ?.Pipe(s => int.Parse(s, CultureInfo.InvariantCulture));

                var resetAfterDelay = response
                    .Headers.TryGetValue("X-RateLimit-Reset-After")
                    ?.Pipe(s => double.Parse(s, CultureInfo.InvariantCulture))
                    .Pipe(TimeSpan.FromSeconds);

                if (remainingRequestCount <= 0 && resetAfterDelay is not null)
                {
                    var delay = (resetAfterDelay.Value + TimeSpan.FromSeconds(1)).Clamp(
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(60)
                    );
                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (response.IsSuccessStatusCode)
                return;

            // Non-success
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new DiscordChatExporterException(
                        "Authentication token is invalid.",
                        true
                    );
                case HttpStatusCode.Forbidden:
                    throw new DiscordChatExporterException("Failed to add reaction: forbidden.");
                case HttpStatusCode.NotFound:
                    throw new DiscordChatExporterException("Failed to add reaction: not found.");
                default:
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new DiscordChatExporterException(
                        $"Failed to add reaction: {response.StatusCode.ToString().ToSpaceSeparatedWords().ToLowerInvariant()}. \nResponse content: {content}",
                        true
                    );
            }
        }
    }

    public async ValueTask RemoveReactionAsync(
        Snowflake channelId,
        Snowflake messageId,
        Emoji emoji,
        CancellationToken cancellationToken = default
    )
    {
        var tokenKind = await ResolveTokenKindAsync(cancellationToken);

        var reactionName = emoji.Id is not null
            ? emoji.Name + ':' + emoji.Id
            : (EmojiIndex.TryGetName(emoji.Name) ?? emoji.Name);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = new Uri(
                _baseUri,
                $"channels/{channelId}/messages/{messageId}/reactions/{Uri.EscapeDataString(reactionName)}/@me"
            );

            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                tokenKind == TokenKind.Bot ? $"Bot {token}" : token
            );

            using var response = await Http.Client.SendAsync(request, cancellationToken);

            // Hard limit handling
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                TimeSpan? retryAfter =
                    response.Headers.RetryAfter?.Delta
                    ?? response
                        .Headers.TryGetValue("X-RateLimit-Reset-After")
                        ?.Pipe(s => double.Parse(s, CultureInfo.InvariantCulture))
                        .Pipe(TimeSpan.FromSeconds);

                if (retryAfter is null)
                {
                    try
                    {
                        var body = await response.Content.ReadAsJsonAsync(cancellationToken);
                        if (body.TryGetProperty("retry_after", out var retryProp))
                            retryAfter = TimeSpan.FromSeconds(retryProp.GetDouble());
                    }
                    catch
                    {
                        // ignore parse errors
                    }
                }

                retryAfter ??= TimeSpan.FromSeconds(1);
                var wait = (retryAfter.Value + TimeSpan.FromMilliseconds(200)).Clamp(
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(60)
                );
                await Task.Delay(wait, cancellationToken);
                continue;
            }

            // Advisory limit handling
            if (rateLimitPreference.IsRespectedFor(tokenKind))
            {
                var remainingRequestCount = response
                    .Headers.TryGetValue("X-RateLimit-Remaining")
                    ?.Pipe(s => int.Parse(s, CultureInfo.InvariantCulture));

                var resetAfterDelay = response
                    .Headers.TryGetValue("X-RateLimit-Reset-After")
                    ?.Pipe(s => double.Parse(s, CultureInfo.InvariantCulture))
                    .Pipe(TimeSpan.FromSeconds);

                if (remainingRequestCount <= 0 && resetAfterDelay is not null)
                {
                    var delay = (resetAfterDelay.Value + TimeSpan.FromSeconds(1)).Clamp(
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(60)
                    );
                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (response.IsSuccessStatusCode)
                return;

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new DiscordChatExporterException(
                        "Authentication token is invalid.",
                        true
                    );
                case HttpStatusCode.Forbidden:
                    throw new DiscordChatExporterException("Failed to remove reaction: forbidden.");
                case HttpStatusCode.NotFound:
                    // The reaction might not be present; treat as non-fatal not found
                    throw new DiscordChatExporterException("Failed to remove reaction: not found.");
                default:
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new DiscordChatExporterException(
                        $"Failed to remove reaction: {response.StatusCode.ToString().ToSpaceSeparatedWords().ToLowerInvariant()}. \nResponse content: {content}",
                        true
                    );
            }
        }
    }

    private async ValueTask<JsonElement> GetJsonResponseAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var tokenKind = await ResolveTokenKindAsync(cancellationToken);

        using var response = await GetResponseAsync(url, tokenKind, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new DiscordChatExporterException(
                    "Authentication token is invalid.",
                    true
                ),
                HttpStatusCode.Forbidden => new DiscordChatExporterException(
                    $"Request to '{url}' failed: forbidden."
                ),
                HttpStatusCode.NotFound => new DiscordChatExporterException(
                    $"Request to '{url}' failed: not found."
                ),
                _ => new DiscordChatExporterException(
                    $"Request to '{url}' failed: {response.StatusCode.ToString().ToSpaceSeparatedWords().ToLowerInvariant()}.\nResponse content: {await response.Content.ReadAsStringAsync(cancellationToken)}",
                    true
                ),
            };
        }

        return await response.Content.ReadAsJsonAsync(cancellationToken);
    }

    private async ValueTask<JsonElement?> TryGetJsonResponseAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var tokenKind = await ResolveTokenKindAsync(cancellationToken);
        using var response = await GetResponseAsync(url, tokenKind, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsJsonAsync(cancellationToken)
            : null;
    }

    public async ValueTask<Application> GetApplicationAsync(
        CancellationToken cancellationToken = default
    )
    {
        var response = await GetJsonResponseAsync("applications/@me", cancellationToken);
        return Application.Parse(response);
    }

    public async ValueTask<User?> TryGetUserAsync(
        Snowflake userId,
        CancellationToken cancellationToken = default
    )
    {
        var response = await TryGetJsonResponseAsync($"users/{userId}", cancellationToken);
        return response?.Pipe(User.Parse);
    }

    public async IAsyncEnumerable<Guild> GetUserGuildsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return Guild.DirectMessages;

        var currentAfter = Snowflake.Zero;
        while (true)
        {
            var url = new UrlBuilder()
                .SetPath("users/@me/guilds")
                .SetQueryParameter("limit", "100")
                .SetQueryParameter("after", currentAfter.ToString())
                .Build();

            var response = await GetJsonResponseAsync(url, cancellationToken);

            var count = 0;
            foreach (var guildJson in response.EnumerateArray())
            {
                var guild = Guild.Parse(guildJson);
                yield return guild;

                currentAfter = guild.Id;
                count++;
            }

            if (count <= 0)
                yield break;
        }
    }

    public async ValueTask<Guild> GetGuildAsync(
        Snowflake guildId,
        CancellationToken cancellationToken = default
    )
    {
        if (guildId == Guild.DirectMessages.Id)
            return Guild.DirectMessages;

        var response = await GetJsonResponseAsync($"guilds/{guildId}", cancellationToken);
        return Guild.Parse(response);
    }

    public async IAsyncEnumerable<Channel> GetGuildChannelsAsync(
        Snowflake guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (guildId == Guild.DirectMessages.Id)
        {
            var response = await GetJsonResponseAsync("users/@me/channels", cancellationToken);
            foreach (var channelJson in response.EnumerateArray())
                yield return Channel.Parse(channelJson);
        }
        else
        {
            var response = await GetJsonResponseAsync(
                $"guilds/{guildId}/channels",
                cancellationToken
            );

            var channelsJson = response
                .EnumerateArray()
                .OrderBy(j => j.GetProperty("position").GetInt32())
                .ThenBy(j => j.GetProperty("id").GetNonWhiteSpaceString().Pipe(Snowflake.Parse))
                .ToArray();

            var parentsById = channelsJson
                .Where(j => j.GetProperty("type").GetInt32() == (int)ChannelKind.GuildCategory)
                .Select((j, i) => Channel.Parse(j, null, i + 1))
                .ToDictionary(j => j.Id);

            // Discord channel positions are relative, so we need to normalize them
            // so that the user may refer to them more easily in file name templates.
            var position = 0;

            foreach (var channelJson in channelsJson)
            {
                var parent = channelJson
                    .GetPropertyOrNull("parent_id")
                    ?.GetNonWhiteSpaceStringOrNull()
                    ?.Pipe(Snowflake.Parse)
                    .Pipe(parentsById.GetValueOrDefault);

                yield return Channel.Parse(channelJson, parent, position);
                position++;
            }
        }
    }

    public async IAsyncEnumerable<Channel> GetGuildThreadsAsync(
        Snowflake guildId,
        bool includeArchived = false,
        Snowflake? before = null,
        Snowflake? after = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (guildId == Guild.DirectMessages.Id)
            yield break;

        var channels = await GetGuildChannelsAsync(guildId, cancellationToken);

        foreach (
            var channel in await GetChannelThreadsAsync(
                channels,
                includeArchived,
                before,
                after,
                cancellationToken
            )
        )
        {
            yield return channel;
        }
    }

    public async IAsyncEnumerable<Role> GetGuildRolesAsync(
        Snowflake guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (guildId == Guild.DirectMessages.Id)
            yield break;

        var response = await GetJsonResponseAsync($"guilds/{guildId}/roles", cancellationToken);
        foreach (var roleJson in response.EnumerateArray())
            yield return Role.Parse(roleJson);
    }

    public async ValueTask<Member?> TryGetGuildMemberAsync(
        Snowflake guildId,
        Snowflake memberId,
        CancellationToken cancellationToken = default
    )
    {
        if (guildId == Guild.DirectMessages.Id)
            return null;

        var response = await TryGetJsonResponseAsync(
            $"guilds/{guildId}/members/{memberId}",
            cancellationToken
        );
        return response?.Pipe(j => Member.Parse(j, guildId));
    }

    public async ValueTask<Invite?> TryGetInviteAsync(
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var response = await TryGetJsonResponseAsync($"invites/{code}", cancellationToken);
        return response?.Pipe(Invite.Parse);
    }

    public async ValueTask<Channel> GetChannelAsync(
        Snowflake channelId,
        CancellationToken cancellationToken = default
    )
    {
        var response = await GetJsonResponseAsync($"channels/{channelId}", cancellationToken);

        var parentId = response
            .GetPropertyOrNull("parent_id")
            ?.GetNonWhiteSpaceStringOrNull()
            ?.Pipe(Snowflake.Parse);

        try
        {
            var parent = parentId is not null
                ? await GetChannelAsync(parentId.Value, cancellationToken)
                : null;

            return Channel.Parse(response, parent);
        }
        // It's possible for the parent channel to be inaccessible, despite the
        // child channel being accessible.
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/1108
        catch (DiscordChatExporterException)
        {
            return Channel.Parse(response);
        }
    }

    public async IAsyncEnumerable<Channel> GetChannelThreadsAsync(
        IReadOnlyList<Channel> channels,
        bool includeArchived = false,
        Snowflake? before = null,
        Snowflake? after = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var filteredChannels = channels
            // Categories cannot have threads
            .Where(c => !c.IsCategory)
            // Voice channels cannot have threads
            .Where(c => !c.IsVoice)
            // Empty channels cannot have threads
            .Where(c => !c.IsEmpty)
            // If the 'before' boundary is specified, skip channels that don't have messages
            // for that range, because thread-start event should always be accompanied by a message.
            // Note that we don't perform a similar check for the 'after' boundary, because
            // threads may have messages in range, even if the parent channel doesn't.
            .Where(c => before is null || c.MayHaveMessagesBefore(before.Value))
            .ToArray();

        // User accounts can only fetch threads using the search endpoint
        if (await ResolveTokenKindAsync(cancellationToken) == TokenKind.User)
        {
            foreach (var channel in filteredChannels)
            {
                // Either include both active and archived threads, or only active threads
                foreach (
                    var isArchived in includeArchived ? new[] { false, true } : new[] { false }
                )
                {
                    // Offset is just the index of the last thread in the previous batch
                    var currentOffset = 0;
                    while (true)
                    {
                        var url = new UrlBuilder()
                            .SetPath($"channels/{channel.Id}/threads/search")
                            .SetQueryParameter("sort_by", "last_message_time")
                            .SetQueryParameter("sort_order", "desc")
                            .SetQueryParameter("archived", isArchived.ToString().ToLowerInvariant())
                            .SetQueryParameter("offset", currentOffset.ToString())
                            .Build();

                        // Can be null on channels that the user cannot access or channels without threads
                        var response = await TryGetJsonResponseAsync(url, cancellationToken);
                        if (response is null)
                            break;

                        var breakOuter = false;

                        foreach (
                            var threadJson in response.Value.GetProperty("threads").EnumerateArray()
                        )
                        {
                            var thread = Channel.Parse(threadJson, channel);

                            // If the 'after' boundary is specified, we can break early,
                            // because threads are sorted by last message timestamp.
                            if (after is not null && !thread.MayHaveMessagesAfter(after.Value))
                            {
                                breakOuter = true;
                                break;
                            }

                            yield return thread;
                            currentOffset++;
                        }

                        if (breakOuter)
                            break;

                        if (!response.Value.GetProperty("has_more").GetBoolean())
                            break;
                    }
                }
            }
        }
        // Bot accounts can only fetch threads using the threads endpoint
        else
        {
            var guilds = new HashSet<Snowflake>();
            foreach (var channel in filteredChannels)
                guilds.Add(channel.GuildId);

            // Active threads
            foreach (var guildId in guilds)
            {
                var parentsById = filteredChannels.ToDictionary(c => c.Id);

                var response = await GetJsonResponseAsync(
                    $"guilds/{guildId}/threads/active",
                    cancellationToken
                );

                foreach (var threadJson in response.GetProperty("threads").EnumerateArray())
                {
                    var parent = threadJson
                        .GetPropertyOrNull("parent_id")
                        ?.GetNonWhiteSpaceStringOrNull()
                        ?.Pipe(Snowflake.Parse)
                        .Pipe(parentsById.GetValueOrDefault);

                    if (filteredChannels.Contains(parent))
                        yield return Channel.Parse(threadJson, parent);
                }
            }

            // Archived threads
            if (includeArchived)
            {
                foreach (var channel in filteredChannels)
                {
                    foreach (var archiveType in new[] { "public", "private" })
                    {
                        // This endpoint parameter expects an ISO8601 timestamp, not a snowflake
                        var currentBefore = before
                            ?.ToDate()
                            .ToString("O", CultureInfo.InvariantCulture);

                        while (true)
                        {
                            // Threads are sorted by archive timestamp, not by last message timestamp
                            var url = new UrlBuilder()
                                .SetPath($"channels/{channel.Id}/threads/archived/{archiveType}")
                                .SetQueryParameter("before", currentBefore)
                                .Build();

                            // Can be null on certain channels
                            var response = await TryGetJsonResponseAsync(url, cancellationToken);
                            if (response is null)
                                break;

                            foreach (
                                var threadJson in response
                                    .Value.GetProperty("threads")
                                    .EnumerateArray()
                            )
                            {
                                var thread = Channel.Parse(threadJson, channel);
                                yield return thread;

                                currentBefore = threadJson
                                    .GetProperty("thread_metadata")
                                    .GetProperty("archive_timestamp")
                                    .GetString();
                            }

                            if (!response.Value.GetProperty("has_more").GetBoolean())
                                break;
                        }
                    }
                }
            }
        }
    }

    private async ValueTask<Message?> TryGetLastMessageAsync(
        Snowflake channelId,
        Snowflake? before = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = new UrlBuilder()
            .SetPath($"channels/{channelId}/messages")
            .SetQueryParameter("limit", "1")
            .SetQueryParameter("before", before?.ToString())
            .Build();

        var response = await GetJsonResponseAsync(url, cancellationToken);
        return response.EnumerateArray().Select(Message.Parse).LastOrDefault();
    }

    public async IAsyncEnumerable<Message> GetMessagesAsync(
        Snowflake channelId,
        Snowflake? after = null,
        Snowflake? before = null,
        IProgress<Percentage>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        bool descending = false
    )
    {
        // Snapshot the latest message in range; used for progress and boundary checks.
        var lastMessage = await TryGetLastMessageAsync(channelId, before, cancellationToken);
        if (lastMessage is null || lastMessage.Timestamp < after?.ToDate())
            yield break;

        // Keep track of the first yielded message to compute progress regardless of order
        var firstMessage = default(Message);

        if (!descending)
        {
            var currentAfter = after ?? Snowflake.Zero;
            while (true)
            {
                var url = new UrlBuilder()
                    .SetPath($"channels/{channelId}/messages")
                    .SetQueryParameter("limit", "100")
                    .SetQueryParameter("after", currentAfter.ToString())
                    .Build();

                var response = await GetJsonResponseAsync(url, cancellationToken);

                var messages = response
                    .EnumerateArray()
                    .Select(Message.Parse)
                    // Messages are returned from newest to oldest, so we need to reverse them
                    .Reverse()
                    .ToArray();

                if (!messages.Any())
                    yield break;

                if (
                    messages.All(m => m.IsEmpty)
                    && await ResolveTokenKindAsync(cancellationToken) == TokenKind.Bot
                )
                {
                    var application = await GetApplicationAsync(cancellationToken);
                    if (!application.IsMessageContentIntentEnabled)
                    {
                        throw new DiscordChatExporterException(
                            "Provided bot account does not have the Message Content Intent enabled.",
                            true
                        );
                    }
                }

                foreach (var message in messages)
                {
                    firstMessage ??= message;

                    // Ensure that the messages are in range
                    if (message.Timestamp > lastMessage.Timestamp)
                        yield break;

                    if (progress is not null)
                    {
                        var exportedDuration = (
                            message.Timestamp - firstMessage.Timestamp
                        ).Duration();
                        var totalDuration = (
                            lastMessage.Timestamp - firstMessage.Timestamp
                        ).Duration();
                        progress.Report(
                            Percentage.FromFraction(
                                totalDuration > TimeSpan.Zero ? exportedDuration / totalDuration : 1
                            )
                        );
                    }

                    yield return message;
                    currentAfter = message.Id;
                }
            }
        }
        else
        {
            Snowflake? currentBefore = before;
            while (true)
            {
                var url = new UrlBuilder()
                    .SetPath($"channels/{channelId}/messages")
                    .SetQueryParameter("limit", "100")
                    .SetQueryParameter("before", currentBefore?.ToString())
                    .Build();

                var response = await GetJsonResponseAsync(url, cancellationToken);

                var messages = response
                    .EnumerateArray()
                    .Select(Message.Parse)
                    // Keep newest -> oldest order for descending
                    .ToArray();

                if (!messages.Any())
                    yield break;

                if (
                    messages.All(m => m.IsEmpty)
                    && await ResolveTokenKindAsync(cancellationToken) == TokenKind.Bot
                )
                {
                    var application = await GetApplicationAsync(cancellationToken);
                    if (!application.IsMessageContentIntentEnabled)
                    {
                        throw new DiscordChatExporterException(
                            "Provided bot account does not have the Message Content Intent enabled.",
                            true
                        );
                    }
                }

                foreach (var message in messages)
                {
                    // Apply 'after' boundary when iterating backwards
                    if (after is not null && message.Timestamp < after.Value.ToDate())
                        yield break;

                    firstMessage ??= message;

                    if (progress is not null)
                    {
                        var exportedDuration = (
                            message.Timestamp - firstMessage.Timestamp
                        ).Duration();
                        var totalDuration = (
                            lastMessage.Timestamp - firstMessage.Timestamp
                        ).Duration();
                        progress.Report(
                            Percentage.FromFraction(
                                totalDuration > TimeSpan.Zero ? exportedDuration / totalDuration : 1
                            )
                        );
                    }

                    yield return message;
                    currentBefore = message.Id; // move window towards older messages
                }
            }
        }
    }

    public async IAsyncEnumerable<User> GetMessageReactionsAsync(
        Snowflake channelId,
        Snowflake messageId,
        Emoji emoji,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var reactionName = emoji.Id is not null
            // Custom emoji
            ? emoji.Name + ':' + emoji.Id
            // Standard emoji
            : emoji.Name;

        var currentAfter = Snowflake.Zero;
        while (true)
        {
            var url = new UrlBuilder()
                .SetPath(
                    $"channels/{channelId}/messages/{messageId}/reactions/{Uri.EscapeDataString(reactionName)}"
                )
                .SetQueryParameter("limit", "100")
                .SetQueryParameter("after", currentAfter.ToString())
                .Build();

            // Can be null on reactions with an emoji that has been deleted (?)
            // https://github.com/Tyrrrz/DiscordChatExporter/issues/1226
            var response = await TryGetJsonResponseAsync(url, cancellationToken);
            if (response is null)
                yield break;

            var count = 0;
            foreach (var userJson in response.Value.EnumerateArray())
            {
                var user = User.Parse(userJson);
                yield return user;

                currentAfter = user.Id;
                count++;
            }

            if (count <= 0)
                yield break;
        }
    }
}
