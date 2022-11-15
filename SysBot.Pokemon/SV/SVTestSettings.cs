using System.Collections.Generic;
using PKHeX.Core;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class SVTestSettings : IBotStateSettings
    {
        private const string SVTestSetting = nameof(SVTestSettings);
        public override string ToString() => "Test Bot Settings";
        [Category(SVTestSetting)]
        public string filename { get; set; }

        public bool ScreenOff { get; set; } = false;
    }
}
