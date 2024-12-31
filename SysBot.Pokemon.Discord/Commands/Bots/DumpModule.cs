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
    public class DumpModule<T> : InteractionModuleBase<SocketInteractionContext> where T : PKM, new()
    {
        public static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [SlashCommand("dump", "Dumps the Pokémon you show via Link Trade.")]
 
        public async Task DumpAsync()
        {
            await DeferAsync();
            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump,lgcode).ConfigureAwait(false);
        }
    }
}