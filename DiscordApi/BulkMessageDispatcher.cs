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

namespace DiscordApi
{
    public class BulkMessageDispatcher
    {
        private Subject<Tuple<string, ulong>> delayedMessages = new Subject<Tuple<string, ulong>>();

        private IObservable<Tuple<string, ulong>> WhenMessageReceived => this.delayedMessages.AsObservable();

        private DiscordApi discordApi;

        public BulkMessageDispatcher(DiscordApi discordApi, string message, ulong channelID, int delayInSeconds, int amount)
        {
            this.discordApi = discordApi;

            //this.WhenMessageReceived.RateLimit(TimeSpan.FromSeconds(delayInSeconds)).Subscribe(this.DoMessage);

            StartSending(new Tuple<string, ulong>(message, channelID));
        }

        public void StartSending(Tuple<string, ulong> messageData)
        {
            this.delayedMessages.OnNext(messageData);
        }


        private void DoMessage(Tuple<string, ulong> messageData)
        {
            this.discordApi.SendCustomMessage(messageData.Item2, messageData.Item1);
        }
    }
}

