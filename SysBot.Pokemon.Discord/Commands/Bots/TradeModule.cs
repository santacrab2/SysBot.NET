using System;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class TradeModule : InteractionModuleBase<SocketInteractionContext>
    {
        public static TradeQueueInfo<PB8> Info => SysCord<PB8>.Runner.Hub.Queues.Info;


        [SlashCommand("trade", "Receive a Pokémon From Showdown text or File")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsync([Summary("PokemonText")]string content="",Attachment pb8 = default)
        {
            await DeferAsync();
            if (content != "")
            {
                
                var code = new Random().Next(99999999);
                var set = ShowdownUtil.ConvertToShowdown(content);
                var template = AutoLegalityWrapper.GetTemplate(set);
                if (set.InvalidLines.Count != 0)
                {
                    var msg = $"Unable to parse Showdown Set:\n{string.Join("\n", set.InvalidLines)}";
                    await FollowupAsync(msg, ephemeral: true).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var sav = AutoLegalityWrapper.GetTrainerInfo<PB8>();
                    var pkm = sav.GetLegal(template, out var result);
                    if (pkm.Species == 132)
                        TradeExtensions<PB8>.DittoTrade(pkm);

                    if (pkm.Nickname.ToLower() == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                        TradeExtensions<PB8>.EggTrade(pkm,template);

                    var la = new LegalityAnalysis(pkm);
                    var spec = GameInfo.Strings.Species[template.Species];
                    pkm = EntityConverter.ConvertToType(pkm, typeof(PB8), out _) ?? pkm;
                   

                    if (pkm is not PB8 pk || !la.Valid)
                    {
                        var reason = result == "Timeout" ? $"That {spec} set took too long to generate." : $"I wasn't able to create a {spec} from that set.";
                        var imsg = $"Oops! {reason}";
                        if (result == "Failed")
                            imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                        await FollowupAsync(imsg, ephemeral: true).ConfigureAwait(false);
                        return;
                    }
                    pk.ResetPartyStats();

                    var sig = Context.User.GetFavor();
                    await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    LogUtil.LogSafe(ex, nameof(TradeModule));
                    var msg = $"Oops! An unexpected problem happened with this Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                    await FollowupAsync(msg,ephemeral:true).ConfigureAwait(false);
                }
            }
            if(pb8 != default)
            {
                
                var code = new Random().Next(99999999);
                var sig = Context.User.GetFavor();
                await TradeAsyncAttach(pb8,code, sig, Context.User).ConfigureAwait(false);
            }
        }






        private RemoteControlAccess GetReference(ulong id, string comment) => new()
        {
            ID = id,
            Name = id.ToString(),
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
        };

        [SlashCommand("tradeuser","owner only")]
        [RequireSudo]
        public async Task TradeAsyncAttachUser(SocketUser user,Attachment pkfile,int code = 0)
        {
            await DeferAsync(); 
            if (code == 0)
                code = new Random().Next(99999999);
            var usr = user;
            var sig = usr.GetFavor();
            await TradeAsyncAttach(pkfile,code, sig, usr).ConfigureAwait(false);
        }

    

        private async Task TradeAsyncAttach(Attachment pkfile,int code, RequestSignificance sig, SocketUser usr)
        {
            var attachment = pkfile;
            if (attachment == default)
            {
                await FollowupAsync("No attachment provided!").ConfigureAwait(false);
                return;
            }

            var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
            var pk = GetRequest(att);
            if (pk == null)
            {
                await FollowupAsync("Attachment provided is not compatible with this module!",ephemeral:true).ConfigureAwait(false);
                return;
            }

            await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr).ConfigureAwait(false);
        }

        private static PB8? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                PB8 pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(PB8), out _) as PB8,
            };
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, PB8 pk, RequestSignificance sig, SocketUser usr)
        {
            if (!pk.CanBeTraded())
            {
                await FollowupAsync("Provided Pokémon content is blocked from trading!",ephemeral:true).ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                await FollowupAsync($"{typeof(PB8).Name} attachment is not legal, Here's Why: {la.Report()}",ephemeral:true).ConfigureAwait(false);
                return;
            }

            await QueueHelper<PB8>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr).ConfigureAwait(false);
        }
    }
}
