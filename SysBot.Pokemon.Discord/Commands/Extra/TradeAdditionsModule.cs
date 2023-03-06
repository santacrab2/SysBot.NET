using PKHeX.Core;
using Discord;
using Discord.Interactions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading.Channels;
using NLog.Fluent;

namespace SysBot.Pokemon.Discord
{
    [EnabledInDm(false)]

    public class TradeAdditionsModule<T> : InteractionModuleBase<SocketInteractionContext> where T : PKM, new()
    {
        
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;
        private readonly ExtraCommandUtil<T> Util = new();
        private readonly LairBotSettings LairSettings = SysCord<T>.Runner.Hub.Config.LairSWSH;
        private readonly RollingRaidSettings RollingRaidSettings = SysCord<T>.Runner.Hub.Config.RollingRaidSWSH;


       

       

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
            var ping = SysCord<T>.Runner.Hub.Config.StopConditions.MatchFoundEchoMention;
            while (!LairBotUtil.EmbedSource.IsCancellationRequested)
            {
                if (LairBotUtil.EmbedMon.Item1 != null)
                {
                    var url = TradeExtensions<T>.PokeImg(LairBotUtil.EmbedMon.Item1, LairBotUtil.EmbedMon.Item1.CanGigantamax, false);
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

        [SlashCommand("raidembedstart", "Initialize posting of RollingRaidBot embeds to specified Discord channels.")]

        public async Task InitializeRaidEmbeds()
        {
            await DeferAsync(ephemeral:true);
            if (RollingRaidSettings.RollingRaidEmbedChannels == string.Empty)
            {
                await FollowupAsync("No channels to post embeds in.").ConfigureAwait(false);
                return;
            }

            List<ulong> channels = new();
            List<ITextChannel> embedChannels = new();
            if (!RollingRaidBot.RollingRaidEmbedsInitialized)
            {
                var chStrings = RollingRaidSettings.RollingRaidEmbedChannels.Split(',');
                foreach (var channel in chStrings)
                {
                    if (ulong.TryParse(channel, out ulong result) && !channels.Contains(result))
                        channels.Add(result);
                }

                if (channels.Count == 0)
                {
                    await FollowupAsync("No valid channels found.").ConfigureAwait(false);
                    return;
                }

                foreach (var guild in Context.Client.Guilds)
                {
                    foreach (var id in channels)
                    {
                        var channel = guild.Channels.FirstOrDefault(x => x.Id == id);
                        if (channel is not null && channel is ITextChannel ch)
                            embedChannels.Add(ch);
                    }
                }

                if (embedChannels.Count == 0)
                {
                    await FollowupAsync("No matching guild channels found.").ConfigureAwait(false);
                    return;
                }
            }

            RollingRaidBot.RollingRaidEmbedsInitialized ^= true;
            await FollowupAsync(!RollingRaidBot.RollingRaidEmbedsInitialized ? "RollingRaid Embed task stopped!" : "RollingRaid Embed task started!",ephemeral:true).ConfigureAwait(false);

            if (!RollingRaidBot.RollingRaidEmbedsInitialized)
            {
                RollingRaidBot.RaidEmbedSource.Cancel();
                return;
            }

            RollingRaidBot.RaidEmbedSource = new();
            _ = Task.Run(async () => await RollingRaidEmbedLoop(embedChannels).ConfigureAwait(false));
        }

        private static async Task RollingRaidEmbedLoop(List<ITextChannel> channels)
        {
            while (!RollingRaidBot.RaidEmbedSource.IsCancellationRequested)
            {
                if (RollingRaidBot.EmbedQueue.TryDequeue(out var embedInfo))
                {
                    var url = TradeExtensions<T>.PokeImg(embedInfo.RaidPk, embedInfo.RaidPk.CanGigantamax, false);
                    var embed = new EmbedBuilder
                    {
                        Title = embedInfo.EmbedName,
                        Description = embedInfo.EmbedString,
                        Color = Color.Blue,
                        ThumbnailUrl = url,
                    };

                    foreach (var channel in channels)
                    {
                        try
                        {
                            await channel.SendMessageAsync(null, false, embed: embed.Build()).ConfigureAwait(false);
                        }
                        catch { }
                    }
                }
                else await Task.Delay(0_500).ConfigureAwait(false);
            }
        }

        public static FilteredGameDataSource datasourcefiltered = new(new SAV9SV(), new GameDataSource(GameInfo.Strings));

        [SlashCommand("terarequest","Displays a Form to fill out to request a Shiny Tera Raid to be Hosted")]
        public async Task Terarequest()
        {

            var teramodal = new ModalBuilder().WithCustomId("terarequest").WithTitle("Tera Raid Request");
            teramodal.AddTextInput("Species", "species", placeholder: "Species",required:false);
            teramodal.AddTextInput("Tera Type", "tera", placeholder: "Tera Type", required: false);
            teramodal.AddTextInput("Rewards", "reward", TextInputStyle.Paragraph, placeholder: "Rewards will be fulfilled over species. Leave Blank for shinies.", required: false);
            
            
            await RespondWithModalAsync(teramodal.Build());
           
        }
        [SlashCommand("botrequest","Request a bot to be turned on")]
        public async Task botrequest()
        {
            await DeferAsync();
            var menubuilder = new SelectMenuBuilder().WithCustomId("botmenu")
                .WithMaxValues(1)
                .WithMinValues(1)
                .WithPlaceholder("Select a Bot")
                .AddOption("Articuno LGPE", "Articuno LGPE")
                .AddOption("Empoleon SWSH", "Empoleon SWSH")
                .AddOption("Spiritomb BDSP", "Spiritomb BDSP")
                .AddOption("Basculegion LA", "Basculegion LA");
             //   .AddOption("Klawf SV", "Klawf SV");
            var builder = new ComponentBuilder().WithSelectMenu(menubuilder);
            await FollowupAsync(ephemeral: true, components: builder.Build());

        }


    }
}