# Using the CLI

## Step 1

After extracting the `.zip` archive, open your preferred terminal.

## Step 2

Change the current directory to the app folder, e.g. `cd C:\path\to\DiscordEmotify` (`cd /path/to/DiscordEmotify` on **macOS** and **Linux**), then press ENTER.

## Step 3

List available commands and options:

```console
./DiscordEmotify.Cli
```

> Note for Windows `cmd`: omit the leading `./`.

> Docker users: see [Docker usage](Docker.md).

## CLI commands

| Command  | Description                                         |
| -------- | --------------------------------------------------- |
| react    | React to every message in one or more channels      |
| reactdm  | React to every message across all DM channels       |
| channels | Outputs the list of channels in the given server    |
| dm       | Outputs the list of direct message channels         |
| guilds   | Outputs the list of accessible servers              |
| guide    | Explains how to obtain token, server, and channelID |

To use the commands, you need a token. See [Token and IDs](Token-and-IDs.md) or run `./DiscordEmotify.Cli guide`.

To get help for a command:

```console
./DiscordEmotify.Cli react --help
```

## React to a specific channel

You need `--token`, at least one `--channel`, and an `--emoji`.

```console
./DiscordEmotify.Cli react -t "<TOKEN>" -c 123456789012345678 --emoji 🙂
```

Supported emoji inputs:
- Unicode character: `🙂`
- Shortcode: `:smile:`
- Custom emoji: `name:123456789012345678`

### Date range and filters

Limit to messages after/before a date or snowflake ID, and filter messages:

```console
./DiscordEmotify.Cli react -t "<TOKEN>" -c 123 --emoji :smile: --after 2024-01-01 --before 2024-06-01 --filter "from:me has:embed"
```

See [Message filters](Message-filters.md) for syntax.

### Order, delay, and clearing

- Process oldest-first or newest-first: `--order asc|desc` (default: `asc`)
- Add delay between reactions (ms): `--delay 200`
- Remove the reaction instead of adding it: `--clear`

```console
./DiscordEmotify.Cli react -t "<TOKEN>" -c 123 --emoji party_parrot:987 --order desc --delay 250
./DiscordEmotify.Cli react -t "<TOKEN>" -c 123 --emoji 🙂 --clear
```

### React across all DMs

```console
./DiscordEmotify.Cli reactdm -t "<TOKEN>" --emoji 🙂
```

### Utility commands

- List channels in a server:

```console
./DiscordEmotify.Cli channels -t "<TOKEN>" -g 21814
```

- List DM channels:

```console
./DiscordEmotify.Cli dm -t "<TOKEN>"
```

- List servers:

```console
./DiscordEmotify.Cli guilds -t "<TOKEN>"
```
