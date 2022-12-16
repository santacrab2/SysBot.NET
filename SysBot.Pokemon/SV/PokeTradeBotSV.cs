using PKHeX.Core;
using PKHeX.Core.Searching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSCVI;

namespace SysBot.Pokemon
{
    public class PokeTradeBotSV : PokeRoutineExecutor9SCVI
    {
        private readonly IDumper DumpSetting;
        public bool ShouldWaitAtBarrier { get; private set; }
        public TradeSettings TradeSettings { get; private set; }
     

        /// <summary>
        /// Tracks failed synchronized starts to attempt to re-sync.
        /// </summary>
        public int FailedBarrier { get; private set; }
       
        public static SAV9SV sav = new();
       
        public static PK9 pkm = new();
        public static PokeTradeHub<PK9> Hub;
        public static TradePartnerSV TradeReceiver;

        public PokeTradeBotSV(PokeTradeHub<PK9> hub, PokeBotState cfg) : base(cfg)
        {
            Hub = hub;
            TradeSettings = hub.Config.Trade;
            DumpSetting = hub.Config.Folder;
        }
        protected virtual (PokeTradeDetail<PK9>? detail, uint priority) GetTradeData(PokeRoutineType type)
        {
            if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
                return (detail, priority);
            if (Hub.Queues.TryDequeueLedy(out detail))
                return (detail, PokeTradePriorities.TierFree);
            return (null, PokeTradePriorities.TierFree);
        }
        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
               var sav = await IdentifyTrainer(token).ConfigureAwait(false);
               RecentTrainerCache.SetRecentTrainer(sav);

                
               

                Log($"Starting main {nameof(PokeTradeBotSV)} loop.");
                await InnerLoop(sav, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(PokeTradeBotSV)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            UpdateBarrier(false);
            await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
        }
        private async Task InnerLoop(SAV9SV sav, CancellationToken token)
        {
            
            while (!token.IsCancellationRequested)
            {
                Config.IterateNextRoutine();
                var task = Config.CurrentRoutineType switch
                {
                    PokeRoutineType.Idle => DoNothing(token),
                    _ => DoTrades(sav, token),
                };
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (SocketException e)
                {
                    Log(e.Message);
                    Connection.Reset();
                }
            }
        }

        private async Task DoNothing(CancellationToken token)
        {
            int waitCounter = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
            {
                if (waitCounter == 0)
                    Log("No task assigned. Waiting for new task assignment.");
                waitCounter++;
                if (waitCounter % 10 == 0 && Hub.Config.AntiIdle)
                    await Click(B, 1_000, token).ConfigureAwait(false);
                else
                    await Task.Delay(1_000, token).ConfigureAwait(false);
            }
        }

        private async Task DoTrades(SAV9SV sav, CancellationToken token)
        {
            var type = Config.CurrentRoutineType;
            int waitCounter = 0;
            //await SetCurrentBox(0, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested && Config.NextRoutineType == type)
            {
                var (detail, priority) = GetTradeData(type);
                if (detail is null)
                {
                    await WaitForQueueStep(waitCounter++, token).ConfigureAwait(false);
                    continue;
                }
                waitCounter = 0;

                detail.IsProcessing = true;
                string tradetype = $" ({detail.Type})";
                Log($"Starting next {type}{tradetype} Bot Trade. Getting data...");
                Hub.Config.Stream.StartTrade(this, detail, Hub);
                Hub.Queues.StartTrade(this, detail);

                await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
            }

        }

        private async Task PerformTrade(SAV9SV sav, PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, CancellationToken token)
        {
            PokeTradeResult result;
            try
            {
                result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
                if (result == PokeTradeResult.Success)
                    return;
            }
            catch (SocketException socket)
            {
                Log(socket.Message);
                result = PokeTradeResult.ExceptionConnection;
                HandleAbortedTrade(detail, type, priority, result);
                throw; // let this interrupt the trade loop. re-entering the trade loop will recheck the connection.
            }
            catch (Exception e)
            {
                Log(e.Message);
                result = PokeTradeResult.ExceptionInternal;
            }

            HandleAbortedTrade(detail, type, priority, result);
        }

        private void HandleAbortedTrade(PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
        {
            detail.IsProcessing = false;
            if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
            {
                detail.IsRetry = true;
                Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                detail.SendNotification(this, "Oops! Something happened. I'll requeue you for another attempt.");
            }
            else
            {
                detail.SendNotification(this, $"Oops! Something happened. Canceling the trade: {result}.");
                detail.TradeCanceled(this, result);
            }
        }

        private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV9SV sav, PokeTradeDetail<PK9> poke, CancellationToken token)
        {
            if(!await IsConnected(token))
            {
                await Click(X, 2000, token);
                await Click(L, 10_000, token);
                await Click(A, 500, token);
                await Click(B, 1500, token);
            }
        
            TradeReceiver = await GetTradePartnerInfo(token);
            UpdateBarrier(poke.IsSynchronized);
            poke.TradeInitialize(this);
            var toSend = poke.TradeData;
            await SetBoxPokemon(toSend,1,1, token).ConfigureAwait(false);
            Log("Opening Menu");
            await Click(X, 2000, token).ConfigureAwait(false);
            Log("Navigating to PokePortal");
            await Click(A, 10000, token).ConfigureAwait(false);
            Log("Selecting Link Trade");
            await Click(DDOWN, 1500, token).ConfigureAwait(false);
            await Click(DDOWN, 1000, token).ConfigureAwait(false);
            await Click(A, 2000, token).ConfigureAwait(false);
            Log("clearing any residual link codes");
            await Click(X, 500, token);
            if (poke.Type != PokeTradeType.Random)
            {
                await Click(PLUS, 1000, token);
                var code = poke.Code;
                Log($"Entering Link Trade code: {code:0000 0000}...");
                await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);
                await Click(PLUS, 1000, token);
            }
            await Click(A, 1000, token);
            Log("searching...");
            await Click(A, 500, token);
      
            
            poke.TradeSearching(this);
          while(await IsSearching(token))
            {
                await Task.Delay(100);
            }
            TradeReceiver = await GetTradePartnerInfo(token);
            if (TradeReceiver.TrainerName == sav.OT || TradeReceiver.TrainerName == string.Empty)
                TradeReceiver = await GetTradePartnerInfo2(token);
            Log($"Found Link Trade partner: {TradeReceiver.TrainerName}-{TradeReceiver.TID7}");
            poke.SendNotification(this, $"Found Link Trade partner: {TradeReceiver.TrainerName} TID: {TradeReceiver.TID7} SID: {TradeReceiver.SID7}. Waiting for a Pokémon...");
      
           PK9 received = await ReadPokemonPointer(BoxStartPokemonPointer, 344, token);

            if (poke.Type == PokeTradeType.Clone)
            {
                PK9 offered = await ReadPokemonPointer(OfferedPokemonPointer, 344, token);
                var cloneresult = await Handleclones(sav,poke,offered,token);
                if(cloneresult != PokeTradeResult.Success)
                {
                    return cloneresult;
                }
            }
            Stopwatch time = new();
            time.Restart();
            while(SearchUtil.HashByDetails(toSend) == SearchUtil.HashByDetails(received) && time.ElapsedMilliseconds < 45_000)
            {
                await Click(A, 500, token);
                received = await ReadPokemonPointer(BoxStartPokemonPointer, 344, token);
            }
            if(time.ElapsedMilliseconds > 45_000)
            {
                await Click(B, 1000, token);
                await ExitTrade(false, token);
                return PokeTradeResult.TrainerTooSlow;
            }
            await ExitTrade(false, token);
            poke.Notifier.TradeFinished(this,poke,received);
            return PokeTradeResult.Success;
        }

        public override Task<PK9> ReadPokemon(ulong offset, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<PK9> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            throw new NotImplementedException();
        }
        private void UpdateBarrier(bool shouldWait)
        {
            if (ShouldWaitAtBarrier == shouldWait)
                return; // no change required

            ShouldWaitAtBarrier = shouldWait;
            if (shouldWait)
            {
                Hub.BotSync.Barrier.AddParticipant();
                Log($"Joined the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
            else
            {
                Hub.BotSync.Barrier.RemoveParticipant();
                Log($"Left the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
        }
        private async Task WaitForQueueStep(int waitCounter, CancellationToken token)
        {
            if (waitCounter == 0)
            {
                // Updates the assets.
                Hub.Config.Stream.IdleAssets(this);
                Log("Nothing to check, waiting for new users...");
            }

            const int interval = 10;
            if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
        protected virtual async Task EnterLinkCode(int code, PokeTradeHubConfig config, CancellationToken token)
        {
            // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
            var keys = TradeUtil.GetPresses(code);
            foreach (var key in keys)
            {
                int delay = config.Timings.KeypressTime;
                await Click(key, delay, token).ConfigureAwait(false);
            }
            // Confirm Code outside of this method (allow synchronization)
        }
        private async Task<TradePartnerSV> GetTradePartnerInfo(CancellationToken token)
        {
         
            var traineroff = await SwitchConnection.PointerRelative(TradePartnerStatusBlockPointer, token).ConfigureAwait(false);
            var partnerread = await SwitchConnection.ReadBytesAsync((uint)traineroff, 4, token);
            var partnernameread = await SwitchConnection.ReadBytesAsync((uint)traineroff + 0x08, 24, token);
            return new TradePartnerSV(partnerread, partnernameread);
        }
        private async Task<TradePartnerSV> GetTradePartnerInfo2(CancellationToken token)
        {

            var traineroff = await SwitchConnection.PointerRelative(TradePartnerStatusBlockPointer2, token).ConfigureAwait(false);
            var partnerread = await SwitchConnection.ReadBytesAsync((uint)traineroff, 4, token);
            var partnernameread = await SwitchConnection.ReadBytesAsync((uint)traineroff + 0x08, 24, token);
            return new TradePartnerSV(partnerread, partnernameread);
        }
       
        private async Task ExitTrade(bool unexpected, CancellationToken token)
        {
            Stopwatch tim = new();
            tim.Restart();
            while(!await CanPlayerMove(token).ConfigureAwait(false))
            {
                await Click(B, 500, token);
                await Click(A, 500, token);
                for (int n = 0; n < 5; n++)
                {
                    await Click(B, 1000, token);
                }
                if (tim.ElapsedMilliseconds > 60_000) 
                { 
                    await resetgame(token);
                    return;
                }
            }
        }
        public async Task<PokeTradeResult> Handleclones(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered,CancellationToken token)
        {
            PK9 newoffer = await ReadPokemonPointer(OfferedPokemonPointer, 344, token);
            Stopwatch thetime = new();
            thetime.Restart();
            while(SearchUtil.HashByDetails(newoffer) == SearchUtil.HashByDetails(offered) && thetime.ElapsedMilliseconds < 45_000)
            {
                newoffer = await ReadPokemonPointer(OfferedPokemonPointer, 344, token);
            }
            if (thetime.ElapsedMilliseconds > 45_000)
                return PokeTradeResult.TrainerTooSlow;
            if (Hub.Config.Discord.ReturnPKMs)
                poke.SendNotification(this, newoffer, "Here's what you showed me!");
            var la = new LegalityAnalysis(offered);
            if (!la.Valid)
            {
                Log($"Clone request (from {poke.Trainer.TrainerName}) has detected an invalid Pokémon: {(Species)offered.Species}.");
                if (DumpSetting.Dump)
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                var report = la.Report();
                Log(report);
                poke.SendNotification(this, "This Pokémon is not legal per PKHeX's legality checks. I am forbidden from cloning this. Exiting trade.");
                poke.SendNotification(this, report);

                return PokeTradeResult.IllegalTrade;
            }
            var clone = (PK9)newoffer.Clone();
            if (Hub.Config.Legality.ResetHOMETracker)
                clone.Tracker = 0;
            poke.SendNotification(this, $"**Cloned your {(Species)clone.Species}!**\nNow press B to cancel your offer and trade me a Pokémon you don't want.");
            Log($"Cloned a {(Species)clone.Species}. Waiting for user to change their Pokémon...");
            newoffer = await ReadPokemonPointer(OfferedPokemonPointer, 344, token);
            
            thetime.Restart();
            while (SearchUtil.HashByDetails(newoffer) == SearchUtil.HashByDetails(clone) && thetime.ElapsedMilliseconds < 30_000)
            {
                newoffer = await ReadPokemonPointer(OfferedPokemonPointer, 344, token);
                if(thetime.ElapsedMilliseconds > 15_000)
                {
                    poke.SendNotification(this, "**HEY CHANGE IT NOW OR I AM LEAVING!!!**");
                }
            }
            if (thetime.ElapsedMilliseconds > 45_000)
            {
                Log("Trade partner did not change their Pokémon.");
                return PokeTradeResult.TrainerTooSlow;
            }
            await SetBoxPokemon(clone, 1, 1, token).ConfigureAwait(false);
            return PokeTradeResult.Success;
        }
        public async Task resetgame(CancellationToken token)
        {
            Log("Restarting the game!");
            //not on overworld - restart game
            await Click(B, 700, token).ConfigureAwait(false);
            await Click(HOME, 3_000, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);

            await Task.Delay(3_000);
            await Click(A, 1_000, token).ConfigureAwait(false);
            if (Hub.Config.Timings.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Task.Delay(25_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);

            await Task.Delay(15_000, token).ConfigureAwait(false);
            //navigate back to 'pokeportal selection upon restart
            await Click(X, 2000, token);
            await Click(DRIGHT, 500, token);
            await PressAndHold(DUP, 3000, 500, token);
            for (int i = 0; i < 3; i++)
            {
                await Click(DDOWN, 1000, token);
            }
            await Click(B, 500, token);
        }
    }
}
