using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Base.SwitchStick;
using static SysBot.Base.SwitchButton;
using System.Net.Sockets;
namespace SysBot.Pokemon
{
    public class LineBot : PokeRoutineExecutor9SV
    {
        protected readonly PokeTradeHub<PK9> Hub;
        public LineBot(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
        {
            Hub= hub;
        }

        public override async Task MainLoop(CancellationToken token)
        {

            while (!token.IsCancellationRequested)
            {

                await Preparize(token);
                await Click(Y,1500,token);
                await Click(A,500,token);
                await Task.Delay((int)Hub.Config.EncounterSWSH.onedayskip*1000);
            }
        }
        public override async Task HardStop()
        {
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, CancellationToken.None).ConfigureAwait(false); // reset
            await CleanExit(CancellationToken.None).ConfigureAwait(false);
        }
        private async Task Preparize(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Log("Navigating to picnic..");
                await Click(X, 1_000, token).ConfigureAwait(false);
                Log("Attempting to enter picnic!");
                await Click(A,10_000,token).ConfigureAwait(false);
                Log("Continuing the hunt..");
                return;
            }
        }

        private async Task<bool> NavigateToPicnic(CancellationToken token)
        {
            var OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                var tries = 0;
                Log("Not in picnic! Wrong menu? Attempting recovery.");
                
                await Click(B, 4_500, token).ConfigureAwait(false); // Not in picnic, press B to reset
                while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                {
                    Log("Scrolling through menus...");
                    await SetStick(LEFT, 0, -32000, 1_000, token).ConfigureAwait(false);
                    await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(0_100, token).ConfigureAwait(false);
                    Log("Tap tap tap...");
                    for (int i = 0; i < 3; i++)
                        await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                    Log("Attempting to enter picnic!");
                    await Click(A, 9_500, token).ConfigureAwait(false);
                    tries++;
                    if (tries == 5)
                    {
                        await CloseGame(Hub.Config, token).ConfigureAwait(false);
                        await StartGame(Hub.Config, token).ConfigureAwait(false);
                        return false;
                    }
                }
            }
            return true;
        }
    }
}