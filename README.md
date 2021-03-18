# Medic-Bot || A (very painful) DSharpPlus Bot
If you want to run this bot on your own machine (for some reason?), you can clone this repo, or download the binaries from the Actions tab. An artifact is built on each push, but there is no standart at the moment, so the latest build might be broken in some ways.

The source is targeted for .NET Core 3.1 but you should be able to create your own project for whatever framework you have installed and the source code should work fine, as long as DSharpPlus is compatible.

Note: I am planning on migrating to .NET 5 soon...

This program currently uses DSharpPlus Nigthly, which means you might not able to `nuget restore` it easily. Nightly builds are available on MyGet, and you can get the links from DSharpPlus' GitHub repo. The repo is included in the `NuGet.Config` file, but you might wanna double check related issues before going mad.

To run the bot, you'll need an environment variable called `Bot_Token` which is the token for the Discord bot application you intend to use.

You will also need to have ffmpeg and youtube-dl installed on your system and available in the PATH.

You'll need to install libopus and libsodium for your system. You can refer to the DSharpPlus documentation for help with this, but basically, if you're on Linux/Mac, you simply install them from your package manager; if you're on Windows, you'll need to get the binaries and put them next to your compiled executable. Additionally, the binaries for Windows are included in this repo, but may not be up to date.

The bot has two prefixes, `#` and `$` and it has built-in help for commands. Altough most things are written mainly in Turkish.

I'm adding empty commits to get free money:
This is commit 9.
