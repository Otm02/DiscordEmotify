# Scheduling reactions with Cron

## Creating the script

1. Open Terminal and create a new text file with `nano /path/to/DiscordEmotify/cron.sh`

> Note:
> You can't use your mouse in nano. Use the arrow keys to move the cursor.

2. Paste the following into the text file:

```bash
#!/bin/bash

# Required parameters
TOKEN=tokenhere
CHANNELID=channelhere
APPFOLDER=/path/to/DiscordEmotify

# Optional parameters
EMOJI=🙂
ORDER=asc        # asc | desc
DELAY=0          # delay in milliseconds between reactions
CLEAR=false      # set to true to remove reactions instead of adding
FILTER=""        # e.g. from:me has:link etc.
AFTER=""         # e.g. 2024-01-01
BEFORE=""        # e.g. 2024-02-01

cd "$APPFOLDER" || exit 1

ARGS=(react -t "$TOKEN" -c "$CHANNELID" --emoji "$EMOJI" --order "$ORDER" --delay "$DELAY")
if [ -n "$FILTER" ]; then ARGS+=(--filter "$FILTER"); fi
if [ -n "$AFTER" ]; then ARGS+=(--after "$AFTER"); fi
if [ -n "$BEFORE" ]; then ARGS+=(--before "$BEFORE"); fi
if [ "$CLEAR" = true ]; then ARGS+=(--clear); fi

./DiscordEmotify.Cli "${ARGS[@]}"
```

3. Replace:

- `tokenhere` with your [Token](Token-and-IDs.md)
- `channelhere` with a [Channel ID](Token-and-IDs.md)
- `/path/to/DiscordEmotify` with the CLI folder containing `DiscordEmotify.Cli`

> Note:
> Remember to escape spaces (add `\` before them) or quote paths (`"/home/my user"`).

> Saving in nano:
> Press Ctrl+O to save, Enter to confirm, then Ctrl+X to exit. See the [nano basics guide](https://wiki.gentoo.org/wiki/Nano/Basics_Guide) for more.

4. Make your script executable with `chmod +x /path/to/DiscordEmotify/cron.sh`

5. Edit your crontab. To run as current user: `crontab -e`. To run as root: `sudo crontab -e`.

6. Add a line like the following at the end (adjust the schedule and path):

```
* * * * * /path/to/DiscordEmotify/cron.sh >/tmp/discordemotify.log 2>/tmp/discordemotify-error.log
```

> Note: If you don't want logs, redirect to `/dev/null` instead of a file.

Then replace the asterisks according to this diagram:

![](https://i.imgur.com/RY7USM6.png)

Examples:

- Minute 15 of every hour: `15 * * * *`
- Every 30 minutes: `*/30 * * * *`
- Every day at midnight: `0 0 * * *`
- Every day at noon: `0 12 * * *`
- Every day at 3, 4 and 6 PM: `0 15,16,18 * * *`
- Every Wednesday at 9 AM: `0 9 * * 3`

Verify your cron time at https://crontab.guru.

Additional info:

- The week starts on Sunday. 0 = SUN, 1 = MON ... 7 = SUN.
- If you set day to `31`, the job runs only in months that have the 31st.

Don't forget to update your token in the script after you reset it!
