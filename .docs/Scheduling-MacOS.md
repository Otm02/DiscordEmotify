# Scheduling reactions on macOS

## Creating the script

1. Open TextEdit.app and create a new file

2. Convert the file to a plain text one in 'Format > Make Plain Text' (⇧⌘T)

![](https://i.imgur.com/WXrTtXM.png)

3. Paste the following into the text editor:

```bash
#!/bin/bash

# Required parameters
TOKEN=tokenhere
CHANNELID=channelhere
APPFOLDER=/Users/user/Desktop/DiscordEmotify

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

4. Replace:

- `tokenhere` with your [Token](Token-and-IDs.md)
- `channelhere` with a [Channel ID](Token-and-IDs.md)
- `APPFOLDER` with the app folder path (e.g. `/Users/user/Desktop/DiscordEmotify`)

To quickly get file or folder paths, select the file/folder, then hit Command+I (⌘I) and copy what's after `Where:`.
If a folder has spaces in its name, either quote the path (e.g., `"/Users/user/My Folder"`) or escape spaces with `\` (e.g., `/Users/user/My\ Folder`).

![Screenshot of mac info window](https://i.imgur.com/29u6Nyx.png)

5. Save the file as `filename.sh` (not `.txt`).
6. Open Terminal.app, type `chmod +x`, press SPACE, then drag & drop the `filename.sh` into Terminal and hit RETURN.

## Creating the .plist file

Open TextEdit, make a Plain Text (⇧⌘T) and then paste the following into it:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>Label</key>
  <string>local.discordemotify</string>
    <key>Program</key>
    <string>/path/to/filename.sh</string>
    REPLACEME
  </dict>
</plist>
```

- The `Label` string is the name of the job. Replace `local.discordemotify` with another unique name if you'd like to run more than one script.
- The `Program` string is the path to the script. Replace `/path/to/filename.sh` between the `<string>` with the path of the previously created script.
- Replace the `REPLACEME` with the content presented in the following sections according to when you want to run reactions.

When you're done, save the file with the same name as the `Label` and with the `.plist` extension (not `.txt`), like `local.discordemotify.plist`.

### Run on System Boot/User Login

```xml
<key>RunAtLoad</key>
<true/>
```

### Run every n seconds

The following example runs every 3600 seconds (1 hour); replace the integer with your desired interval:

```xml
<key>StartInterval</key>
<integer>3600</integer>
```

### Run at a specific time and date

```xml
<key>StartCalendarInterval</key>
<dict>
  <key>Weekday</key>
  <integer>0</integer>
  <key>Month</key>
  <integer>0</integer>
  <key>Day</key>
  <integer>0</integer>
  <key>Hour</key>
  <integer>0</integer>
  <key>Minute</key>
  <integer>0</integer>
</dict>
```

| Key         | Integer           |
| ----------- | ----------------- |
| **Month**   | 1-12              |
| **Day**     | 1-31              |
| **Weekday** | 0-6 (0 is Sunday) |
| **Hour**    | 0-23              |
| **Minute**  | 0-59              |

**Sunday** - 0; **Monday** - 1; **Tuesday** - 2; **Wednesday** - 3; **Thursday** - 4; **Friday** - 5; **Saturday** - 6

Replace the template's `0`s according to the desired times.

You can delete the `<key>`s you don't need, don't forget to remove the `<integer>0</integer>` under it.
Omitted keys are interpreted as wildcards, for example, if you delete the Minute key, the script will run at every minute, delete the Weekday key and it'll run at every weekday, and so on.

Be aware that if you set the day to '31', the script will only run on months that have the 31st day.

**Check the examples below ([or skip to step 3 (loading the file)](#3-loading-the-plist-into-launchctl)):**

Run every day at 5:15 PM:

```xml
<key>StartCalendarInterval</key>
<dict>
  <key>Hour</key>
  <integer>17</integer>
  <key>Minute</key>
  <integer>15</integer>
</dict>

```

Run at minute 15 of every hour (xx:15):

```xml
<key>StartCalendarInterval</key>
<dict>
  <key>Minute</key>
  <integer>15</integer>
</dict>

```

Every Sunday at midnight and every Wednesday on the hour (xx:00). Notice the inclusion of `<array>` and `</array>` to allow multiple values:

```xml
<key>StartCalendarInterval</key>
<array>
  <dict>
    <key>Weekday</key>
    <integer>0</integer>
    <key>Hour</key>
    <integer>00</integer>
    <key>Minute</key>
    <integer>00</integer>
  </dict>
  <dict>
    <key>Weekday</key>
    <integer>3</integer>
    <key>Minute</key>
    <integer>00</integer>
  </dict>
</array>
```

## Loading the .plist into launchctl

1. Copy your `filename.plist` file to one of these folders according to how you want it to run:

- `~/Library/LaunchAgents` runs as the current logged-in user.

- `/Library/LaunchDaemons` runs as the system administrator (root).

- If macOS has a single user:
  - If you want to run only when the user is logged in, choose the first one.
  - If you want the script to always run on System Startup, choose the second one.
- If macOS has multiple users:
  - If you want the script to run only when a certain user is logged in, choose the first one.
  - If you want the script to always run on System Startup, choose the second one.

To quickly go to these directories, open Finder and press Command+Shift+G (⌘⇧G), then paste the path into the text box.

2. To load the job into launchctl, in Terminal, type `launchctl load`, press SPACE, drag and drop the `.plist` into the Terminal window, then hit RETURN. It won't output anything if it was successfully loaded.

### Extra launchctl commands

**Unloading a job**

```
launchctl unload /path/to/Library/LaunchAgents/local.discordemotify.plist
```

**List every loaded job**

```
launchctl list
```

**Check if a specific job is enabled**
You can also see error codes (2nd number) by running this command.

```
launchctl list | grep local.discordemotify
```

---

Further reading: [Script management with launchd in Terminal on Mac](https://support.apple.com/guide/terminal/script-management-with-launchd-apdc6c1077b-5d5d-4d35-9c19-60f2397b2369/mac) and [launchd.info](https://launchd.info/).
Special thanks to [@Yudi](https://github.com/Yudi)
