using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class BotFactory9SV : BotFactory<PK9>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK9> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
           PokeRoutineType.FlexTrade
            or PokeRoutineType.LinkTrade
            or PokeRoutineType.Clone
            or PokeRoutineType.Dump
            => new PokeTradeBotSV(Hub,cfg),

            PokeRoutineType.RemoteControl => new RemoteControlBot(cfg),

            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };
        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade or
            PokeRoutineType.SVInject 
            or PokeRoutineType.SVShinify 
            or PokeRoutineType.SVCloneShinify
            or PokeRoutineType.svdump
            or PokeRoutineType.files
             => true,


            _ => false,
        };
    }
}
