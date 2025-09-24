# Using the GUI

## Video tutorial

If you're using macOS, you may need to remove quarantine before first launch:

```bash
xattr -rd com.apple.quarantine DiscordEmotify.app
```

## Guide

### Step 1

After extracting the `.zip`, run `DiscordEmotify.exe` (Windows) or `DiscordEmotify` (Linux). On macOS, open `DiscordEmotify.app`.

### Step 2

Please refer to the on-screen instructions to get your token, then paste your token in the upper text box and hit ENTER or click the arrow (→).

> **Warning**:
> **Never share your token!**
> A token gives full access to an account, treat it like a password.

<img src="https://i.imgur.com/SuLQ5tZ.png" height="400"/>

### Step 3

The app shows your DMs and servers. Select one or more channels, then click the emoji button to set up a reaction run.

<img src="https://i.imgur.com/JHMFRh2.png" height="400"/>

### Step 4

In the reaction setup dialog you can configure:

- Emoji (Unicode shortcode or custom `name:id`)
- After/Before boundaries and filter
- Order (asc/desc), delay between reactions
- Clear mode (remove reaction instead of adding)

## Settings

- **Auto-update** — Perform automatic updates on launch. Default: Enabled

  > **Note**:
  > Keep this option enabled to receive the latest features and bug fixes!

- **Dark mode** — Toggle dark theme.

- **Persist token** — Save the last used token. Default: Enabled

- **Show threads** — Controls whether threads are shown in the channel list.

- **Locale** — Customize how dates are shown.

- **Rate limit preference** — Advisory vs hard limits.

- **Parallel limit** — Number of channels processed at the same time. Default: 1

  > **Note**:
  > Try to keep this number low so that your account doesn't get flagged.

- **Normalize to UTC** — Normalize dates to UTC.

