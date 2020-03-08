using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeSearch;

namespace MedicBot
{
    [RequirePrefixes("#")]
    public class MedicCommands : DSharpPlus.CommandsNext.BaseCommandModule
    {
        //private ConcurrentDictionary<uint, Process> ffmpegs;
        bool checkAudioExists = true;
        bool nowPlaying;
        string nowPlayingName;
        //bool recordingDisabled = true;
        private List<string> queuedSongs = new List<string>();
        private string lastPlayedSong = "";

        //CancellationTokenSource playerCancellationToken = new CancellationTokenSource();

        #region Commands related to connectivity.
        [Command("disconnect")]
        [Hidden]
        [Aliases("siktir", "siktirgit", "sg", "dc")]
        [Description("Botu kapatır.")]
        public async Task Disconnect(CommandContext ctx)
        {
            if (ctx.User.Id != 134336937224830977)
            {
                DiscordUser medicUser = await ctx.Guild.GetMemberAsync(134336937224830977);
                await ctx.RespondWithFileAsync(Path.Combine(Directory.GetCurrentDirectory(), "res", "hahaha_no.gif"), "Bu komutu sadece " + medicUser.Mention + " kullanabilir.");
                return;
            }
            await ctx.Client.DisconnectAsync();
            Environment.Exit(0);
        }

        [Command("join")]
        [Aliases("katıl", "gel")]
        [Description("Botu ses kanalına çağırır.")]
        public async Task Join(CommandContext ctx, ulong channelID = 0)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection != null)
            {
                await ctx.RespondAsync(IsSafeServer(ctx.Guild.Id) ? "Bot zaten ses kanalına bağlı" : "Buradayız işte lan ne join atıyon");
                throw new InvalidOperationException("Already connected, no need to reconnect.");
            }
            if (channelID == 0 && ctx.Member.VoiceState == null)
            {
                voiceNextConnection = await voiceNext.ConnectAsync(ctx.Guild.Channels.Where(ch => ch.Value.Type == DSharpPlus.ChannelType.Voice && ch.Value != ctx.Guild.AfkChannel).OrderBy(ch => ch.Value.Name).FirstOrDefault().Value);
            }
            else if (channelID == 0)
            {
                voiceNextConnection = await voiceNext.ConnectAsync(ctx.Member.VoiceState.Channel);
            }
            else
            {
                voiceNextConnection = await voiceNext.ConnectAsync(ctx.Guild.GetChannel(channelID));
            }
            //this.ffmpegs = new ConcurrentDictionary<uint, Process>();
            //voiceNextConnection.VoiceReceived += OnVoiceReceived;
        }

        [Command("join")]
        [Description("Botu ses kanalına çağırır.")]
        public async Task Join(CommandContext ctx, string channelName)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection != null)
            {
                await ctx.RespondAsync(IsSafeServer(ctx.Guild.Id) ? "Bot zaten ses kanalına bağlı." : "Buradayız işte lan ne join atıyon");
                throw new InvalidOperationException("Already connected, no need to reconnect.");
            }
            var voiceChannels = ctx.Guild.Channels.Where(ch => ch.Value.Type == DSharpPlus.ChannelType.Voice && ch.Value.Name.Contains(channelName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
            if (voiceChannels.Count() == 1)
            {
                voiceNextConnection = await voiceNext.ConnectAsync(voiceChannels.FirstOrDefault());
            }
            else
            {
                await ctx.RespondAsync("Ses kanalı bulunamadı ya da birden fazla bulundu.");
                throw new InvalidOperationException("Multiple or no voice channels found.");
            }
            //this.ffmpegs = new ConcurrentDictionary<uint, Process>();
            //voiceNextConnection.VoiceReceived += OnVoiceReceived;
        }

        [Command("leave")]
        [Aliases("ayrıl", "git", "çık")]
        [Description("Botu ses kanalından kovar.")]
        public async Task Leave(CommandContext ctx)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection == null)
            {
                await ctx.RespondAsync("Daha gelmedik ki kovuyorsun");
                throw new InvalidOperationException("Not connected, can't leave.");
            }
            /*
            voiceNextConnection.VoiceReceived -= OnVoiceReceived;
            foreach (var kvp in this.ffmpegs)
            {
                await kvp.Value.StandardInput.BaseStream.FlushAsync();
                kvp.Value.StandardInput.Dispose();
                kvp.Value.WaitForExit();
            }
            this.ffmpegs = null;
            */
            await voiceNextConnection.SendSpeakingAsync(false);
            nowPlaying = false;
            queuedSongs.Clear();
            voiceNextConnection.Disconnect();
        }
        #endregion

        #region Commands related to playback.
        [Command("stop")]
        [Aliases("dur", "durdur")]
        [Description("Bot ses çalıyorsa susturur.")]
        public async Task Stop(CommandContext ctx)
        {
            if (queuedSongs != null)
            {
                queuedSongs.Clear();
            }
            //var vc = ctx.Client.GetVoiceNext();
            //var vcc = vc.GetConnection(ctx.Guild);
            //var ts = vcc.GetTransmitStream();
            //await ts.FlushAsync();
            //await ts.DisposeAsync();
            await ctx.Client.UpdateStatusAsync();
            await Leave(ctx);
            await Join(ctx);
        }

        [Command("play")]
        [Aliases("oynatbakalım")]
        [Description("Bir ses oynatır. Bir dosya ile birlikte gönderildiğinde, birlikte gönderildiği ses dosyasını çalar.")]
        public async Task Play(CommandContext ctx, [Description("Çalınacak sesin adı. `#liste` komutuyla tüm seslerin listesini DM ile alabilirsiniz. En son çalan sesi tekrar çalmak için \"!!\" yazabilirsiniz. Ses adı YouTube linki de olabilir.")][RemainingText] string fileName)
        {
            //playerCancellationToken = new CancellationTokenSource();
            if (fileName == "!!")
            {
                fileName = lastPlayedSong;
            }
            string filePath;
            bool disconnectAfterPlaying = false;
            bool isUrl = false;
            bool isChainCommand = false;
            List<string> commandQueue = null;
            if (fileName != null)
            {
                if (Uri.TryCreate(fileName, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeHttp))
                {
                    isUrl = true;
                    ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
                    {
                        FileName = "youtube-dl",
                        Arguments = "-f bestaudio -g " + fileName,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    Process youtubeDl = Process.Start(ydlStartInfo);
                    youtubeDl.WaitForExit();
                    string stderr = youtubeDl.StandardError.ReadToEnd();
                    if (!String.IsNullOrEmpty(stderr))
                    {
                        await ctx.RespondAsync(stderr);
                        await ctx.RespondAsync("You can try sending the file directly!");
                        throw new Exception("Content is unavailable to the VPS. (Geo-restriction)");
                    }
                    filePath = youtubeDl.StandardOutput.ReadToEnd();
                    youtubeDl.Dispose();
                }
                else if (ctx.Message.Content.Length == 18 + 6 && UInt64.TryParse(ctx.Message.Content.Substring(6, 18), out ulong msgId))
                {
                    // we're going to play a file attached to a message
                    DiscordMessage msg = await ctx.Channel.GetMessageAsync(msgId);
                    if (msg.Attachments.Count != 1)
                    {
                        await ctx.RespondAsync("Mesaj bir dosya içermiyor.");
                        throw new InvalidOperationException("Message does not contain an attachment.");
                    }
                    DiscordAttachment attachedFile = msg.Attachments.First();
                    filePath = attachedFile.Url;
                }
                else
                {
                    if (fileName.Contains(','))
                    {
                        commandQueue = fileName.Split(',').Select(s => s.Trim()).ToList();
                        fileName = commandQueue.First();
                        isChainCommand = true;
                    }

                    filePath = Path.Combine(Directory.GetCurrentDirectory(), "res", IsSafeServer(ctx.Guild.Id) ? "safe" : "", fileName + ".opus");

                    if (!File.Exists(filePath))
                    {
                        await ctx.RespondAsync("Öyle bir şey yok. ._.");
                        throw new InvalidOperationException("File not found.");
                    }
                }
            }
            else if (ctx.Message.Attachments.Count == 1)
            {
                DiscordAttachment attachedFile = ctx.Message.Attachments.First();
                fileName = ctx.Message.Id.ToString();
                filePath = attachedFile.Url;
            }
            else
            {
                Random rnd = new Random();
                string[] allFiles = GetAllFiles(ctx.Guild.Id);
                filePath = allFiles[rnd.Next(allFiles.Length)];
                fileName = Path.GetFileNameWithoutExtension(filePath);
            }

            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection == null)
            {
                await Join(ctx);
                voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
                disconnectAfterPlaying = true;
            }

            lastPlayedSong = fileName == null ? Path.GetFileNameWithoutExtension(filePath) : fileName;

            if (nowPlaying) // IF playing
            { // Add the current song to the bottom of the queue
                // OR Add entire chain queue to the bottom of the queue
                if (isChainCommand)
                {
                    queuedSongs.AddRange(commandQueue);
                }
                else
                {
                    queuedSongs.Add(fileName);
                }
                return;
            }
            else // If not playing, play
            { // do nothing
                // OR Remove the first entry from the chain queue and add the rest to the bottom of the queue
                if (isChainCommand)
                {
                    commandQueue.RemoveAt(0);
                    queuedSongs.AddRange(commandQueue);
                }
            }
            await voiceNextConnection.SendSpeakingAsync(true);
            nowPlaying = true;
            if (isUrl)
            {
                ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
                {
                    FileName = "youtube-dl",
                    Arguments = "-f bestaudio --get-title " + fileName,
                    RedirectStandardOutput = true
                };
                Process youtubeDl = Process.Start(ydlStartInfo);
                youtubeDl.WaitForExit();
                string songName = youtubeDl.StandardOutput.ReadToEnd();
                youtubeDl.Dispose();
                await ctx.Client.UpdateStatusAsync(new DiscordActivity(songName, ActivityType.Playing));
                nowPlayingName = songName;
            }
            else
            {
                await ctx.Client.UpdateStatusAsync(new DiscordActivity(fileName, ActivityType.Playing));
                nowPlayingName = fileName;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $@"-v quiet -stats -i ""{filePath}"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            Process ffmpegPlayer = Process.Start(psi);
            var ffout = ffmpegPlayer.StandardOutput.BaseStream;
            VoiceTransmitStream transmitStream = voiceNextConnection.GetTransmitStream();
            await ffout.CopyToAsync(transmitStream);
            await transmitStream.FlushAsync();
            await voiceNextConnection.WaitForPlaybackFinishAsync();
            await voiceNextConnection.SendSpeakingAsync(false);
            nowPlaying = false;
            if (queuedSongs.Count != 0)
            {
                string onQueue = queuedSongs.First();
                queuedSongs.RemoveAt(0);
                await ctx.CommandsNext.ExecuteCommandAsync(ctx.CommandsNext.CreateFakeContext(ctx.User, ctx.Channel, "#play " + onQueue, "#", ctx.CommandsNext.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, onQueue));
            }
            await ctx.Client.UpdateStatusAsync();
            if (disconnectAfterPlaying)
            {
                await Leave(ctx);
                voiceNextConnection = null;
            }
        }

        [Command("playall")]
        [Aliases("listplay", "playrange")]
        [Description("Girilen kelimeyle eşleşen tüm sesleri oynatır.")]
        public async Task PlayRange(CommandContext ctx, [Description("Listedeki seslerle eşleştirilecek kelime.")][RemainingText] string searchString)
        {
            List<string> matchingFileNames = GetAllFiles(ctx.Guild.Id).Select(f => f = Path.GetFileNameWithoutExtension(f)).Where(f => f.Contains(searchString)).ToList();
            matchingFileNames.Sort();
            if (matchingFileNames.Count == 0)
            {
                await ctx.RespondAsync("Ses dosyası bulunamadı.");
                throw new FileNotFoundException("Audio file not found.");
            }
            string playerString = "";
            foreach (string fileName in matchingFileNames)
            {
                playerString += fileName + ", ";
            }
            playerString = playerString.Substring(0, playerString.Length - 2);
            var fakeContext = ctx.CommandsNext.CreateFakeContext(ctx.User, ctx.Channel, "#play " + playerString, ctx.Prefix, ctx.CommandsNext.RegisteredCommands["play"], playerString);
            await Play(fakeContext, playerString);
        }

        [Command("playrandom")]
        [Aliases("playrand", "playrnd")]
        [Description("Girilen kelimeyle eşleşen seslerden birini rastgele seçerek oynatır.")]
        public async Task PlayRandom(CommandContext ctx, [Description("Listedeki seslerle eşleştirilecek kelime.")][RemainingText] string searchString)
        {
            List<string> matchingFileNames = GetAllFiles(ctx.Guild.Id).Select(f => f = Path.GetFileNameWithoutExtension(f)).Where(f => f.Contains(searchString)).ToList();
            if (matchingFileNames.Count == 0)
            {
                await ctx.RespondAsync("Ses dosyası bulunamadı.");
                throw new FileNotFoundException("Audio file not found.");
            }
            string audioName;
            if (matchingFileNames.Count == 1)
            {
                audioName = matchingFileNames.First();
            }
            else
            {
                Random random = new Random();
                audioName = matchingFileNames[random.Next(0, matchingFileNames.Count)];
            }
            var fakeContext = ctx.CommandsNext.CreateFakeContext(ctx.User, ctx.Channel, "#play " + audioName, ctx.Prefix, ctx.CommandsNext.RegisteredCommands["play"], audioName);
            await Play(fakeContext, audioName);
        }

        [Command("queue")]
        [Aliases("q")]
        [Description("Şu anda çalan ve sırada olan sesleri listeler.")]
        public async Task Queue(CommandContext ctx)
        {
            string message = "";
            if (!nowPlaying)
            {
                message += "Hiçbir şey çalmıyor.";
            }
            else
            {
                message += "__Now Playing:__\n";
                message += await GetQueueEntry(ctx, nowPlayingName) + "\n";
                message += "\n" + DiscordEmoji.FromName(ctx.Client, ":arrow_down:") + " __Up Next:__ " + DiscordEmoji.FromName(ctx.Client, ":arrow_down:") + "\n";
                int order = 0;
                foreach (string audioName in queuedSongs)
                {
                    message += "\n`" + ++order + ".` " + await GetQueueEntry(ctx, audioName) + "\n";
                }
            }
            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
            {
                Title = "MedicBot Queue",
                Description = message,
                Color = new DiscordColor("3498DB")
            };
            await ctx.RespondAsync("", false, embedBuilder.Build());
        }

        public async Task<string> GetQueueEntry(CommandContext ctx, string audioName)
        {
            audioName = audioName.Trim();
            if (audioName.Length == 18 && UInt64.TryParse(audioName, out ulong msgId))
            {
                DiscordMessage msg = await ctx.Channel.GetMessageAsync(msgId);
                DiscordAttachment attachment = msg.Attachments.First();
                return String.Format("[{0}]({1})", attachment.FileName, msg.JumpLink);
            }
            else
            {
                return audioName;
            }
        }
        #endregion

        #region Commands related to metadata requests.
        [Command("liste")]
        [Description("Botun çalabileceği tüm seslerin listesi. Her zaman günceldir.")]
        public async Task List(
            CommandContext ctx,
            [Description("Seslerin içinde aranacak harf/kelime")][RemainingText]string searchString)
        {
            // This method seems retarded at first but it has some thought behind it:
            // I could filter allFiles to only include the ones that match the searchString, but, I'd have to first turn all the paths in allFiles to filenames only. So in this code, instead of 1 long and
            // 1 potentially short loop, I just loop once.
            // It has *some* thought, not a lot.
            //TODO: Try using the searchString inside the search pattern parameter of the GetFiles method below.                                    searchString goes here?
            string[] allFiles = GetAllFiles(ctx.Guild.Id);
            Array.Sort(allFiles);
            string response = "```\n";
            if (searchString != null)
            {
                response = response.Insert(0, "`" + searchString + "` için sonuçlar gösteriliyor.");
            }
            int fileAddedToResponseCount = 0;
            foreach (string file in allFiles)
            {
                DateTime modifiedDate = File.GetLastWriteTimeUtc(file); //use GetLastWriteTime to get the date the file was first download. this date is not affected by deletion, unlike GetCreationTime.
                TimeSpan fileAge = DateTime.Now - modifiedDate;
                string fileOnly = Path.GetFileNameWithoutExtension(file);
                if (searchString == null || fileOnly.Contains(searchString))
                {
                    if (fileAge > TimeSpan.FromDays(7))
                    {
                        response += "[ • ] " + fileOnly + "\n";
                    }
                    else
                    {
                        response += "[NEW] " + fileOnly + "\n";
                    }
                    fileAddedToResponseCount++;
                    if (fileAddedToResponseCount >= 100)
                    {
                        response += "```";
                        await ctx.Member.SendMessageAsync(response);
                        response = "```\n";
                        fileAddedToResponseCount = 0;
                    }
                }
            }
            if (response == "`" + searchString + "` için sonuçlar gösteriliyor.```\n")
            {
                response += "Ses dosyası bulunamadı.";
            }
            response += "```";
            await ctx.Member.SendMessageAsync(response);
        }

        [Command("news")]
        [Description("En son eklenen sesleri gösterir.")]
        public async Task News(CommandContext ctx, [Description("Gösterilecek yeni ses sayısı.")]int count = 10)
        {
            if (count > 20)
            {
                await ctx.RespondAsync("Çok fazla ses istedin.");
                throw new InvalidOperationException("Count was entered too high in News command.");
            }
            string[] allFiles = GetAllFiles(ctx.Guild.Id);
            allFiles = allFiles.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).ToArray();
            string msg = "```\n";
            for (int i = 0; i < count; i++)
            {
                msg += Path.GetFileNameWithoutExtension(allFiles[i]) + "\n";
            }
            msg += "```";
            await ctx.RespondAsync(msg);
        }

        [Command("link")]
        [Description("Bir ses kaydının (varsa) indirildiği linki getirir.")]
        public async Task Link(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            ProcessStartInfo ffprobeInfo = new ProcessStartInfo()
            {
                FileName = "ffprobe",
                Arguments = "-i \"" + audioName + ".opus\" -v error -of default=noprint_wrappers=1:nokey=1 -hide_banner -show_entries stream_tags=comment",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "res")
            };
            string URL = "Bu dosya için link bulunamadı.";
            Process ffprobe = Process.Start(ffprobeInfo);
            while (!ffprobe.StandardOutput.EndOfStream)
            {
                URL = ffprobe.StandardOutput.ReadLine();
            }
            ffprobe.WaitForExit();
            ffprobe.Dispose();
            await ctx.RespondAsync(URL);
        }

        [Command("owner")]
        [Description("Bir ses kaydını ekleyen kişiyi (varsa) getirir.")]
        [Aliases("sahibi")]
        public async Task Owner(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", IsSafeServer(ctx.Guild.Id) ? "safe" : "", audioName + ".opus")))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }

            ProcessStartInfo ffprobeInfo = new ProcessStartInfo()
            {
                FileName = "ffprobe",
                Arguments = "-i \"" + audioName + ".opus\" -v error -of default=noprint_wrappers=1:nokey=1 -hide_banner -show_entries stream_tags=author",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "res")
            };
            string UId = "0";
            Process ffprobe = Process.Start(ffprobeInfo);
            while (!ffprobe.StandardOutput.EndOfStream)
            {
                UId = ffprobe.StandardOutput.ReadLine();
            }
            ffprobe.WaitForExit();
            ffprobe.Dispose();
            if (UId == "0")
            {
                await ctx.RespondAsync("Bu dosyanın sahibi bulunamadı.");
                throw new InvalidOperationException("File doesn't have an owner.");
            }
            DiscordUser discordUser = await ctx.Client.GetUserAsync(Convert.ToUInt64(UId));
            await ctx.RespondAsync("`" + audioName + "` sesinin sahibi: " + discordUser.Username);
        }
        #endregion

        #region Commands related to adding, managing and removing audio files.
        [Command("ekle")]
        [Description("Verilen linkteki sesi, verilen süre parametrelerine göre ayarlayıp botun ses listesinde çalınmak üzere ekler.")]
        public async Task Add(
            CommandContext ctx,
            [Description("Ses kaynağının linki. YouTube, Dailymotion ve başka video paylaşım sitelerini, video gömülü sayfaları deneyebilirsiniz. Çalışma garanitisi vermiyorum.")]string URL,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]string startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]string durationSec,
            [Description("Sesin kayıtlardaki adı. Örn. #play [gireceğiniz ad] komutuyla çalmak için.")][RemainingText]string audioName)
        {
            if (checkAudioExists)
            {
                if (Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "res"), "*.opus").Contains(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus")))
                {
                    await ctx.RespondAsync("Bu isimdeki ses kaydı zaten bota eklenmiştir.");
                    throw new InvalidOperationException("Audio file with same name already exists.");
                }
            }
            else
            {
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus"));
            }
            checkAudioExists = true;

            ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
            {
                FileName = "youtube-dl",
                Arguments = "-f bestaudio -g " + URL,
                RedirectStandardOutput = true
            };
            Process youtubeDl = Process.Start(ydlStartInfo);
            youtubeDl.WaitForExit();
            string downloadLink = youtubeDl.StandardOutput.ReadToEnd();
            youtubeDl.Dispose();

            ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments =
                "-ss " + startSec +
                " -t " + durationSec +
                " -i \"" + downloadLink + "\" -filter:a loudnorm=I=-12:TP=0:LRA=11 " +
                "-b:a 128K -metadata comment=\"" + URL +
                "\" -metadata author=\"" + ctx.User.Id +
                "\" \"" + audioName + ".opus\"",
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "res", "işlenecekler")
            };
            Process ffmpeg = Process.Start(ffmpegStartInfo);
            ffmpeg.WaitForExit();
            ffmpeg.Dispose();
            File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "res", "işlenecekler", audioName + ".webm"));
            File.Move(Path.Combine(Directory.GetCurrentDirectory(), "res", "işlenecekler", audioName + ".opus"), Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus"));
            await ctx.RespondAsync(String.Format("{0} Added {1}", DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), audioName));
        }

        [Command("intro")]
        [Aliases("giriş")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi giriş sesiniz olarak ayarlar.")]
        public async Task Intro(
            CommandContext ctx,
            [RemainingText]string audioName)
        {
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", ctx.User.Id.ToString())))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "res", ctx.User.Id.ToString()));
            }
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", ctx.User.Id.ToString(), audioName + ".opus")))
            {
                await ctx.RespondAsync("\"" + audioName + "\" ses efekti zaten sizin giriş efektiniz olarak kayıtlı.");
                throw new InvalidOperationException("An audio file with the same name is already added to the user's audio list.");
            }
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus")))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }
            File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus"), Path.Combine(Directory.GetCurrentDirectory(), "res", ctx.User.Id.ToString(), audioName + ".opus"));
        }


        [Command("edit")]
        [Aliases("değiştir")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi yeniden indirip keserek değiştirir.")]
        public async Task Edit(
            CommandContext ctx,
            [Description("Ses kaynağının linki. YouTube, Dailymotion ve başka video paylaşım sitelerini, video gömülü sayfaları deneyebilirsiniz. Çalışma garanitisi vermiyorum.")]string URL,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]string startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]string durationSec,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus")))
            {
                await ctx.RespondAsync("Bu isme sahip ses kaydı bulunamadı. Lütfen `#ekle` komutunu kullanın.");
                throw new InvalidOperationException("The file to edit doesn't exist.");
            }
            if (!IsOwner(ctx.User.Id, audioName))
            {
                await ctx.RespondAsync("Bu sesi, sahibi siz olmadığınız için değiştiremezsiniz.");
                throw new InvalidOperationException("The user trying to edit the file is not the owner of the file.");
            }
            checkAudioExists = false;
            //await ctx.CommandsNext.SudoAsync(ctx.User, ctx.Channel, string.Format("#ekle {0} {1} {2} {3}", URL, startSec, durationSec, audioName));
            await ctx.CommandsNext.ExecuteCommandAsync(ctx.CommandsNext.CreateFakeContext(ctx.User, ctx.Channel, string.Format("#ekle {0} {1} {2} {3}", URL, startSec, durationSec, audioName), "#", ctx.CommandsNext.RegisteredCommands.Where(c => c.Key == "ekle").FirstOrDefault().Value, string.Format("{0} {1} {2} {3}", URL, startSec, durationSec, audioName)));
        }

        [Command("delete")]
        [Description("Bir ses kaydını siler.")]
        [Aliases("sil")]
        public async Task Delete(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (String.IsNullOrWhiteSpace(audioName))
            {
                throw new ArgumentException("Audio name cannot be empty.");
            }
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus")))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File to delete not found.");
            }
            if (!IsOwner(ctx.User.Id, audioName))
            {
                await ctx.RespondAsync("Bu sesi, sahibi siz olmadığınız için silemezsiniz.");
                throw new InvalidOperationException("The user trying to delete the file is not the owner of the file.");
            }
            File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus"), Path.Combine(Directory.GetCurrentDirectory(), "res", "trash", audioName + ".opus")); //Copy and then Delete instead of Move so the Date Created property updates to reflect the date and time the sound file was deleted.
            File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus")); //Using Move leads to the Date Created and Date Modified properties not change at all.
            await ctx.RespondAsync("🗑️");
        }

        [Command("download")]
        [Aliases("indir")]
        [Description("Bir ses kaydını Discord'a mesaj olarak gönderir.")]
        public async Task Download(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (String.IsNullOrWhiteSpace(audioName))
            {
                throw new ArgumentException("Audio name cannot be empty.");
            }
            else if (audioName == "!!")
            {
                audioName = lastPlayedSong;
            }
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "res", IsSafeServer(ctx.Guild.Id) ? "safe" : "", audioName + ".opus");
            if (!File.Exists(filePath))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                await ctx.RespondWithFileAsync(fs);
            }
        }

        [Command("mp3")]
        [Description("Bir ses kaydını mp3 dosyası olarak gönderir.")]
        public async Task Mp3(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (String.IsNullOrWhiteSpace(audioName))
            {
                throw new ArgumentException("Audio name cannot be empty.");
            }
            else if (audioName == "!!")
            {
                audioName = lastPlayedSong;
            }
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "res", IsSafeServer(ctx.Guild.Id) ? "safe" : "", audioName + ".opus");
            if (!File.Exists(filePath))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }
            string mp3Path = Path.Combine(Directory.GetCurrentDirectory(), "res", "işlenecekler", audioName + ".mp3");
            ProcessStartInfo ffmpegInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg.exe",
                Arguments = "-y -i \"" + filePath + "\" \"" + mp3Path + "\""
            };
            Process ffmpeg = Process.Start(ffmpegInfo);
            ffmpeg.WaitForExit();
            using (FileStream fs = new FileStream(mp3Path, FileMode.Open))
            {
                await ctx.RespondWithFileAsync(fs);
            }
            File.Delete(mp3Path);
        }

        #endregion

        [Command("youtube")]
        [Description("Verilen sözcüğü/sözcükleri youtube'da arar ve ilk (X) sonucu yazar.")]
        public async Task Youtube(
            CommandContext ctx,
            [RemainingText][Description("(arama terimi) [sonuç sayısı] şeklinde girilebilir.")] string mainString)
        {
            string searchString;
            VideoSearch items = new VideoSearch();

            if (int.TryParse(mainString.Split(' ').Last(), out int itemCount)) // if last part of the mainString sent with the command is a number
            {
                searchString = mainString.Remove(mainString.LastIndexOf(' '));
                if (itemCount == 1)
                {
                    VideoInformation searchResult = items.SearchQuery(searchString, 1).FirstOrDefault();
                    await ctx.RespondAsync(searchResult.Url);
                    return;
                }

                InteractivityExtension interactivity = ctx.Client.GetInteractivity();
                int pageCount = (itemCount + 18) / 19;
                List<VideoInformation> searchResults = items.SearchQuery(searchString, pageCount); //(ADDED) Add logic so the page count changes depending on how many items the user requested (itemCount) ??? It seems to get 19 results per page
                searchResults.RemoveRange(itemCount, searchResults.Count - itemCount);
                string response = "```\n-----------------------------------------------------------------\n";
                int i = 1;
                foreach (VideoInformation video in searchResults)
                {
                    response += "[" + i + "] " + video.Title + " ||by|| " + video.Author + "\n" + video.Url + " (" + video.Duration + ")" + "\n-----------------------------------------------------------------\n";
                    i++;
                }
                response += "```";
                if (response.Length > 2000)
                {
                    await ctx.RespondAsync("Karakter limitine ulaşıldı. Lütfen daha az video arayın.");
                    throw new InvalidOperationException("Message too long to send.");
                }
                await ctx.RespondAsync(response);
                var userSelectionCtx = await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.Member.Id && int.TryParse(m.Content, out int a) && a <= i);
                if (!userSelectionCtx.TimedOut && userSelectionCtx.Result.Content != "0")
                {
                    await ctx.RespondAsync(searchResults[Convert.ToInt32(userSelectionCtx.Result.Content) - 1].Url);
                }
                else if (userSelectionCtx.Result.Content == "0")
                {
                    await ctx.RespondAsync("Arama isteğiniz iptal edildi. (0 yazdığınız için)");
                }
                else
                {
                    await ctx.RespondAsync("Arama isteğiniz zaman aşımına uğradı. (1 dakika)");
                }
            }
            else
            {
                searchString = mainString;
                VideoInformation searchResult = items.SearchQuery(searchString, 1).FirstOrDefault();
                await ctx.RespondAsync(searchResult.Url);
                return;
            }


            // (x + y - 1) ÷ y =
            // x = itemCount    y = 19

        }

        [Command("yeniyıl")]
        [Description("Yeniyıl gerisayımını başlatır! (Saat tam 00:00 olduğunda belirtilen sesi çalar) Lütfen ")]
        public async Task NewYear(CommandContext ctx, [RemainingText][Description("Çalınacak ses")] string audioName)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "res", audioName + ".opus");
            if (!File.Exists(filePath))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }

            while (DateTime.Now.Month != 12 || DateTime.Now.Day != 31)
            {
                await ctx.RespondAsync("Yılbaşına daha var..");
                throw new InvalidOperationException("Command triggered on non-eve day.");
            }
            while (DateTime.Now.Minute != 59)
            {
                Console.WriteLine("Waiting 5 seconds");
                System.Threading.Thread.Sleep(5000);
            }
            while (DateTime.Now.Second < 54)
            {
                Console.WriteLine("Waiting half a second");
                System.Threading.Thread.Sleep(500);
            }
            await Play(ctx, audioName);
        }

        [Command("test")]
        public async Task Test(CommandContext ctx)
        {
            GC.Collect();
            await ctx.RespondAsync("🗑️");
        }

        //[Command("purge")]
        //public async Task Purge(CommandContext ctx)
        //{
        //    var voiceNext = ctx.Client.GetVoiceNext();
        //    var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
        //    voiceNextConnection.Disconnect();
        //}

        //[Command("record")]
        //public async Task RecordToggle(CommandContext ctx)
        //{
        //    if (recordingDisabled)
        //    {
        //        recordingDisabled = false;
        //    }
        //    else
        //    {
        //        recordingDisabled = true;
        //    }
        //    await ctx.RespondAsync("Recording has been " + (recordingDisabled ? "disabled" : "enabled"));
        //}

        public bool IsOwner(ulong UId, string audioName)
        {
            ProcessStartInfo ffprobeInfo = new ProcessStartInfo()
            {
                FileName = "ffprobe",
                Arguments = "-i \"" + audioName + ".opus\" -v error -of default=noprint_wrappers=1:nokey=1 -hide_banner -show_entries stream_tags=author",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "res")
            };
            Process ffprobe = Process.Start(ffprobeInfo);
            string UserId = "0";
            while (!ffprobe.StandardOutput.EndOfStream)
            {
                UserId = ffprobe.StandardOutput.ReadLine();
            }
            ffprobe.WaitForExit();
            ffprobe.Dispose();
            return UserId == UId.ToString();
        }

        public string[] GetAllFiles(ulong guildId)
        {
            return Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "res", IsSafeServer(guildId) ? "safe" : ""), "*.opus");
        }

        public bool IsSafeServer(ulong guildId)
        {
            return File.ReadLines("safe-guilds.txt").Contains(guildId.ToString());
        }
        /*
        public async Task OnVoiceReceived(VoiceReceiveEventArgs ea)
        {
            if (recordingDisabled)
                return;
            if (!this.ffmpegs.ContainsKey(ea.SSRC))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $@"-ac 2 -f s16le -ar 48000 -i pipe:0 -ac 2 -ar 44100 {ea.SSRC}.wav",
                    UseShellExecute = false,
                    RedirectStandardInput = true
                };

                this.ffmpegs.TryAdd(ea.SSRC, Process.Start(psi));
            }

            var buff = ea.Voice.ToArray();

            var ffmpeg = this.ffmpegs[ea.SSRC];
            await ffmpeg.StandardInput.BaseStream.WriteAsync(buff, 0, buff.Length);
            await ffmpeg.StandardInput.BaseStream.FlushAsync();
        }
        */
    }
}
