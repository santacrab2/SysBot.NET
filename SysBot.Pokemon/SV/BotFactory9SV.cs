using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class BotFactory9SV : BotFactory<PK8>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.FlexTrade 
                => new SVTestBot(Hub, cfg),

            PokeRoutineType.RemoteControl => new RemoteControlBot(cfg),

            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };
        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade
                => true,

            PokeRoutineType.RemoteControl => true,

            _ => false,
        };
    }
}
