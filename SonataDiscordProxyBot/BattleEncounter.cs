using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SonataDiscordProxyBot
{
    public class BattleEncounter
    {
        private DateTime encounterStart;
        private DateTime lastNotification;
        public readonly string attackedSystem;
        private List<string> attackingTeams = new List<string>();
        private List<string> attackingPlayers = new List<string>();
        private DiscordApi.DiscordApi discordApi;
        private ulong channelID;
        private bool isOver = false;
        private int totalMessageAmount = 0;

        private readonly TimeSpan interval = TimeSpan.FromMinutes(1);
        private System.Threading.CancellationToken cancellationToken;

        public BattleEncounter(DiscordApi.DiscordApi discordApi, string attackedSystem, ulong channelID)
        {
            this.discordApi = discordApi;
            this.channelID = channelID;
            this.attackedSystem = attackedSystem;
            this.encounterStart = DateTime.Now;
            this.lastNotification = DateTime.Now;
            Task.Run(async () => await CheckIfEndEncounter());
        }

        public void AddAttackingTeam(string teamName)
        {
            if (!this.attackingTeams.Exists(s => s == teamName))
            {
                this.attackingTeams.Add(teamName);
            }
        }
        public void AddAttackingPlayer(string playerName)
        {
            if (!this.attackingPlayers.Exists(s => s == playerName))
            {
                this.attackingPlayers.Add(playerName);
            }
        }

        private TimeSpan GetEncounterTime()
        {
            return (this.lastNotification - this.encounterStart);
        }

        public void UpdateLastNotification()
        {
            this.lastNotification = DateTime.Now;
            this.totalMessageAmount++;
        }

        public bool IsOver()
        {
            return this.isOver;
        }

        private void EndEncounter()
        {
            if((DateTime.Now - this.lastNotification).TotalMinutes > 0.5)
            {
                this.isOver = true;
                this.SendRaport();
            }
        }

        private async Task CheckIfEndEncounter()
        {
            while (!this.isOver)
            {
                this.EndEncounter();
                await Task.Delay(this.interval, this.cancellationToken);
            }
        }

        private void SendRaport()
        {
            var embed = new Discord.EmbedBuilder();
            embed.WithTitle($"Results of the battle in __**{this.attackedSystem}**__");
            embed.WithColor(new Discord.Color(255, 0, 0));
            embed.WithCurrentTimestamp();
            embed.AddField("Time of battle", $"From: {this.encounterStart.ToString("T")}\nTo: {this.lastNotification.ToString("T")}\nTotal: {this.GetEncounterTime().ToString(@"hh\:mm\:ss")}", false);
            embed.AddField("Statistics", $"Total number of characters attacking: {this.attackingPlayers.Count}\nTotal number of teams attacking: {this.attackingTeams.Count}\nNumber of messages: {this.totalMessageAmount}", false);
            embed.AddField("Players attacking", string.Join("\n", this.attackingPlayers.ToArray()), false);
            embed.AddField("Teams attacking", string.Join("\n", this.attackingTeams.ToArray()), false);
            _ = this.discordApi.EmbedObjectAsync(this.channelID, embed.Build(), true);
        }
    }
}
