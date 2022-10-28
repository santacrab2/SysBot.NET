using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class PokeTradeBotLGPE : PokeRoutineExecutor7LGPE
    {
        private readonly PokeTradeHub<PB7> Hub;
        private readonly TradeSettings TradeSettings;
        private readonly TradeAbuseSettings AbuseSettings;
        /// <summary>
        /// Folder to dump received trade data to.
        /// </summary>
        /// <remarks>If null, will skip dumping.</remarks>
        private readonly IDumper DumpSetting;

        /// <summary>
        /// Synchronized start for multiple bots.
        /// </summary>
        public bool ShouldWaitAtBarrier { get; private set; }

        /// <summary>
        /// Tracks failed synchronized starts to attempt to re-sync.
        /// </summary>
        public int FailedBarrier { get; private set; }

        public PokeTradeBotLGPE(PokeTradeHub<PB7> hub, PokeBotState cfg) : base(cfg)
        {
            Hub = hub;
            TradeSettings = hub.Config.Trade;
            AbuseSettings = hub.Config.TradeAbuse;
            DumpSetting = hub.Config.Folder;
        }
        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                var sav = await IdentifyTrainer(token).ConfigureAwait(false);
                RecentTrainerCache.SetRecentTrainer(sav);
               

                Log($"Starting main {nameof(PokeTradeBotLA)} loop.");
                await InnerLoop(sav, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(PokeTradeBotLA)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            UpdateBarrier(false);
            await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
        }
        private async Task InnerLoop(SAV7b sav, CancellationToken token)
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
        private async Task DoTrades(SAV7b sav, CancellationToken token)
        {
            var type = Config.CurrentRoutineType;
            int waitCounter = 0;
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
        protected virtual (PokeTradeDetail<PB7>? detail, uint priority) GetTradeData(PokeRoutineType type)
        {
            if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
                return (detail, priority);
            if (Hub.Queues.TryDequeueLedy(out detail))
                return (detail, PokeTradePriorities.TierFree);
            return (null, PokeTradePriorities.TierFree);
        }
        private async Task PerformTrade(SAV7b sav, PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, CancellationToken token)
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

        private void HandleAbortedTrade(PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
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
        private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV7b sav, PokeTradeDetail<PB7> poke, CancellationToken token)
        {
            UpdateBarrier(poke.IsSynchronized);
            poke.TradeInitialize(this);
            Hub.Config.Stream.EndEnterCode(this);
            var toSend = poke.TradeData;
            if (toSend.Species != 0)
                WriteBoxPokemon(toSend, 0, 1, token);
            if (!await IsOnOverworldStandard(token))
            {
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverStart;
            }
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
        private async Task ExitTrade(bool unexpected, CancellationToken token)
        {
            if (unexpected)
                Log("Unexpected behavior, recovering position.");

        }
    }
}
