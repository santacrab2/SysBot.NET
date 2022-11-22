using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class BotFactory9SV : BotFactory<PK8>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.SVInject 
            or PokeRoutineType.SVShinify
            or PokeRoutineType.SVCloneShinify
            or PokeRoutineType.svdump
            => new SVTestBot(Hub, cfg),

            PokeRoutineType.RemoteControl => new RemoteControlBot(cfg),

            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };
        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.SVInject 
            or PokeRoutineType.SVShinify 
            or PokeRoutineType.SVCloneShinify
            or PokeRoutineType.svdump
             => true,


            _ => false,
        };
    }
}
