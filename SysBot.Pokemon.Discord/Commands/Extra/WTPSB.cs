using PKHeX.Core;
using PKHeX.Core.AutoMod;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using static PKHeX.Core.Species;
using System.Reflection;

namespace SysBot.Pokemon.Discord
{
    public class WTPSB : InteractionModuleBase<SocketInteractionContext>
    {
        public static bool buttonpressed = false;
        public static bool tradepokemon = false;
        public static CancellationTokenSource WTPsource = new();
        public static TradeQueueInfo<PB7> Info => SysCord<PB7>.Runner.Hub.Queues.Info;
        public static PokeTradeHub<PB7> Hub = SysCord<PB7>.Runner.Hub;
       // public readonly ExtraCommandUtil<PK8> Util = new();
      
        public static GameVersion Game = GameVersion.GG;
        public static string guess = "";
        public static SocketUser usr;
        public static ushort randspecies;
        public static SocketInteractionContext con;
        [SlashCommand("wtpstart", "owner only")]
        [RequireOwner]
        public static async Task WhoseThatPokemon()
        {
           
            var wtpchan = (ITextChannel)await SysCord<PB7>._client.GetChannelAsync(Hub.Config.Discord.WTPchannelid);
            await wtpchan.AddPermissionOverwriteAsync(wtpchan.Guild.EveryoneRole, new OverwritePermissions(sendMessages: PermValue.Allow));
            await wtpchan.ModifyAsync(prop => prop.Name = wtpchan.Name.Replace("❌", "✅"));
            while (!WTPsource.IsCancellationRequested)
            {
                Stopwatch sw = new();
                sw.Restart();
                Random random = new Random();
                var code = Hub.Config.Trade.GetRandomLGTradeCode();
                var Dex = GetPokedex();
                randspecies = Dex[random.Next(Dex.Length)];
                EmbedBuilder embed = new EmbedBuilder();
                embed.Title = "Who's That Pokemon";
                embed.AddField(new EmbedFieldBuilder { Name = "instructions", Value = "Type /guess <pokemon name> to guess the name of the pokemon displayed and you get that pokemon in your actual game!" });
                if (randspecies < 891)
                    embed.ImageUrl = $"https://logoassetsgame.s3.us-east-2.amazonaws.com/wtp/pokemon/{randspecies}q.png";
                else
                    embed.ImageUrl = $"https://raw.githubusercontent.com/santacrab2/SysBot.NET/RNGstuff/finalimages/{randspecies}q.png";
                await wtpchan.SendMessageAsync(embed: embed.Build());
                while (guess.ToLower() != SpeciesName.GetSpeciesName(randspecies,2).ToLower() && sw.ElapsedMilliseconds / 1000 < 600)
                {
                    await Task.Delay(25);
                }
                var entry = File.ReadAllLines(Properties.Resources.DexFlavor)[randspecies];
                embed = new EmbedBuilder().WithFooter(entry);
                embed.Title = $"It's {SpeciesName.GetSpeciesName(randspecies,2)}";
                embed.AddField(new EmbedFieldBuilder { Name = "instructions", Value = $"Type /guess <pokemon name> to guess the name of the pokemon displayed and you get that pokemon in your actual game!" });
                if (randspecies < 891)
                    embed.ImageUrl = $"https://logoassetsgame.s3.us-east-2.amazonaws.com/wtp/pokemon/{randspecies}a.png";
                else
                    embed.ImageUrl = $"https://raw.githubusercontent.com/santacrab2/SysBot.NET/RNGstuff/finalimages/{randspecies}a.png";
                await wtpchan.SendMessageAsync(embed: embed.Build());
              
                if (guess.ToLower() == SpeciesName.GetSpeciesName(randspecies, 2).ToLower())
                {
                    var compmessage = new ComponentBuilder().WithButton("Yes", "wtpyes",ButtonStyle.Success).WithButton("No", "wtpno", ButtonStyle.Danger);
                    var embedmes = new EmbedBuilder();
                    embedmes.AddField("Receive Pokemon?", $"Would you like to receive {SpeciesName.GetSpeciesName(randspecies, 2)} in your game?");
                    await wtpchan.SendMessageAsync($"<@{usr.Id}>",embed: embedmes.Build(), components: compmessage.Build());

                    while (!buttonpressed)
                    {
                        await Task.Delay(25);
                    }
                    if (tradepokemon)
                    {

                        var set = new ShowdownSet($"{SpeciesName.GetSpeciesName(randspecies, 2)}\nShiny: Yes");
                        var template = new RegenTemplate(set);
                        var sav = SaveUtil.GetBlankSAV(GameVersion.GE, "Piplup");
                        var pk = sav.GetLegalFromSet(template, out var result);
                        if (!new LegalityAnalysis(pk).Valid)
                        {
                            set = new ShowdownSet(SpeciesName.GetSpeciesName(randspecies, 2));
                            template = new RegenTemplate(set);
                            sav = SaveUtil.GetBlankSAV(GameVersion.GE, "Piplup");
                            pk = sav.GetLegalFromSet(template, out result);
                        }
                        pk.Ball = BallApplicator.ApplyBallLegalByColor(pk);
                        ushort[] sugmov = MoveSetApplicator.GetMoveSet(pk, true);
                        pk.SetMoves(sugmov);
                        int natue = random.Next(24);
                        pk.Nature = natue;
                       

                        await QueueHelper<PB7>.AddToQueueAsync(con, 0, usr.Username, RequestSignificance.None, (PB7)pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr,code).ConfigureAwait(false);
                    }
                    usr = null;
                    guess = "";
                    tradepokemon = false;
                    buttonpressed = false;
                }
                usr = null;
                guess = "";
            }
            WTPsource = new();
        }
        [SlashCommand("guess","guess what the pokemon displayed is")]
       
        public async Task WTPguess([Summary("pokemon","put the pokemon name here")]string userguess)
        {
            if (userguess.ToLower() == SpeciesName.GetSpeciesName(randspecies, 2).ToLower())
            {
                await RespondAsync($"{Context.User.Username} You are correct! It's {userguess}");
                guess = userguess;
                usr = Context.User;
                con = Context;
            }
            else
                await RespondAsync($"{Context.User.Username} You are incorrect. It is not {userguess}");
        }
        [SlashCommand("wtpcancel","owner only")]
        [RequireOwner]
        public async Task wtpcancel()
        {
            WTPsource.Cancel();
            await RespondAsync("\"Who's That Pokemon\" mode stopped.",ephemeral:true);
            ITextChannel wtpchannel = (ITextChannel)Context.Channel;
            await wtpchannel.ModifyAsync(newname => newname.Name = wtpchannel.Name.Replace("✅","❌"));
            await wtpchannel.AddPermissionOverwriteAsync(wtpchannel.Guild.EveryoneRole, new OverwritePermissions(sendMessages: PermValue.Deny));
        }

        public static ushort[] GetPokedex()
        {
            List<ushort> dex = new();
            for (ushort i = 1; i < 151; i++)
            {
               
              

                var species = SpeciesName.GetSpeciesNameGeneration(i, 2, 7);
                var set = new ShowdownSet($"{species}{(i == (int)NidoranF ? "-F" : i == (int)NidoranM ? "-M" : "")}");
                var template = AutoLegalityWrapper.GetTemplate(set);
                var trainer = AutoLegalityWrapper.GetTrainerInfo<PB7>();
                var sav = SaveUtil.GetBlankSAV((GameVersion)trainer.Game, trainer.OT);
                _ = sav.GetLegal(template, out var result);

                if (result == "Regenerated")
                    dex.Add(i);
            }
            return dex.ToArray();
        }
        private async Task HandleMessageAsync(SocketMessage arg)
        {
            if (arg is not SocketUserMessage msg)
                return;
            guess = msg.ToString();
        }
    }
}
