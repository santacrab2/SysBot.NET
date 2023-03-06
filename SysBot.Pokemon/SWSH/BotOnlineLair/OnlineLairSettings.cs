using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class OnlineLairBotSettings : IBotStateSettings, ICountSettings
    {
        private const string FeatureToggle = nameof(FeatureToggle);
        private const string Lair = nameof(Lair);
        private const string Counts = nameof(Counts);
        public override string ToString() => "Online Lair Bot Settings";

        [Category(Lair), Description("LairBot mode.")]
        public LairBotModes LairBotMode { get; set; } = LairBotModes.OffsetLog;

        [Category(Lair), Description("Lair Bot Online Settings"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public LairOnlineSettings OnlineSettings { get; set; } = new();

        [Category(Lair)]
        [TypeConverter(typeof(LairOnlineSettingsConverter))]
        public class LairOnlineSettings
        {
            public override string ToString() => "LairBot Online Mode Settings";
            [Category(Lair), Description("Link Code, put -1 for no code")]
            public int LairOnlineCode { get; set; } = -1;
            [Category(Lair), Description("Minimum time to wait in seconds before starting the adventure, or rehosting")]
            public int LairOnlineMinWait { get; set; } = 87;
            [Category(Lair), Description("Amount of Coded raids to host before doing a no code to allow you to still shiny hunt!")]
            public int LairOnlineNoGoMax { get; set; } = 5;
            [Category(Lair), Description("The Bot accounts SID")]
            public int lairBotSID { get; set; } = 0000;
        }

        [Category(Lair), Description("Legendary Pokémon hunt queue.")]
        public LairSpecies[] LairSpeciesQueue { get; set; } = { LairSpecies.None, LairSpecies.None, LairSpecies.None };

        [Category(FeatureToggle), Description("Toggle \"True\" to reset the flag of the legendary you JUST caught. It is best to start on a save with all legends not caught, as this reads all the legend flags before the adventure, then just restores it to the previous state after the adventure.")]
        public bool ResetLegendaryCaughtFlag { get; set; } = false;

        [Category(Lair), Description("Select path of choice.")]
        public SelectPath SelectPath { get; set; } = SelectPath.GoLeft;

        [Category(FeatureToggle), Description("Toggle \"True\" to use \"StopConditions\" to only hunt legendaries with specific stop conditions.")]
        public bool UseStopConditionsPathReset { get; set; } = false;

        [Category(FeatureToggle), Description("Toggle \"False\" to continue doing random lairs after a shiny legendary is caught.")]
        public bool StopOnLegendary { get; set; } = true;

        [Category(FeatureToggle), Description("Toggle \"True\" to catch Pokémon. Default is false for speed routes.")]
        public bool CatchLairPokémon { get; set; } = false;

        [Category(FeatureToggle), Description("Toggle \"True\" to catch a Lair encounter if it's better than our current Pokémon.")]
        public bool UpgradePokemon { get; set; } = false;

        [Category(FeatureToggle), Description("Toggle \"True\" to inject a desired adventure seed.")]
        public bool InjectSeed { get; set; } = false;

        [Category(Lair), Description("Enter your desired Lair Seed in HEX to inject. MUST be 16 characters long.")]
        public string SeedToInject { get; set; } = string.Empty;

        [Category(Lair), Description("Select your desired ball to catch Pokémon with.")]
        public LairBall LairBall { get; set; } = LairBall.Poke;

        [Category(FeatureToggle), Description("Output Showdown Set for all catches regardless of match.")]
        public bool AlwaysOutputShowdown { get; set; } = false;

        [Category(Lair), Description("Enter a Discord channel ID(s) to post shiny result embeds to. Feature has to be initialized via \"$lairEmbed\" after every client restart.")]
        public string ResultsEmbedChannels { get; set; } = string.Empty;

        [Category(FeatureToggle), Description("Toggle \"True\" to enable OHKO.")]
        public bool EnableOHKO { get; set; } = false;

        [Category(FeatureToggle), Description("If \"OHKO\", \"CatchLairPokemon\", and \"InjectSeed\" are disabled, should we reset to keep the current path?")]
        public bool KeepPath { get; set; } = false;

        [Category(Lair), Description("Personal screen offset values for LairBot."), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public LairOnlineScreenValueCategory LairScreenValues { get; set; } = new();

        [Category(FeatureToggle), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        private int _completedAdventures;

        [Category(Counts), Description("Dynamax Adventures Completed")]
        public int CompletedAdventures
        {
            get => _completedAdventures;
            set => _completedAdventures = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedAdventures() => Interlocked.Increment(ref _completedAdventures);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedAdventures != 0)
                yield return $"Adventures Completed: {CompletedAdventures}";
        }

        [Category(Lair)]
        [TypeConverter(typeof(LairOnlineScreenValueCategoryConverter))]
        public class LairOnlineScreenValueCategory
        {
            public override string ToString() => "LairBot Screen Values";

            [Category(Lair), Description("Lair Lobby (CurrentScreen).")]
            public string LairLobbyValue { get; set; } = "0x00008FE0";

            [Category(Lair), Description("Lair Adventure Path (CurrentScreen).")]
            public string LairAdventurePathValue { get; set; } = "0x0000CAD8";

            [Category(Lair), Description("Lair Dmax Band Animation (MiscScreen).")]
            public string LairDmaxValue { get; set; } = "0x00000B8B";

            [Category(Lair), Description("Lair Battle Menu (MiscScreen).")]
            public string LairBattleMenuValue { get; set; } = "0x0000032E";

            [Category(Lair), Description("Lair Move Selection (MiscScreen).")]
            public string LairMovesMenuValue { get; set; } = "0x00000387";

            [Category(Lair), Description("Lair Catch Screen (MiscScreen).")]
            public string LairCatchScreenValue { get; set; } = "0x000003C9";

            [Category(Lair), Description("Lair Rewards Screen (CurrentScreen).")]
            public string LairRewardsScreenValue { get; set; } = "0x00008DC0";
        }
        private sealed class LairOnlineSettingsConverter : TypeConverter
        {
            public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes) => TypeDescriptor.GetProperties(typeof(LairOnlineSettings));

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
        }
        private sealed class LairOnlineScreenValueCategoryConverter : TypeConverter
        {
            public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes) => TypeDescriptor.GetProperties(typeof(LairOnlineScreenValueCategory));

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
        }
    }
}