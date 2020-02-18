using Newtonsoft.Json;
using StarSonataApi.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SonataDiscordProxyBot
{
    public class Squad
    {
        public Squad(StarSonataApi.StarSonataApi ssApi)
        {
            this.ssApi = ssApi;
            SquadThread = Channel.CreateUnbounded<Func<Task>>();
            InviteEveryone = false;
            _ = Task.Run(
                async () =>
                {
                    while (await SquadThread.Reader.WaitToReadAsync())
                    {
                        if (SquadThread.Reader.TryRead(out var func))
                        {
                            if (this.IsCancelled)
                            {
                                continue;
                            }

                            await func();
                            await Task.Delay(TimeSpan.FromMilliseconds(1000));
                        }
                    }
                });

            if(File.Exists(filePath))
            {
                var definition = new
                {
                    squadLeader = "",
                    memberList = new List<string>()
                };

                using (StreamReader file = File.OpenText(filePath))
                {
                    var fileData = JsonConvert.DeserializeAnonymousType(file.ReadToEnd(), definition);
                    this.SquadActive = true;
                    this.SquadLeader = fileData.squadLeader;
                    this.InvitedSquadMembers = fileData.memberList;
                }
            }
        }

        public string SquadLeader { get; set; }

        public List<string> InvitedSquadMembers { get; set; } = new List<string>();

        public List<string> SquadMembers { get; set; } = new List<string>();

        public bool SquadActive { get; set; } = false;

        public Dictionary<string, int> Loot { get; set; } = new Dictionary<string, int>();

        public bool InviteEveryone { get; set; }

        private readonly Channel<Func<Task>> SquadThread;

        private StarSonataApi.StarSonataApi ssApi;

        private AutoResetEvent resetEvent = null;

        private bool IsCancelled = false;

        private const string filePath = @"./squad.json";

        public void Invite(string character)
        {
            ssApi.SendChatAsync($"/squadinvite {character}", MessageChannel.Team);
            if (!InvitedSquadMembers.Contains(character))
            {
                InvitedSquadMembers.Add(character);
                SaveToFile();
            }

        }

        public void SquadCreate(string leader, List<string> members)
        {
            ClearSquad();
            SquadActive = true;
            SquadLeader = leader;
            resetEvent = new AutoResetEvent(false);
            SquadThread.Writer.WriteAsync(SquadCreateTask);
            SquadThread.Writer.WriteAsync(() =>
            {
                Invite(leader);
                return Task.CompletedTask;
            });

            SquadThread.Writer.WriteAsync(WaitForLeaderJoin);

            if (InviteEveryone)
            {
                foreach (var member in members)
                {
                    if (member == leader)
                    {
                        continue;
                    }

                    SquadThread.Writer.WriteAsync(() =>
                    {
                        Invite(member);
                        return Task.CompletedTask;
                    });
                }
            }

            SaveToFile();
        }

        public void LeaveSquad()
        {
            ClearSquad();
            ssApi.SendChatAsync("/squadleave", MessageChannel.Team);
        }

        private void ClearSquad()
        {
            SquadLeader = "";
            InvitedSquadMembers.Clear();
            SquadMembers.Clear();
            Loot.Clear();
            SquadActive = false;
            IsCancelled = false;
            DeleteFile();
        }

        public void SetSSApi(StarSonataApi.StarSonataApi ssApi)
        {
            this.ssApi = ssApi;
        }

        private void SaveToFile()
        {
            var data = new
            {
                squadLeader = this.SquadLeader,
                memberList = this.InvitedSquadMembers
            };

            File.WriteAllText(filePath, JsonConvert.SerializeObject(data));
        }

        private void DeleteFile()
        {
            File.Delete(filePath);
        }

        public bool IsLeader(string name)
        {
            return name == SquadLeader;
        }

        public void LeaderJoined()
        {
            resetEvent.Set();
        }

        private Task SquadCreateTask()
        {
            ssApi.SendChatAsync($"/squadcreate", MessageChannel.Team);
            return Task.CompletedTask;
        }

        private Task WaitForLeaderJoin()
        {
            var waitEvent = resetEvent;
            bool done = false;
            bool cancel = false;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                lock (waitEvent)
                {
                    if (done)
                    {
                        return;
                    }

                    cancel = true;
                    waitEvent.Set();
                }
            });

            return Task.Run(() =>
            {
                waitEvent.WaitOne();
                lock (waitEvent)
                {
                    done = true;
                    if (cancel)
                    {
                        IsCancelled = true;
                        LeaveSquad();
                        ssApi.SendChatAsync($"Leader did not accept, squad was not created", MessageChannel.Team);
                    }
                }
            });
        }
    }
}
