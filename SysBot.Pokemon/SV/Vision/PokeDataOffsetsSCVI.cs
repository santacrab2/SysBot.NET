using System.Collections.Generic;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Pokémon Scarlet and Violet Offsets
    /// </summary>
    public class PokeDataOffsetsSCVI
    {
        //Scarlet and Violet

        public const string ScarletID = "0100A3D008C5C000";
        public const string VioletID = "01008F6008C5E000";

       public static IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x4384B18, 0x128, 0x9B0, 0x0 };
     

        public static IReadOnlyList<long> MyStatusBlockPointer { get; } = new long[] { 0x43A77C8, 0x128, 0x40 };

        public static IReadOnlyList<long> OfferedPokemonPointer { get; } = new long[] { 0x4347468, 0x778, 0x38, 0x08, 0xB7E };
        public static IReadOnlyList<long> TradePartnerStatusBlockPointer { get; } = new long[] { 0x43A7910, 0x28, 0xE0, 0x0 };
        public static IReadOnlyList<long> TradePartnerStatusBlockPointer2 { get; } = new long[] { 0x43A7910, 0x28, 0xB0, 0x00 };
        public static IReadOnlyList<long> ConnectionPointer { get; } = new long[] { 0x43A7918, 0x10 };
        public static IReadOnlyList<long> OverworldPointer { get; } = new long[] { 0x43F3538, 0x00, 0x388, 0x3C0, 0x00, 0x1A1C };
        public static IReadOnlyList<long> IsSearchingPointer { get; } = new long[] { 0x43A7718, 0x58 };

        public const int EncryptedSize = 0x158;
    }
}
