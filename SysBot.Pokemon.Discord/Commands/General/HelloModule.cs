using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class HelloModule : InteractionModuleBase<SocketInteractionContext> 
    {
        [SlashCommand("hello", "Say hello to the bot")]
      
        public async Task PingAsync()
        {
            var str = SysCordSettings.Settings.HelloResponse;
            var msg = string.Format(str, Context.User.Mention);
            await RespondAsync(msg, ephemeral:true).ConfigureAwait(false);
      
        }
    }
}