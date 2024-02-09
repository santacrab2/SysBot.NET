﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Discord.GatewayIntents;


namespace SysBot.Pokemon.Discord
{
    public static class SysCordSettings
    {
        public static DiscordManager Manager { get; internal set; } = default!;
        public static DiscordSettings Settings => Manager.Config;
        public static PokeTradeHubConfig HubConfig { get; internal set; } = default!;
    }

    public sealed class SysCord<T> where T : PKM, new()
    {
        public static PokeBotRunner<T> Runner { get; private set; } = default!;

        public static DiscordSocketClient _client;
        private readonly DiscordManager Manager;
        public readonly PokeTradeHub<T> Hub;

        // Keep the CommandService and DI container around for use with commands.
        // These two types require you install the Discord.Net.Commands package.
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        // Bot listens to channel messages to reply with a ShowdownSet whenever a PKM file is attached (not with a command).
        private bool ConvertPKMToShowdownSet { get; } = true;

        // Track loading of Echo/Logging channels so they aren't loaded multiple times.
        private bool MessageChannelsLoaded { get; set; }

        public SysCord(PokeBotRunner<T> runner)
        {
            Runner = runner;
            Hub = runner.Hub;
            Manager = new DiscordManager(Hub.Config.Discord);

            SysCordSettings.Manager = Manager;
            SysCordSettings.HubConfig = Hub.Config;

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                // How much logging do you want to see?
                LogLevel = LogSeverity.Info,
                
                // If you or another service needs to do anything with messages
                // (eg. checking Reactions, checking the content of edited/deleted messages),
                // you must set the MessageCacheSize. You may adjust the number as needed.
                MessageCacheSize = 100,
            });

            _commands = new CommandService(new CommandServiceConfig
            {
                // Again, log level:
                LogLevel = LogSeverity.Info,

                // This makes commands get run on the task thread pool instead on the websocket read thread.
                // This ensures long running logic can't block the websocket connection.
                //DefaultRunMode = Hub.Config.Discord.AsyncCommands ? RunMode.Async : RunMode.Sync,

                // There's a few more properties you can set,
                // for example, case-insensitive commands.
                CaseSensitiveCommands = false,
            });

            // Subscribe the logging handler to both the client and the CommandService.
            _client.Log += Log;
            _commands.Log += Log;

            // Setup your DI container.
            _services = ConfigureServices();
        }

        // If any services require the client, or the CommandService, or something else you keep on hand,
        // pass them as parameters into this method as needed.
        // If this method is getting pretty long, you can separate it out into another file using partials.
        private static IServiceProvider ConfigureServices()
        {
            var map = new ServiceCollection();//.AddSingleton(new SomeServiceClass());

            // When all your required services are in the collection, build the container.
            // Tip: There's an overload taking in a 'validateScopes' bool to make sure
            // you haven't made any mistakes in your dependency graph.
            return map.BuildServiceProvider();
        }

        // Example of a logging handler. This can be reused by add-ons
        // that ask for a Func<LogMessage, Task>.

        private static Task Log(LogMessage msg)
        {
            var text = $"[{msg.Severity,8}] {msg.Source}: {msg.Message} {msg.Exception}";
            Console.ForegroundColor = GetTextColor(msg.Severity);
            Console.WriteLine($"{DateTime.Now,-19} {text}");
            Console.ResetColor();

            LogUtil.LogText($"SysCord: {text}");

            return Task.CompletedTask;
        }

        private static ConsoleColor GetTextColor(LogSeverity sv) => sv switch
        {
            LogSeverity.Critical => ConsoleColor.Red,
            LogSeverity.Error => ConsoleColor.Red,

            LogSeverity.Warning => ConsoleColor.Yellow,
            LogSeverity.Info => ConsoleColor.White,

            LogSeverity.Verbose => ConsoleColor.DarkGray,
            LogSeverity.Debug => ConsoleColor.DarkGray,
            _ => Console.ForegroundColor,
        };

        public async Task MainAsync(string apiToken, CancellationToken token)
        {
            _client.Ready += LoadLoggingAndEcho;
            // Centralize the logic for commands into a separate method.
            await InitCommands().ConfigureAwait(false);
           
            // Login and connect.
            await _client.LoginAsync(TokenType.Bot, apiToken).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);

            var app = await _client.GetApplicationInfoAsync().ConfigureAwait(false);
            Manager.Owner = app.Owner.Id;

            // Wait infinitely so your bot actually stays connected.
            await MonitorStatusAsync(token).ConfigureAwait(false);
        }
   
        public Task slashtask(SocketSlashCommand arg1)
        {


            return Task.CompletedTask;

        }
        public async Task InitCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();

            await _commands.AddModulesAsync(assembly, _services).ConfigureAwait(false);
            var genericTypes = assembly.DefinedTypes.Where(z => z.IsSubclassOf(typeof(ModuleBase<SocketCommandContext>)) && z.IsGenericType);
            foreach (var t in genericTypes)
            {
                var genModule = t.MakeGenericType(typeof(T));
                await _commands.AddModuleAsync(genModule, _services).ConfigureAwait(false);
            }
            var modules = _commands.Modules.ToList();

            var blacklist = Hub.Config.Discord.ModuleBlacklist
                .Replace("Module", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(z => z.Trim()).ToList();

            foreach (var module in modules)
            {
                var name = module.Name;
                name = name.Replace("Module", "");
                var gen = name.IndexOf('`');
                if (gen != -1)
                    name = name[..gen];
                if (blacklist.Any(z => z.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    await _commands.RemoveModuleAsync(module).ConfigureAwait(false);
            }

            // Subscribe a handler to see if a message invokes a command.
          
            _client.MessageReceived += HandleMessageAsync;
            _client.ButtonExecuted += handlebuttonpress;
        }
        public async Task handlebuttonpress(SocketMessageComponent arg)
        {
            var currentcache = TradeModule<T>.simpletradecache.Find(z => z.user == arg.User);
            var lastpage = currentcache.opti.Length % 25 == 0 ? (currentcache.opti.Length / 25) - 1 : currentcache.opti.Length / 25;
            switch (arg.Data.CustomId)
            {

                case "next":
                    if (currentcache.page < lastpage)
                    {
                        currentcache.page++;

                        await arg.UpdateAsync(z => z.Components = TradeModule<T>.compo(currentcache.currenttype, currentcache.page, currentcache.opti));
                    }
                    else
                    {
                        currentcache.page = 0;
                        await arg.UpdateAsync(z => z.Components = TradeModule<T>.compo(currentcache.currenttype, currentcache.page, currentcache.opti));
                    }
                    break;
                case "prev":
                    if (currentcache.page > 0)
                    {
                        currentcache.page--;
                        await arg.UpdateAsync(z => z.Components = TradeModule<T>.compo(currentcache.currenttype, currentcache.page, currentcache.opti));
                    }
                    else if (currentcache.opti.Length > 25)
                    {
                        if (currentcache.opti.Length % 25 == 0)
                            currentcache.page = (currentcache.opti.Length / 25) - 1;
                        else
                            currentcache.page = (currentcache.opti.Length / 25);
                        await arg.UpdateAsync(z => z.Components = TradeModule<T>.compo(currentcache.currenttype, currentcache.page, currentcache.opti));
                    }
                    break;

            }
        }

            private async Task HandleMessageAsync(SocketMessage arg)
        {
            // Bail out if it's a System Message.
            if (arg is not SocketUserMessage msg)
                return;

            // We don't want the bot to respond to itself or other bots.
       

            // Create a number to track where the prefix ends and the command begins
            int pos = 0;
            if (msg.HasStringPrefix(Hub.Config.Discord.CommandPrefix, ref pos))
            {
                bool handled = await TryHandleCommandAsync(msg, pos).ConfigureAwait(false);
                if (handled)
                    return;
            }

            await TryHandleMessageAsync(msg).ConfigureAwait(false);
        }

        private async Task TryHandleMessageAsync(SocketMessage msg)
        {
            // should this be a service?
            if (msg.Attachments.Count > 0 && ConvertPKMToShowdownSet)
            {
                foreach (var att in msg.Attachments)
                    await msg.Channel.RepostPKMAsShowdownAsync(att).ConfigureAwait(false);
            }
            if(msg.Channel is SocketDMChannel && !msg.Author.IsBot)
            {
                var chan = (ITextChannel)await _client.GetChannelAsync(872613946471899196);
                await chan.SendMessageAsync($"DM Log: User: {msg.Author.Username} ID: {msg.Author.Id} Message: {msg.Content}");
            }
        }

        private async Task<bool> TryHandleCommandAsync(SocketUserMessage msg, int pos)
        {
            // Create a Command Context.
            var context = new SocketCommandContext(_client, msg);

            // Check Permission
            var mgr = Manager;
            if (!mgr.CanUseCommandUser(msg.Author.Id))
            {
                await msg.Channel.SendMessageAsync("You are not permitted to use this command.").ConfigureAwait(false);
                return true;
            }
            if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != mgr.Owner)
            {
                if (Hub.Config.Discord.ReplyCannotUseCommandInChannel)
                    await msg.Channel.SendMessageAsync("You can't use that command here.").ConfigureAwait(false);
                return true;
            }

            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully).
            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "Unknown Guild";
            await Log(new LogMessage(LogSeverity.Info, "Command", $"Executing command from {guild}#{msg.Channel.Name}:@{msg.Author.Username}. Content: {msg}")).ConfigureAwait(false);
            var result = await _commands.ExecuteAsync(context, pos, _services).ConfigureAwait(false);

            if (result.Error == CommandError.UnknownCommand)
                return false;

            // Uncomment the following lines if you want the bot
            // to send a message if it failed.
            // This does not catch errors from commands with 'RunMode.Async',
            // subscribe a handler for '_commands.CommandExecuted' to see those.
            if (!result.IsSuccess)
                await msg.Channel.SendMessageAsync(result.ErrorReason).ConfigureAwait(false);
            return true;
        }

        private async Task MonitorStatusAsync(CancellationToken token)
        {
            
           
            while (!token.IsCancellationRequested)
            {
                if (Hub.Config.Discord.announcements)
                {
                    if (!Runner.IsRunning)
                    {
                        var bots = Runner.Bots;
                        foreach (var bot in bots)
                        {
                            if (bot.Bot.Config.NextRoutineType == PokeRoutineType.FlexTrade || bot.Bot.Config.CurrentRoutineType == PokeRoutineType.FlexTrade)
                            {
                                
                                foreach (var channel in Hub.Config.Discord.announcementchannels)
                                {
                                   
                                    var districhan = (ITextChannel)await SysCord<T>._client.GetChannelAsync(channel);
                                    if (districhan.Name.Contains("✅"))
                                    {

                                        var role = districhan.Guild.EveryoneRole;
                                        await districhan.AddPermissionOverwriteAsync(role, new OverwritePermissions(sendMessages: PermValue.Deny));
                                        await districhan.ModifyAsync(prop => prop.Name = districhan.Name.Replace("✅", "❌"));
                                        var offembed = new EmbedBuilder();
                                        var game = AutoLegalityWrapper.GetTrainerInfo<T>();
                                        offembed.AddField($"{_client.CurrentUser.Username} Bot Announcement", $"{Array.Find<ComboItem>(GameInfo.VersionDataSource.ToArray(),z=>z.Value == game.Game).Text} Trade Bot is Offline");
                                        await districhan.SendMessageAsync(embed: offembed.Build());
                                    }
                                }
                                
                            }
                            if (bot.Bot.Config.NextRoutineType == PokeRoutineType.RollingRaidSWSH || bot.Bot.Config.CurrentRoutineType == PokeRoutineType.RollingRaidSWSH)
                            {
                                List<ulong> channels = new();
                                List<ITextChannel> embedChannels = new();
                                var chStrings = Hub.Config.RollingRaidSWSH.RollingRaidEmbedChannels;
                               
                                    var cid = ulong.Parse(chStrings);
                                    var districhan = (ITextChannel)await SysCord<T>._client.GetChannelAsync(cid);
                                    if (districhan.Name.Contains("✅"))
                                    {

                                        await districhan.ModifyAsync(prop => prop.Name = districhan.Name.Replace("✅", "❌"));
                                        var offembed = new EmbedBuilder();
                                        var game = AutoLegalityWrapper.GetTrainerInfo<T>();
                                        offembed.AddField($"{_client.CurrentUser.Username} Bot Announcement", $"{Array.Find<ComboItem>(GameInfo.VersionDataSource.ToArray(), z => z.Value == game.Game).Text} Raid Bot is Offline");
                                        await districhan.SendMessageAsync(embed: offembed.Build());
                                    }
                                

                            }
                        }
                    }
                    else
                    {
                        var bots = Runner.Bots;
                        foreach (var bot in bots)
                        {
                            if (bot.Bot.Config.NextRoutineType == PokeRoutineType.FlexTrade || bot.Bot.Config.CurrentRoutineType == PokeRoutineType.FlexTrade)
                            {
                                foreach (var chan in Hub.Config.Discord.announcementchannels)
                                {
                                    var districhan = (ITextChannel)await SysCord<T>._client.GetChannelAsync(chan);
                                    if (districhan.Name.Contains("❌"))
                                    {
                                        var role = districhan.Guild.EveryoneRole;
                                        await districhan.AddPermissionOverwriteAsync(role, new OverwritePermissions(sendMessages: PermValue.Allow));
                                        await districhan.ModifyAsync(prop => prop.Name = districhan.Name.Replace("❌", "✅"));
                                        var offembed = new EmbedBuilder();
                                        var game = AutoLegalityWrapper.GetTrainerInfo<T>();
                                        offembed.AddField($"{_client.CurrentUser.Username} Bot Announcement", $"{Array.Find<ComboItem>(GameInfo.VersionDataSource.ToArray(), z => z.Value == game.Game).Text} Trade Bot is Online");
                                        await districhan.SendMessageAsync($"<@&{Hub.Config.Discord.pingroleid}>", embed: offembed.Build());

                                       
                                    }
                                }
                            }
                            if (bot.Bot.Config.NextRoutineType == PokeRoutineType.RollingRaidSWSH || bot.Bot.Config.CurrentRoutineType == PokeRoutineType.RollingRaidSWSH)
                            {
                                List<ulong> channels = new();
                                List<ITextChannel> embedChannels = new();
                                var chStrings = Hub.Config.RollingRaidSWSH.RollingRaidEmbedChannels.Split(',');
                                foreach (var chan in chStrings)
                                {
                                    var cid = ulong.Parse(chan);
                                    var districhan = (ITextChannel)await SysCord<T>._client.GetChannelAsync(cid);
                                    if (districhan.Name.Contains("❌"))
                                    {

                                        await districhan.ModifyAsync(prop => prop.Name = districhan.Name.Replace("❌", "✅"));
                                        var offembed = new EmbedBuilder();
                                        var game = AutoLegalityWrapper.GetTrainerInfo<T>();
                                        offembed.AddField($"{_client.CurrentUser.Username} Bot Announcement", $"{Array.Find<ComboItem>(GameInfo.VersionDataSource.ToArray(), z => z.Value == game.Game).Text} Raid Bot is Online");
                                        await districhan.SendMessageAsync("<@&872641196990795826>", embed: offembed.Build());
                                    }

                                }
                            }
                        }
                    }
                }
               
                
                await Task.Delay(20_000, token).ConfigureAwait(false);
            }
        }

        public async Task LoadLoggingAndEcho()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var _interactionService = new InteractionService(_client);
            await _interactionService.AddModulesAsync(assembly, _services);
            var genericTypes = assembly.DefinedTypes.Where(z => z.IsSubclassOf(typeof(InteractionModuleBase<SocketInteractionContext>)) && z.IsGenericType);
            foreach (var t in genericTypes)
            {
                var genModule = t.MakeGenericType(typeof(T));
                await _interactionService.AddModuleAsync(genModule, _services).ConfigureAwait(false);
            }
            await _interactionService.RegisterCommandsToGuildAsync(872587205787394119);
            _client.InteractionCreated += async interaction =>
            {

                var ctx = new SocketInteractionContext(_client, interaction);
                var result = await _interactionService.ExecuteCommandAsync(ctx, null);
            };
            _client.SlashCommandExecuted += slashtask;
            _client.ModalSubmitted += modalsubmit;
            _client.SelectMenuExecuted += MyMenuHandler;
            if (MessageChannelsLoaded)
                return;

            // Restore Echoes
            EchoModule.RestoreChannels(_client, Hub.Config.Discord);

            // Restore Logging
            LogModule.RestoreLogging(_client, Hub.Config.Discord);
            TradeStartModule<T>.RestoreTradeStarting(_client);

            // Don't let it load more than once in case of Discord hiccups.
            await Log(new LogMessage(LogSeverity.Info, "LoadLoggingAndEcho()", "Logging and Echo channels loaded!")).ConfigureAwait(false);
            MessageChannelsLoaded = true;

            var game = Hub.Config.Discord.BotGameStatus;
            if (!string.IsNullOrWhiteSpace(game))
                await _client.SetGameAsync(game).ConfigureAwait(false);
        }

        private async Task modalsubmit(SocketModal arg)
        {
            if (!File.Exists($"{Hub.Config.Discord.terarequestfolder}/Tera-Raid-Request.txt")) File.Create($"{Hub.Config.Discord.terarequestfolder}/Tera-Raid-Request.txt");
            if (!File.ReadAllLines($"{Hub.Config.Discord.terarequestfolder}/Tera-Raid-Request.txt").Contains($"{arg.User.Id}"))
            {
                List<SocketMessageComponentData> components =
                arg.Data.Components.ToList();
                string species = components.First(x => x.CustomId == "species").Value;
                string terat = components.First(x => x.CustomId == "tera").Value;
                string scale = components.First(x => x.CustomId == "scale").Value;
                string reward = components.First(x => x.CustomId == "reward").Value;
                var chan = (ITextChannel)await _client.GetChannelAsync(872606380434026508);

                await chan.SendMessageAsync($"Requestor: {arg.User.Mention}\nSpecies: {species}\nTeraType: {terat}\nScale: {scale}\nReward Requests: {reward}\n");
              
                var therecordsarr = File.ReadAllLines($"{Hub.Config.Discord.terarequestfolder}/Tera-Raid-Request.txt");
                var therecordslist = therecordsarr!=null ? therecordsarr.ToList():new();
                therecordslist.Add($"{arg.User.Id}\n{arg.User.Username}\n");
                File.WriteAllLines($"{Hub.Config.Discord.terarequestfolder}/Tera-Raid-Request.txt", therecordslist);
                await arg.RespondAsync("Your Request has been submitted!", ephemeral: true);
            }
            await arg.RespondAsync("One Request at a time! Please wait until your current request has been fulfilled.", ephemeral: true);



        }
        public async Task MyMenuHandler(SocketMessageComponent arg)
        {
            var currentcache = TradeModule<T>.simpletradecache.Find(z => z.user == arg.User);
            switch (arg.Data.CustomId) {
                case "botmenu":
                    if (!File.Exists($"{Hub.Config.Discord.terarequestfolder}/bot-Request.txt")) File.Create($"{Hub.Config.Discord.terarequestfolder}/bot-Request.txt");
                    if (!File.ReadAllLines($"{Hub.Config.Discord.terarequestfolder}/bot-Request.txt").Contains($"{arg.User.Id}"))
                    {
                        var chan = (ITextChannel)_client.GetChannel(1081994925635289131);
                        await chan.SendMessageAsync($"Requestor: {arg.User.Mention}\nBot Request: {arg.Data.Values.First()}");
                        var therecordsarr = File.ReadAllLines($"{Hub.Config.Discord.terarequestfolder}/bot-Request.txt");
                        var therecordslist = therecordsarr != null ? therecordsarr.ToList() : new();
                        therecordslist.Add($"{arg.User.Id}\n{arg.User.Username}\n");
                        File.WriteAllLines($"{Hub.Config.Discord.terarequestfolder}/bot-Request.txt", therecordslist);
                        await arg.RespondAsync("Your Request has been submitted!", ephemeral: true);
                        await arg.Message.DeleteAsync();
                    }
                    await arg.RespondAsync("One Request at a time! Please wait until your current request has been fulfilled.", ephemeral: true);
                    await arg.Message.DeleteAsync();
                    break;
                default:
                    currentcache.response = arg;
                    currentcache.responded = true;
                    await arg.RespondAsync();
                    break;
            }
        }
    }
}