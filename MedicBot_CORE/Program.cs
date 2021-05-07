using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.VoiceNext;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        static readonly List<ulong> alreadyPlayedForUsers = new List<ulong>();
        static DiscordClient discord;
        static CommandsNextExtension commands;
        static InteractivityExtension interactivity;
        static VoiceNextExtension voice;
        static void Main()
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        static async Task MainAsync()
        {
            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = Environment.GetEnvironmentVariable("Bot_Token"),
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug
            });
            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                CaseSensitive = false,
                EnableDms = false,
                StringPrefixes = new string[] { "#", "$" }
            });
            commands.RegisterCommands<MedicCommands>();
            commands.CommandErrored += Commands_CommandErrored;
            commands.CommandExecuted += Commands_CommandExecuted;
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

            AudioHelper.Load();
            AudioHelper.CheckForErrors();


            System.Timers.Timer timer = new System.Timers.Timer(900000); // change this to a larger value later: 900000
            timer.Elapsed += Timer_ElapsedAsync;
            timer.Enabled = true;

            if (!File.Exists("safe-guilds.txt"))
            {
                File.WriteAllText("safe-guilds.txt", "386570547267502080");
            }

            discord.VoiceStateUpdated += async (client, e) =>
            {
                if (voice.GetConnection(e.Guild) != null) //Remove(d) second check so bot can play audio for itself??   (&& e.User != discord.CurrentUser)
                {
                    if (e.Channel == voice.GetConnection(e.Guild).TargetChannel && !alreadyPlayedForUsers.Contains(e.User.Id))
                    { // If the user who triggered the event is in the same voice channel as the bot; AND the intro hasn't been played for the user yet
                        List<AudioEntry> intros = AudioHelper.GetUniversalIntros();
                        List<AudioEntry> userIntros = AudioHelper.GetUserIntros(e.After.User.Id);
                        if (userIntros != null || userIntros.Count != 0) // Exception here
                        {
                            intros.AddRange(userIntros);
                        }

                        AudioEntry introEntry = intros.OrderBy(e => new Random().Next()).First();

                        await Task.Delay(1000);

                        await commands.ExecuteCommandAsync(commands.CreateFakeContext(e.User, e.Guild.Channels[505103389537992704], "#play " + introEntry.Name, "#", commands.RegisteredCommands["play"], introEntry.Name));
                        alreadyPlayedForUsers.Add(e.User.Id);
                    }
                    else if (e.Channel == null)
                    { // Someone left
                        alreadyPlayedForUsers.Remove(e.User.Id);
                        if (e.Before.Channel.Users.Count() == 1)
                        {
                            await commands.ExecuteCommandAsync(commands.CreateFakeContext(e.User, e.Guild.Channels[505103389537992704], "#leave", "#", commands.RegisteredCommands["leave"]));
                        }
                    }
                }
                else if (e.User.Id == client.CurrentUser.Id)
                { // Bot did something
                    if ((e.Before == null || e.Before.Channel == null) && (e.After != null && e.After.Channel != null))
                    { // Bot joined
                        alreadyPlayedForUsers.AddRange(e.After.Channel.Users.Where(u => u.Id != client.CurrentUser.Id).Select(u => u.Id));
                    }
                    else
                    {
                        // Bot left
                    }
                }
            };

            discord.MessageCreated += async (client, e) =>
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
                    DateTime iftarTime = GetIftarTime(cityScheduleLinks.Keys.Where(s => messageContent.ToLower().Contains(s)).FirstOrDefault());
                    TimeSpan timeLeft = iftarTime.Subtract(DateTime.UtcNow.AddHours(3));
                    await e.Channel.SendMessageAsync("Akşam ezanı " + iftarTime.ToString("HH:mm") + " saatinde okunuyor, yani " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                }
                else if (messageContent.Contains("sahura"))
                {
                    DateTime imsakTime = GetImsakTime(cityScheduleLinks.Keys.Where(s => messageContent.ToLower().Contains(s)).FirstOrDefault());
                    TimeSpan timeLeft = imsakTime.Subtract(DateTime.UtcNow.AddHours(3));
                    await e.Channel.SendMessageAsync("İmsak " + imsakTime.ToString("HH:mm") + " saatinde, yani " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                }
                else if (messageContent.Contains("okundu mu") || messageContent.Contains("kaçta oku"))
                {
                    DateTime iftarTime = GetIftarTime(cityScheduleLinks.Keys.Where(s => messageContent.ToLower().Contains(s)).FirstOrDefault());
                    if (iftarTime.Day == DateTime.Today.Day)
                    {
                        TimeSpan timeLeft = iftarTime.Subtract(DateTime.UtcNow.AddHours(3));
                        await e.Channel.SendMessageAsync("Akşam ezanı " + iftarTime.ToString("HH:mm") + " saatinde okunuyor, yani " + (timeLeft.Hours == 0 ? "" : timeLeft.Hours + " saat ") + timeLeft.Minutes + " dakika kaldı.");
                    }
                    else
                    {
                        DateTime imsakTime = GetImsakTime(cityScheduleLinks.Keys.Where(s => messageContent.ToLower().Contains(s)).FirstOrDefault());
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

            HttpListener listener = new HttpListener();
            if (!Debugger.IsAttached)
            {   // In production
                listener.Prefixes.Add("http://*:3131/medicbotapi/");
            }
            else
            {   // Debugging
                listener.Prefixes.Add("http://127.0.0.1:3131/medicbotapi/");
            }
            listener.Start();
            _ = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }

        public static void ListenerCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            // Call EndGetContext to complete the asynchronous operation.
            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            // Read request body
            StreamReader bodyStream = new StreamReader(request.InputStream);
            string bodyString = bodyStream.ReadToEnd();
            // Log the request
            Console.WriteLine("INCOMING API REQUEST: " + request.RemoteEndPoint.ToString());
            Console.WriteLine("||" + bodyString + "||");

            // Obtain a response object.
            HttpListenerResponse response = context.Response;
            // """security"""
            bodyString = bodyString.Trim();
            var splitString = bodyString.Split('\n');
            string playString = splitString[0];
            playString = playString.Trim();
            ulong guildId = 0;
            try
            {
                guildId = Convert.ToUInt64(splitString[1]);
            }
            catch (Exception)
            {
                response.StatusCode = 400;
            }
            if (response.StatusCode != 400) // If we haven't already set the response code, indicating an error..
            {
                if (!playString.StartsWith("#play"))
                {
                    response.StatusCode = 400; // Bad request, no commands except play are to be accepted through the API yet.
                }
                else
                {

                    DiscordUser medicUser = discord.GetUserAsync(134336937224830977).Result;
                    if (playString.Length >= 6)
                    {
                        commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, discord.GetGuildAsync(guildId).Result.Channels.FirstOrDefault().Value, playString, "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, playString.Substring(6)));
                    }
                    else
                    {
                        commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, discord.GetGuildAsync(guildId).Result.Channels.FirstOrDefault().Value, playString, "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value));
                    }
                }
            }
            // Construct a response.
            response.ContentType = "application/json";
            string responseString = $"{{\"time\": \"{DateTime.Now}\"}}";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
            _ = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
        }

        private static Task Commands_CommandExecuted(CommandsNextExtension commandsNextExtension, CommandExecutionEventArgs e)
        {
            string[] logEnabledCommands = { "add", "delete", "edit" };
            if (!logEnabledCommands.Contains(e.Command.Name))
            {
                return Task.CompletedTask;
            }
            else
            {
                string logMessage = String.Format("{0}: Command triggered by {1} ({2}) :: {3}", e.Command.Name.ToUpper(), e.Context.User.Username, e.Context.User.Id, e.Context.Message.Content);
                return Task.Run(() =>
                {
                    File.AppendAllText(Path.Combine(Directory.GetCurrentDirectory(), "res", "log.txt"),
                        Environment.NewLine + DateTime.Now.ToString() + " || " + logMessage);
                });
            }
        }

        private static Task Commands_CommandErrored(CommandsNextExtension commandsNextExtension, CommandErrorEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Exception.Message);
            Console.ResetColor();
            if (e.Exception is ArgumentException)
            {
                return commands.ExecuteCommandAsync(commands.CreateFakeContext(e.Context.User, e.Context.Channel, "#help " + e.Command.Name, e.Context.Prefix, commands.RegisteredCommands["help"], e.Command.Name));
            }
            else if (e.Exception is AudioEntryNotFoundException)
            {
                return e.Context.RespondAsync("Ses dosyası bulunamadı.");
            }
            else if (e.Exception is NoResultsFoundException)
            {
                return e.Context.RespondAsync("Aranan terime uygun ses dosyası bulunamadı.");
            }
            else
            {
                throw e.Exception;
            }
        }

        private static Dictionary<string, string> cityScheduleLinks = new Dictionary<string, string>()
        {
            { "istanbul", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9541/istanbul-icin-namaz-vakti" },
            { "izmir", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9560/izmir-icin-namaz-vakti" },
            { "ordu", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9782/ordu-icin-namaz-vakti" },
            { "antalya", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9225/antalya-icin-namaz-vakti" },
            { "samsun", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9819/samsun-icin-namaz-vakti" },
            { "kocaeli", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9654/kocaeli-icin-namaz-vakti"},
            { "bursa", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9335/bursa-icin-namaz-vakti" },
            { "bayburt", @"https://namazvakitleri.diyanet.gov.tr/tr-TR/9295/bayburt-icin-namaz-vakti"}
        };

        private static DateTime GetImsakTime(string city = "istanbul")
        {
            string response = new WebClient().DownloadString(cityScheduleLinks.GetValueOrDefault(city.ToLower()));
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

        private static DateTime GetIftarTime(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                city = "istanbul";
            }
            string response = new WebClient().DownloadString(cityScheduleLinks.GetValueOrDefault(city.ToLower()));
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
