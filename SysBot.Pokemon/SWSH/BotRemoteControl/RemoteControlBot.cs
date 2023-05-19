﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class RemoteControlBot : PokeRoutineExecutor8
    {
        public RemoteControlBot(PokeBotState cfg) : base(cfg)
        {
        }
        public override Task<PK8> ReadPokemon(ulong offset, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }
     

        public override Task<PK8> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }

        public override Task<PK8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }

        public override Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }

    
        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                Log("Identifying trainer data of the host console.");
                await IdentifyTrainer(token).ConfigureAwait(false);

                Log("Starting main loop, then waiting for commands.");
                Config.IterateNextRoutine();
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                    ReportStatus();
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(PokeTradeBot)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, CancellationToken.None).ConfigureAwait(false); // reset
            await CleanExit(CancellationToken.None).ConfigureAwait(false);
        }

        private class DummyReset : IBotStateSettings
        {
            public bool ScreenOff => true;
        }
    }
}
