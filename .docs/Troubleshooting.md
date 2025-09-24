# Troubleshooting

Welcome to the Frequently Asked Questions (FAQ) and Troubleshooting page!
Here you'll find the answers to most of the questions related to **DiscordEmotify** and its core features.

- ❓ If you still have unanswered questions _after_ reading this page, feel free to open an issue on the [DiscordEmotify repo](https://github.com/Otm02/DiscordEmotify/issues/new).
- 🐞 If you've encountered a problem that's not described here, include your platform (Windows, Mac, Linux, etc.), CLI/GUI version, and a detailed description of your question/problem.

## General questions

### Token stealer?

No. That's why this kind of software needs to be open-source, so the code can be audited by anyone.
Your token is only used to connect to Discord's API, it's not sent anywhere else.
If you're using the GUI, be aware that your token will be saved to a plain text file unless you disable it in the settings menu.

### Why should I be worried about the safety of my token?

A token can be used to log into your account, so treat it like a password and never share it.

### How can I reset my token?

Follow the [instructions here](Token-and-IDs.md).

### Will I get banned if I use this?

Automating user accounts is technically against [TOS](https://discord.com/terms), use at your discretion. [Bot accounts](https://discord.com/developers/docs/topics/oauth2#bot-users) don't have this restriction.

### Can DiscordEmotify react to deleted messages?

No. Deleted messages cannot be reacted to.

### Can DiscordEmotify react in private chats (DMs)?

Yes, if your account has access to them. Use `reactdm` in the CLI or select DM channels in the GUI.

## First steps

### How can I find my token?

Check the following page: [Obtaining token](Token-and-IDs.md)

### When I open DiscordEmotify a black window pops up quickly or nothing shows up

You might have downloaded the CLI flavor of the app, which is meant to be run in a terminal. Try [downloading the GUI](Getting-started.md#gui-or-cli) instead if that's what you want.

### Can I schedule bulk reactions?

You can schedule CLI runs with your OS tools (Task Scheduler, cron, launchd). Be mindful of Discord ToS when automating user accounts.

### Reactions fail with Unknown Emoji

Make sure your emoji is valid:

- Unicode: paste the emoji directly or use shortcodes like `:smile:`
- Custom: `name:emoji_id` (optionally `name:emoji_id:yes` for animated)

### Reactions fail due to missing permissions

Your account must have permission to add reactions in the target channels.

## CLI

### How do I use the CLI?

Check the following page:

- [Using the CLI](Using-the-CLI.md)

If you're using **Docker**, please refer to the [Docker Usage Instructions](Docker.md) instead.

### Where can I find the 'Channel IDs'?

Check the following page:

- [Obtaining Channel IDs](Token-and-IDs.md)

### Docker usage

See: [Docker usage instructions](Docker.md)

### I can't react in Direct Messages

Make sure you're [copying the DM Channel ID](Token-and-IDs.md#how-to-get-a-direct-message-channel-id), not the person's user ID.

## Errors

```yml
Authentication token is invalid.
```

↳ Make sure the provided token is correct.

```yml
Requested resource does not exist.
```

↳ Check your channel ID, it might be invalid. [Read this if you need help](Token-and-IDs.md).

```yml
Access is forbidden.
```

↳ This means you don't have access to the channel.

```yml
System.Net.WebException: Error: TrustFailure ... Invalid certificate received from server.
```

↳ Try running cert-sync.

Debian/Ubuntu: `cert-sync /etc/ssl/certs/ca-certificates.crt`

Red Hat: `cert-sync --user /etc/pki/tls/certs/ca-bundle.crt`

If it still doesn't work, try mozroots: `mozroots --import --ask-remove`

## macOS-specific

### DiscordEmotify is damaged and can’t be opened. You should move it to the Trash.

Check the [Using the GUI page](Using-the-GUI.md#step-1) for instructions on how to run the app.

---

> ❓ If you still have unanswered questions, feel free to open an issue on the DiscordEmotify repo.
>
> 🐞 If you've encountered a problem that's not described here, please open a bug report with details.
