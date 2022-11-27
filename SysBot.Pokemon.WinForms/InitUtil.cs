#if NETFRAMEWORK
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace SysBot.Pokemon.WinForms
{
    public static class InitUtil
    {
        public static void InitializeStubs(ProgramMode mode)
        {
            SaveFile sav8 = mode switch
            {
                ProgramMode.SWSH => new SAV8SWSH(),
                ProgramMode.BDSP => new SAV8BS(),
                ProgramMode.LA   => new SAV8LA(),
<<<<<<< HEAD
                ProgramMode.LGPE => new SAV7b(),
                ProgramMode.SV => new SAV8SWSH(),
=======
                ProgramMode.SV   => new SAV9SV(),
>>>>>>> Add support for SV 1.1.0
                _                => throw new System.ArgumentOutOfRangeException(nameof(mode)),
            };

            SetUpSpriteCreator(sav8);
        }

        private static void SetUpSpriteCreator(SaveFile sav)
        {
            SpriteUtil.Initialize(sav);
            StreamSettings.CreateSpriteFile = (pk, fn) =>
            {
                var png = pk.Sprite();
                png.Save(fn);
            };
        }
    }
}
#endif
