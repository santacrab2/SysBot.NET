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
    public class CloneModule : InteractionModuleBase<SocketInteractionContext> 
    {
        public static TradeQueueInfo<PB7> Info => SysCord<PB7>.Runner.Hub.Queues.Info;

        [SlashCommand("clone", "Clones the Pokémon you show via Link Trade.")]
    
        public async Task CloneAsync()
        {
            await DeferAsync();
            var code = Info.GetRandomLGTradeCode();
           
            var sig = Context.User.GetFavor();
            await QueueHelper<PB7>.AddToQueueAsync(Context, 0, Context.User.Username, sig, new PB7(), PokeRoutineType.Clone, PokeTradeType.Clone,code).ConfigureAwait(false);
        }

    }
}
