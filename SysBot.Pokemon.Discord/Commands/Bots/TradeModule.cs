using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;
using System.Drawing.Printing;
using PKHeX.Core.AutoMod;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class TradeModule<T> : InteractionModuleBase<SocketInteractionContext> where T : PKM,new()
    {
      
        public static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;


        [SlashCommand("trade", "Receive a Pokémon From Showdown text or File")]
   
        public async Task TradeAsync([Summary("PokemonText")]string content="",Attachment PKM = default)
        {
            
            await DeferAsync();
           
            if (content != "")
            {
                var code = Info.GetRandomTradeCode();
                var lgcode = Info.GetRandomLGTradeCode();
                var set = ShowdownUtil.ConvertToShowdown(content);
                if(set == null)
                {
                    await FollowupAsync("Your text was in an incorrect format. Please go read ⁠<#872614034619367444> while you are muted for the next hour. Consider using the /simpletrade command once you are unmuted! <:happypip:872674980222107649> \nIf you feel this is in error DM the bot to appeal and santacrab will review.");
                    var user = (SocketGuildUser)Context.Interaction.User;
                    await user.SetTimeOutAsync(TimeSpan.FromHours(1));
                    return;
                }
                var template = AutoLegalityWrapper.GetTemplate(set);
                if (set.InvalidLines.Count != 0)
                {
                    var msg = $"Unable to parse Showdown Set:\n{string.Join("\n", set.InvalidLines)}";
                    msg += "\nDouble Check your spelling and text format. <#872614034619367444> for more info.";
                    await FollowupAsync(msg, ephemeral: true).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var trainer = AutoLegalityWrapper.GetTrainerInfo<T>();
                    var sav = SaveUtil.GetBlankSAV((GameVersion)trainer.Game, trainer.OT);
                    var pkm = sav.GetLegal(template, out var result);
                   
                    if (pkm is PB7)
                    {
                        if(pkm.Species == (int)Species.Mew)
                        {
                            if (pkm.IsShiny)
                            {
                                await FollowupAsync("Mew can not be Shiny in this game. PoGo Mew does not transfer and Pokeball Plus Mew is shiny locked");
                                return;
                            }
                        }
                    }

            

                    var la = new LegalityAnalysis(pkm);
                    var spec = GameInfo.Strings.Species[template.Species];
                    if(pkm is not T)
                        pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
                   

                    if (pkm is not T pk || !la.Valid)
                    {
                        var reason = result == "Timeout" ? $"That {spec} set took too long to generate." : $"I wasn't able to create a {spec} from that set.";
                        var imsg = $"Oops! {reason}";
                        if (result == "Failed")
                            imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                        await FollowupAsync(imsg, ephemeral: true).ConfigureAwait(false);
                        return;
                    }
                    if (pkm.RequiresHomeTracker() && !APILegality.AllowHOMETransferGeneration)
                    {
                        await FollowupAsync($"{SpeciesName.GetSpeciesName(pkm.Species, 2)}{(pkm.Form != 0 ? $"-{ShowdownParsing.GetStringFromForm(pkm.Form, GameInfo.Strings, pkm.Species, EntityContext.Gen9)}" : "")} requires a Home Tracker to be in this Game. You need to generate it in the correct origin game and transfer through Home.");
                        return;
                    }
                    pk.ResetPartyStats();

                    var sig = Context.User.GetFavor();
               
                    await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User,lgcode).ConfigureAwait(false);
                    return;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                    var msg = $"Oops! An unexpected problem happened with this Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                    msg += "\nDouble Check your spelling and text format. <#872614034619367444> for more info."; 
                    await FollowupAsync(msg,ephemeral:true).ConfigureAwait(false);
                    return;
                }
            }
            if(PKM != default)
            {

                var code = Info.GetRandomTradeCode();
                var lgcode = Info.GetRandomLGTradeCode();
                var sig = Context.User.GetFavor();
                await TradeAsyncAttach(PKM,code, sig, Context.User,lgcode).ConfigureAwait(false);
                return;
            }
            await FollowupAsync("You did not include any pokemon information, Please make sure the command boxes are filled out. See <#872614034619367444> for instructions and examples");
        }
        public static List<simpletradeobject> simpletradecache = new();
        [SlashCommand("simpletrade","helps you build a pokemon with a simple form")]
        
        public async Task DumbassTrade()
        {
            await DeferAsync(ephemeral: true);
            
            var trainer = AutoLegalityWrapper.GetTrainerInfo<T>();
            var sav = SaveUtil.GetBlankSAV((GameVersion)trainer.Game, trainer.OT);
            var pk =(T) EntityBlank.GetBlank(sav);
            var datasource = new FilteredGameDataSource(sav, GameInfo.Sources);
            var cache = new simpletradeobject();
            cache.user = Context.User;
            simpletradecache.Add(cache);
            cache.currenttype = "species";
            cache.opti = datasource.Species.Select(z => z.Text).ToArray();

            var component = compo(cache.currenttype, cache.page=0, cache.opti );
            await FollowupAsync("Choose", components: component, ephemeral: true);
            while (!cache.responded)
                await Task.Delay(250);
            cache.responded = false;
            var set = new ShowdownSet(cache.response.Data.Values.First());
            pk = (T)sav.GetLegalFromSet(set).Created;
            cache.opti = FormConverter.GetFormList(pk.Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, pk.Context);
            if (cache.opti.Length > 1)
            {
                var tempspec = cache.response.Data.Values.First();
                cache.currenttype = "Form";
                cache.opti = FormConverter.GetFormList(pk.Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, pk.Context);
                await Context.Interaction.ModifyOriginalResponseAsync(z => z.Components = compo(cache.currenttype, cache.page=0, cache.opti));
                while (!cache.responded)
                    await Task.Delay(250);
                cache.responded = false;
                var tempspecform = $"{tempspec}-{cache.response.Data.Values.First()}";
                set = new ShowdownSet(tempspecform);
            }
            cache.currenttype = "Shiny";
            cache.opti = new string[] { "Yes", "No" };
            await Context.Interaction.ModifyOriginalResponseAsync(z => z.Components = compo(cache.currenttype, cache.page=0, cache.opti));
            while (!cache.responded)
                await Task.Delay(250);
            cache.responded = false;
            set = new ShowdownSet($"{set.Text}\nShiny: {cache.response.Data.Values.First()}");
            pk = (T)sav.GetLegalFromSet(set).Created;
            if (!pk.PersonalInfo.Genderless)
            {
                cache.currenttype = "Gender";
                cache.opti = new string[] { "Male ♂", "Female ♀" };
                await Context.Interaction.ModifyOriginalResponseAsync(z => z.Components = compo(cache.currenttype, cache.page = 0, cache.opti));
                while (!cache.responded)
                    await Task.Delay(250);
                cache.responded = false;
                pk.Gender = cache.response.Data.Values.First() == "Male ♂" ? 0 : 1;
            }
            cache.currenttype = "Item";
            cache.opti = datasource.Items.Select(z => z.Text).ToArray();
            await Context.Interaction.ModifyOriginalResponseAsync(z => z.Components = compo(cache.currenttype, cache.page=0, cache.opti));
            while (!cache.responded)
                await Task.Delay(250);
            cache.responded = false;
            var item = datasource.Items.Where(z => z.Text == cache.response.Data.Values.First()).First();
           pk.ApplyHeldItem(item != null? item.Value : 0, sav.Context);
            cache.currenttype = "Level";
            cache.opti = new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31", "32", "33", "34", "35", "36", "37", "38", "39", "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "50", "51", "52", "53", "54", "55", "56", "57", "58", "59", "60", "61", "62", "63", "64", "65", "66", "67", "68", "69", "70", "71", "72", "73", "74", "75", "76", "77", "78", "79", "80", "81", "82", "83", "84", "85", "86", "87", "88", "89", "90", "91", "92", "93", "94", "95", "96", "97", "98", "99", "100" };
            await Context.Interaction.ModifyOriginalResponseAsync(z => z.Components = compo(cache.currenttype, cache.page=0, cache.opti));
            while (!cache.responded)
                await Task.Delay(250);
            cache.responded = false;
            pk.CurrentLevel = int.Parse(cache.response.Data.Values.First());
            cache.currenttype = "Ball";
            List<string> balllist = BallApplicator.GetLegalBalls(pk).Select(z => z.ToString()).ToList();
            balllist.Insert(0, "Any");
            cache.opti = balllist.ToArray();
            await Context.Interaction.ModifyOriginalResponseAsync(z => z.Components = compo(cache.currenttype, cache.page = 0, cache.opti));
            while (!cache.responded)
                await Task.Delay(250);
            cache.responded = false;
            if(cache.response.Data.Values.First() != "Any")
            {
                var ball = BallApplicator.GetLegalBalls(pk).Where(z => z.ToString() == cache.response.Data.Values.First()).First();
                pk.Ball = (int)ball;
            }
            await Context.Interaction.DeleteOriginalResponseAsync();
            simpletradecache.Remove(cache);
            pk = (T)sav.Legalize(pk);
            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = Context.User.GetFavor();
            
            if (pk is PB7)
            {
                if (pk.Species == (int)Species.Mew)
                {
                    if (pk.IsShiny)
                    {
                        await Context.Interaction.ModifyOriginalResponseAsync(z=>z.Content ="Mew can not be Shiny in this game. PoGo Mew does not transfer and Pokeball Plus Mew is shiny locked");
                        return;
                    }
                }
            }
            var la = new LegalityAnalysis(pk);
            var spec = GameInfo.Strings.Species[pk.Species];
            //pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;


            if (pk is not T pkm || !la.Valid)
            {
                var reason = $"I wasn't able to create a {spec} from those options.";
                var imsg = $"Oops! {reason}";
                await FollowupAsync(imsg).ConfigureAwait(false);
                return;
            }
            if (pk.RequiresHomeTracker() && APILegality.AllowHOMETransferGeneration)
            {
                await FollowupAsync($"{SpeciesName.GetSpeciesName(pk.Species,2)}{(pk.Form != 0 ? $"-{ShowdownParsing.GetStringFromForm(pk.Form, GameInfo.Strings, pk.Species, EntityContext.Gen9)}" : "")} requires a Home Tracker to be in this Game, You need to generate it in the correct origin game and transfer through Home.");
                return;
            }
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode).ConfigureAwait(false);
            return;
        }
        public static SelectMenuBuilder GetSelectMenu(string type, int page, string[] options)
        {
            var returnMenu = new SelectMenuBuilder().WithCustomId(type).WithPlaceholder($"Select a {type}");
            var newoptions = options.Skip(page * 25).Take(25);
            foreach (var option in newoptions)
            {
                returnMenu.AddOption(option, option);
            }
            
            
            return returnMenu;

        }
        public static MessageComponent compo(string type,int page, string[] options)
        {


            var nextbutton = new ActionRowBuilder().WithButton("Next 25", "next");
            var previousbutton = new ActionRowBuilder().WithButton("Previous 25", "prev");
            var select = new ComponentBuilder().WithSelectMenu(GetSelectMenu(type, page, options),0);
            select.AddRow(nextbutton);
            select.AddRow(previousbutton);
           
            

            return select.Build();
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

        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => null,
            };
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr, List<pictocodes>lgcode)
        {
            if (!pk.CanBeTraded())
            {
                await FollowupAsync("Provided Pokémon content is blocked from trading!",ephemeral:true).ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                await FollowupAsync($"{typeof(T).Name} attachment is not legal, Here's Why: {la.Report()}",ephemeral:true).ConfigureAwait(false);
                return;
            }
            if (pk.RequiresHomeTracker() && !APILegality.AllowHOMETransferGeneration && pk is IHomeTrack { HasTracker:false})
            {
                await FollowupAsync($"{SpeciesName.GetSpeciesName(pk.Species, 2)}{(pk.Form != 0 ? $"-{ShowdownParsing.GetStringFromForm(pk.Form, GameInfo.Strings, pk.Species, EntityContext.Gen9)}" : "")} requires a Home Tracker to be in this Game. You need to generate it in the correct origin game and transfer through Home.");
                return;
            }
            await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr,lgcode).ConfigureAwait(false);
        }
    }
}
public class simpletradeobject
{
    public int page { get; set; } = 0;
    public string currenttype { get; set; } = "Species";
    public string[] opti { get; set; } = Array.Empty<string>();
    public SocketMessageComponent response { get; set; }
    public bool responded { get; set; } = false;
    public SocketUser user { get; set; }
    public simpletradeobject()
    {

    }

}