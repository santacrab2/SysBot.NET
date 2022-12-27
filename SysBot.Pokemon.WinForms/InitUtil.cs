
using PKHeX.Core;

namespace SysBot.Pokemon.WinForms
{
    public static class InitUtil
    {
        public static void InitializeStubs(ProgramMode mode)
        {
            SaveFile sav = mode switch
            {
                ProgramMode.SWSH => new SAV8SWSH(),
                ProgramMode.BDSP => new SAV8BS(),
                ProgramMode.LA   => new SAV8LA(),
                ProgramMode.LGPE => new SAV7b(),
                ProgramMode.SV   => new SAV9SV(),

                _                => throw new System.ArgumentOutOfRangeException(nameof(mode)),
            };

         
        }

    }
}

