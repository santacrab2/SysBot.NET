using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Base;

public abstract class SwitchRoutineExecutor<T> : RoutineExecutor<T> where T : class, IConsoleBotConfig
{
    public readonly bool UseCRLF;
    protected readonly ISwitchConnectionAsync SwitchConnection;

    protected SwitchRoutineExecutor(IConsoleBotManaged<IConsoleConnection, IConsoleConnectionAsync> Config) : base(Config)
    {
        UseCRLF = Config.GetInnerConfig() is ISwitchConnectionConfig { UseCRLF: true };
        if (Connection is not ISwitchConnectionAsync connect)
            throw new System.Exception("Not a valid switch connection");
        SwitchConnection = connect;
    }

    public override Task InitialStartup(CancellationToken token) => EchoCommands(false, token);

    public async Task Click(SwitchButton b, int delay, CancellationToken token)
    {
        await Connection.SendAsync(SwitchCommand.Click(b, UseCRLF), token).ConfigureAwait(false);
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    public async Task PressAndHold(SwitchButton b, int hold, int delay, CancellationToken token)
    {
        await Connection.SendAsync(SwitchCommand.Hold(b, UseCRLF), token).ConfigureAwait(false);
        await Task.Delay(hold, token).ConfigureAwait(false);
        await Connection.SendAsync(SwitchCommand.Release(b, UseCRLF), token).ConfigureAwait(false);
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    public async Task DaisyChainCommands(int delay, IEnumerable<SwitchButton> buttons, CancellationToken token)
    {
        SwitchCommand.Configure(SwitchConfigureParameter.mainLoopSleepTime, delay, UseCRLF);
        var commands = buttons.Select(z => SwitchCommand.Click(z, UseCRLF)).ToArray();
        var chain = commands.SelectMany(x => x).ToArray();
        await Connection.SendAsync(chain, token).ConfigureAwait(false);
        SwitchCommand.Configure(SwitchConfigureParameter.mainLoopSleepTime, 0, UseCRLF);
    }

    public async Task SetStick(SwitchStick stick, short x, short y, int delay, CancellationToken token)
    {
        var cmd = SwitchCommand.SetStick(stick, x, y, UseCRLF);
        await Connection.SendAsync(cmd, token).ConfigureAwait(false);
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    public async Task DetachController(CancellationToken token)
    {
        await Connection.SendAsync(SwitchCommand.DetachController(UseCRLF), token).ConfigureAwait(false);
    }

        public override async Task SetController(ControllerType ControllerType, CancellationToken token)
        {
            var cmd = SwitchCommand.Configure(SwitchConfigureParameter.controllerType, (int)ControllerType);
            await Connection.SendAsync(cmd, token).ConfigureAwait(false);
        }
        public async Task SetScreen(ScreenState state, CancellationToken token)
        {
            await Connection.SendAsync(SwitchCommand.SetScreen(state, UseCRLF), token).ConfigureAwait(false);
        }

    public async Task EchoCommands(bool value, CancellationToken token)
    {
        var cmd = SwitchCommand.Configure(SwitchConfigureParameter.echoCommands, value ? 1 : 0, UseCRLF);
        await Connection.SendAsync(cmd, token).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ReadUntilChanged(ulong,byte[],int,int,bool,bool,CancellationToken)"/>
    public Task<bool> ReadUntilChanged(uint offset, byte[] comparison, int waitms, int waitInterval, bool match, CancellationToken token) =>
        ReadUntilChanged(offset, comparison, waitms, waitInterval, match, false, token);

    /// <summary>
    /// Reads an offset until it changes to either match or differ from the comparison value.
    /// </summary>
    /// <returns>If <see cref="match"/> is set to true, then the function returns true when the offset matches the given value.<br>Otherwise, it returns true when the offset no longer matches the given value.</br></returns>
    public async Task<bool> ReadUntilChanged(ulong offset, byte[] comparison, int waitms, int waitInterval, bool match, bool absolute, CancellationToken token)
    {
        var sw = new Stopwatch();
        sw.Start();
        do
        {
            var task = absolute
                ? SwitchConnection.ReadBytesAbsoluteAsync(offset, comparison.Length, token)
                : SwitchConnection.ReadBytesAsync((uint)offset, comparison.Length, token);
            var result = await task.ConfigureAwait(false);
            if (match == result.SequenceEqual(comparison))
                return true;

                await Task.Delay(waitInterval, token).ConfigureAwait(false);
            } while (sw.ElapsedMilliseconds < waitms);
            return false;
        }

        public async Task DaySkip(CancellationToken token) => await Connection.SendAsync(SwitchCommand.DaySkip(UseCRLF), token).ConfigureAwait(false);
        public async Task ResetTime(CancellationToken token) => await Connection.SendAsync(SwitchCommand.ResetTime(UseCRLF), token).ConfigureAwait(false);
    }
    public enum ControllerType
    {
        JoyRight1 = 1,   ///< Joy-Con right controller.
        JoyLeft2 = 2,   ///< Joy-Con left controller.
        ProController = 3,   ///< Pro Controller and Gc controller.
        JoyLeft4 = 4,    ///< Joy-Con left controller.
        JoyRight5 = 5,   ///< Joy-Con right controller.
        ProController2 = 6,   ///< Pro Controller and GC Controller
        FamicomLeft = 7,   ///< Famicom left controller.
        FamicomRight = 8,    ///< Famicom right controller (with microphone).
        NESLeft = 9,    ///< NES left controller.
        NESRight = 10,   ///< NES right controller.
        SNES = 11,   ///< SNES controller
        PokeBallPlus = 12,  ///< Pok� Ball Plus controller.
        ProController3 = 13,  ///< Pro Controller and Gc controller.
        ProController4 = 15,  ///< Pro Controller and Gc controller.
        DebugPad = 17,  ///< DebugPad
        System19 = 19,  ///< Generic controller.
        System20 = 20,  ///< Generic controller.
        System21 = 21,  ///< Generic controller.
        N64 = 22,  ///< N64 controller
        SegaGenesis = 28,   ///< Sega Genesis controller
    }
}