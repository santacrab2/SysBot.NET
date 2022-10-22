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
            byte[] KCoordinates;
            List<PK8> PK8s;

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
                   
                }
                switch (Hub.Config.EncounterSWSH.MoveDirection)
                {
                    case EncounterSettings.MovementDirection.up:
                        await SetStick(LEFT, 0, 30000, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.down:
                        await SetStick(LEFT, 0, -30000, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.left:
                        await SetStick(LEFT, -30000, 0, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.right:
                        await SetStick(LEFT, 30000, 0, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.upleft:
                        await SetStick(LEFT, -30000, 30000, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.upright:
                        await SetStick(LEFT, 30000, 30000, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.downleft:
                        await SetStick(LEFT, -30000, -30000, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;
                    case EncounterSettings.MovementDirection.downright:
                        await SetStick(LEFT, 30000, -30000, 1000, token);
                        await SetStick(LEFT, 0, 0, 0, token);
                        break;

                }
                    
                
                await Click(X, 2_000, token).ConfigureAwait(false);
                await Click(R, 2_000, token).ConfigureAwait(false);
                await Click(A, 5_000, token).ConfigureAwait(false);
                
                KCoordinates = await ReadOverWorldSpawnBlock(token).ConfigureAwait(false);

                PK8s = await ReadOwPokemonFromBlock(KCoordinates, sav, token).ConfigureAwait(false);
                await Click(HOME, 0, token);
                foreach(var pk in PK8s)
                {
                    bool hasMark = HasMark(pk, out RibbonIndex mark);
                    bool isSquare = pk.ShinyXor == 0;
                    string markString = hasMark ? $"Mark: {mark.ToString().Replace("Mark", "")}" : string.Empty;
                    string form = pk.Form == 0 ? "" : $"-{pk.Form}";
                    string gender = pk.Gender switch
                    {
                        0 => " (M)",
                        1 => " (F)",
                        _ => string.Empty
                    };
                    string output = $"{(isSquare ? "■ - " : pk.ShinyXor <= 16 ? "★ - " : "")}{(Species)pk.Species}{form}{gender}{Environment.NewLine}PID: {pk.PID:X8}{Environment.NewLine}EC: {pk.EncryptionConstant:X8}{Environment.NewLine}{GameInfo.GetStrings(1).Natures[pk.Nature]} Nature{Environment.NewLine}Ability: {GameInfo.GetStrings(1).Ability[pk.Ability]}{Environment.NewLine}IVs: {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}{Environment.NewLine}{markString}";
                    Log($"{output}");
                }
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
        public static bool HasMark(IRibbonIndex pk, out RibbonIndex result)
        {
            result = default;
            for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
            {
                if (pk.GetRibbon((int)mark))
                {
                    result = mark;
                    return true;
                }
            }
            return false;
        }
    }
}
