# Docker usage instructions

The DiscordEmotify Docker image lets you run the CLI in a container without installing .NET.

## Pulling

Pull the image from the registry:

```console
$ docker pull otm02/discordemotify:stable
```

## Usage

To run the CLI in Docker and render help text:

```console
$ docker run --rm otm02/discordemotify:stable
```

React to a channel:

```console
$ docker run --rm otm02/discordemotify:stable react -t TOKEN -c CHANNELID --emoji 🙂
```

For colored output and progress, pass `-it` (interactive + pseudo-terminal):

```console
$ docker run --rm -it otm02/discordemotify:stable react -t TOKEN -c CHANNELID --emoji 🙂
```

For more information, see the [Dockerfile](../DiscordEmotify.Cli.dockerfile) and [Docker documentation](https://docs.docker.com/engine/reference/run).

To get your Token and Channel IDs, please refer to [this page](Token-and-IDs.md).

## Unix permissions issues

This image was designed with a user running as uid:gid of 1000:1000.

If your current user has different IDs, and you want to generate files directly editable for your user, you might want to run the container like this:

```console
$ mkdir data # or chown -R $(id -u):$(id -g) data
$ docker run -it --rm -v $PWD/data:/out --user $(id -u):$(id -g) otm02/discordemotify:stable react -t TOKEN -c CHANNELID --emoji 🙂
```

## Environment variables

DiscordEmotify CLI accepts the `DISCORD_TOKEN` environment variable as a fallback for the `--token` option. You can set this variable with `--env` or `--env-file`.

Please refer to the [Docker documentation](https://docs.docker.com/engine/reference/commandline/run/#set-environment-variables--e---env---env-file) for more information.
