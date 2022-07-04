using Discord;
using System;
using Discord.Interactions;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class DumpModule : InteractionModuleBase<SocketInteractionContext> 
    {
        public static TradeQueueInfo<PB8> Info => SysCord<PB8>.Runner.Hub.Queues.Info;

        [SlashCommand("dump", "Dumps the Pokémon you show via Link Trade.")]
 
        public async Task DumpAsync()
        {
            await DeferAsync();
           
            var code = new Random().Next(99999999);
            
            var sig = Context.User.GetFavor();
            await QueueHelper<PB8>.AddToQueueAsync(Context, code, Context.User.Username, sig, new PB8(), PokeRoutineType.Dump, PokeTradeType.Dump).ConfigureAwait(false);
        }
    }
}