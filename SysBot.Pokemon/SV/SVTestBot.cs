using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSCVI ;

namespace SysBot.Pokemon
{
    public class SVTestBot : PokeRoutineExecutor9SCVI
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly SVTestSettings TradeSettings;
        public SVTestBot(PokeTradeHub<PK8> hub, PokeBotState cfg) : base(cfg)
        {
            Hub = hub;
            TradeSettings = hub.Config.test;

        }
        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                //var sav = await IdentifyTrainer(token).ConfigureAwait(false);
               // RecentTrainerCache.SetRecentTrainer(sav);

                Log($"Starting main {nameof(SVTestBot)} loop.");
                await InnerLoop(new SAV8SWSH(), token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.Message);
                Connection.LogError(e.StackTrace);
            }

            Log($"Ending {nameof(SVTestBot)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            
            await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task InnerLoop(SAV8SWSH sav, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var off = await GetPointerAddress("[[[[[[[[[main+42DBC98]+B0]+0]+0]+30]+B8]+50]+10]+988]", token);
                var pk1 = System.IO.File.ReadAllBytes(TradeSettings.filename);
                var test = EntityFormat.GetFromBytes(pk1);
                Log($"{test.Species}");
                await Connection.WriteBytesAsync(test.EncryptedPartyData,(uint) off, token);

                return;
            }
        }
     

        public override Task<PK8> ReadPokemon(ulong offset, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            throw new NotImplementedException();
        }
        private async Task<ulong> GetPointerAddress( string ptr, CancellationToken token, bool heaprealtive = true)
        {
           
            while (ptr.Contains("]]"))
                ptr = ptr.Replace("]]", "]+0]");
            uint? finadd = null;
            if (!ptr.EndsWith("]"))
            {
                finadd = Util.GetHexValue(ptr.Split('+').Last());
                ptr = ptr[..ptr.LastIndexOf('+')];
            }
            var jumps = ptr.Replace("main", "").Replace("[", "").Replace("]", "").Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries);
   

            var initaddress = Util.GetHexValue(jumps[0].Trim());
            ulong address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesMainAsync(initaddress, 0x8,token), 0);
            foreach (var j in jumps)
            {
                var val = Util.GetHexValue(j.Trim());
                if (val == initaddress)
                    continue;
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + val, 0x8,token), 0);
            }
            if (finadd != null) address += (ulong)finadd;
            if (heaprealtive)
            {
                ulong heap = await SwitchConnection.GetHeapBaseAsync(token);
                address -= heap;
            }
            return address;
        }

    }
}
