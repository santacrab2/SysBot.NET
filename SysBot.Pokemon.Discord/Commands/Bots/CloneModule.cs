using Discord;
using System;
using Discord.Interactions;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class CloneModule : InteractionModuleBase<SocketInteractionContext> 
    {
        public static TradeQueueInfo<PK8> Info => SysCord<PK8>.Runner.Hub.Queues.Info;

        [SlashCommand("clone", "Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesClone))]
        public async Task CloneAsync([Summary("TradeCode","leave it blank for random")]int code = 0)
        {
            await DeferAsync();
            if (code == 0)
                code = new Random().Next(99999999);
            var sig = Context.User.GetFavor();
            await QueueHelper.AddToQueueAsync(Context, code, Context.User.Username, sig, new PK8(), PokeRoutineType.Clone, PokeTradeType.Clone).ConfigureAwait(false);
        }

    }
}
