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
using System.Threading.Tasks;
using YoutubeSearch;
using DSharpPlus;
using System.Net;
using System.Text;

namespace MedicBot
{
    public class MedicCommands : BaseCommandModule
    {
        bool isPlaying;
        AudioEntry nowPlaying;
        private readonly List<AudioEntry> queuedEntries = new List<AudioEntry>();
        private AudioEntry lastPlayedAudio;


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
            if (channelID == 0 && (ctx.Member.VoiceState == null || ctx.Member.VoiceState.Channel == null))
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
            await voiceNextConnection.SendSpeakingAsync(false);
            isPlaying = false;
            queuedEntries.Clear();
            voiceNextConnection.Disconnect();
        }
        #endregion

        #region Commands related to playback.
        [Command("stop")]
        [Aliases("dur", "durdur")]
        [Description("Bot ses çalıyorsa susturur.")]
        public async Task Stop(CommandContext ctx)
        {
            queuedEntries.Clear();
            //var vc = ctx.Client.GetVoiceNext();
            //var vcc = vc.GetConnection(ctx.Guild);
            //var ts = vcc.GetTransmitStream();
            //await ts.FlushAsync();
            //await ts.DisposeAsync();
            //await ctx.Client.UpdateStatusAsync();
            await Leave(ctx);
            await Join(ctx);
        }

        private async Task PlayAudio(CommandContext ctx)
        {
            bool disconnectAfterPlaying = false;
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection == null)
            {
                await Join(ctx);
                voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
                disconnectAfterPlaying = true;
            }

            await voiceNextConnection.SendSpeakingAsync(true);
            isPlaying = true;

            do
            {
                AudioEntry currentAudio = queuedEntries.First();
                queuedEntries.RemoveAt(0);
                lastPlayedAudio = currentAudio;
                if (currentAudio.Name.Length <= 50)
                {
                    await ctx.Client.UpdateStatusAsync(new DiscordActivity(currentAudio.Name, ActivityType.Playing));
                }
                else
                {
                    await ctx.Client.UpdateStatusAsync(new DiscordActivity("something too long to show here", ActivityType.Playing));
                }
                nowPlaying = currentAudio;

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-stats -i \"{currentAudio.Path}\" -ac 2 -f s16le -ar 48000 -filter:a loudnorm pipe:1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                Process ffmpeg = Process.Start(psi);
                Stream ffout = ffmpeg.StandardOutput.BaseStream;
                VoiceTransmitStream transmitStream = voiceNextConnection.GetTransmitStream();
                if (ffmpeg.StandardOutput.Peek() == -1)
                {
                    Console.WriteLine(ffmpeg.StandardError.ReadToEnd());
                    throw new Exception("FFmpeg error.");
                }
                await ffout.CopyToAsync(transmitStream);
                await transmitStream.FlushAsync();
                await voiceNextConnection.WaitForPlaybackFinishAsync();
                await voiceNextConnection.SendSpeakingAsync(false);
            } while (queuedEntries.Count != 0);

            isPlaying = false;
            await ctx.Client.UpdateStatusAsync();
            if (disconnectAfterPlaying)
            {
                await Leave(ctx);
            }
        }

        [Command("play")]
        [Aliases("oynatbakalım", "p")]
        [Description("Bir ses oynatır. Bir dosya ile birlikte gönderildiğinde, birlikte gönderildiği ses dosyasını çalar.")]
        public async Task Play(CommandContext ctx, [Description("Çalınacak sesin adı. `#liste` komutuyla tüm seslerin listesini DM ile alabilirsiniz. En son çalan sesi tekrar çalmak için \"!!\" yazabilirsiniz. Ses adı YouTube linki de olabilir.")][RemainingText] string fileNameArg)
        {
            List<string> args = fileNameArg != null ? fileNameArg.Split(',').Select(s => s.Trim()).ToList() : new List<string>() { "" };
            List<AudioEntry> audioEntriesToPlay = new List<AudioEntry>();

            // Populate the list with (or concatenate to the list) the audio entries (or entry) queued for playback.
            foreach (string argument in args)
            {
                if (argument == "!!")
                {
                    audioEntriesToPlay.Add(lastPlayedAudio);
                }
                else if (Uri.TryCreate(argument, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeHttp))
                {
                    try
                    {
                        audioEntriesToPlay.Add(new AudioEntry(uriResult));
                    }
                    catch (Exception ex)
                    {
                        await ctx.RespondAsync(ex.Message);
                        await ctx.RespondAsync("Error trying to access URL. You can try sending the file directly!");
                        throw new Exception("Content is unavailable to the VPS. (Geo-restriction)");
                    }
                }
                else if (ctx.Message.Content.Length == 18 + 6 && ulong.TryParse(ctx.Message.Content.Substring(6, 18), out ulong msgId))
                {
                    // input is an ID to a message, we're going to play a file attached to a message
                    DiscordMessage msg = await ctx.Channel.GetMessageAsync(msgId);
                    if (msg == null)
                    {
                        await ctx.RespondAsync("Girilen sayı bir mesaj ID'si değil.");
                        throw new InvalidOperationException("Given ulong is not a valid message ID.");
                    }
                    if (msg.Attachments.Count != 1)
                    {
                        await ctx.RespondAsync("Mesaj bir dosya içermiyor.");
                        throw new InvalidOperationException("Message does not contain an attachment.");
                    }
                    DiscordAttachment attachedFile = msg.Attachments.First();
                    audioEntriesToPlay.Add(new AudioEntry(attachedFile));
                }
                else if (!string.IsNullOrWhiteSpace(argument))
                {
                    AudioEntry audioEntry = AudioHelper.FindAudio(argument);
                    audioEntriesToPlay.Add(audioEntry);
                }
                else if (ctx.Message.Attachments.Count == 1)
                {
                    DiscordAttachment attachedFile = ctx.Message.Attachments.First();
                    audioEntriesToPlay.Add(new AudioEntry(attachedFile));
                }
                else
                {
                    audioEntriesToPlay.Add(AudioHelper.FindAudio("random"));
                }
            }

            queuedEntries.AddRange(audioEntriesToPlay);
            if (!isPlaying)
            {
                await PlayAudio(ctx);
            }
        }

        [Command("playall")]
        [Aliases("listplay", "playrange")]
        [Description("Girilen kelimeyle eşleşen tüm sesleri oynatır.")]
        public async Task PlayRange(CommandContext ctx, [Description("Listedeki seslerle eşleştirilecek kelime.")][RemainingText] string searchString)
        {
            List<AudioEntry> matchingAudioEntries = AudioHelper.FindAll(searchString);
            queuedEntries.AddRange(matchingAudioEntries);
            if (!isPlaying)
            {
                await PlayAudio(ctx);
            }
        }

        [Command("playrandom")]
        [Aliases("playrand", "playrnd")]
        [Description("Girilen kelimeyle eşleşen seslerden birini rastgele seçerek oynatır.")]
        public async Task PlayRandom(CommandContext ctx, [Description("Çalınacak rastgele ses sayısı")]int count, [Description("Listedeki seslerle eşleştirilecek kelime.")][RemainingText] string searchString)
        {
            if (count > 20)
            {
                await ctx.RespondAsync("Lütfen spamlama");
                throw new InvalidOperationException("Play random is not allowed for more than 20 entries.");
            }
            List<AudioEntry> matchingAudioEntries = AudioHelper.FindAll(searchString);
            for (int i = 0; i < count; i++)
            {
                queuedEntries.Add(matchingAudioEntries[new Random().Next(matchingAudioEntries.Count)]);
            }
            if (!isPlaying)
            {
                await PlayAudio(ctx);
            }
        }

        [Command("queue")]
        [Aliases("q")]
        [Description("Şu anda çalan ve sırada olan sesleri listeler.")]
        public async Task Queue(CommandContext ctx)
        {
            string message = "";
            if (!isPlaying)
            {
                message += "Hiçbir şey çalmıyor.";
            }
            else
            {
                message += "__Now Playing:__\n";
                message += await GetQueueEntry(ctx, nowPlaying.Name) + "\n";
                if (queuedEntries.Count != 0)
                {
                    message += "\n" + DiscordEmoji.FromName(ctx.Client, ":arrow_down:") + " __Up Next:__ " + DiscordEmoji.FromName(ctx.Client, ":arrow_down:") + "\n";
                    int order = 0;
                    foreach (AudioEntry audioEntry in queuedEntries)
                    {
                        message += "\n`" + ++order + ".` " + await GetQueueEntry(ctx, audioEntry.Name) + "\n";
                    }
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

        [Command("clear")]
        [Aliases("c")]
        [Description("Sırada bekleyen seslerin tümünü sıradan kaldırır.")]
        public async Task Clear(CommandContext ctx)
        {
            queuedEntries.Clear();
            await ctx.RespondAsync(DiscordEmoji.FromName(ctx.Client, ":wastebasket:"));
        }

        [Command("remove")]
        [Aliases("r")]
        [Description("Sırada bekleyen seslerden birini sıradan kaldırır.")]
        public async Task Remove(CommandContext ctx, [Description("Kaldırılacak sesin listedeki sırası")] int place)
        {
            AudioEntry removedEntry = null;
            try
            {
                removedEntry = queuedEntries[place - 1];
            }
            catch (IndexOutOfRangeException)
            {
                await ctx.RespondAsync("Verilen sıra sayısı yanlış.");
            }
            await ctx.RespondAsync(String.Format("Removed `{0}`", removedEntry.Name));
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
            if (string.IsNullOrWhiteSpace(searchString))
            {
                await ctx.RespondAsync("Lütfen https://comaristan.cf/medicbot adresindeki listeyi kullanmayı tercih edin. Eminseniz ve devam etmek istiyorsanız \"evet\" yazın.");
                InteractivityExtension interactivity = ctx.Client.GetInteractivity();
                InteractivityResult<DiscordMessage> msg = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == ctx.User.Id && xm.Content.ToLower() == "evet", TimeSpan.FromSeconds(15));
                if (msg.Result == null)
                {
                    await ctx.RespondAsync("Liste isteği zaman aşımına uğradı.");
                    return;
                }
            }



            string response = "";
            if (searchString != null && !string.IsNullOrWhiteSpace(searchString))
            {
                response = $"`{searchString}` için sonuçlar gösteriliyor:\n";
            }
            List<AudioEntry> searchResults = AudioHelper.FindAll(searchString);

            IEnumerable<(string Key, List<AudioEntry>)> result = searchResults.GroupBy(e => e.Collections.FirstOrDefault() ?? AudioHelper.NoCollectionName).OrderBy(g => g.Key).Select(g => (g.Key, g.ToList()));

            response += "```\n";

            int entriesAdded = 0;
            int entryLimit = 50;

            foreach ((string Key, List<AudioEntry> Entries) in result)
            {
                if (Key != AudioHelper.NoCollectionName)
                {
                    response += $"~~~~ {Key} ~~~~\n";
                }
                Entries.Sort();
                foreach (AudioEntry entry in Entries)
                {
                    TimeSpan entryAge = DateTime.Now - entry.CreationDate;
                    if (entryAge > TimeSpan.FromDays(7))
                    {
                        response += "[ • ]";
                    }
                    else
                    {
                        response += "[NEW]";
                    }
                    response += " " + entry.Name + "\n";
                    entriesAdded++;
                    if (entriesAdded >= entryLimit)
                    {
                        response += "```";
                        await ctx.Member.SendMessageAsync(response);
                        response = "```\n";
                        entriesAdded = 0;
                    }
                }
            }
            response += "```";
            await ctx.Member.SendMessageAsync(response);
        }

        [Command("updatelist")]
        [Hidden]
        [RequireOwner]
        public async Task UpdateList(CommandContext ctx)
        {
            List<AudioEntry> searchResults = AudioHelper.FindAll("");
            IEnumerable<(string Key, List<AudioEntry>)> result = searchResults.GroupBy(e => e.Collections.FirstOrDefault() ?? AudioHelper.NoCollectionName).OrderBy(g => g.Key).Select(g => (g.Key, g.ToList()));
            StringBuilder sb = new StringBuilder("const list = [");
            foreach ((string CollectionName, List<AudioEntry> Entries) in result)
            {
                Entries.Sort();
                if (CollectionName == AudioHelper.NoCollectionName)
                {
                    foreach (AudioEntry entry in Entries)
                    {
                        sb.Append($"\"{entry.Name}\", ");
                    }
                }
                else
                {
                    foreach (AudioEntry entry in Entries)
                    {
                        sb.Append($"\"{entry.Collections.FirstOrDefault()}: {entry.Name}\", ");
                    }
                }
            }
            sb.Length -= 2;
            sb.Append("];");
            sb.AppendLine();
            sb.Append("const servers = {\"Freedom\":\"463052720509812736\", \"野郎\": \"718160932299079681\"}");
            sb.AppendLine();
            sb.Append($"const updatetime = \"{DateTime.UtcNow.AddHours(3).ToString("dd.MM.yyyy HH:mm G\\MT+3")}\"");
            File.WriteAllText("array.js", sb.ToString());
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                File.Move("array.js", "/var/www/comaristan/medicbot/array.js", true);
            }
            await ctx.RespondAsync("Entry list can be found at: https://comaristan.cf/medicbot");
        }

        [Command("news")]
        [Description("En son eklenen sesleri gösterir.")]
        public async Task News(CommandContext ctx, int count = 10)
        {
            if (count > 20)
            {
                await ctx.RespondAsync("Maksimum 20 ses gösterilebilir.");
                throw new InvalidOperationException("Entry count was too high.");
            }
            string response = "```\n";
            List<AudioEntry> newEntries = AudioHelper.FindAll("").OrderByDescending(e => e.CreationDate).Take(count).ToList();
            foreach (AudioEntry entry in newEntries)
            {
                response += $"[ • ] {entry.Name}\n";
            }
            response += "```";
            await ctx.RespondAsync(response);
        }

        [Command("link")]
        [Description("Bir ses kaydının (varsa) indirildiği linki getirir.")]
        public async Task Link(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);
            if (entry.DownloadedFrom == null || string.IsNullOrWhiteSpace(entry.DownloadedFrom))
            {
                await ctx.RespondAsync("Bu dosya için kaynak bulunamadı.");
            }
            else
            {
                await ctx.RespondAsync(entry.DownloadedFrom);
            }
        }

        [Command("owner")]
        [Description("Bir ses kaydını ekleyen kişiyi (varsa) getirir.")]
        [Aliases("sahibi")]
        public async Task Owner(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            if (entry.OwnerId == 0)
            {
                await ctx.RespondAsync($"`{entry.Name}` ses kaydının bir sahibi yok.");
            }
            else
            {
                await ctx.RespondAsync($"`{entry.Name}` ses kaydının sahibi: {(await ctx.Client.GetUserAsync(entry.OwnerId)).Username}");
            }
        }
        #endregion

        #region Commands related to adding, managing and removing audio files.
        [Command("ekle")]
        [Description("Verilen linkteki sesi, verilen süre parametrelerine göre ayarlayıp botun ses listesine çalınmak üzere ekler.")]
        public async Task Add(
            CommandContext ctx,
            [Description("Ses kaynağının linki. Link yerine bir dosya eki de gönderilebilir.")]string URL,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]double startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]double durationSec,
            [Description("Sesin kayıtlardaki adı. Örn. #play [gireceğiniz ad] komutuyla çalmak için.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            if (AudioHelper.AudioExists(audioName, true))
            {
                await ctx.RespondAsync("Bu isimde bir ses kaydı zaten bota eklenmiş.");
                throw new InvalidOperationException("Audio entry already exists.");
            }
            string filePath = Path.Combine(AudioHelper.ResDirectory, audioName + ".opus");

            string downloadLink = GetYouTubeLink(URL);

            // Try editing directly from the HTTP source
            FfmpegEdit(startSec, durationSec, downloadLink, audioName + ".opus");

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                // Alternate: Download whole file with youtube-dl and edit with ffmpeg method
                string tempFilePath = YouTubeDl(URL, Path.Combine(AudioHelper.ResDirectory, "işlenecekler"));
                FfmpegEdit(startSec, durationSec, tempFilePath, filePath);
                File.Delete(tempFilePath);
                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    await ctx.RespondAsync("Ses kaydı eklenemedi.");
                    throw new Exception("ffmpeg/youtube-dl error");
                }
            }
            AudioEntry newEntry = new AudioEntry(audioName, AudioEntry.AudioType.File, ctx.Member.Id, URL);
            AudioHelper.AddAudio(newEntry);
            AudioHelper.Save();
            await ctx.RespondAsync(String.Format("{0} `{1}` eklendi.", DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), audioName));
        }

        [Command("ekle")]
        public async Task Add(
            CommandContext ctx,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]double startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]double durationSec,
            [Description("Sesin kayıtlardaki adı. Örn. #play [gireceğiniz ad] komutuyla çalmak için.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            if (ctx.Message.Attachments.Count != 1)
            {
                throw new ArgumentException();
            }
            if (AudioHelper.AudioExists(audioName, true))
            {
                await ctx.RespondAsync("Bu isimde bir ses kaydı zaten bota eklenmiş.");
                throw new InvalidOperationException("Audio entry already exists.");
            }

            string downloadLink = ctx.Message.Attachments.First().Url;

            FfmpegEdit(startSec, durationSec, downloadLink, audioName + ".opus");

            string filePath = Path.Combine(AudioHelper.ResDirectory, audioName + ".opus");
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                await ctx.RespondAsync("Ses kaydı eklenemedi.");
                throw new Exception("ffmpeg/youtube-dl error");
            }
            AudioEntry newEntry = new AudioEntry(audioName, AudioEntry.AudioType.File, ctx.Member.Id, ctx.Message.Id.ToString());
            AudioHelper.AddAudio(newEntry);
            AudioHelper.Save();
            await ctx.RespondAsync(String.Format("{0} Added {1}", DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), newEntry.Name));
        }

        [Command("ekle")]
        public async Task Add(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı. Örn. #play [gireceğiniz ad] komutuyla çalmak için.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            if (ctx.Message.Attachments.Count != 1)
            {
                throw new ArgumentException();
            }
            if (AudioHelper.AudioExists(audioName, true))
            {
                await ctx.RespondAsync("Bu isimde bir ses kaydı zaten bota eklenmiş.");
                throw new InvalidOperationException("Audio entry already exists.");
            }

            string downloadLink = ctx.Message.Attachments.First().Url;
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(downloadLink, Path.Combine(AudioHelper.ResDirectory, audioName + ".opus"));
            }

            string filePath = Path.Combine(AudioHelper.ResDirectory, audioName + ".opus");
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                await ctx.RespondAsync("Ses kaydı eklenemedi.");
                throw new Exception("IO error");
            }
            AudioEntry newEntry = new AudioEntry(audioName, AudioEntry.AudioType.File, ctx.Member.Id, ctx.Message.Id.ToString());
            AudioHelper.AddAudio(newEntry);
            AudioHelper.Save();
            await ctx.RespondAsync(String.Format("{0} Added {1}", DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), newEntry.Name));
        }

        private void FfmpegEdit(double startSec, double durationSec, string input, string output)
        {
            ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-ss {startSec} -t {durationSec} -i \"{input}\" -filter:a loudnorm=I=-12:TP=0:LRA=11 -b:a 128K \"{output}\"",
                WorkingDirectory = AudioHelper.ResDirectory
            };
            Process ffmpeg = Process.Start(ffmpegStartInfo);
            ffmpeg.WaitForExit();
            ffmpeg.Dispose();
        }

        private string GetYouTubeLink(string url)
        {
            ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
            {
                FileName = "youtube-dl",
                Arguments = "-f bestaudio -g " + url,
                RedirectStandardOutput = true
            };
            Process youtubeDl = Process.Start(ydlStartInfo);
            youtubeDl.WaitForExit();
            string returnVal = youtubeDl.StandardOutput.ReadToEnd();
            youtubeDl.Dispose();
            return returnVal;
        }

        private string YouTubeDl(string url, string outputDir)
        {
            ProcessStartInfo ydlFileName = new ProcessStartInfo()
            {
                FileName = "youtube-dl",
                Arguments = $"--get-filename -f bestaudio -o \"%(title)s.%(ext)s\" {url}",
                RedirectStandardOutput = true
            };
            Process ydlFileNameProc = Process.Start(ydlFileName);
            ydlFileNameProc.WaitForExit();
            ydlFileNameProc.Dispose();
            string filename = ydlFileNameProc.StandardOutput.ReadToEnd();

            ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
            {
                FileName = "youtube-dl",
                Arguments = $"-f bestaudio {url} -o {filename}",
                WorkingDirectory = outputDir
            };
            Process youtubeDl = Process.Start(ydlStartInfo);
            youtubeDl.WaitForExit();
            youtubeDl.Dispose();

            return Path.Combine(outputDir, filename);
        }

        [Command("intro")]
        [Aliases("giriş")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi giriş sesiniz olarak ayarlar.")]
        public async Task Intro(
            CommandContext ctx,
            [RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            var userIntros = AudioHelper.GetUserIntros(ctx.Member.Id);
            if (userIntros != null && userIntros.Contains(entry))
            {
                await ctx.RespondAsync($"`{entry.Name}` isimli ses kaydı zaten giriş sesiniz olarak ayarlı.");
                return;
            }

            AudioHelper.AddToUserIntros(ctx.Member.Id, entry);
            AudioHelper.Save();
            await ctx.RespondAsync($"`{entry.Name}` giriş sesiniz olarak ayarlandı.");
        }

        [Command("intro-del")]
        [Description("Giriş seslerinizden bir ses kaydını çıkarır.")]
        public async Task IntroDelete(
            CommandContext ctx,
            [RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            var userIntros = AudioHelper.GetUserIntros(ctx.Member.Id);
            if (userIntros == null || !userIntros.Contains(entry))
            {
                await ctx.RespondAsync($"`{entry.Name}` isimli ses kaydı giriş sesiniz değil.");
                return;
            }

            AudioHelper.RemoveFromUserIntros(ctx.Member.Id, entry);
            AudioHelper.Save();
            await ctx.RespondAsync($"`{entry.Name}` giriş seslerinizden çıkarıldı.");
        }

        [Command("uintro")]
        [Hidden]
        [RequireOwner]
        public async Task UniversalIntro(CommandContext ctx, [RemainingText] string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            var intros = AudioHelper.GetUniversalIntros();
            if (intros != null && intros.Contains(entry))
            {
                await ctx.RespondAsync($"`{entry.Name}` isimli ses kaydı zaten evrensel giriş sesi olarak ayarlı.");
                return;
            }

            AudioHelper.AddToUniversalIntros(entry);
            AudioHelper.Save();
            await ctx.RespondAsync($"`{entry.Name}` evrensel giriş sesi olarak ayarlandı.");
        }

        [Command("uintro-del")]
        [Hidden]
        [RequireOwner]
        public async Task UniversalIntroDelete(CommandContext ctx, [RemainingText] string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            var intros = AudioHelper.GetUniversalIntros();
            if (intros == null && !intros.Contains(entry))
            {
                await ctx.RespondAsync($"`{entry.Name}` isimli ses kaydı evrensel giriş sesi değil.");
                return;
            }

            AudioHelper.RemoveFromUniversalIntros(entry);
            AudioHelper.Save();
            await ctx.RespondAsync($"`{entry.Name}` evrensel giriş seslerinden çıkarıldı.");
        }

        [Command("edit")]
        [Aliases("değiştir")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi yeniden indirip keserek değiştirir.")]
        public async Task Edit(
            CommandContext ctx,
            [Description("Ses kaynağının linki. Link yerine bir dosya eki de gönderilebilir.")]string URL,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]double startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]double durationSec,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);
            string filePath = Path.Combine(AudioHelper.ResDirectory, entry.Name + ".opus");
            string oldFilePath = Path.Combine(AudioHelper.ResDirectory, "trash", entry.Name + ".opus");

            File.Move(filePath, oldFilePath);

            string downloadLink = GetYouTubeLink(URL);

            // Try editing directly from the HTTP source
            FfmpegEdit(startSec, durationSec, downloadLink, entry.Name + ".opus");

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                // Alternate: Download whole file with youtube-dl and edit with ffmpeg method
                string tempFilePath = YouTubeDl(URL, Path.Combine(AudioHelper.ResDirectory, "işlenecekler"));
                FfmpegEdit(startSec, durationSec, tempFilePath, filePath);
                File.Delete(tempFilePath);
                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    await ctx.RespondAsync("Ses kaydı düzenlenemedi.");
                    File.Move(oldFilePath, filePath);
                    throw new Exception("ffmpeg/youtube-dl error");
                }
            }
            await ctx.RespondAsync(String.Format("{0} `{1}` düzenlendi.", DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), entry.Name));
            File.Delete(oldFilePath);
        }

        [Command("edit")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi yeniden keserek değiştirir.")]
        public async Task Edit(
            CommandContext ctx,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]double startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]double durationSec,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            if (ctx.Message.Attachments.Count != 1)
            {
                throw new ArgumentException();
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);
            string filePath = Path.Combine(AudioHelper.ResDirectory, entry.Name + ".opus");
            string oldFilePath = Path.Combine(AudioHelper.ResDirectory, "trash", entry.Name + ".opus");

            File.Move(filePath, oldFilePath);

            string downloadLink = ctx.Message.Attachments.First().Url;

            // Try editing directly from the HTTP source
            FfmpegEdit(startSec, durationSec, downloadLink, entry.Name + ".opus");

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    await ctx.RespondAsync("Ses kaydı düzenlenemedi.");
                    File.Move(oldFilePath, filePath);
                    throw new Exception("ffmpeg/youtube-dl error");
                }
            }
            await ctx.RespondAsync(string.Format("{0} `{1}` düzenlendi.", DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), entry.Name));
            File.Delete(oldFilePath);
        }

        [Command("delete")]
        [Description("Bir ses kaydını siler.")]
        [Aliases("sil")]
        [Hidden]
        [RequireOwner]
        public async Task Delete(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            AudioHelper.DeleteAudio(entry);
            await ctx.RespondAsync("🗑️");
        }

        [Command("download")]
        [Aliases("indir")]
        [Description("Bir ses kaydını Discord'a mesaj olarak gönderir.")]
        public async Task Download(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);

            using FileStream fs = new FileStream(entry.Path, FileMode.Open);
            await ctx.RespondWithFileAsync(fs);
        }

        [Command("mp3")]
        [Description("Bir ses kaydını mp3 dosyası olarak gönderir.")]
        public async Task Mp3(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry entry = AudioHelper.FindAudio(audioName);
            Console.WriteLine($"entry: {entry}");
            string mp3Path = Path.Combine(AudioHelper.ResDirectory, "işlenecekler", entry.Name + ".mp3");
            Console.WriteLine($"mp3Path: {mp3Path}");
            ProcessStartInfo ffmpegInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{entry.Path}\" \"{mp3Path}\""
            };
            Process ffmpeg = Process.Start(ffmpegInfo);
            ffmpeg.WaitForExit();
            ffmpeg.Dispose();
            Console.WriteLine("FFmpeg Done!");
            using (FileStream fs = new FileStream(mp3Path, FileMode.Open))
            {
                await ctx.RespondWithFileAsync(fs);
            }
            File.Delete(mp3Path);
        }

        [Command("alias")]
        [Description("Bir ses kaydı için takma ad ekler. Botun verdiği cevaptan sonra takma adı veya takma adların virgülle ayrılmış listesini girin.")]
        public async Task SetAlias(CommandContext ctx, [Description("Sesin adı.")][RemainingText]string audioName)
        {
            if (string.IsNullOrWhiteSpace(audioName))
            {
                await ctx.RespondAsync("Bir ses ismi girmeniz gerek.");
                throw new InvalidOperationException("Audio name cannot be null");
            }
            AudioEntry audioEntry = AudioHelper.FindAudio(audioName);
            var interactivity = ctx.Client.GetInteractivity();
            await ctx.RespondAsync("Lütfen " + audioEntry.Name + " için takma ad veya takma adları girin (İptal için sadece x harfi gönderin):");
            InteractivityResult<DiscordMessage> result = await interactivity.WaitForMessageAsync(xm => xm.Author == ctx.Member);
            if (result.Result != null)
            {
                DiscordMessage msg = result.Result;
                string content = msg.Content;
                if (string.IsNullOrWhiteSpace(content) || content.ToLower() == "x")
                {
                    await ctx.RespondAsync(DiscordEmoji.FromName(ctx.Client, ":x:"));
                    return;
                }
                if (content.Contains(","))
                {
                    string[] tokenized = content.Split(",");
                    audioEntry.AddAliases(tokenized);
                    await ctx.RespondAsync($"{audioEntry.Name} için {tokenized.Length} alias kaydedildi.");
                }
                else
                {
                    audioEntry.AddAlias(content);
                    await ctx.RespondAsync($"`{audioEntry.Name}` için `{content}` isimli alias kaydedildi.");
                }
            }
        }

        [Command("collection")]
        [Description("Bir koleksiyona ses kaydı veya kayıtları ekler. Botun verdiği cevaptan sonra sesin adının veya seslerin adlarının virülle ayrılmış listesini girin.")]
        public async Task AddToCollection(CommandContext ctx, [Description("Koleksiyon adı.")][RemainingText]string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                await ctx.RespondAsync("Bir koleksiyon ismi girmeniz gerek.");
                throw new InvalidOperationException("Collection name cannot be null");
            }
            var interactivity = ctx.Client.GetInteractivity();
            await ctx.RespondAsync($"Lütfen {collectionName} koleksiyonuna eklenecek sesi veya sesleri girin (İptal için sadece x harfi gönderin):");
            InteractivityResult<DiscordMessage> result = await interactivity.WaitForMessageAsync(xm => xm.Author == ctx.Member);
            if (result.Result != null)
            {
                DiscordMessage msg = result.Result;
                string content = msg.Content;
                if (content.ToLower() == "x")
                {
                    return;
                }
                if (content.Contains(","))
                {
                    string[] tokenized = content.Split(",");
                    List<AudioEntry> entriesToAdd = new List<AudioEntry>();
                    foreach (string audioName in tokenized)
                    {
                        AudioEntry audioEntry = AudioHelper.FindAudio(audioName);
                        entriesToAdd.Add(audioEntry);
                    }
                    int addedEntries = 0;
                    foreach (AudioEntry entry in entriesToAdd)
                    {
                        if (entry.Collections.Contains(collectionName))
                        {
                            await ctx.RespondAsync($"`{entry.Name}` zaten `{collectionName}` koleksiyonuna eklenmiş. Atlanıyor.");
                            continue;
                        }
                        AudioHelper.AddToCollection(collectionName, entry);
                        addedEntries++;
                    }
                    await ctx.RespondAsync($"`{addedEntries}` adet ses kaydı `{collectionName}` koleksiyonuna eklendi.");
                }
                else
                {
                    AudioEntry audioEntry = AudioHelper.FindAudio(content);
                    AudioHelper.AddToCollection(collectionName, audioEntry);
                    await ctx.RespondAsync($"`{audioEntry.Name}` isimli ses kaydı `{collectionName}` koleksiyonuna eklendi.");
                }
                AudioHelper.Save();
            }
        }

        [Command("collection-remove")]
        [Aliases("collection-r")]
        [Description("Verilen ses kaydı veya kayıtlarını koleksiyondan çıkarır. Botun verdiği cevaptan sonra çıkarılmasını istediğiniz seslerin adlarını virgülle ayrılmış şekilde girin.")]
        public async Task RemoveFromCollection(CommandContext ctx, [Description("Koleksyion adı.")][RemainingText] string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                await ctx.RespondAsync("Bir koleksiyon ismi girmeniz gerek.");
                throw new InvalidOperationException("Collection name cannot be null");
            }
            var interactivity = ctx.Client.GetInteractivity();
            await ctx.RespondAsync($"Lütfen {collectionName} koleksiyonundan çıkartılacak sesi veya sesleri girin (İptal için sadece x harfi gönderin):");
            InteractivityResult<DiscordMessage> result = await interactivity.WaitForMessageAsync(xm => xm.Author == ctx.Member);
            if (result.Result != null)
            {
                DiscordMessage msg = result.Result;
                string content = msg.Content;
                if (content.ToLower() == "x")
                {
                    return;
                }
                if (content.Contains(","))
                {
                    string[] tokenized = content.Split(",");
                    foreach (string audioName in tokenized)
                    {
                        AudioEntry audioEntry = AudioHelper.FindAudio(audioName);
                        AudioHelper.RemoveFromCollection(collectionName, audioEntry);
                    }
                    await ctx.RespondAsync($"`{tokenized.Length}` adet ses kaydı `{collectionName}` koleksiyonundan çıkartıldı.");
                }
                else
                {
                    AudioEntry audioEntry = AudioHelper.FindAudio(content);
                    AudioHelper.RemoveFromCollection(collectionName, audioEntry);
                    await ctx.RespondAsync($"`{audioEntry.Name}` isimli ses kaydı `{collectionName}` koleksiyonundan çıkartıldı.");
                }
                AudioHelper.Save();
            }
        }
        #endregion

        [Command("tdk")]
        [Description("TDK Güncel Türkçe Sözlük'te verilen kelime ile arama yapar. Kelime girilmezse günün kelimesini yollar.")]
        public async Task GetTdkWord(CommandContext ctx, [Description("Sözlükte aranacak kelime.")][RemainingText]string searchTerm = "")
        {
            TdkWord wotd;
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                wotd = TdkWord.GetWordOfTheDay();
            }
            else
            {
                wotd = TdkWord.GetWord(searchTerm);
            }
            await ctx.RespondAsync(null, false, wotd.GetEmbed());
            if (ctx.Member.VoiceState != null || ctx.Client.GetVoiceNext().GetConnection(ctx.Guild) != null)
            {
                queuedEntries.Add(new AudioEntry(wotd.GetAudioUrl()));
                queuedEntries.Add(new AudioEntry(Tts.GetTtsLink(wotd.GetTtsString())));
                await PlayAudio(ctx); 
            }
        }

        [Command("delete-msg")]
        [Aliases("del-msg")]
        [RequireOwner]
        [Hidden]
        public async Task DeleteMessages(CommandContext ctx, ulong msgId)
        {
            InteractivityExtension interactivity = ctx.Client.GetInteractivity();
            IReadOnlyList<DiscordMessage> messages = await ctx.Channel.GetMessagesAfterAsync(msgId);
            DiscordMessage areYouSureMsg = await ctx.RespondAsync($"Are you absolutely sure you want to delete {messages.Count} messages?");
            InteractivityResult<DSharpPlus.EventArgs.MessageReactionAddEventArgs> interactivityResult = await interactivity.WaitForReactionAsync(x => x.Emoji == DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"), ctx.User, TimeSpan.FromSeconds(5));
            if (interactivityResult.Result != null)
            {
                var combinedMessagesToDelete = new List<DiscordMessage>
                {
                    await ctx.Channel.GetMessageAsync(msgId),
                    areYouSureMsg
                };
                combinedMessagesToDelete.AddRange(messages);
                await ctx.Channel.DeleteMessagesAsync(combinedMessagesToDelete);
            }
            else
            {
                await ctx.RespondAsync("Cancelled.");
            }
        }

        [Command("score")]
        [Hidden]
        [RequireOwner]
        public async Task SetScore(CommandContext ctx, int minScore = -1)
        {
            if (minScore == -1)
            {
                await ctx.RespondAsync("Current minimum score is: " + AudioHelper.MinimumScore);
            }
            else if (minScore < 0 || minScore > 100)
            {
                await ctx.RespondAsync("Score must be in range [0, 100]");
            }
            else
            {
                AudioHelper.MinimumScore = (int)minScore;
                AudioHelper.Save();
            }
        }

        //TODO Add an integer parameter before the mainString parameter to get the desired result number
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
        [Description("Yeniyıl gerisayımını başlatır! (Saat tam 00:00 olduğunda belirtilen sesi çalar)")]
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
        [Hidden]
        [RequireOwner]
        public async Task Test(CommandContext ctx)
        {
            await ctx.RespondAsync(AudioHelper.GetIntroDump());
        }

        public bool IsSafeServer(ulong guildId)
        {
            return File.ReadLines("safe-guilds.txt").Contains(guildId.ToString());
        }
    }
}
