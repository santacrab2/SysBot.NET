using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public sealed class BotFactory8 : BotFactory<PK8>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.SurpriseTrade
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                or PokeRoutineType.SeedCheck
                => new PokeTradeBot(Hub, cfg),

            PokeRoutineType.EggFetch => new EggBot(cfg, Hub),
            PokeRoutineType.FossilBot => new FossilBot(cfg, Hub),
            PokeRoutineType.RaidBotSWSH => new RaidBot(cfg, Hub),
            PokeRoutineType.EncounterLine => new EncounterBotLine(cfg, Hub),
            PokeRoutineType.Reset => new EncounterBotReset(cfg, Hub),
            PokeRoutineType.Dogbot => new EncounterBotDog(cfg, Hub),
            PokeRoutineType.OverWorldRNG => new OverworldRNG(cfg, Hub),
            PokeRoutineType.RemoteControl => new RemoteControlBot(cfg),
            PokeRoutineType.RollingRaidSWSH => new RollingRaidBot(cfg, Hub),
            PokeRoutineType.OnlineLairBot => new onlineLairBot(cfg, Hub),
            PokeRoutineType.DenBotSWSH => new DenBot(cfg,Hub),
            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.SurpriseTrade
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                or PokeRoutineType.SeedCheck 
                or PokeRoutineType.RollingRaidSWSH
                or PokeRoutineType.OnlineLairBot
                or PokeRoutineType.EggFetch
                or PokeRoutineType.FossilBot
            or PokeRoutineType.RaidBotSWSH
            or PokeRoutineType.EncounterLine
            or PokeRoutineType.Reset
            or PokeRoutineType.Dogbot
            or PokeRoutineType.OverWorldRNG
            or PokeRoutineType.RemoteControl
            or PokeRoutineType.DenBotSWSH
              => true,
            _ => false,
        };
    }
}
