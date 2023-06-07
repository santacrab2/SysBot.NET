using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using PKHeX.Core;
using SysBot.Base;
using System.Threading;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Executor for SW/SH games.
    /// </summary>
    public abstract class PokeRoutineExecutor8SWSH : PokeRoutineExecutor<PK8>
    {
        protected PokeDataOffsetsSWSH Offsets { get; } = new();
        protected PokeRoutineExecutor8SWSH(PokeBotState cfg) : base(cfg) { }

        public static uint GetBoxSlotOffset(int box, int slot) => BoxStartOffset + (uint)(BoxFormatSlotSize * ((30 * box) + slot));

        public override async Task<PK8> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

        public override async Task<PK8> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync((uint)offset, size, token).ConfigureAwait(false);
            return new PK8(data);
        }

        public override async Task<PK8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
            if (!valid)
                return new PK8();
            return await ReadPokemon(offset, token).ConfigureAwait(false);
        }

        public async Task<PK8> ReadSurpriseTradePokemon(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(SurpriseTradePartnerPokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            return new PK8(data);
        }

        public async Task SetBoxPokemon(PK8 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
        {
            if (sav != null)
            {
                // Update PKM to the current save's handler data
                DateTime Date = DateTime.Now;
                pkm.Trade(sav, Date.Day, Date.Month, Date.Year);
                pkm.RefreshChecksum();
            }
            var ofs = GetBoxSlotOffset(box, slot);
            pkm.ResetPartyStats();
            await Connection.WriteBytesAsync(pkm.EncryptedPartyData, ofs, token).ConfigureAwait(false);
        }

        public override async Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            var ofs = GetBoxSlotOffset(box, slot);
            return await ReadPokemon(ofs, BoxFormatSlotSize, token).ConfigureAwait(false);
        }

        public async Task SetCurrentBox(byte box, CancellationToken token)
        {
            await Connection.WriteBytesAsync(new[] { box }, CurrentBoxOffset, token).ConfigureAwait(false);
        }

        public async Task<byte> GetCurrentBox(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentBoxOffset, 1, token).ConfigureAwait(false);
            return data[0];
        }

        public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
        {
            var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
            return !result.SequenceEqual(original);
        }

        public async Task<SAV8SWSH> IdentifyTrainer(CancellationToken token)
        {
            

            // Check title so we can warn if mode is incorrect.
            string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            if (title is not (SwordID or ShieldID))
                throw new Exception($"{title} is not a valid SWSH title. Is your mode correct?");

            

            Log("Grabbing trainer data of host console...");
            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            if (!IsValidTrainerData())
                throw new Exception("Trainer data is not valid. Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) to fix this error.");
            if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
                throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

            return sav;
        }

        public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
        {
            Log("Detaching on startup.");
            await DetachController(token).ConfigureAwait(false);
            if (settings.ScreenOff)
            {
                Log("Turning off screen.");
                await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
            }
        }

        public async Task CleanExit(CancellationToken token)
        {
            await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Identifies the trainer information and loads the current runtime language.
        /// </summary>
        public async Task<SAV8SWSH> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV8SWSH();
            var info = sav.MyStatus;
            var read = await Connection.ReadBytesAsync(TrainerDataOffset, TrainerDataLength, token).ConfigureAwait(false);
            read.CopyTo(info.Data, 0);
            return sav;
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

        public async Task EnsureConnectedToYComm(ulong overworldOffset, PokeTradeHubConfig config, CancellationToken token)
        {
            if (!await IsGameConnectedToYComm(token).ConfigureAwait(false))
            {
                Log("Reconnecting to Y-Comm...");
                await ReconnectToYComm(overworldOffset, config, token).ConfigureAwait(false);
            }
        }

        public async Task<bool> IsGameConnectedToYComm(CancellationToken token)
        {
            // Reads the Y-Comm Flag to check if the game is connected online
            var data = await Connection.ReadBytesAsync(IsConnectedOffset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task ReconnectToYComm(ulong overworldOffset, PokeTradeHubConfig config, CancellationToken token)
        {
            // Press B in case a Error Message is Present
            await Click(B, 2000, token).ConfigureAwait(false);

            // Return to Overworld
            if (!await IsOnOverworld(overworldOffset, token).ConfigureAwait(false))
            {
                for (int i = 0; i < 5; i++)
                {
                    await Click(B, 500, token).ConfigureAwait(false);
                }
            }

            await Click(Y, 1000, token).ConfigureAwait(false);

            // Press it twice for safety -- sometimes misses it the first time.
            await Click(PLUS, 2_000, token).ConfigureAwait(false);
            await Click(PLUS, 5_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);

            for (int i = 0; i < 5; i++)
            {
                await Click(B, 500, token).ConfigureAwait(false);
            }
        }

        public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
        {
            // Reopen the game if we get soft-banned
            Log("Potential soft ban detected, reopening game just in case!");
            await CloseGame(config, token).ConfigureAwait(false);
            await StartGame(config, token).ConfigureAwait(false);

            // In case we are soft banned, reset the timestamp
            await UnSoftBan(token).ConfigureAwait(false);
        }

        public async Task UnSoftBan(CancellationToken token)
        {
            // Like previous generations, the game uses a Unix timestamp for 
            // how long we are soft banned and once the soft ban is lifted
            // the game sets the value back to 0 (1970/01/01 12:00 AM (UTC))
            Log("Soft ban detected, unbanning.");
            var data = BitConverter.GetBytes(0);
            await Connection.WriteBytesAsync(data, SoftBanUnixTimespanOffset, token).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfSoftBanned(CancellationToken token)
        {
            // Check if the Unix Timestamp isn't zero, if so we are soft banned.
            var data = await Connection.ReadBytesAsync(SoftBanUnixTimespanOffset, 1, token).ConfigureAwait(false);
            return data[0] > 1;
        }

        public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Close out of the game
            await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
            Log("Closed out of the game!");
        }

        public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Open game.
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (timing.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            await Click(A, 1_000 + timing.ExtraTimeCheckDLC, token).ConfigureAwait(false);
            // If they have DLC on the system and can't use it, requires an UP + A to start the game.
            // Should be harmless otherwise since they'll be in loading screen.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);

            Log("Restarting the game!");

            // Switch Logo lag, skip cutscene, game load screen
            await Task.Delay(10_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

            for (int i = 0; i < 4; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timer = 60_000;
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
            {
                await Task.Delay(0_200, token).ConfigureAwait(false);
                timer -= 0_250;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0 && !timing.AvoidSystemUpdate)
                {
                    Log("Still not in the game, initiating rescue protocol!");
                    while (!await IsOnOverworldTitle(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    break;
                }
            }

            Log("Back in the overworld!");
        }

        public async Task<bool> IsCorrectScreen(uint expectedScreen, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
            return BitConverter.ToUInt32(data, 0) == expectedScreen;
        }

        public async Task<uint> GetCurrentScreen(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
            return BitConverter.ToUInt32(data, 0);
        }

        public async Task<bool> IsInBattle(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(Version == GameVersion.SH ? InBattleRaidOffsetSH : InBattleRaidOffsetSW, 1, token).ConfigureAwait(false);
            return data[0] == (Version == GameVersion.SH ? 0x40 : 0x41);
        }

        public async Task<bool> IsInBox(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
            var dataint = BitConverter.ToUInt32(data, 0);
            return dataint is CurrentScreen_Box1 or CurrentScreen_Box2;
        }

        // Only used to check if we made it off the title screen.
        private async Task<bool> IsOnOverworldTitle(CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (!valid)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }

        public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
            return (TextSpeedOption)(data[0] & 3);
        }

        public async Task SetTextSpeed(TextSpeedOption speed, CancellationToken token)
        {
            var textSpeedByte = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
            var data = new[] { (byte)((textSpeedByte[0] & 0xFC) | (int)speed) };
            await Connection.WriteBytesAsync(data, TextSpeedOffset, token).ConfigureAwait(false);
        }

        public async Task ToggleAirplane(int delay, CancellationToken token)
        {
            await PressAndHold(HOME, 2_000, 1_000, token).ConfigureAwait(false);
            for (int i = 0; i < 4; i++)
                await Click(DDOWN, i == 3 ? delay : 0_150, token).ConfigureAwait(false);
            await Click(A, 2_000, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
        }

        public async Task<bool> SpinTrade(uint offset, byte[] comparison, int waitms, int waitInterval, bool match, CancellationToken token)
        {
            // Revival of Red's SpinTrade
            var name = $"{Connection.Name} {Connection.Label}";
            await GetCoordinatesForSpin(name, token).ConfigureAwait(false);
            var sw = new Stopwatch();
            sw.Start();

            do
            {
                var result = await Connection.ReadBytesAsync(offset, comparison.Length, token).ConfigureAwait(false);
                if (match == result.SequenceEqual(comparison))
                {
                    await SetStick(SwitchStick.LEFT, 0, 0, 0_100, token).ConfigureAwait(false);
                    await Task.Delay(waitInterval, token).ConfigureAwait(false);
                    return true;
                }

                if (sw.ElapsedMilliseconds < waitms - 4_000) // Give it ample time to finish the pirouette end animation before correcting position
                {
                    await SetStick(SwitchStick.LEFT, -3_500, 0, 0, token).ConfigureAwait(false); // ←
                    await SetStick(SwitchStick.LEFT, 0, -3_500, 0, token).ConfigureAwait(false); // ↓
                    await SetStick(SwitchStick.LEFT, 3_500, 0, 0, token).ConfigureAwait(false); // →
                    await SetStick(SwitchStick.LEFT, 0, 3_500, 0, token).ConfigureAwait(false); // ↑
                }
                else await SetStick(SwitchStick.LEFT, 0, 0, 0_100, token).ConfigureAwait(false);
            } while (sw.ElapsedMilliseconds < waitms);

            await Task.Delay(waitInterval, token).ConfigureAwait(false);
            await SpinCorrection(name, token).ConfigureAwait(false);
            return false;
        }

        public async Task SpinCorrection(string name, CancellationToken token)
        {
            await SwitchConnection.WriteBytesAbsoluteAsync(TradeExtensions<PK8>.Coordinates[name].Item1, TradeExtensions<PK8>.CoordinatesOffset, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(TradeExtensions<PK8>.Coordinates[name].Item2, TradeExtensions<PK8>.CoordinatesOffset + 0x4, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(TradeExtensions<PK8>.Coordinates[name].Item3, TradeExtensions<PK8>.CoordinatesOffset + 0x8, token).ConfigureAwait(false);
        }

        private async Task GetCoordinatesForSpin(string name, CancellationToken token)
        {
            if (TradeExtensions<PK8>.Coordinates.TryGetValue(name, out _))
                return;

            TradeExtensions<PK8>.CoordinatesOffset = await ParsePointer("[[[[[[main+26365B8]+88]+1F8]+E0]+10]+E0]+60", token).ConfigureAwait(false); // Thank you for the pointer, Zyro <3
            TradeExtensions<PK8>.Coordinates.Add(name,
                (
                    await SwitchConnection.ReadBytesAbsoluteAsync(TradeExtensions<PK8>.CoordinatesOffset, 4, token).ConfigureAwait(false),
                    await SwitchConnection.ReadBytesAbsoluteAsync(TradeExtensions<PK8>.CoordinatesOffset + 0x4, 4, token).ConfigureAwait(false),
                    await SwitchConnection.ReadBytesAbsoluteAsync(TradeExtensions<PK8>.CoordinatesOffset + 0x8, 4, token).ConfigureAwait(false)
                ));
        }

        public async Task SaveGame(ulong offset, CancellationToken token)
        {
            await Click(B, 0_200, token).ConfigureAwait(false);
            Log("Saving the game...");
            await Click(X, 2_000, token).ConfigureAwait(false);
            await Click(R, 0_250, token).ConfigureAwait(false);
            while (!await IsOnOverworld(offset,token).ConfigureAwait(false))
                await Click(A, 0_500, token).ConfigureAwait(false);
            Log("Game saved!");
        }

        public async Task<bool> LairStatusCheck(ushort val, uint ofs, CancellationToken token) => BitConverter.GetBytes(val).SequenceEqual(await Connection.ReadBytesAsync(ofs, 2, token).ConfigureAwait(false));
        public async Task<bool> LairStatusCheck(uint val, uint ofs, CancellationToken token) => BitConverter.GetBytes(val).SequenceEqual(await Connection.ReadBytesAsync(ofs, 4, token).ConfigureAwait(false));
        public async Task<bool> LairStatusCheckMain(ushort val, ulong ofs, CancellationToken token) => BitConverter.GetBytes(val).SequenceEqual(await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 2, token).ConfigureAwait(false));

        public async Task<ulong> ParsePointer(string pointer, CancellationToken token) //Code from LiveHex
        {
            var ptr = pointer;
            uint finadd = 0;
            if (!ptr.EndsWith("]"))
                finadd = Util.GetHexValue(ptr.Split('+').Last());
            var jumps = ptr.Replace("main", "").Replace("[", "").Replace("]", "").Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries);
            if (jumps.Length == 0)
            {
                Log("Invalid Pointer");
                return 0;
            }

            var initaddress = Util.GetHexValue(jumps[0].Trim());
            ulong address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesMainAsync(initaddress, 0x8, token).ConfigureAwait(false), 0);
            foreach (var j in jumps)
            {
                var val = Util.GetHexValue(j.Trim());
                if (val == initaddress)
                    continue;
                if (val == finadd)
                {
                    address += val;
                    break;
                }
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + val, 0x8, token).ConfigureAwait(false), 0);
            }
            return address;
        }

        public async Task<PK8?> ReadUntilPresentAbsolute(ulong offset, int waitms, int waitInterval, CancellationToken token, int size = BoxFormatSlotSize) // Need to eliminate duplicate code, currently a hack
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
                var pk = new PK8(data);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;

                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }
        public async Task<byte[]> ReadOverWorldSpawnBlock(CancellationToken token) => await Connection.ReadBytesAsync(0x4505B3C0, 24592, token).ConfigureAwait(false);
        public async Task<List<PK8>> ReadOwPokemonFromBlock(byte[] KCoordinates, SAV8SWSH sav, CancellationToken token)
        {
            var PK8s = new List<PK8>();

            var i = 8;
            var j = 0;
            var count = 0;
            var last_index = i;

            while (!token.IsCancellationRequested && i < KCoordinates.Length)
            {
                if (j == 12)
                {
                    if (KCoordinates[i - 68] != 0 && KCoordinates[i - 68] != 255)
                    {
                        var bytes = KCoordinates.Slice(i - 68, 56);
                        j = 0;
                        i = last_index + 8;
                        last_index = i;
                        count++;
                        var pkm = await ReadOwPokemon(0, 0, bytes, sav, token).ConfigureAwait(false);
                        if (pkm != null)
                            PK8s.Add(pkm);
                    }
                }

                if (KCoordinates[i] == 0xFF)
                {
                    if (i % 8 == 0)
                        last_index = i;
                    i++;
                    j++;
                }
                else
                {
                    j = 0;
                    if (i == last_index)
                    {
                        i += 8;
                        last_index = i;
                    }
                    else
                    {
                        i = last_index + 8;
                        last_index = i;
                    }
                }

            }
            return PK8s;
        }
        public async Task<PK8?> ReadOwPokemon(Species target, uint startoffset, byte[]? mondata, SAV8SWSH TrainerData, CancellationToken token)
        {
            byte[]? data = null;
            Species species = 0;
            uint offset = startoffset;
            int i = 0;

            if (target != (Species)0)
            {
                do
                {
                    data = await Connection.ReadBytesAsync(offset, 56, token).ConfigureAwait(false);
                    species = (Species)BitConverter.ToUInt16(data.Slice(0, 2), 0);
                    offset += 192;
                    i++;
                } while (target != 0 && species != 0 && target != species && i <= 40);
                if (i > 40)
                    data = null;
            }
            else if (mondata != null)
            {
                data = mondata;
                species = (Species)BitConverter.ToUInt16(data.Slice(0, 2), 0);
            }

            if (data != null && data[20] == 1)
            {
                var pk = new PK8
                {
                    Species = (ushort)species,
                    Form = data[2],
                    CurrentLevel = data[4],
                    Met_Level = data[4],
                    Nature = data[8],
                    Gender = (data[10] == 1) ? 0 : 1,
                    OT_Name = TrainerData.OT,
                    TID16 = TrainerData.TID16,
                    SID16 = TrainerData.SID16,
                    OT_Gender = TrainerData.Gender,
                    HT_Name = TrainerData.OT,
                    HT_Gender = TrainerData.Gender,
                    Move1 = BitConverter.ToUInt16(data.Slice(48, 2), 0),
                    Move2 = BitConverter.ToUInt16(data.Slice(50, 2), 0),
                    Move3 = BitConverter.ToUInt16(data.Slice(52, 2), 0),
                    Move4 = BitConverter.ToUInt16(data.Slice(54, 2), 0),
                    Version = 44,
                };
                pk.SetNature(data[8]);
                pk.SetAbility(data[12] - 1);
                if (data[22] != 255)
                    pk.SetRibbonIndex((RibbonIndex)data[22]);
                if (!pk.IsGenderValid())
                    pk.Gender = 2;
                if (data[14] == 1)
                    pk.HeldItem = data[16];

                FakeShiny shinyness = (FakeShiny)(data[6] + 1);
                int ivs = data[18];
                uint seed = BitConverter.ToUInt32(data.Slice(24, 4), 0);

                pk = CalculateFromSeed(pk, shinyness, ivs, seed);

                return pk;
            }
            else
                return null;
        }
        public static PK8 CalculateFromSeed(PK8 pk, FakeShiny shiny, int flawless, uint seed)
        {
            var UNSET = -1;
            var xoro = new Xoroshiro128Plus(seed);

            // Encryption Constant
            pk.EncryptionConstant = (uint)xoro.NextInt(uint.MaxValue);

            // PID
            var pid = (uint)xoro.NextInt(uint.MaxValue);
            if (shiny == FakeShiny.Never)
            {
                if (GetIsShiny(pk.TID16, pk.SID16, pid))
                    pid ^= 0x1000_0000;
            }

            else if (shiny != FakeShiny.Random)
            {
                if (!GetIsShiny(pk.TID16, pk.SID16, pid))
                    pid = GetShinyPID(pk.TID16, pk.SID16, pid, 0);
            }

            pk.PID = pid;

            // IVs
            var ivs = new[] { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            const int MAX = 31;
            for (int i = 0; i < flawless; i++)
            {
                int index;
                do { index = (int)xoro.NextInt(6); }
                while (ivs[index] != UNSET);

                ivs[index] = MAX;
            }

            for (int i = 0; i < ivs.Length; i++)
            {
                if (ivs[i] == UNSET)
                    ivs[i] = (int)xoro.NextInt(32);
            }

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            return pk;
        }

        private static uint GetShinyPID(int tid, int sid, uint pid, int type)
        {
            return (uint)(((tid ^ sid ^ (pid & 0xFFFF) ^ type) << 16) | (pid & 0xFFFF));
        }

        private static bool GetIsShiny(int tid, int sid, uint pid)
        {
            return GetShinyXor(pid, (uint)((sid << 16) | tid)) < 16;
        }

        private static uint GetShinyXor(uint pid, uint oid)
        {
            var xor = pid ^ oid;
            return (xor ^ (xor >> 16)) & 0xFFFF;
        }
        public enum FakeShiny : byte
        {
            /// <summary>
            /// PID is fixed to a specified value.
            /// </summary>
            FixedValue = 0,

            /// <summary>
            /// PID is purely random; can be shiny or not shiny.
            /// </summary>
            Random = 1,

            /// <summary>
            /// PID is randomly created and forced to be shiny.
            /// </summary>
            Always = 2,

            /// <summary>
            /// PID is randomly created and forced to be not shiny.
            /// </summary>
            Never = 3,

            /// <summary>
            /// PID is randomly created and forced to be shiny as Stars.
            /// </summary>
            AlwaysStar = 5,

            /// <summary>
            /// PID is randomly created and forced to be shiny as Squares.
            /// </summary>
            AlwaysSquare = 6,
        }
        public async Task SetupBoxState(IDumper DumpSetting, CancellationToken token)
        {
            await SetCurrentBox(0, token).ConfigureAwait(false);

            var existing = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (existing.Species != 0 && existing.ChecksumValid)
            {
                Log("Destination slot is occupied! Dumping the Pokémon found there...");
                DumpPokemon(DumpSetting.DumpFolder, "saved", existing);
            }

            Log("Clearing destination slot to start the bot.");
            PK8 blank = new();
            await SetBoxPokemon(blank, 0, 0, token).ConfigureAwait(false);
        }
    }
}
