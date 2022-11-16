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
using SWSH_OWRNG_Generator.Core;

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
            var desiredmark = "Any Mark";
            Log("desiredmark set");
            StopConditionSettings.InitializeTargetIVs(Hub.Config, out var DesiredMinIVs, out var DesiredMaxIVs);
            Log("target ivs set");
            var shinyfilter = Hub.Config.StopConditions.ShinyTarget switch
            {
                TargetShinyType.DisableOption => "Ignore",
                TargetShinyType.NonShiny => "No",
                TargetShinyType.AnyShiny => "Star/Square",
                TargetShinyType.SquareOnly => "Square",
                TargetShinyType.StarOnly => "Star",
                
            };
            Log("shiny filter set");
            var searchfilters = new SWSH_OWRNG_Generator.Core.Overworld.Filter()
            {

                TSV = SWSH_OWRNG_Generator.Core.Util.Common.GetTSV(Hub.Config.EncounterSWSH.TID, Hub.Config.EncounterSWSH.SID),
                SlotMin = Hub.Config.EncounterSWSH.slotmin,
                SlotMax = Hub.Config.EncounterSWSH.slotmax,
                LevelMax = Hub.Config.EncounterSWSH.levelmax,
                LevelMin = Hub.Config.EncounterSWSH.levelmin,
                KOs = 500,
                EggMoveCount = Hub.Config.EncounterSWSH.EMs,
                FlawlessIVs = Hub.Config.EncounterSWSH.flawlessivs,
                Weather = Hub.Config.EncounterSWSH.weather,
                Static = Hub.Config.EncounterSWSH.Static,
                Fishing = Hub.Config.EncounterSWSH.fishing,
                HeldItem = Hub.Config.EncounterSWSH.helditem,
                AbilityLocked = false,
                TIDSIDSearch = false,
                CuteCharm = false,
                ShinyLocked = false,
                Hidden = Hub.Config.EncounterSWSH.hidden,
                MenuClose = false,
                DesiredAura = Hub.Config.EncounterSWSH.theaura.ToString(),
                DesiredNature = Hub.Config.StopConditions.TargetNature == Nature.Random ? "Ignore" : Hub.Config.StopConditions.TargetNature.ToString(),
                DesiredMark = desiredmark,
                DesiredShiny = shinyfilter,
                MarkRolls = Hub.Config.EncounterSWSH.markcharm ? 3 : 1,
                ShinyRolls = Hub.Config.EncounterSWSH.shinycharm ? 3: 1,
                MaxIVs = DesiredMaxIVs,
                MinIVs = DesiredMinIVs
        };
            Log("search filter set");
            bool initrun = true;
            bool secondrun = false;
            while (!token.IsCancellationRequested)
            {
                Log("made it to tracking loop");
                var frameadvancetarget = await calculateframeadvance(searchfilters, token);
                var daystoskip = await calculatedaystoskip(frameadvancetarget, token);
                ulong TotalAdvances = 0;
                Log($"Total Advances: {TotalAdvances:N0}");
                var (s0, s1) = await GetGlobalRNGState(token).ConfigureAwait(false);
                await Click(B, 500, token);
                while(frameadvancetarget-Hub.Config.EncounterSWSH.movementdelay > TotalAdvances)
                {
                 
                    if (initrun && frameadvancetarget > Hub.Config.EncounterSWSH.onedayskip+100)
                    {
                        for(ulong i = 0; i < daystoskip; i++)
                        {
                            Log($"skipping day {i}");
                            await SwitchConnection.DaySkip(token);
                            await Task.Delay(360);
                        }
                        await SwitchConnection.ResetTime(token);
                        await Task.Delay(1000);
                       
                        
                    }
                    
                  
                    var (_s0, _s1) = await GetGlobalRNGState(token).ConfigureAwait(false);
                    // Only update if it changed.
                    if (_s0 == s0 && _s1 == s1)
                        continue;
                    var passed = GetAdvancesPassed(s0, s1, _s0, _s1);
                    s0 = _s0;
                    s1 = _s1;
                    TotalAdvances += passed;
                    Log($"Total Advances: {TotalAdvances:N0}");
                    if (secondrun)
                    {
                        await Click(X, 1000, token);
                        if ((frameadvancetarget - 100) > (TotalAdvances))
                        {

                            await Click(A, 500, token);
                            var skips = (frameadvancetarget - TotalAdvances) - 100;
                            Log($"Skipping {skips} frames)");
                            for (ulong j = 0; j < skips; j++)
                            {
                                await Click(LSTICK, 150, token);
                                if (skips >= 500 && j % 500 == 0)
                                    Log($"{j} frames skipped");
                                else if (skips <500 && j % 100 == 0)
                                    Log($"{j} frames skipped");
                            }
                            await Click(B, 1000, token);

                        }
                        while(!await IsOnOverworld(Hub.Config,token))
                            await Click(B, 1000, token);
                        secondrun = false;
                    }
                    if (initrun)
                    {
                        initrun = false;
                        secondrun = true;
                    }
                }
                await moveplayerandsave(token);
                
                KCoordinates = await ReadOverWorldSpawnBlock(token).ConfigureAwait(false);

                PK8s = await ReadOwPokemonFromBlock(KCoordinates, sav, token).ConfigureAwait(false);
                await Click(HOME, 1000, token);
                bool matchfound = false;
                foreach(var pk in PK8s)
                {
                    var match = StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null);
                    if (!match)
                        continue;
                       
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
                    
                    matchfound = true;
                    break;
                }
                if (!matchfound)
                {

                    
                    await Click(A, 1000, token);
                    await resetplayerposition(token);
                    await Click(X, 1000, token);
                    initrun = true;
                    secondrun = false;
                    continue;
                }
                   
                return;
            }
            
        }

        public async Task<ulong> calculatedaystoskip(ulong advances,CancellationToken token)
        {
            var daystoskip = advances / Hub.Config.EncounterSWSH.onedayskip;
            if (daystoskip < 0)
                return 0;
            return daystoskip;
        }
        public async Task resetplayerposition(CancellationToken token)
        {
            var movetime = (int)(Hub.Config.EncounterSWSH.moveduration * 1000);
            switch (Hub.Config.EncounterSWSH.MoveDirection)
            {
                case EncounterSettings.MovementDirection.up:
                    await SetStick(LEFT, 0, -30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.down:
                    await SetStick(LEFT, 0, 30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.left:
                    await SetStick(LEFT, 30000, 0, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.right:
                    await SetStick(LEFT, -30000, 0, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.upleft:
                    await SetStick(LEFT, 30000, -30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.upright:
                    await SetStick(LEFT, -30000, -30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.downleft:
                    await SetStick(LEFT, 30000, 30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.downright:
                    await SetStick(LEFT, -30000, 30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;

            }
        }
        public async Task moveplayerandsave(CancellationToken token)
        {
            var movetime = (int)(Hub.Config.EncounterSWSH.moveduration*1000);
            switch (Hub.Config.EncounterSWSH.MoveDirection)
            {
                case EncounterSettings.MovementDirection.up:
                    await SetStick(LEFT, 0, 30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.down:
                    await SetStick(LEFT, 0, -30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.left:
                    await SetStick(LEFT, -30000, 0, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.right:
                    await SetStick(LEFT, 30000, 0, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.upleft:
                    await SetStick(LEFT, -30000, 30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.upright:
                    await SetStick(LEFT, 30000, 30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.downleft:
                    await SetStick(LEFT, -30000, -30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;
                case EncounterSettings.MovementDirection.downright:
                    await SetStick(LEFT, 30000, -30000, movetime, token);
                    await SetStick(LEFT, 0, 0, 0, token);
                    break;

            }


            await Click(X, 2_000, token).ConfigureAwait(false);
            await Click(R, 2_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);
        }
        public async Task<ulong> calculateframeadvance(SWSH_OWRNG_Generator.Core.Overworld.Filter filters,CancellationToken token)
        {
            var (s0, s1) = await GetGlobalRNGState(token).ConfigureAwait(false);
            var frames = Generator.Generate(s0, s1, 500000, 0, null, filters, 0);
            if (frames == null)
                return 0;
            Log(frames[0].Advances);
            var adv = ulong.Parse(frames[0].Advances.Replace(",",""));
            return adv;

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
            for (; i < 500000; i++) // 20,000 is an arbitrary number to prevent an infinite loop
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
