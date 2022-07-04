using PKHeX.Core;
using Discord;
using Discord.Interactions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]
    [DefaultMemberPermissions(GuildPermission.ViewChannel)]
    public class TradeAdditionsModule : InteractionModuleBase<SocketInteractionContext> 
    {
        private static TradeQueueInfo<PK8> Info => SysCord<PK8>.Runner.Hub.Queues.Info;
        private readonly ExtraCommandUtil<PK8> Util = new();
        private readonly LairBotSettings LairSettings = SysCord<PK8>.Runner.Hub.Config.LairSWSH;
        private readonly RollingRaidSettings RollingRaidSettings = SysCord<PK8>.Runner.Hub.Config.RollingRaidSWSH;


        [SlashCommand("fixot", "Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
        public async Task FixAdOT()
        {
            await DeferAsync();
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<PK8>.AddToQueueAsync(Context, code, Context.User.Username, sig, new PK8(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [SlashCommand("itemtrade", "Makes the bot trade you a Pokémon holding the requested item")]

        public async Task ItemTrade(string item)
        {
            await DeferAsync();
            var code = Info.GetRandomTradeCode();
            await ItemTrade(code, item).ConfigureAwait(false);
        }

      
        public async Task ItemTrade(int code, string item)
        {
            Species species = Species.Diglett;
            var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((int)species, 2, 8)} @ {item.Trim()}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
            var pkm = sav.GetLegal(template, out var result);
            pkm = EntityConverter.ConvertToType(pkm, typeof(PK8), out _) ?? pkm;
            if (pkm.HeldItem == 0)
            {
                await FollowupAsync($"{Context.User.Username}, the item you entered wasn't recognized.").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);
         
            
            if (pkm is not PK8 pk || !la.Valid)
            {
                var reason = result == "Timeout" ? "That set took too long to generate." : "I wasn't able to create something from that.";
                var imsg = $"Oops! {reason} Here's my best attempt for that {species}!";
                await Context.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await QueueHelper<PK8>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }

       

        [SlashCommand("lairembed","starts lair embed routine")]
        [RequireOwner]
        public async Task InitializeEmbeds()
        {
            await DeferAsync();
            if (LairSettings.ResultsEmbedChannels == string.Empty)
            {
                await FollowupAsync("No channels to post embeds in.",ephemeral:true).ConfigureAwait(false);
                return;
            }

            List<ulong> channels = new();
            foreach (var channel in LairSettings.ResultsEmbedChannels.Split(',', ' '))
            {
                if (ulong.TryParse(channel, out ulong result) && !channels.Contains(result))
                    channels.Add(result);
            }

            if (channels.Count == 0)
            {
                await FollowupAsync("No valid channels found.",ephemeral:true).ConfigureAwait(false);
                return;
            }

            await FollowupAsync(!LairBotUtil.EmbedsInitialized ? "Lair Embed task started!" : "Lair Embed task stopped!",ephemeral:true).ConfigureAwait(false);
            if (LairBotUtil.EmbedsInitialized)
                LairBotUtil.EmbedSource.Cancel();
            else _ = Task.Run(async () => await LairEmbedLoop(channels));
            LairBotUtil.EmbedsInitialized ^= true;
        }

        private async Task LairEmbedLoop(List<ulong> channels)
        {
            var ping = SysCord<PK8>.Runner.Hub.Config.StopConditions.MatchFoundEchoMention;
            while (!LairBotUtil.EmbedSource.IsCancellationRequested)
            {
                if (LairBotUtil.EmbedMon.Item1 != null)
                {
                    var url = TradeExtensions<PK8>.PokeImg(LairBotUtil.EmbedMon.Item1, LairBotUtil.EmbedMon.Item1.CanGigantamax, false);
                    var ballStr = $"{(Ball)LairBotUtil.EmbedMon.Item1.Ball}".ToLower();
                    var ballUrl = $"https://serebii.net/itemdex/sprites/pgl/{ballStr}ball.png";
                    var author = new EmbedAuthorBuilder { IconUrl = ballUrl, Name = LairBotUtil.EmbedMon.Item2 ? "Legendary Caught!" : "Result found, but not quite Legendary!" };
                    var embed = new EmbedBuilder { Color = Color.Blue, ThumbnailUrl = url }.WithAuthor(author).WithDescription(ShowdownParsing.GetShowdownText(LairBotUtil.EmbedMon.Item1));

                    var userStr = ping.Replace("<@", "").Replace(">", "");
                    if (ulong.TryParse(userStr, out ulong usr))
                    {
                        var user = await Context.Client.Rest.GetUserAsync(usr).ConfigureAwait(false);
                        embed.WithFooter(x => { x.Text = $"Requested by: {user}"; });
                    }

                    foreach (var guild in Context.Client.Guilds)
                    {
                        foreach (var channel in channels)
                        {
                            if (guild.Channels.FirstOrDefault(x => x.Id == channel) != default)
                                await guild.GetTextChannel(channel).SendMessageAsync(ping, embed: embed.Build()).ConfigureAwait(false);
                        }
                    }
                    LairBotUtil.EmbedMon.Item1 = null;
                }
                else await Task.Delay(1_000).ConfigureAwait(false);
            }
            LairBotUtil.EmbedSource = new();
        }

        [SlashCommand("raidembed", "Initialize posting of RollingRaidBot embeds to specified Discord channels.")]
  
        [RequireOwner]
        public async Task InitializeRaidEmbeds()
        {
            await DeferAsync();
            if (RollingRaidSettings.RollingRaidEmbedChannels == string.Empty)
            {
                await FollowupAsync("No channels to post embeds in.",ephemeral:true).ConfigureAwait(false);
                return;
            }

            List<ulong> channels = new();
            foreach (var channel in RollingRaidSettings.RollingRaidEmbedChannels.Split(',', ' '))
            {
                if (ulong.TryParse(channel, out ulong result) && !channels.Contains(result))
                    channels.Add(result);
            }

            if (channels.Count == 0)
            {
                await FollowupAsync("No valid channels found.",ephemeral:true).ConfigureAwait(false);
                return;
            }

            await FollowupAsync(!RollingRaidBot.RollingRaidEmbedsInitialized ? "RollingRaid Embed task started!" : "RollingRaid Embed task stopped!",ephemeral:true).ConfigureAwait(false);
            if (RollingRaidBot.RollingRaidEmbedsInitialized)
                RollingRaidBot.RaidEmbedSource.Cancel();
            else _ = Task.Run(async () => await RollingRaidEmbedLoop(channels, RollingRaidBot.RaidEmbedSource.Token));
            RollingRaidBot.RollingRaidEmbedsInitialized ^= true;
        }

        private async Task RollingRaidEmbedLoop(List<ulong> channels, CancellationToken token)
        {
            while (!RollingRaidBot.RaidEmbedSource.IsCancellationRequested)
            {
                if (RollingRaidBot.EmbedQueue.TryDequeue(out var embedInfo))
                {
                    var url = TradeExtensions<PK8>.PokeImg(embedInfo.Item1, embedInfo.Item1.CanGigantamax, false);
                    var embed = new EmbedBuilder
                    {
                        Title = embedInfo.Item3,
                        Description = embedInfo.Item2,
                        Color = Color.Blue,
                        ThumbnailUrl = url,
                    };

                    foreach (var guild in Context.Client.Guilds)
                    {
                        var channel = guild.Channels.FirstOrDefault(x => channels.Contains(x.Id));
                        if (channel is not null && channel is IMessageChannel ch)
                            await ch.SendMessageAsync(null, false, embed: embed.Build()).ConfigureAwait(false);
                    }
                }
                else await Task.Delay(0_500, token).ConfigureAwait(false);
            }
            RollingRaidBot.RollingRaidEmbedsInitialized = false;
            RollingRaidBot.RaidEmbedSource = new();
        }

       
    }
}