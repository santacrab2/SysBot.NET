using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading.Tasks;
using PKHeX.Core.AutoMod;

namespace SysBot.Pokemon.Discord
{
    public static class AutoLegalityExtensionsDiscord
    {
        public static async Task ReplyWithLegalizedSetAsync(this SocketInteractionContext channel, ITrainerInfo sav, ShowdownSet set)
        {
            if (set.Species <= 0)
            {
                await channel.Interaction.FollowupAsync("Oops! I wasn't able to interpret your message! If you intended to convert something, please double check what you're pasting!").ConfigureAwait(false);
                
                return;
            }

            try
            {
                if (set.Species == (ushort)Species.Meloetta && set.Shiny)
                {
                    ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Fishy;
                    APILegality.AllowHOMETransferGeneration = true;
                }
                var template = AutoLegalityWrapper.GetTemplate(set);
                var pkm = sav.GetLegal(template, out var result);
                if (pkm is PB7)
                {
                    if (pkm.Species == (int)Species.Mew)
                    {
                        if (pkm.IsShiny)
                        {
                            await channel.Interaction.FollowupAsync("Mew can not be Shiny in this game. PoGo Mew does not transfer and Pokeball Plus Mew is shiny locked");
                            return;
                        }
                    }
                }
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];
                if (!la.Valid)
                {
                    var reason = result == "Timeout" ? $"That {spec} set took too long to generate." : result == "VersionMismatch" ? "Request refused: PKHeX and Auto-Legality Mod version mismatch." : $"I wasn't able to create a {spec} from that set.";
                    var imsg = $"Oops! {reason}";
                    if (result == "Failed")
                        imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                    await channel.Interaction.FollowupAsync(imsg).ConfigureAwait(false);
                    if (set.Species == (ushort)Species.Meloetta && set.Shiny)
                    {
                        ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Invalid;
                        APILegality.AllowHOMETransferGeneration = false;
                    }
                    return;
                }

                var msg = $"Here's your ({result}) legalized PKM for {spec} ({la.EncounterOriginal.Name})!";
                await channel.SendPKMAsync(pkm, msg + $"\n{ReusableActions.GetFormattedShowdownText(pkm)}").ConfigureAwait(false);
                if (set.Species == (ushort)Species.Meloetta && set.Shiny)
                {
                    ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Invalid;
                    APILegality.AllowHOMETransferGeneration = false;
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(AutoLegalityExtensionsDiscord));
                var msg = $"Oops! An unexpected problem happened with this Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                await channel.Interaction.FollowupAsync(msg).ConfigureAwait(false);
            }
        }

        public static async Task ReplyWithLegalizedSetAsync(this SocketInteractionContext channel, string content, int gen)
        {
            var set = ShowdownUtil.ConvertToShowdown(content);
            var sav = AutoLegalityWrapper.GetTrainerInfo(gen);
            await channel.ReplyWithLegalizedSetAsync(sav, set).ConfigureAwait(false);
        }

        public static async Task ReplyWithLegalizedSetAsync<T>(this SocketInteractionContext channel, string content) where T : PKM, new()
        {
           
            var set = ShowdownUtil.ConvertToShowdown(content);
            var trainer = AutoLegalityWrapper.GetTrainerInfo<T>();
            var sav = SaveUtil.GetBlankSAV(trainer.Version, trainer.OT);
            await channel.ReplyWithLegalizedSetAsync(sav, set).ConfigureAwait(false);
        }

        public static async Task ReplyWithLegalizedSetAsync(this SocketInteractionContext channel, IAttachment att)
        {
            var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
            if (!download.Success)
            {
                await channel.Interaction.FollowupAsync(download.ErrorMessage).ConfigureAwait(false);
                return;
            }

            var pkm = download.Data!;
            if (new LegalityAnalysis(pkm).Valid)
            {
                await channel.Interaction.FollowupAsync($"{download.SanitizedFileName}: Already legal.").ConfigureAwait(false);
                return;
            }

            var legal = pkm.LegalizePokemon();
            if (!new LegalityAnalysis(legal).Valid)
            {
                await channel.Interaction.FollowupAsync($"{download.SanitizedFileName}: Unable to legalize.").ConfigureAwait(false);
                return;
            }

            legal.RefreshChecksum();

            var msg = $"Here's your legalized PKM for {download.SanitizedFileName}!\n{ReusableActions.GetFormattedShowdownText(legal)}";
            await channel.SendPKMAsync(legal, msg).ConfigureAwait(false);
        }
    }
}