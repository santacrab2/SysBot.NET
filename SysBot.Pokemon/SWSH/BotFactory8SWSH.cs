using PKHeX.Core;
using System;

namespace SysBot.Pokemon;

public sealed class BotFactory8SWSH : BotFactory<PK8>
{
    public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
    {
        PokeRoutineType.FlexTrade or PokeRoutineType.Idle
            or PokeRoutineType.SurpriseTrade
            or PokeRoutineType.LinkTrade
            or PokeRoutineType.Clone
            or PokeRoutineType.Dump
            or PokeRoutineType.SeedCheck
            => new PokeTradeBotSWSH(Hub, cfg),

            
            PokeRoutineType.RaidBotSWSH => new RaidBotSWSH(cfg, Hub),
            PokeRoutineType.EncounterLine => new EncounterBotLineSWSH(cfg, Hub),
            PokeRoutineType.Reset => new EncounterBotResetSWSH(cfg, Hub),
            PokeRoutineType.DogBot => new EncounterBotDogSWSH(cfg, Hub),
            PokeRoutineType.OverWorldRNG => new OverworldRNG(cfg, Hub),
            PokeRoutineType.RemoteControl => new RemoteControlBotSWSH(cfg),
            PokeRoutineType.RollingRaidSWSH => new RollingRaidBot(cfg, Hub),
            PokeRoutineType.OnlineLairBot => new onlineLairBot(cfg, Hub),
            PokeRoutineType.DenBotSWSH => new DenBot(cfg,Hub),
            PokeRoutineType.EggFetch => new EncounterBotEggSWSH(cfg, Hub),
            PokeRoutineType.FossilBot => new EncounterBotFossilSWSH(cfg, Hub),
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
            or PokeRoutineType.DogBot
            or PokeRoutineType.OverWorldRNG
            or PokeRoutineType.RemoteControl
            or PokeRoutineType.DenBotSWSH
              => true,
            _ => false,
        };
    }

