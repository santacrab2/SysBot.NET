using System.Collections.Generic;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Pokémon Scarlet and Violet Offsets
    /// </summary>
    public class PokeDataOffsetsSCVI
    {
        //Scarlet and Violet

        public const string ScarletID = "0100A3D008C5C000 ";
        public const string VioletID = "01008F6008C5E000";

        public IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x42DBC98, 0xb0, 0x0, 0x0, 0x30, 0xb8, 0x50, 0x10, 0x988 };

        public const int EncryptedSize = 0x158;
    }
}
