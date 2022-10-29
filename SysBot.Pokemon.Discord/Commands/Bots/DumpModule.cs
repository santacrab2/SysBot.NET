using Discord;
using System;
using Discord.Interactions;
using PKHeX.Core;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class DumpModule : InteractionModuleBase<SocketInteractionContext> 
    {
        public static TradeQueueInfo<PB7> Info => SysCord<PB7>.Runner.Hub.Queues.Info;

        [SlashCommand("dump", "Dumps the Pokémon you show via Link Trade.")]
 
        public async Task DumpAsync()
        {
            await DeferAsync();

            var code = Info.GetRandomLGTradeCode();

            var sig = Context.User.GetFavor();
            await QueueHelper<PB7>.AddToQueueAsync(Context, 0, Context.User.Username, sig, new PB7(), PokeRoutineType.Dump, PokeTradeType.Dump,code).ConfigureAwait(false);
        }
    }
}