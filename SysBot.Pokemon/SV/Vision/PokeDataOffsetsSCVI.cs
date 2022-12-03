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

        public static IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x42FD510,0xA90,0x9B0 };

        public static IReadOnlyList<long> MyStatusBlockPointer { get; } = new long[] { 0x42F2F80, 0xA0, 0x10, 0xB0 };

    public const int EncryptedSize = 0x158;
    }
}
