using Discord;
using System;
using Discord.Interactions;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class SeedCheckModule : InteractionModuleBase<SocketInteractionContext>
    {
        public static TradeQueueInfo<PK8> Info => SysCord<PK8>.Runner.Hub.Queues.Info;

        [SlashCommand("seedcheck", "Checks the seed for a Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSeed))]
        public async Task SeedCheckAsync([Summary("TradeCode","leave blank for random")]int code = 0)
        {
            await DeferAsync();
            if(code == 0)
                code = new Random().Next(99999999);
            var sig = Context.User.GetFavor();
            await QueueHelper.AddToQueueAsync(Context, code, Context.User.Username, sig, new PK8(), PokeRoutineType.SeedCheck, PokeTradeType.Seed).ConfigureAwait(false);
        }

  

   

       

        [SlashCommand("findframe","shows a shiny frame from the provided seed.")]
       
        public async Task FindFrameAsync(string seedString)
        {
            var me = SysCord<PK8>.Runner;
            var hub = me.Hub;

            seedString = seedString.ToLower();
            if (seedString.StartsWith("0x"))
                seedString = seedString[2..];

            var seed = Util.GetHexValue64(seedString);

            var r = new SeedSearchResult(Z3SearchResult.Success, seed, -1, hub.Config.SeedCheck.ResultDisplayMode);
            var msg = r.ToString();

            var embed = new EmbedBuilder { Color = Color.LighterGrey };

            embed.AddField(x =>
            {
                x.Name = $"Seed: {seed:X16}";
                x.Value = msg;
                x.IsInline = false;
            });
            await RespondAsync($"Here are the details for `{r.Seed:X16}`:", embed: embed.Build(),ephemeral:true).ConfigureAwait(false);
        }
    }
}
