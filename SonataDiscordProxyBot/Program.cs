namespace SonataDiscordProxyBot
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using Discord;

    using DiscordApi;

    using Newtonsoft.Json;

    using SonataDiscordProxyBot.ExtensionMethods;

    using StarSonataApi;
    using StarSonataApi.Messages.Incoming;
    using StarSonataApi.Objects;

    public class Program
    {
        private readonly SemaphoreSlim loginSemaphore = new SemaphoreSlim(1);

        private DateTime? lastError;

        private DateTime startTime;

        private enum EAppState
        {
            WaitingForLogin,

            ManuallyStopped,

            Ready,
        }

        private EAppState AppState { get; set; } = EAppState.WaitingForLogin;

        private EAppState PreStoppedState { get; set; }

        public static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            this.startTime = DateTime.UtcNow;
            var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("config.json"));
            var battleEncounters = new List<BattleEncounter>();
            var encounterNotifications = new Dictionary<string, DateTime>();
            var onlineCharacters = new List<string>();
            var warDictionary = new Dictionary<string, WarTimer>();

            var discordApi = new DiscordApi();
            await discordApi.StartAsync(settings.DiscordToken).ConfigureAwait(false);

            var ssApi = new StarSonataApi(settings.ServerUrl);
            ssApi.Connect();
            this.TryGameLoginAsync(ssApi, discordApi, settings).Forget();

            var squad = new Squad(ssApi);

            discordApi.WhenMessageRecieved.Subscribe(
                msg =>
                {
                    if (msg.Author.Id == settings.BotDiscordId)
                    {
                        return;
                    }

                    if (msg.Channel.IsDM)
                    {
                        if(msg.Author.Id == settings.AnimeBotId)
                        {
                            this.HandleBotRequest(msg.Channel.Id, discordApi, msg.Text, onlineCharacters);
                        }
                    }
                    else
                    {
                        if (msg.Channel.Id == settings.CommandChannel)
                        {
                            this.HandleCommands(settings, discordApi, ssApi, msg.Text, squad);
                            return;
                        }

                        if (this.AppState != EAppState.Ready)
                        {
                            return;
                        }

                        var channelMapping =
                            settings.ChannelMappings.Discord.FirstOrDefault(c => c.Discord == msg.Channel.Id);
                        if (channelMapping == null)
                        {
                            return;
                        }

                        try
                        {
                            // ssApi.SendImpersonationChatAsync(channelMapping.Game, msg.Author.Name, msg.Text);
                            ssApi.SendChatAsync($"<{msg.Author.Name}>: {msg.Text}", (MessageChannel)Enum.Parse(typeof(MessageChannel), channelMapping.Game));
                        }
                        catch (Exception)
                        {
                            lock (this)
                            {
                                this.lastError = DateTime.Now;
                            }

                            discordApi.SendMessageAsync(
                                settings.ErrorChannel,
                                "Error: Failed to send a message. Exitting.").Forget();

                            System.Environment.Exit(-1);
                            //this.TryGameLoginAsync(ssApi, discordApi, settings).Forget();
                        }
                    }
                });

            ssApi.WhenMessageReceived.Where(m => !string.IsNullOrEmpty((m as TextMessage)?.Message?.Username))
                 .Subscribe(
                     m =>
                     {
                         var msg = (TextMessage)m;
                         var channelMapping =
                             settings.ChannelMappings.Game.FirstOrDefault(
                                 c => c.Game == msg.Message.Channel.ToString());
                         if (channelMapping == null)
                         {
                             return;
                         }

                         try
                         {
                             discordApi.SendMessageAsync(
                                 channelMapping.Discord,
                                 msg.Message.Username,
                                 msg.Message.Message).Forget();
                         }
                         catch (Exception e)
                         {
                             Console.Error.WriteLine("Error sending discord message, exitting. " + e.Message);
                             System.Environment.Exit(-1);
                         }

                     });

            ssApi.WhenMessageReceived.Where(m => !string.IsNullOrEmpty((m as TextMessage)?.Message?.Username))
                .Subscribe(m =>
                {
                    var msg = (TextMessage)m;
                    if (msg.Message.Channel.ToString() == "Team")
                    {
                        if (msg.Message.Message.StartsWith("!"))
                        {
                            if (msg.Message.Message.StartsWith("!squad"))
                            {
                                if (squad.SquadActive)
                                {
                                    ssApi.SendChatAsync($"Squad is already active and is currently owned by: {squad.SquadLeader}", MessageChannel.Team);
                                    return;
                                }

                                if (msg.Message.Message.Contains("all"))
                                {
                                    squad.InviteEveryone = true;
                                }
                                else
                                {
                                    squad.InviteEveryone = false;
                                }

                                squad.SquadCreate(msg.Message.Username, onlineCharacters);
                                ssApi.SendChatAsync("Creating new squad", MessageChannel.Team);
                            }

                            if (msg.Message.Message.StartsWith("!invite"))
                            {
                                if (!squad.SquadActive)
                                {
                                    ssApi.SendChatAsync("There's no squad to invite you to. Type !squad to create squad", MessageChannel.Team);
                                    return;
                                }

                                squad.Invite(msg.Message.Username);
                            }

                            if (msg.Message.Message.StartsWith("!leave"))
                            {
                                if (squad.SquadActive)
                                {
                                    if (squad.IsLeader(msg.Message.Username))
                                    {
                                        squad.LeaveSquad();
                                    }
                                    else
                                    {
                                        ssApi.SendChatAsync("Only squad leader can do that", MessageChannel.Team);
                                    }
                                }
                            }
                        }
                    }
                });

            ssApi.WhenMessageReceived.Where(m => !string.IsNullOrEmpty((m as TextMessage)?.Message?.Message))
                .Subscribe(m =>
                {
                    var msg = (TextMessage)m;
                    if (msg.Message.Message.Contains("has joined your squad"))
                    {
                        squad.LeaderJoined();
                    }
                });


            ssApi.WhenMessageReceived.Where(m => !string.IsNullOrEmpty((m as TextMessage)?.Message?.Message))
                 .Subscribe(
                     m =>
                     {
                         var msg = (TextMessage)m;
                         if (!string.IsNullOrEmpty(msg.Message.Username))
                         {
                             return;
                         }

                         var channel = msg.Message.Channel.ToString();

                         //custom messages
                         foreach (var customMessage in settings.CustomMessages)
                         {
                             if (msg.Message.Message.Contains(customMessage.RecievedMessage))
                             {
                                 try
                                 {
                                     if(customMessage.MessageToSend.Length == 0)
                                     {
                                         discordApi.SendCustomMessage(customMessage.DiscordChannelID, msg.Message.Message);
                                     }
                                     else
                                     {
                                         discordApi.SendCustomMessage(customMessage.DiscordChannelID, customMessage.MessageToSend);
                                     }
                                 }
                                 catch (Exception)
                                 {
                                     System.Environment.Exit(-1);
                                 }
                             }

                         }

                         //team warnings and initializing playerlist
                         if (channel == "Team")
                         {
                             var messageContent = msg.Message.Message;
                             if (messageContent.StartsWith("[WARNING]"))
                             {
                                 Regex rx = new Regex(@"\[WARNING\](.*?) in (.*?) is under attack from player (.*?) on team (.*?)$");
                                 MatchCollection matches = rx.Matches(messageContent);
                                 matches.ToArray();
                                 var asset = matches[0].Groups[1].Value;
                                 var galaxy = matches[0].Groups[2].Value;
                                 var player = matches[0].Groups[3].Value;
                                 var team = matches[0].Groups[4].Value;

                                 if (team == "Star Revolution X" || galaxy == "Galactic Colosseum")
                                 {
                                     return;
                                 }

                                 var now = DateTime.Now;
                                 var attackedSystem = battleEncounters.FirstOrDefault(s => messageContent.Contains(s.attackedSystem));
                                 if (attackedSystem != null)
                                 {
                                     if (attackedSystem.IsOver())
                                     {
                                         battleEncounters.Remove(attackedSystem); 
                                         discordApi.SendCustomMessage(settings.WarMessagesChannelID, $"@here -> Galaxy **{galaxy}** is under attack by player **{player}** from team **{team}**");
                                         encounterNotifications.Add(galaxy, DateTime.Now);
                                     }
                                     else
                                     {
                                         attackedSystem.AddAttackingTeam(team);
                                         attackedSystem.AddAttackingPlayer(player);
                                         attackedSystem.UpdateLastNotification();
                                     }
                                 
                                 } 
                                 else
                                 {
                                     if (encounterNotifications.ContainsKey(galaxy))
                                     {
                                         var timeStampInSeconds = (DateTime.Now - encounterNotifications.GetValueOrDefault(galaxy)).TotalSeconds;
                                         if (timeStampInSeconds > 30 && timeStampInSeconds < 120)
                                         {
                                             battleEncounters.Add(new BattleEncounter(discordApi, galaxy, settings.WarMessagesChannelID));
                                             encounterNotifications.Remove(galaxy);
                                             discordApi.SendCustomMessage(settings.WarMessagesChannelID, $"@everyone -> Galaxy **{galaxy}** is under attack by player **{player}** from team **{team}**");
                                         }
                                         if(timeStampInSeconds > 120)
                                         {
                                             discordApi.SendCustomMessage(settings.WarMessagesChannelID, $"@here -> Galaxy **{galaxy}** is under attack by player **{player}** from team **{team}**");
                                             encounterNotifications[galaxy] = DateTime.Now;
                                         }
                                     }
                                     else
                                     {
                                         discordApi.SendCustomMessage(settings.WarMessagesChannelID, $"@here -> Galaxy **{galaxy}** is under attack by player **{player}** from team **{team}**");
                                         encounterNotifications.Add(galaxy, DateTime.Now);
                                     }
                                 }


                             }

                             if (messageContent.Contains("has declared war on") || messageContent.Contains("is no longer at war with"))
                             {
                                 if(messageContent.Contains("declared war on"))
                                 {
                                     Regex rx = new Regex(@"Team (.*?) has declared war on (.*?)\.");
                                     MatchCollection matches = rx.Matches(messageContent);
                                     matches.ToArray();
                                     var team = matches[0].Groups[1].Value;
                                     var target = matches[0].Groups[2].Value;
                                     warDictionary.Add(team, new WarTimer(discordApi, team, settings.WarMessagesChannelID));
                                     warDictionary.GetValueOrDefault(team).Notify(target);
                                     if(team == "Eminence Front")
                                     {
                                         for(int i = 0; i < 10; i++)
                                         {
                                             discordApi.SendCustomMessage(settings.WarMessagesChannelID, "@everyone -> EF WARRED US");
                                         }
                                         //new BulkMessageDispatcher(discordApi, "everyone -> EF WARRED US", settings.WarMessagesChannelID, 10, 10);
                                     }
                                 }

                                 if (messageContent.Contains("is no longer at war"))
                                 {
                                     Regex rx = new Regex(@"Team (.*?) is no longer at war");
                                     MatchCollection matches = rx.Matches(messageContent);
                                     matches.ToArray();
                                     var team = matches[0].Groups[1].Value;
                                     warDictionary.GetValueOrDefault(team).OnCancelWarMessage();
                                     warDictionary.Remove(team);
                                     discordApi.SendCustomMessage(settings.WarMessagesChannelID, $"@everyone -> Team **{team}** has ended the war!");

                                 }
                             }
                         }
                     });

            ssApi.WhenMessageReceived.Where(m => !string.IsNullOrEmpty((m as TeamCharacterStatus)?.Character?.Name))
                .Subscribe(
                m =>
                {
                    var user = (TeamCharacterStatus)m;
                    var characterName = user.Character.Name;
                    if (user.Character.LastOnline < 0)
                    {
                        onlineCharacters.Add(characterName);
                        //discordApi.SendCustomMessage(settings.ChannelMappings.Game.FirstOrDefault(ch => ch.Game == "Team").Discord, $"**__{characterName}__ is now online**");
                    }
                    else
                    {
                        if (onlineCharacters.Contains(characterName))
                        {
                            onlineCharacters.Remove(characterName);
                            //discordApi.SendCustomMessage(settings.ChannelMappings.Game.FirstOrDefault(ch => ch.Game == "Team").Discord, $"**__{characterName}__ is now offline**");
                        }
                    }
                });

            await Task.Delay(-1).ConfigureAwait(false);
        }


        private void HandleCommands(Settings settings, DiscordApi discordApi, StarSonataApi ssApi, string commandText, Squad squad)
        {
            if (commandText.Equals("!wtfkill", StringComparison.OrdinalIgnoreCase))
            {
                discordApi.SendMessageAsync(settings.CommandChannel, "`:( Okay`", true).Forget();
                Thread.Sleep(1000);
                Environment.Exit(-1);
            }

            if (commandText.Equals("!kill", StringComparison.OrdinalIgnoreCase))
            {
                discordApi.MessagingDisabled = true;
                this.PreStoppedState = this.AppState;
                this.AppState = EAppState.ManuallyStopped;
                discordApi.SendMessageAsync(settings.CommandChannel, "`Processing stopped`", true).Forget();
            }

            if (commandText.Equals("!continue", StringComparison.OrdinalIgnoreCase)
                && this.AppState == EAppState.ManuallyStopped)
            {
                discordApi.MessagingDisabled = false;
                this.AppState = this.PreStoppedState;
                discordApi.SendMessageAsync(settings.CommandChannel, "`Processing started`", true).Forget();
                if (this.AppState == EAppState.WaitingForLogin)
                {
                    this.TryGameLoginAsync(ssApi, discordApi, settings).Forget();
                }
            }

            if (commandText.Equals("!squadreset", StringComparison.OrdinalIgnoreCase))
            {
                squad.LeaveSquad();
            }

            if (commandText.Equals("!help"))
            {
                var eb = new EmbedBuilder();
                eb.WithTitle("Commands");
                eb.AddField("!status", "Returns the current bot status.", true);
                eb.AddField("!kill", "Stop processing. Use this if the bot is messing up.", true);
                eb.AddField("!continue", "Continue processing. Use this to continue after a !kill command.", true);
                eb.AddField(
                    "!wtfkill",
                    "Will kill the bots process. Only use this if !kill doesn't work no one is around who can fix it.", true);
                eb.AddField("!squadreset", "Will hard reset the squad. Use if owner made a squad and is now offline", true);
                discordApi.EmbedObjectAsync(settings.CommandChannel, eb.Build(), true).Forget();
            }

            if (commandText.Equals("!status"))
            {
                var eb = new EmbedBuilder();
                eb.WithTitle("Bot Status");
                eb.AddField("Status", this.AppState.ToString(), true);
                eb.AddField("Uptime", (DateTime.Now - this.startTime).ToPrettyFormat(), true);
                lock (this)
                {
                    if (this.lastError.HasValue)
                    {
                        eb.AddField(
                            "Last Error",
                            (DateTime.Now - this.lastError.Value).ToPrettyFormat() + " ago", true);
                    }
                }

                discordApi.EmbedObjectAsync(settings.CommandChannel, eb.Build(), true).Forget();
            }
        }
        private void HandleBotRequest(ulong channelID, DiscordApi discordApi, string requestText, List<string> onlineCharacters)
        {
            if (requestText.Equals("!online"))
            {
                discordApi.SendDMAsync(channelID, string.Join(", ", onlineCharacters)).Forget();
            }
        }

        private Task TryGameLoginAsync(StarSonataApi ssApi, DiscordApi discordApi, Settings settings)
        {
            return Task.Run(
                async () =>
                {
                    var connected = false;
                    var semaphoreEntered = false;
                    try
                    {
                        semaphoreEntered = await this.loginSemaphore.WaitAsync(0).ConfigureAwait(false);
                        if (semaphoreEntered)
                        {
                            if (this.AppState == EAppState.ManuallyStopped)
                            {
                                return;
                            }

                            this.AppState = EAppState.WaitingForLogin;
                            ssApi.TryLoginAsync(settings.GameUsername, settings.GamePassword).Wait();
                           

                            if (!ssApi.IsConnected)
                            {
                                lock (this)
                                {
                                    this.lastError = DateTime.Now;
                                }
                            }
                            else
                            {
                                connected = true;
                                if (this.AppState == EAppState.ManuallyStopped)
                                {
                                    this.PreStoppedState = EAppState.Ready;
                                }
                                else
                                {
                                    this.AppState = EAppState.Ready;
                                }
                            }


                            if (!connected)
                            {
                                lock (this)
                                {
                                    this.lastError = DateTime.Now;
                                }

                                Environment.Exit(-1);
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Environment.Exit(-1);
                    }
                });
        }
    }
}
