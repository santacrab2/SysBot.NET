using System;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class TradeModule : InteractionModuleBase<SocketInteractionContext>
    {
        public static TradeQueueInfo<PB7> Info => SysCord<PB7>.Runner.Hub.Queues.Info;


        [SlashCommand("trade", "Receive a Pokémon From Showdown text or File")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsync([Summary("PokemonText")]string content="",Attachment PB7 = default)
        {
            await DeferAsync();
            if (content != "")
            {

                var code = Info.GetRandomLGTradeCode();
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
                    var trainer = AutoLegalityWrapper.GetTrainerInfo<PB7>();
                    var sav = SaveUtil.GetBlankSAV((GameVersion)trainer.Game, trainer.OT);
                    var pkm = sav.GetLegal(template, out var result);
               

            

                    var la = new LegalityAnalysis(pkm);
                    var spec = GameInfo.Strings.Species[template.Species];
                    pkm = EntityConverter.ConvertToType(pkm, typeof(PB7), out _) ?? pkm;
                   

                    if (pkm is not PB7 pk || !la.Valid)
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
                    await AddTradeToQueueAsync(0, Context.User.Username, pk, sig, Context.User,code).ConfigureAwait(false);
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
            if(PB7 != default)
            {

                var code = Info.GetRandomLGTradeCode();
                var sig = Context.User.GetFavor();
                await TradeAsyncAttach(PB7,0, sig, Context.User,code).ConfigureAwait(false);
            }
        }






        private RemoteControlAccess GetReference(ulong id, string comment) => new()
        {
            ID = id,
            Name = id.ToString(),
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
        };


    

        private async Task TradeAsyncAttach(Attachment pkfile,int code, RequestSignificance sig, SocketUser usr, List<pictocodes>lgcode)
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

            await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr,lgcode).ConfigureAwait(false);
        }

        private static PB7? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                PB7 pk => pk,
                _ => null,
            };
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, PB7 pk, RequestSignificance sig, SocketUser usr, List<pictocodes>lgcode)
        {
            if (!pk.CanBeTraded())
            {
                await FollowupAsync("Provided Pokémon content is blocked from trading!",ephemeral:true).ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                await FollowupAsync($"{typeof(PB7).Name} attachment is not legal, Here's Why: {la.Report()}",ephemeral:true).ConfigureAwait(false);
                return;
            }

            await QueueHelper<PB7>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr,lgcode).ConfigureAwait(false);
        }
    }
}
