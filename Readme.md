# DiscordEmotify

[![Status](https://img.shields.io/badge/status-maintenance-ffd700.svg)](https://github.com/Tyrrrz/.github/blob/master/docs/project-status.md)
[![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7)](https://tyrrrz.me/ukraine)
<!-- Badges updated for DiscordEmotify (adjust as your CI/registry is configured) -->
[![Build](https://img.shields.io/github/actions/workflow/status/Otm02/DiscordEmotify/main.yml?branch=master)](https://github.com/Otm02/DiscordEmotify/actions)
[![Release](https://img.shields.io/github/release/Otm02/DiscordEmotify.svg)](https://github.com/Otm02/DiscordEmotify/releases)
[![Downloads](https://img.shields.io/github/downloads/Otm02/DiscordEmotify/total.svg)](https://github.com/Otm02/DiscordEmotify/releases)
[![Discord](https://img.shields.io/discord/869237470565392384?label=discord)](https://discord.gg/2SUWKFnHSm)
[![Fuck Russia](https://img.shields.io/badge/fuck-russia-e4181c.svg?labelColor=000000)](https://twitter.com/tyrrrz/status/1495972128977571848)

<table>
    <tr>
        <td width="99999" align="center">Development of this project is entirely funded by the community. <b><a href="https://tyrrrz.me/donate">Consider donating to support!</a></b></td>
    </tr>
</table>

<p align="center">
    <img src="favicon.png" alt="Icon" />
</p>

**DiscordEmotify** is a CLI/GUI tool that reacts to every message in a selected Discord channel (including DMs) with an emoji you provide. It uses a robust Discord API client and rate-limit handling to perform bulk reactions safely.

> ❔ If you have questions or issues, **please refer to the [docs](.docs)**.

> 💬 If you want to chat, **join my [Discord server](https://discord.gg/2SUWKFnHSm)**.

## Terms of use<sup>[[?]](https://github.com/Tyrrrz/.github/blob/master/docs/why-so-political.md)</sup>

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all the following statements:

- You **condemn Russia and its military aggression against Ukraine**
- You **recognize that Russia is an occupant that unlawfully invaded a sovereign state**
- You **support Ukraine's territorial integrity, including its claims over temporarily occupied territories of Crimea and Donbas**
- You **reject false narratives perpetuated by Russian state propaganda**

To learn more about the war and how you can help, [click here](https://tyrrrz.me/ukraine). Glory to Ukraine! 🇺🇦

## Download

- This repository contains a CLI/GUI application; build locally using the .NET SDK:

  1) Install .NET 9 SDK: https://dotnet.microsoft.com/download

  2) Build:

     - Windows PowerShell:

       ```powershell
       dotnet build .\DiscordEmotify.sln -c Release
       ```

  3) Run from the CLI project output folder or with `dotnet run`.

> **Important**:
> To launch the GUI version of the app on MacOS, you need to first remove the downloaded file from quarantine.
> You can do that by running the following command in the terminal: `xattr -rd com.apple.quarantine DiscordEmotify.app`.

> **Note**:
> If you're unsure which build is right for your system, consult with [this page](https://useragent.cc) to determine your OS and CPU architecture.

> **Note**:
> AUR and Nix packages linked above are maintained by the community.
> If you have any issues with them, please contact the corresponding maintainers.

## Features

- Add reactions to every message in a channel or across all DMs
- Supports user or bot tokens (token type is auto-detected)
- Respects Discord rate limits (configurable advisory vs hard limits)
- Date range boundaries (`--after`, `--before`) and message filtering (`--filter`)
- Parallel processing across channels with `--parallel`

Note: Your account/bot must have permission to add reactions in the target channels.

Automating user accounts may be against Discord ToS — use at your own risk.

## Usage

- React to specific channel(s) (can pass category IDs to include all its channels):

  ```powershell
  # Standard emoji by Unicode
  DiscordEmotify.Cli.exe react --token "<TOKEN>" --channel 123456789012345678 --emoji 🙂

  # Standard emoji by code
  DiscordEmotify.Cli.exe react --token "<TOKEN>" --channel 123 --emoji :smile:

  # Custom emoji: name:id
  DiscordEmotify.Cli.exe react --token "<TOKEN>" --channel 123 --emoji party_parrot:987654321098765432

  # Only messages after/before specific IDs or dates, with a filter
  DiscordEmotify.Cli.exe react -t "<TOKEN>" -c 123 --emoji 🙂 --after 2024-01-01 --filter "from:me AND has:embed"
  ```

- React across all direct message channels:

  ```powershell
  DiscordEmotify.Cli.exe reactdm --token "<TOKEN>" --emoji 🙂
  ```

## Commands

- `react` — React to every message in one or more channels.
  - Options: `--token`, `--respect-rate-limits`, `--channel`, `--emoji`, `--after`, `--before`, `--filter`, `--parallel`
- `reactdm` — React to every message across all DM channels.
  - Options: `--token`, `--respect-rate-limits`, `--emoji`, `--after`, `--before`, `--filter`, `--parallel`

## See also

- Discord API docs for reactions: https://discord.com/developers/docs/resources/channel#create-reaction
