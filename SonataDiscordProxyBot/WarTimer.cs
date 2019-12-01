using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
public static class RxExtension
{
    public static IObservable<T> RateLimit<T>(this IObservable<T> source, TimeSpan minDelay)
    {
        return source.Select(x =>
            Observable.Empty<T>()
                .Delay(minDelay)
                .StartWith(x)
        ).Concat();
    }
}

namespace SonataDiscordProxyBot
{
    class WarTimer
    {
        private DateTime lastEventDate;
        private Subject<DateTime> warMessageSubject = new Subject<DateTime>();
        private IObservable<DateTime> warMessageRecieved => warMessageSubject.AsObservable();
        private DiscordApi.DiscordApi discordApi;
        private string teamName;
        private ulong channelID;

        public WarTimer(DiscordApi.DiscordApi discordApi, string teamName, ulong channelID)
        {
            this.discordApi = discordApi;
            this.teamName = teamName;
            this.channelID = channelID;

            this.warMessageRecieved.Delay(TimeSpan.FromMinutes(30)).Subscribe(this.SendFirstWarReminder);
            this.warMessageRecieved.Delay(TimeSpan.FromMinutes(55)).Subscribe(this.SendSecondWarReminder);
            this.warMessageRecieved.Delay(TimeSpan.FromMinutes(60)).Subscribe(this.WarActiveReminder);
        }

        public void Notify(string target)
        {
            string message;
            if (target == "you")
            {
                message = $"@everyone -> Team **{this.teamName}** has declared war on the team!";
            }
            else
            {
                message = $"@everyone -> Team **{this.teamName}** has declared war on **{target}**!";
            }

            this.discordApi.SendCustomMessage(this.channelID, message);
            this.lastEventDate = DateTime.Now;
            this.warMessageSubject.OnNext(this.lastEventDate);
        }

        public void OnCancelWarMessage()
        {
            this.lastEventDate = DateTime.Now;
        }

        private void SendFirstWarReminder(DateTime time)
        {
            if(time != this.lastEventDate)
            {
                return;
            }

            this.discordApi.SendCustomMessage(this.channelID, $"@everyone -> War with team **{this.teamName}** will become active in around **30 minutes!**");
        }

        private void SendSecondWarReminder(DateTime time)
        {
            if (time != this.lastEventDate)
            {
                return;
            }

            this.discordApi.SendCustomMessage(this.channelID, $"@everyone -> War with team **{this.teamName}** will become active in **under 5 minutes!**");
        }

        private void WarActiveReminder(DateTime time)
        {
            if (time != this.lastEventDate)
            {
                return;
            }

            this.discordApi.SendCustomMessage(this.channelID, $"@everyone -> War with team **{this.teamName}** is **NOW ACTIVE!!!**");
        }
    }
}
