using NLog.Fluent;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Windows.Input;

namespace SysBot.Pokemon;

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
            if (restorenick.TrimStart().StartsWith('('))
                return null;
            setstring = setstring[(nickIndex + 1)..];
        }

        foreach (string i in splittables)
        {
            if (setstring.Contains(i))
                setstring = setstring.Replace(i, $"\r\n{i}");
        }

            var finalset = restorenick + setstring;
            
            var setsplit = finalset.Split("\r\n");
            var TheShow = new ShowdownSet(finalset);
            
            if (TheShow.Species == 0)
            {

                
                var langID = (LanguageID)0;

                if (setsplit[0].IndexOf("(") > -1 && setsplit[0].IndexOf(")") - 2 != setsplit[0].IndexOf("("))
                {
                    var spec = setsplit[0][(setsplit[0].IndexOf("(")+1)..setsplit[0].IndexOf(")")];
                    var nick = setsplit[0][..setsplit[0].IndexOf("(")];
                    ushort specid = 0;
                    
                    for (int i = 1; i < 11; i++)
                    {
                        SpeciesName.TryGetSpecies(spec, i,out specid);
                        if (specid > 0)
                        {
                            langID = (LanguageID)i;
                            break;
                        }
                    }
                    if (specid == 0)
                        return null;
                    var engspec = SpeciesName.GetSpeciesNameGeneration((ushort)specid, 2, 9);
                    setsplit[0] = nick + $"({engspec})";
                    
                    if (!finalset.Contains("Language:"))
                    {
                        var langs = Enum.GetNames(typeof(LanguageID));
                        if (setsplit.Length > 2)
                        {
                            var setlist = setsplit.ToList();
                            setlist.Insert(1, $"Language: {langs[(int)langID]}");
                            setsplit = setlist.ToArray();
                        }
                        else
                            setsplit = setsplit.Append($"Language: {langs[(int)langID]}").ToArray();

                    }
                    var result = new StringBuilder();
                    foreach (var item in setsplit)
                    {
                        result.Append(item + "\r\n");
                    }
                    finalset = result.ToString();
                    TheShow = new ShowdownSet(finalset); 
                }
                else
                {
                    if (setsplit[0].IndexOf("(") > -1)
                    {
                       var spec = setsplit[0][..(setsplit[0].IndexOf("(") - 1)];
                        ushort specid = 0;

                        for (int i = 1; i < 11; i++)
                        {
                            SpeciesName.TryGetSpecies(spec, i,out specid);
                            if (specid > 0)
                            {
                                langID = (LanguageID)i;
                                break;
                            }
                        }
                        if (specid == 0)
                            return null;
                        var engspec = SpeciesName.GetSpeciesNameGeneration((ushort)specid, 2, 9);
                        setsplit[0]=setsplit[0].Replace(spec, "");
                        setsplit[0]=setsplit[0].Insert(0, engspec);
                        if (!finalset.Contains("Language:"))
                        {
                            var langs = Enum.GetNames(typeof(LanguageID));
                            if (setsplit.Length > 2)
                            {
                                var setlist = setsplit.ToList();
                                setlist.Insert(1, $"Language: {langs[(int)langID]}");
                                setsplit = setlist.ToArray();
                            }
                            else
                                setsplit = setsplit.Append($"Language: {langs[(int)langID]}").ToArray();

                        }
                        var result = new StringBuilder();
                        foreach (var item in setsplit)
                        {
                            result.Append(item + "\r\n");
                        }
                        finalset = result.ToString();
                        TheShow = new ShowdownSet(finalset);
                    }
                    else if (setsplit[0].Contains("@"))
                    {
                        var spec = setsplit[0][0..(setsplit[0].IndexOf("@") - 1)];
                        ushort specid = 0;

                        for (int i = 1; i < 11; i++)
                        {
                            SpeciesName.TryGetSpecies(spec, i,out specid);
                            if (specid > 0)
                            {
                                langID = (LanguageID)i;
                                break;
                            }

                        }
                        if (specid == 0)
                            return null;
                        var engspec = SpeciesName.GetSpeciesNameGeneration((ushort)specid, 2, 9);
                        setsplit[0]=setsplit[0].Replace(spec, "");
                        setsplit[0]=setsplit[0].Insert(0, engspec);
                        if (!finalset.Contains("Language:"))
                        {
                            var langs = Enum.GetNames(typeof(LanguageID));
                            if (setsplit.Length > 2)
                            {
                                var setlist = setsplit.ToList();
                                setlist.Insert(1, $"Language: {langs[(int)langID]}");
                                setsplit = setlist.ToArray();
                            }
                            else
                                setsplit = setsplit.Append($"Language: {langs[(int)langID]}").ToArray();

                        }
                       
                        var result = new StringBuilder();
                        foreach (var item in setsplit)
                        {
                            result.Append(item + "\r\n");
                        }
                        finalset = result.ToString();
                        TheShow = new ShowdownSet(finalset);
                    }
                    else
                    {
                        var spec = setsplit[0].Trim();

                        ushort specid = 0;

                        for (int i = 1; i < 11; i++)
                        {
                             SpeciesName.TryGetSpecies(spec, i,out specid);
                            if (specid > 0)
                            {
                                langID = (LanguageID)i;
                                break;
                            }
                        }
                        if (specid == 0)
                            return null;
                        var engspec = SpeciesName.GetSpeciesName((ushort)specid, (int)LanguageID.English);
                           
                        setsplit[0] = engspec;
                        if (!finalset.Contains("Language:"))
                        {
                            var langs = Enum.GetNames(typeof(LanguageID));
                            if (setsplit.Length > 2)
                            {
                                var setlist = setsplit.ToList();
                                setlist.Insert(1, $"Language: {langs[(int)langID]}");
                                setsplit = setlist.ToArray();
                            }
                            else
                            {
                                setsplit = setsplit.Append($"Language: {langs[(int)langID]}").ToArray();
                            }
                            
                        }
                        var result = new StringBuilder();
                        foreach (var item in setsplit)
                        {
                            result.Append(item + "\r\n");
                        }
                        finalset = result.ToString();
                        TheShow = new ShowdownSet(finalset);
                    }
                }
                
               
            }

            foreach (var line in setsplit)
            {
                if (TheShow.Nickname != String.Empty)
                    continue;
                if (!char.IsUpper(line[0]))
                {
                    if (line[0] != '-')
                        return null;
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
            "Rash Nature", "Relaxed Nature", "Sassy Nature", "Serious Nature", "Timid Nature"
        };
    }

