using PKHeX.Core;
using SysBot.Base;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSCVI;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor9SCVI : PokeRoutineExecutor<PK9>
    {
        public static uint GetBoxSlotOffset(ulong BoxStartOffset,int box, int slot) => (uint)BoxStartOffset + (uint)(EncryptedSize * ((30 * box) + slot));

        protected PokeDataOffsetsSCVI Offsets { get; } = new();
        protected PokeRoutineExecutor9SCVI(PokeBotState cfg) : base(cfg) { }

        public new LanguageID GameLang { get; private set; }
        public new GameVersion Version { get; private set; }

        public override void SoftStop() => Config.Pause();

        public new async Task Click(SwitchButton b, int delayMin, int delayMax, CancellationToken token) =>
            await Click(b, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);

        public new async Task SetStick(SwitchStick stick, short x, short y, int delayMin, int delayMax, CancellationToken token) =>
            await SetStick(stick, x, y, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);

        public override async Task<PK9> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync((uint)offset, size, token).ConfigureAwait(false);
            return new PK9(data);
        }
        public async Task SetBoxPokemon(PK9 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
        {
            if (sav != null)
            {
                // Update PKM to the current save's handler data
                DateTime Date = DateTime.Now;
                pkm.Trade(sav, Date.Day, Date.Month, Date.Year);
                pkm.RefreshChecksum();
            }
            var offset = await SwitchConnection.PointerRelative(BoxStartPokemonPointer, token);
            
            //pkm.ResetPartyStats();
            await SwitchConnection.WriteBytesAsync(pkm.EncryptedPartyData, (uint)offset, token).ConfigureAwait(false);
        }
        public override async Task<PK9> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var offset = await SwitchConnection.PointerRelative(jumps, token).ConfigureAwait(false);
            
            return await ReadPokemon(offset,size, token).ConfigureAwait(false);
        }

        public async Task<SAV9SV> SCVIIdentifyTrainer(CancellationToken token)
        {
            Log("Grabbing trainer data of host console...");
            SAV9SV sav = await IdentifyTrainer(token).ConfigureAwait(false);
            GameLang = (LanguageID)sav.Language;
            Version = sav.Version;
            //InGameName = sav.OT;
            Connection.Label = $"{InGameName}-{sav.DisplayTID:000000}";
            Log($"{Connection.Name} identified as {Connection.Label}, using {GameLang}.");

            return sav;
        }

        public static void DumpPokemon(string folder, string subfolder, PKM pk)
        {
            if (!Directory.Exists(folder))
                return;
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            var fn = Path.Combine(dir, Util.CleanFileName(pk.FileName));
            File.WriteAllBytes(fn, pk.DecryptedPartyData);
            LogUtil.LogInfo($"Saved file: {fn}", "Dump");
        }
        public async Task<SAV9SV> IdentifyTrainer(CancellationToken token)
        {
            // Check title so we can warn if mode is incorrect.
            string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            if (title is not (ScarletID or VioletID))
                throw new Exception($"{title} is not a valid SWSH title. Is your mode correct?");

            Log("Grabbing trainer data of host console...");
            var sav = await SCVIGetFakeTrainerSAV(token);
            InitSaveData(sav);

            if (!IsValidTrainerData())
                throw new Exception("Trainer data is not valid. Refer to the SysBot.NET wiki for bad or no trainer data.");
            //if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
                //throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

            return (SAV9SV)sav;
        }
        public async Task<SAV9SV> SCVIGetFakeTrainerSAV(CancellationToken token)
        {
            var traineroff = await SwitchConnection.PointerRelative(MyStatusBlockPointer, token).ConfigureAwait(false);
            var sav = new SAV9SV();
            var info = sav.MyStatus;
            var read = await Connection.ReadBytesAsync((uint)traineroff, 0x68, token).ConfigureAwait(false);
            read.CopyTo(info.Data, 0);
            return sav;
        }


        public async Task CleanExit(IBotStateSettings settings, CancellationToken token)
        {
            if (settings.ScreenOff)
            {
                Log("Turning on screen.");
                await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            }
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
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
    }
}