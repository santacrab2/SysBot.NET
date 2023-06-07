using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System.Linq;
using System.Text;

namespace SysBot.Pokemon
{
    public static class ShowdownUtil
    {
        /// <summary>
        /// Converts a single line to a showdown set
        /// </summary>
        /// <param name="setstring">single string</param>
        /// <returns>ShowdownSet object</returns>
        public static ShowdownSet? ConvertToShowdown(string setstring)
        {
            // LiveStreams remove new lines, so we are left with a single line set
            var restorenick = string.Empty;

            var nickIndex = setstring.LastIndexOf(')');
            if (nickIndex > -1)
            {
                restorenick = setstring[..(nickIndex + 1)];
                if (restorenick.TrimStart().StartsWith("("))
                    return null;
                setstring = setstring[(nickIndex + 1)..];
            }

            foreach (string i in splittables)
            {
                if (setstring.Contains(i))
                    setstring = setstring.Replace(i, $"\r\n{i}");
            }

            var finalset = restorenick + setstring;

            var TheShow = new ShowdownSet(finalset);
            if (TheShow.Species == 0)
            {
                if (finalset.Contains("Language:"))
                {
                    var setsplit = finalset.Split("\r\n");
                    var lang = setsplit.Where(z => z.Contains("Language:")).First().Replace("Language: ", "");
                    var langID = Aesthetics.GetLanguageId(lang);
                   
                    if (setsplit[0].IndexOf("(") > -1 && setsplit[0].IndexOf(")") - 2 != setsplit[0].IndexOf("("))
                    {
                        var spec = setsplit[0][(setsplit[0].IndexOf("(") + 1)..(setsplit[0].IndexOf(")") - 1)];
                        var specid = SpeciesName.GetSpeciesID(spec, (int)langID);
                        var engspec = SpeciesName.GetSpeciesNameGeneration((ushort)specid, 2, 9);
                        setsplit[0].Replace(spec, "");
                        setsplit[0].Insert((setsplit[0].IndexOf("(") + 1), engspec);
                        finalset = string.Empty;
                        foreach (var item in setsplit)
                        {
                            finalset += item;
                        }
                        return new ShowdownSet(finalset);
                    }
                    else
                    {
                        if (setsplit[0].IndexOf("(") > -1)
                        {
                            var spec = setsplit[0][0..(setsplit[0].IndexOf("(") - 1)];
                            var specid = SpeciesName.GetSpeciesID(spec, (int)langID);
                            var engspec = SpeciesName.GetSpeciesNameGeneration((ushort)specid, 2, 9);
                            setsplit[0].Replace(spec, "");
                            setsplit[0].Insert(0, engspec);
                            finalset = string.Empty;
                            foreach (var item in setsplit)
                            {
                                finalset += item;
                            }
                            return new ShowdownSet(finalset);
                        }
                        else if (setsplit[0].Contains("@"))
                        {
                            var spec = setsplit[0][0..(setsplit[0].IndexOf("@") - 2)];
                            var specid = SpeciesName.GetSpeciesID(spec, (int)langID);
                            var engspec = SpeciesName.GetSpeciesNameGeneration((ushort)specid, 2, 9);
                            setsplit[0].Replace(spec, "");
                            setsplit[0].Insert(0, engspec);
                            finalset = string.Empty;
                            foreach (var item in setsplit)
                            {
                                finalset += item;
                            }
                            return new ShowdownSet(finalset);
                        }
                        else
                        {
                            var spec = setsplit[0].Trim();
                           
                            SpeciesName.SpeciesDict[(int)langID].TryGetValue(spec, out var specid);
                           
                            var engspec = SpeciesName.GetSpeciesName((ushort)specid, (int)LanguageID.English);
                           
                            setsplit[0] = engspec;
                            var result = new StringBuilder();
                            foreach (var item in setsplit)
                            {
                                result.Append(item + "\n");
                            }
                            finalset = result.ToString();
                          
                            return new ShowdownSet(finalset);
                        }
                    }
                }
            }
            return TheShow;
        }

        private static readonly string[] splittables =
        {
            "Ability:", "EVs:", "IVs:", "Shiny:", "Gigantamax:", "Ball:", "- ", "Level:",
            "Happiness:", "Language:", "OT:", "OTGender:", "TID:", "SID:", "Alpha:", "Tera Type:",
            "Adamant Nature", "Bashful Nature", "Brave Nature", "Bold Nature", "Calm Nature",
            "Careful Nature", "Docile Nature", "Gentle Nature", "Hardy Nature", "Hasty Nature",
            "Impish Nature", "Jolly Nature", "Lax Nature", "Lonely Nature", "Mild Nature",
            "Modest Nature", "Naive Nature", "Naughty Nature", "Quiet Nature", "Quirky Nature",
            "Rash Nature", "Relaxed Nature", "Sassy Nature", "Serious Nature", "Timid Nature","Tera Type:","."
        };
    }
}
