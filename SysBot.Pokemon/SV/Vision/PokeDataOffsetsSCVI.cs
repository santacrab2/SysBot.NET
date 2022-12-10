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
        public static IReadOnlyList<long> possiblescarletTradePartnerStatusBlock { get; } = new long[] { 0x439D1C0, 0xA0, 0x10, 0xE0 };


    public const int EncryptedSize = 0x158;
    }
}
