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
    public class CloneModule<T> : InteractionModuleBase<SocketInteractionContext> where T : PKM, new() 
    {
        public static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [SlashCommand("clone", "Clones the Pokémon you show via Link Trade.")]
    
        public async Task CloneAsync()
        {
            await DeferAsync();
            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone,lgcode).ConfigureAwait(false);
        }

    }
}
