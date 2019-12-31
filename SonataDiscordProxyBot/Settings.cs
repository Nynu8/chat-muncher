namespace SonataDiscordProxyBot
{
    public class Settings
    {
        public ulong BotDiscordId { get; set; }

        public ulong AnimeBotId { get; set; }

        public Mappings ChannelMappings { get; set; }

        public ulong CommandChannel { get; set; }

        public string DiscordToken { get; set; }

        public ulong ErrorChannel { get; set; }

        public string GamePassword { get; set; }

        public string GameUsername { get; set; }

        public string ServerUrl { get; set; }

        public ulong WarMessagesChannelID { get; set; }

        public CustomMessages[] CustomMessages { get; set; }
    }
}
