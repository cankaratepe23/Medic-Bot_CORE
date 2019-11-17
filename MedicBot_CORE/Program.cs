using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.VoiceNext;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MedicBot
{
    class Program
    {
        // This list was written to handle RythmBot commands but turns out, RyhtmBot ignores bot messages.
        // Maybe test this in the future with Music Bot? Although RyhtmBot is better than MusicBot..
        // static readonly List<string> bannedWords = new List<string>(File.ReadLines(@"banned_words.txt", System.Text.Encoding.UTF8));
        static List<ulong> alreadyPlayedForUsers = new List<ulong>();
        static DiscordClient discord;
        static CommandsNextExtension commands;
        static InteractivityExtension interactivity;
        static VoiceNextExtension voice;
        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        static async Task MainAsync(string[] args)
        {
            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = Environment.GetEnvironmentVariable("Bot_Token"),
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });
            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                CaseSensitive = false,
                EnableDms = false,
                StringPrefixes = new string[] { "#" }
            });
            commands.RegisterCommands<MedicCommands>();
            interactivity = discord.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(1)
            });
            // EnableIncoming = true increases CPU usage and is not being used until Speech Recognition can be handled easily.
            voice = discord.UseVoiceNext(new VoiceNextConfiguration
            {
                AudioFormat = new AudioFormat(48000, 2, VoiceApplication.LowLatency),
                EnableIncoming = false
            });

            System.Timers.Timer timer = new System.Timers.Timer(900000); // change this to a larger value later: 900000
            timer.Elapsed += Timer_ElapsedAsync;
            timer.Enabled = true;

            if (!File.Exists("safe-guilds.txt"))
            {
                File.WriteAllText("safe-guilds.txt", "386570547267502080");
            }

            discord.VoiceStateUpdated += async e =>
            {
                if (voice.GetConnection(e.Guild) != null && e.Channel == voice.GetConnection(e.Guild).Channel) //Remove(d) second check so bot can play audio for itself??   (&& e.User != discord.CurrentUser)
                {
                    if (voice.GetConnection(e.Guild) != null && !alreadyPlayedForUsers.Contains(e.User.Id))
                    {
                        Random rnd = new Random();
                        DiscordUser medicUser = await discord.GetUserAsync(134336937224830977);
                        List<string> userSpecificFiles = new List<string>(Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "res", "0"), "*.opus"));
                        if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "res", e.User.Id.ToString())))
                        {
                            userSpecificFiles.AddRange(Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "res", e.User.Id.ToString()), "*.opus"));
                        }
                        string audioFile = Path.GetFileNameWithoutExtension(userSpecificFiles[rnd.Next(0, userSpecificFiles.Count)]);
                        await Task.Delay(1000);
                        //await commands.SudoAsync(medicUser, e.Guild.Channels.FirstOrDefault(), "#play " + audioFile);
                        await commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, e.Guild.Channels.FirstOrDefault().Value, "#play " + audioFile, "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, audioFile));
                        alreadyPlayedForUsers.Add(e.User.Id);
                    }
                }
                else if (e.Channel == null)
                {
                    alreadyPlayedForUsers.Remove(e.User.Id);
                }
            };

            discord.MessageCreated += async e =>
            {
                if (e.Author.Equals(discord.CurrentUser))
                    return;
                string messageContent = e.Message.Content.ToLower();
                if (e.Author.Id == 477504775907311619 && e.Message.Content == "wrong" && discord.GetVoiceNext().GetConnection(e.Guild) != null)
                {
                    DiscordUser medicUser = await discord.GetUserAsync(134336937224830977);
                    //await commands.SudoAsync(medicUser, e.Channel, "#play wrong");
                    await commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, e.Channel, "#play wrong", "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, "wrong"));
                }
                else if (messageContent.StartsWith("creeper"))
                {
                    await e.Channel.SendMessageAsync("Aww man!");
                    if (discord.GetVoiceNext().GetConnection(e.Guild) != null)
                    {
                        DiscordUser medicUser = await discord.GetUserAsync(134336937224830977);
                        await commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, e.Channel, "#play aw man", "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, "aw man"));
                    }
                }
                else if (messageContent.Contains("iftara") || messageContent.Contains("akşam ezanına"))
                {
                    DateTime iftarTime = GetIftarTime();
                    TimeSpan timeLeft = iftarTime.Subtract(DateTime.UtcNow.AddHours(3));
                    await e.Channel.SendMessageAsync("Akşam ezanı " + iftarTime.ToString("HH:mm") + " saatinde okunuyor, yani " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                }
                else if (messageContent.Contains("sahura"))
                {
                    DateTime imsakTime = GetImsakTime();
                    TimeSpan timeLeft = imsakTime.Subtract(DateTime.UtcNow.AddHours(3));
                    await e.Channel.SendMessageAsync("İmsak " + imsakTime.ToString("HH:mm") + " saatinde, yani " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                }
                else if (messageContent.Contains("okundu mu") || messageContent.Contains("kaçta oku"))
                {
                    DateTime iftarTime = GetIftarTime();
                    if (iftarTime.Day == DateTime.Today.Day)
                    {
                        TimeSpan timeLeft = iftarTime.Subtract(DateTime.UtcNow.AddHours(3));
                        await e.Channel.SendMessageAsync("Akşam ezanı " + iftarTime.ToString("HH:mm") + " saatinde okunuyor, yani " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                    }
                    else
                    {
                        DateTime imsakTime = GetImsakTime();
                        TimeSpan timeLeft = imsakTime.Subtract(DateTime.UtcNow.AddHours(3));
                        await e.Channel.SendMessageAsync("Okundu! Sahura " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                    }
                }
                else if (e.Message.Content.ToUpper().StartsWith("HOFFMAN"))
                {
                    await e.Channel.SendMessageAsync("Yeah?");
                    var userReply = await interactivity.WaitForMessageAsync(m => m.Author.Id == e.Author.Id && m.Content.Contains(" call this"), TimeSpan.FromSeconds(5));
                    await e.Channel.SendMessageAsync("Uh, uhh...");
                    // TODO: Think of functionality for this HOFFMAN
                }
            };
            await discord.ConnectAsync();
            await Task.Delay(-1);
        }

        private static DateTime GetImsakTime()
        {
            string response = new WebClient().DownloadString(@"https://namazvakitleri.diyanet.gov.tr/tr-TR/9541/istanbul-icin-namaz-vakti");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);
            var imsakDiv = htmlDoc.DocumentNode.SelectSingleNode(@"/html/body/div[4]/div[3]/div[1]/section/div/div[2]/div/table/tbody/tr[1]/td[2]");
            DateTime imsakTime = new DateTime(DateTime.UtcNow.AddHours(3).Year, DateTime.UtcNow.AddHours(3).Month, DateTime.UtcNow.AddHours(3).Day, Convert.ToInt32(imsakDiv.InnerText.Substring(0, 2)), Convert.ToInt32(imsakDiv.InnerText.Substring(3, 2)), 00);
            if (DateTime.UtcNow.AddHours(3).Subtract(imsakTime).TotalSeconds > 0)
            {
                imsakDiv = htmlDoc.DocumentNode.SelectSingleNode(@"/html/body/div[4]/div[3]/div[1]/section/div/div[2]/div/table/tbody/tr[2]/td[2]");
                DateTime tomorrow = DateTime.UtcNow.AddHours(3).AddDays(1);
                imsakTime = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, Convert.ToInt32(imsakDiv.InnerText.Substring(0, 2)), Convert.ToInt32(imsakDiv.InnerText.Substring(3, 2)), 00);
            }
            return imsakTime;
        }

        private static DateTime GetIftarTime()
        {
            string response = new WebClient().DownloadString(@"https://namazvakitleri.diyanet.gov.tr/tr-TR/9541/istanbul-icin-namaz-vakti");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);
            var aksamDiv = htmlDoc.DocumentNode.SelectSingleNode(@"/html/body/div[4]/div[3]/div[1]/section/div/div[2]/div/table/tbody/tr[1]/td[6]");
            DateTime aksamTime = new DateTime(DateTime.UtcNow.AddHours(3).Year, DateTime.UtcNow.AddHours(3).Month, DateTime.UtcNow.AddHours(3).Day, Convert.ToInt32(aksamDiv.InnerText.Substring(0, 2)), Convert.ToInt32(aksamDiv.InnerText.Substring(3, 2)), 00);
            if (DateTime.UtcNow.AddHours(3).Subtract(aksamTime).TotalSeconds > 0)
            {
                aksamDiv = htmlDoc.DocumentNode.SelectSingleNode(@"/html/body/div[4]/div[3]/div[1]/section/div/div[2]/div/table/tbody/tr[2]/td[6]");
                DateTime tomorrow = DateTime.UtcNow.AddHours(3).AddDays(1);
                aksamTime = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, Convert.ToInt32(aksamDiv.InnerText.Substring(0, 2)), Convert.ToInt32(aksamDiv.InnerText.Substring(3, 2)), 00);
            }
            return aksamTime;
        }

        private static void Timer_ElapsedAsync(object sender, System.Timers.ElapsedEventArgs e)
        {
            string nextTickString = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), "res", "timer.txt")).FirstOrDefault();
            DateTime nextTick = DateTime.ParseExact(nextTickString, "dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            if (DateTime.UtcNow.Subtract(nextTick).TotalSeconds < 0)
            {
                return; // We haven't reached the tick time yet.
            }
            // We reached the tick time.
            Random rnd = new Random();
            DiscordUser medicUser = discord.GetUserAsync(134336937224830977).Result;
            commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, discord.GetGuildAsync(463052720509812736).Result.Channels.FirstOrDefault().Value, "#play", "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value));
            int spanToNext = rnd.Next(300000, 86400000);
            nextTick = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(spanToNext));
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "res", "timer.txt"), nextTick.ToString("dd.MM.yyyy HH:mm:ss"));
        }
    }
}
