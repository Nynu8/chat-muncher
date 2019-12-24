using System;
using System.Collections.Generic;
using System.Text;

namespace SonataDiscordProxyBot
{
    public class BattleEncounter
    {
        private DateTime encounterStart;
        private DateTime lastNotification;
        public readonly string attackedSystem;
        private List<string> attackingTeams;
        private List<string> attackingPlayers;
        private DiscordApi.DiscordApi discordApi;
        private ulong channelID;
        private bool isOver = false;

        BattleEncounter(DiscordApi.DiscordApi discordApi, string attackedSystem, ulong channelID)
        {
            this.discordApi = discordApi;
            this.channelID = channelID;
            this.attackedSystem = attackedSystem;
            this.encounterStart = new DateTime();
            this.lastNotification = new DateTime();
        }

        public void AddAttackingTeam(string teamName)
        {
            if (this.attackingTeams.Find(s => s.Equals(teamName)) != null)
            {
                attackingTeams.Add(teamName);
            }
        }
        public void AddAttackingPlayer(string playerName)
        {
            if (this.attackingPlayers.Find(s => s.Equals(playerName)) != null)
            {
                attackingTeams.Add(playerName);
            }
        }

        public double GetEncounterTimeInMinutes()
        {
            return (new DateTime() - this.encounterStart).TotalMinutes;
        }

        public void UpdateLastNotification()
        {
            this.lastNotification = new DateTime();
        }

        public bool IsOver()
        {
            return this.isOver;
        }
    }
}
