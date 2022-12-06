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
            TradeReceiver = await GetTradePartnerInfo(token).ConfigureAwait(false);
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
            
            UpdateBarrier(poke.IsSynchronized);
            poke.TradeInitialize(this);
            var toSend = poke.TradeData;
            await SetBoxPokemon(toSend,1,1, token).ConfigureAwait(false);
            Log("Opening Menu");
            await Click(X, 2000, token).ConfigureAwait(false);
            Log("Navigating to PokePortal");
            await Click(A, 8000, token).ConfigureAwait(false);
            Log("Selecting Link Trade");
            await Click(DDOWN, 1000, token).ConfigureAwait(false);
            await Click(DDOWN, 1000, token).ConfigureAwait(false);
            await Click(A, 1000, token).ConfigureAwait(false);
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
            var partnerFound = await WaitForTradePartner(token).ConfigureAwait(false);
            if(!partnerFound)
            {
                await ExitTrade(false, token);
                return PokeTradeResult.NoTrainerFound;

            }
            Log($"Found Link Trade partner: {TradeReceiver.TrainerName}-{TradeReceiver.TID7}");
            poke.SendNotification(this, $"Found Link Trade partner: {TradeReceiver.TrainerName} TID: {TradeReceiver.TID7} SID: {TradeReceiver.SID7}. Waiting for a Pokémon...");

            PK9 received = await ReadPokemonPointer(BoxStartPokemonPointer, 344, token);
            Stopwatch time = new();
            time.Restart();
            while(SearchUtil.HashByDetails(toSend) == SearchUtil.HashByDetails(received) && time.ElapsedMilliseconds < 30_000)
            {
                await Click(A, 500, token);
                received = await ReadPokemonPointer(BoxStartPokemonPointer, 344, token);
            }
            if(time.ElapsedMilliseconds > 30_000)
            {
                await Click(B, 1000, token);
                for (int j = 0; j < 10; j++)
                {
                    await Click(B, 500, token);
                    await Click(A, 500, token);
                    for (int n = 0; n < 5; n++)
                    {
                        await Click(B, 1000, token);
                    }
                }
                return PokeTradeResult.TrainerTooSlow;
            }
            for(int j = 0; j<10;j++)
            {
                await Click(B, 500, token);
                await Click(A, 500, token);
                for(int n =0;n<5;n++)
                {
                    await Click(B, 1000, token);
                }
            }
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
        protected virtual async Task<bool> WaitForTradePartner(CancellationToken token)
        {
    
                var oldreceiver = TradeReceiver;
                Log("Waiting for trainer...");
                int ctr = (Hub.Config.Trade.TradeWaitTime * 1_000) - 2_000;
               
                while (TradeReceiver.TID7 == oldreceiver.TID7 && ctr > 0)
                {
                    TradeReceiver = await GetTradePartnerInfo(token);
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                    ctr -= 1_000;
                  
                }
                if(TradeReceiver.TID7 != oldreceiver.TID7)
                    return true;
                else 
                    return false;
          
        }
        private async Task ExitTrade(bool unexpected, CancellationToken token)
        {
            await Click(B, 1000, token);
            await Click(A, 1000, token);
            for (int i = 0; i < 10; i++)
            {
                await Click(B, 1000, token);
            }
        }
    }
}
