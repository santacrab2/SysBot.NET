using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class OverworldRNG : EncounterBot
    {

        public OverworldRNG(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
        {
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ulong TotalAdvances = 0;
                Log($"Total Advances: {TotalAdvances:N0}");
                var (s0, s1) = await GetGlobalRNGState(token).ConfigureAwait(false);
                while(Hub.Config.EncounterSWSH.FrameAdvanceTarget > TotalAdvances)
                {
                    var (_s0, _s1) = await GetGlobalRNGState(token).ConfigureAwait(false);
                    // Only update if it changed.
                    if (_s0 == s0 && _s1 == s1)
                        continue;
                    var passed = GetAdvancesPassed(s0, s1, _s0, _s1);
                    s0 = _s0;
                    s1 = _s1;
                    TotalAdvances += passed;
                    Log($"Total Advances: {TotalAdvances:N0}");
                    if (TotalAdvances >= (Hub.Config.EncounterSWSH.FrameAdvanceTarget-1))
                        break;
                }
                await SetStick(LEFT, 0, 30000, 1000, token);
                await SetStick(LEFT, 0, 0, 0, token);
                await Click(HOME, 0, token);
                return;
            }
            
        }
        public async Task<(ulong s0, ulong s1)> GetGlobalRNGState( CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAsync(0x4C2AAC18, 16, token).ConfigureAwait(false);
            var s0 = BitConverter.ToUInt64(data, 0);
            var s1 = BitConverter.ToUInt64(data, 8);
            return (s0, s1);
        }
        public static ulong GetAdvancesPassed(ulong prevs0, ulong prevs1, ulong news0, ulong news1)
        {
            if (prevs0 == news0 && prevs1 == news1)
                return 0;

            var rng = new Xoroshiro128Plus(prevs0, prevs1);
            ulong i = 0;
            for (; i < 20_000; i++) // 20,000 is an arbitrary number to prevent an infinite loop
            {
                rng.Next();
                var (s0, s1) = rng.GetState();
                if (s0 == news0 && s1 == news1)
                    return ++i;
            }
            return i;
        }
    }
}
