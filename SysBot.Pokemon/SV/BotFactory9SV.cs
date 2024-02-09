﻿using PKHeX.Core;
using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public sealed class BotFactory9SV : BotFactory<PK9>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK9> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                => new PokeTradeBotSV(Hub, cfg),

            PokeRoutineType.RemoteControl => new RemoteControlBotSV(cfg),
            PokeRoutineType.EncounterLine => new LineBot(cfg,Hub),
            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                or PokeRoutineType.EncounterLine
                => true,

            PokeRoutineType.RemoteControl => true,

            _ => false,
        };
    }
}
