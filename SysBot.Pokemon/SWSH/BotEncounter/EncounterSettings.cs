using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon
{
    public class EncounterSettings : IBotStateSettings, ICountSettings
    {
        private const string Counts = nameof(Counts);
        private const string Encounter = nameof(Encounter);
        private const string Settings = nameof(Settings);
        public override string ToString() => "Encounter Bot SWSH Settings";

        [Category(Encounter), Description("The method used by the Line and Reset bots to encounter Pokémon.")]
        public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

        [Category(Settings)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public FossilSettings Fossil { get; set; } = new();

        [Category(Encounter), Description("When enabled, the bot will continue after finding a suitable match.")]
        public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

        [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]

        public bool ScreenOff { get; set; } = false;
    
        [Category(Encounter), Description("Which Direction should the bot move the player at the designated frame advance target")]
        public MovementDirection MoveDirection { get; set; }
        [Category(Encounter), Description("TID 5 digit")]
        public uint TID { get; set; }
        [Category(Encounter), Description("SID 5 digit")]
        public uint SID { get; set; }
        [Category(Encounter), Description("shiny charm?")]
        public bool shinycharm { get; set; }
        [Category(Encounter), Description("mark charm?")]
        public bool markcharm { get; set; }
        [Category(Encounter), Description("encounter slot min")]
        public uint slotmin { get; set; }
        [Category(Encounter), Description("encounter slot max")]
        public uint slotmax { get; set; }
        [Category(Encounter)]
        public uint levelmin { get; set; }
        [Category(Encounter)]
        public uint levelmax { get; set; }
        [Category(Encounter), Description("egg moves count")]
        public uint EMs { get; set; }
        [Category(Encounter)]
        public uint flawlessivs { get; set; }
        [Category(Encounter)]
        public bool weather { get; set; }
        [Category(Encounter)]
        public bool helditem { get; set; }

        [Category(Encounter)]
        public bool Static { get; set; }
        [Category(Encounter)]
        public bool fishing { get; set; }
        [Category(Encounter)]
        public bool hidden { get; set; }
        [Category(Encounter)]
        public aura theaura { get; set; }
        [Category(Encounter)]
        public ulong onedayskip { get; set; }
        [Category(Encounter), Description("how many frames before the target to start moving")]
        public ulong movementdelay { get; set; }
        [Category(Encounter)]
        public double moveduration { get; set; }
        private int _completedWild;
        private int _completedLegend;
        private int _completedEggs;
        private int _completedFossils;

        [Category(Counts), Description("Encountered Wild Pokémon")]
        public int CompletedEncounters
        {
            get => _completedWild;
            set => _completedWild = value;
        }

        [Category(Counts), Description("Encountered Legendary Pokémon")]
        public int CompletedLegends
        {
            get => _completedLegend;
            set => _completedLegend = value;
        }

        [Category(Counts), Description("Eggs Retrieved")]
        public int CompletedEggs
        {
            get => _completedEggs;
            set => _completedEggs = value;
        }


        [Category(Counts), Description("Fossil Pokémon Revived")]
        public int CompletedFossils
        {
            get => _completedFossils;
            set => _completedFossils = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
        public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);
        public int AddCompletedEggs() => Interlocked.Increment(ref _completedEggs);
        public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedEncounters != 0)
                yield return $"Wild Encounters: {CompletedEncounters}";
            if (CompletedLegends != 0)
                yield return $"Legendary Encounters: {CompletedLegends}";
            if (CompletedEggs != 0)
                yield return $"Eggs Received: {CompletedEggs}";
            if (CompletedFossils != 0)
                yield return $"Completed Fossils: {CompletedFossils}";
        }
        public enum MovementDirection
        {
            up,
            down,
            left,
            right,
            upleft,
            upright,
            downleft,
            downright,


        }
        public enum aura
        {
            Ignore,
            None,
            Brilliant
        }
    }
}
