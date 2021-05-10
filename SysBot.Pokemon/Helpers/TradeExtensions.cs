using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using Newtonsoft.Json;

namespace SysBot.Pokemon
{
    public class TradeExtensions
    {
        public static Random Random = new();
        public static bool TCInitialized;
        private static bool TCRWLockEnable;
        public static readonly List<ulong> CommandInProgress = new();
        private static readonly List<ulong> GiftInProgress = new();
        public static List<string> TradeCordPath = new();
        public static HashSet<string> TradeCordCooldown = new();
        private static readonly string InfoPath = "TradeCord\\UserInfo.json";
        public static TCUserInfoRoot UserInfo = GetRoot<TCUserInfoRoot>(InfoPath);
        public static int XCoordStart = 0;
        public static int YCoordStart = 0;

        public static int[] TradeEvo = { (int)Species.Machoke, (int)Species.Haunter, (int)Species.Boldore, (int)Species.Gurdurr, (int)Species.Phantump, (int)Species.Gourgeist };
        public static int[] ShinyLock = { (int)Species.Victini, (int)Species.Keldeo, (int)Species.Volcanion, (int)Species.Cosmog, (int)Species.Cosmoem, (int)Species.Magearna,
                                          (int)Species.Marshadow, (int)Species.Zacian, (int)Species.Zamazenta, (int)Species.Eternatus, (int)Species.Kubfu, (int)Species.Urshifu,
                                          (int)Species.Zarude, (int)Species.Glastrier, (int)Species.Spectrier, (int)Species.Calyrex };

        public static int[] PikaClones = { 25, 26, 172, 587, 702, 777, 877 };
        public static int[] CherishOnly = { 719, 721, 801, 802, 807, 893 };
        public static int[] Pokeball = { 151, 722, 723, 724, 725, 726, 727, 728, 729, 730, 772, 773, 789, 790, 810, 811, 812, 813, 814, 815, 816, 817, 818, 891, 892 };
        public static int[] GalarFossils = { 880, 881, 882, 883 };
        public static int[] SilvallyMemory = { 0, 904, 905, 906, 907, 908, 909, 910, 911, 912, 913, 914, 915, 916, 917, 918, 919, 920 };
        public static int[] GenesectDrives = { 0, 116, 117, 118, 119 };
        public static int[] Amped = { 3, 4, 2, 8, 9, 19, 22, 11, 13, 14, 0, 6, 24 };
        public static int[] LowKey = { 1, 5, 7, 10, 12, 15, 16, 17, 18, 20, 21, 23 };

        public static string[] PartnerPikachuHeadache = { "-Original", "-Partner", "-Hoenn", "-Sinnoh", "-Unova", "-Alola", "-Kalos", "-World" };
        public static string[] LGPEBalls = { "Poke", "Premier", "Great", "Ultra", "Master" };
        public static readonly string[] Characteristics =
        {
            "Takes plenty of siestas",
            "Likes to thrash about",
            "Capable of taking hits",
            "Alert to sounds",
            "Mischievous",
            "Somewhat vain",
        };

        public class TCRng
        {
            public int CatchRNG { get; set; }
            public int ShinyRNG { get; set; }
            public int EggRNG { get; set; }
            public int EggShinyRNG { get; set; }
            public int GmaxRNG { get; set; }
            public int CherishRNG { get; set; }
            public int SpeciesRNG { get; set; }
            public int SpeciesBoostRNG { get; set; }
            public PK8 CatchPKM { get; set; } = new();
            public PK8 EggPKM { get; set; } = new();
        }

        public class TCUserInfoRoot
        {
            public HashSet<TCUserInfo> Users { get; set; } = new();

            public class TCUserInfo
            {
                public ulong UserID { get; set; }
                public int CatchCount { get; set; }
                public Daycare1 Daycare1 { get; set; } = new();
                public Daycare2 Daycare2 { get; set; } = new();
                public string OTName { get; set; } = "";
                public string OTGender { get; set; } = "";
                public int TID { get; set; }
                public int SID { get; set; }
                public string Language { get; set; } = "";
                public HashSet<int> Dex { get; set; } = new();
                public int DexCompletionCount { get; set; }
                public List<DexPerks> ActivePerks { get; set; } = new();
                public int SpeciesBoost { get; set; }
                public HashSet<int> Favorites { get; set; } = new();
                public HashSet<Catch> Catches { get; set; } = new();
            }

            public class Catch
            {
                public bool Shiny { get; set; }
                public int ID { get; set; }
                public string Ball { get; set; } = "None";
                public string Species { get; set; } = "None";
                public string Form { get; set; } = "";
                public bool Egg { get; set; }
                public string Path { get; set; } = "";
                public bool Traded { get; set; }
            }

            public class Daycare1
            {
                public bool Shiny { get; set; }
                public int ID { get; set; }
                public int Species { get; set; }
                public string Form { get; set; } = "";
                public int Ball { get; set; }
            }

            public class Daycare2
            {
                public bool Shiny { get; set; }
                public int ID { get; set; }
                public int Species { get; set; }
                public string Form { get; set; } = "";
                public int Ball { get; set; }
            }
        }

        private static async Task SerializationMonitor(int interval)
        {
            var sw = new Stopwatch();
            sw.Start();
            while (true)
            {
                if (sw.ElapsedMilliseconds / 1000 >= interval && !TCRWLockEnable)
                {
                    if (File.Exists(InfoPath))
                        File.Copy(InfoPath, "TradeCord\\UserInfo_backup.json", true);

                    SerializeInfo(UserInfo, InfoPath, true);
                    sw.Restart();
                }
                else await Task.Delay(10_000).ConfigureAwait(false);
            }
        }

        public static bool ShinyLockCheck(int species, string ball, bool form)
        {
            if (ShinyLock.Contains(species))
                return true;
            else if (form && (species == (int)Species.Zapdos || species == (int)Species.Moltres || species == (int)Species.Articuno))
                return true;
            else if (ball.Contains("Beast") && (species == (int)Species.Poipole || species == (int)Species.Naganadel))
                return true;
            else return false;
        }

        public static PKM RngRoutine(PKM pkm, IBattleTemplate template, Shiny shiny)
        {
            pkm.Form = pkm.Species == (int)Species.Silvally || pkm.Species == (int)Species.Genesect ? Random.Next(pkm.PersonalInfo.FormCount) : pkm.Form;
            if (pkm.Species == (int)Species.Alcremie)
            {
                var data = pkm.Data;
                var deco = (uint)Random.Next(7);
                BitConverter.GetBytes(deco).CopyTo(data, 0xE4);
                pkm = PKMConverter.GetPKMfromBytes(data) ?? pkm;
            }
            else if (pkm.Form > 0 && pkm.Species == (int)Species.Silvally || pkm.Species == (int)Species.Genesect)
            {
                switch (pkm.Species)
                {
                    case 649: pkm.HeldItem = GenesectDrives[pkm.Form]; break;
                    case 773: pkm.HeldItem = SilvallyMemory[pkm.Form]; break;
                };
            }

            pkm.Nature = pkm.Species == (int)Species.Toxtricity && pkm.Form > 0 ? LowKey[Random.Next(LowKey.Length)] : pkm.Species == (int)Species.Toxtricity && pkm.Form == 0 ? Amped[Random.Next(Amped.Length)] : pkm.FatefulEncounter ? pkm.Nature : Random.Next(25);
            pkm.StatNature = pkm.Nature;
            pkm.Move1_PPUps = pkm.Move2_PPUps = pkm.Move3_PPUps = pkm.Move4_PPUps = 0;
            pkm.SetMaximumPPCurrent(pkm.Moves);
            pkm.ClearHyperTraining();

            var la = new LegalityAnalysis(pkm);
            var enc = la.Info.EncounterMatch;
            var evoChain = la.Info.EvoChainsAllGens[pkm.Format].FirstOrDefault(x => x.Species == pkm.Species);
            pkm.CurrentLevel = enc.LevelMin < evoChain.MinLevel ? evoChain.MinLevel : enc.LevelMin;
            while (!new LegalityAnalysis(pkm).Valid && pkm.CurrentLevel <= 100)
                pkm.CurrentLevel += 1;

            pkm.SetSuggestedMoves();
            pkm.SetRelearnMoves(pkm.GetSuggestedRelearnMoves(enc));
            pkm.HealPP();

            var legends = (int[])Enum.GetValues(typeof(Legends));
            if (!GalarFossils.Contains(pkm.Species) && !legends.Contains(pkm.Species) && !pkm.FatefulEncounter && la.Valid)
            {
                do
                {
                    pkm.SetAbilityIndex(Random.Next(3));
                } while (!new LegalityAnalysis(pkm).Valid);
            }

            bool goMew = pkm.Species == (int)Species.Mew && enc.Version == GameVersion.GO && pkm.IsShiny;
            bool goOther = (pkm.Species == (int)Species.Victini || pkm.Species == (int)Species.Jirachi || pkm.Species == (int)Species.Celebi || pkm.Species == (int)Species.Genesect) && enc.Version == GameVersion.GO;
            pkm.IVs = goOther || goMew || pkm.FatefulEncounter ? pkm.IVs : enc.Version == GameVersion.GO ? pkm.SetRandomIVsGO() : enc is EncounterStatic8N ? pkm.SetRandomIVs(5) : enc is EncounterStatic8 ? pkm.SetRandomIVs() : pkm.SetRandomIVs(4);

            if (enc is EncounterStatic8)
            {
                var criteria = EncounterCriteria.GetCriteria(template);
                int[][] hardcodedIVs = new int[][] { new int[] { 31, 0, 31, 31, 31, 31 }, new int[] { 31, 31, 31, 0, 31, 31 }, new int[] { 31, 31, 31, 31, 31, 31 } };
                int i = 0;
                var laIV = new LegalityAnalysis(pkm);

                while (i < 10)
                {
                    if (laIV.Valid)
                        break;
                    else criteria.SetRandomIVs(pkm);

                    Overworld8RNG.ApplyDetails(pkm, criteria, shiny, pkm.FlawlessIVCount);
                    laIV = new(pkm);
                    i++;
                }

                if (i == 10 && !laIV.Valid)
                {
                    pkm.IVs = hardcodedIVs[Random.Next(hardcodedIVs.Length)];
                    SimpleEdits.TryApplyHardcodedSeedWild8((PK8)pkm, enc, pkm.IVs, shiny);
                }
            }

            var ballRng = Random.Next(3);
            if (ballRng == 2)
                BallApplicator.ApplyBallLegalByColor(pkm);
            else BallApplicator.ApplyBallLegalRandom(pkm);

            if (pkm.Ball == 16)
                BallApplicator.ApplyBallLegalRandom(pkm);

            pkm = TrashBytes(pkm);
            return pkm;
        }

        public static PKM EggRngRoutine(TCUserInfoRoot.TCUserInfo info, string trainerInfo, int evo1, int evo2, bool star, bool square)
        {
            var pkm1 = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(info.Catches.FirstOrDefault(x => x.ID == info.Daycare1.ID).Path));
            var pkm2 = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(info.Catches.FirstOrDefault(x => x.ID == info.Daycare2.ID).Path));
            if (pkm1 == null || pkm2 == null)
                return new PK8();

            var ballRng = $"\nBall: {(Ball)Random.Next(2, 27)}";
            var ballRngDC = Random.Next(1, 3);
            var enumVals = (int[])Enum.GetValues(typeof(ValidEgg));
            bool specificEgg = (evo1 == evo2 && Enum.IsDefined(typeof(ValidEgg), evo1)) || ((evo1 == 132 || evo2 == 132) && (Enum.IsDefined(typeof(ValidEgg), evo1) || Enum.IsDefined(typeof(ValidEgg), evo2))) || ((evo1 == 29 || evo1 == 32) && (evo2 == 29 || evo2 == 32));
            var dittoLoc = DittoSlot(evo1, evo2);
            var speciesRng = specificEgg ? SpeciesName.GetSpeciesNameGeneration(dittoLoc == 1 ? evo2 : evo1, 2, 8) : SpeciesName.GetSpeciesNameGeneration(enumVals[Random.Next(enumVals.Length)], 2, 8);
            var speciesRngID = SpeciesName.GetSpeciesID(speciesRng);
            FormOutput(speciesRngID, 0, out string[] forms);

            if (speciesRng.Contains("Nidoran"))
                speciesRng = speciesRng.Remove(speciesRng.Length - 1);

            string formHelper = speciesRng switch
            {
                "Indeedee" => _ = specificEgg && dittoLoc == 1 ? FormOutput(876, pkm2.Form, out _) : specificEgg && dittoLoc == 2 ? FormOutput(876, pkm1.Form, out _) : FormOutput(876, Random.Next(2), out _),
                "Nidoran" => _ = specificEgg && dittoLoc == 1 ? (evo2 == 32 ? "-M" : "-F") : specificEgg && dittoLoc == 2 ? (evo1 == 32 ? "-M" : "-F") : (Random.Next(2) == 0 ? "-M" : "-F"),
                "Meowth" => _ = FormOutput(speciesRngID, specificEgg && (pkm1.Species == 863 || pkm2.Species == 863) ? 2 : specificEgg && dittoLoc == 1 ? pkm2.Form : specificEgg && dittoLoc == 2 ? pkm1.Form : Random.Next(forms.Length), out _),
                "Yamask" => FormOutput(speciesRngID, specificEgg && (pkm1.Species == 867 || pkm2.Species == 867) ? 1 : specificEgg && dittoLoc == 1 ? pkm2.Form : specificEgg && dittoLoc == 2 ? pkm1.Form : Random.Next(forms.Length), out _),
                "Zigzagoon" => _ = FormOutput(speciesRngID, specificEgg && (pkm1.Species == 862 || pkm2.Species == 862) ? 1 : specificEgg && dittoLoc == 1 ? pkm2.Form : specificEgg && dittoLoc == 2 ? pkm1.Form : Random.Next(forms.Length), out _),
                "Farfetch’d" => _ = FormOutput(speciesRngID, specificEgg && (pkm1.Species == 865 || pkm2.Species == 865) ? 1 : specificEgg && dittoLoc == 1 ? pkm2.Form : specificEgg && dittoLoc == 2 ? pkm1.Form : Random.Next(forms.Length), out _),
                "Corsola" => _ = FormOutput(speciesRngID, specificEgg && (pkm1.Species == 864 || pkm2.Species == 864) ? 1 : specificEgg && dittoLoc == 1 ? pkm2.Form : specificEgg && dittoLoc == 2 ? pkm1.Form : Random.Next(forms.Length), out _),
                "Sinistea" or "Milcery" => "",
                _ => FormOutput(speciesRngID, specificEgg && pkm1.Form == pkm2.Form ? pkm1.Form : specificEgg && dittoLoc == 1 ? pkm2.Form : specificEgg && dittoLoc == 2 ? pkm1.Form : Random.Next(forms.Length), out _),
            };

            var set = new ShowdownSet($"Egg({speciesRng}{formHelper}){(ballRng.Contains("Cherish") || Pokeball.Contains(SpeciesName.GetSpeciesID(speciesRng)) ? "\nBall: Poke" : ballRng)}\n{trainerInfo}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo(8);
            var pkm = sav.GetLegal(template, out _);

            if (!specificEgg && pkm.PersonalInfo.HasForms && pkm.Species != (int)Species.Sinistea && pkm.Species != (int)Species.Indeedee)
                pkm.Form = Random.Next(pkm.PersonalInfo.FormCount);
            if (pkm.Species == (int)Species.Rotom)
                pkm.Form = 0;
            if (FormInfo.IsBattleOnlyForm(pkm.Species, pkm.Form, pkm.Format))
                pkm.Form = FormInfo.GetOutOfBattleForm(pkm.Species, pkm.Form, pkm.Format);

            if (ballRngDC == 1)
                pkm.Ball = info.Daycare1.Ball;
            else if (ballRngDC == 2)
                pkm.Ball = info.Daycare2.Ball;

            EggTrade((PK8)pkm);
            pkm.SetAbilityIndex(Random.Next(3));
            pkm.Nature = Random.Next(25);
            pkm.StatNature = pkm.Nature;
            pkm.IVs = pkm.SetRandomIVs(4);

            if (!pkm.ValidBall())
                BallApplicator.ApplyBallLegalRandom(pkm);

            if (square)
                CommonEdits.SetShiny(pkm, Shiny.AlwaysSquare);
            else if (star)
                CommonEdits.SetShiny(pkm, Shiny.AlwaysStar);

            return pkm;
        }

        public static void DittoTrade(PKM pkm)
        {
            var dittoStats = new string[] { "atk", "spe", "spa" };
            var nickname = pkm.Nickname.ToLower();
            pkm.StatNature = pkm.Nature;
            pkm.Met_Location = 162;
            pkm.Ball = 21;
            pkm.IVs = new int[] { 31, nickname.Contains(dittoStats[0]) ? 0 : 31, 31, nickname.Contains(dittoStats[1]) ? 0 : 31, nickname.Contains(dittoStats[2]) ? 0 : 31, 31 };
            pkm.ClearHyperTraining();
            _ = TrashBytes(pkm, new LegalityAnalysis(pkm));
        }

        public static void EggTrade(PK8 pk)
        {
            pk = (PK8)TrashBytes(pk);
            pk.IsNicknamed = true;
            pk.Nickname = pk.Language switch
            {
                1 => "タマゴ",
                3 => "Œuf",
                4 => "Uovo",
                5 => "Ei",
                7 => "Huevo",
                8 => "알",
                9 or 10 => "蛋",
                _ => "Egg",
            };

            pk.IsEgg = true;
            pk.Egg_Location = 60002;
            pk.MetDate = DateTime.Parse("2020/10/20");
            pk.EggMetDate = pk.MetDate;
            pk.HeldItem = 0;
            pk.CurrentLevel = 1;
            pk.EXP = 0;
            pk.DynamaxLevel = 0;
            pk.Met_Level = 1;
            pk.Met_Location = 30002;
            pk.CurrentHandler = 0;
            pk.OT_Friendship = 1;
            pk.HT_Name = "";
            pk.HT_Friendship = 0;
            pk.HT_Language = 0;
            pk.HT_Gender = 0;
            pk.HT_Memory = 0;
            pk.HT_Feeling = 0;
            pk.HT_Intensity = 0;
            pk.StatNature = pk.Nature;
            pk.EVs = new int[] { 0, 0, 0, 0, 0, 0 };
            pk.Markings = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            pk.ClearRecordFlags();
            pk.ClearRelearnMoves();
            pk.Moves = new int[] { 0, 0, 0, 0 };
            var la = new LegalityAnalysis(pk);
            pk.SetRelearnMoves(MoveSetApplicator.GetSuggestedRelearnMoves(la));
            pk.Moves = pk.RelearnMoves;
            pk.Move1_PPUps = pk.Move2_PPUps = pk.Move3_PPUps = pk.Move4_PPUps = 0;
            pk.SetMaximumPPCurrent(pk.Moves);
            pk.SetSuggestedHyperTrainingData();
            pk.SetSuggestedRibbons(la.EncounterMatch);
        }

        private static List<string> SpliceAtWord(string entry, int start, int length)
        {
            int counter = 0;
            var temp = entry.Contains(",") ? entry.Split(',').Skip(start) : entry.Split('\n').Skip(start);
            List<string> list = new();

            if (entry.Length < length)
            {
                list.Add(entry ?? "");
                return list;
            }

            foreach (var line in temp)
            {
                counter += line.Length + 2;
                if (counter < length)
                    list.Add(line.Trim());
                else break;
            }
            return list;
        }

        public static List<string> ListUtilPrep(string entry)
        {
            var index = 0;
            List<string> pageContent = new();
            var emptyList = "No results found.";
            var round = Math.Round((decimal)entry.Length / 1024, MidpointRounding.AwayFromZero);
            if (entry.Length > 1024)
            {
                for (int i = 0; i <= round; i++)
                {
                    var splice = SpliceAtWord(entry, index, 1024);
                    index += splice.Count;
                    if (splice.Count == 0)
                        break;

                    pageContent.Add(string.Join(entry.Contains(",") ? ", " : "\n", splice));
                }
            }
            else pageContent.Add(entry == "" ? emptyList : entry);
            return pageContent;
        }

        public static string FormOutput(int species, int form, out string[] formString)
        {
            var strings = GameInfo.GetStrings(LanguageID.English.GetLanguage2CharName());
            formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, 8);
            _ = formString.Length == form && form != 0 ? form -= 1 : form;

            if (form == 0)
                return "";
            else return "-" + formString[form];
        }

        private static int DittoSlot(int species1, int species2)
        {
            if (species1 == 132 && species2 != 132)
                return 1;
            else if (species2 == 132 && species1 != 132)
                return 2;
            else return 0;
        }

        public static void EncounterLogs(PK8 pk)
        {
            if (!File.Exists("EncounterLog.txt"))
            {
                var blank = "Total: 0 Pokémon, 0 Eggs, 0 Shiny\n--------------------------------------\n";
                File.Create("EncounterLog.txt").Close();
                File.WriteAllText("EncounterLog.txt", blank);
            }

            var content = File.ReadAllText("EncounterLog.txt").Split('\n').ToList();
            var form = FormOutput(pk.Species, pk.Form, out _);
            var speciesName = SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, 8) + (pk.Species == (int)Species.Sinistea ? "" : form);
            var index = content.FindIndex(2, x => x.Contains(speciesName));
            var split = index != -1 ? content[index].Split('_') : new string[] { };
            var splitTotal = content[0].Split(',');

            if (index == -1 && !speciesName.Contains("Sinistea"))
                content.Add($"{speciesName}_1_★{(pk.IsShiny ? 1 : 0)}");
            else if (index == -1 && speciesName.Contains("Sinistea"))
                content.Add($"{speciesName}_1_{(pk.Form > 0 ? 1 : 0)}_★{(pk.IsShiny ? 1 : 0)}");
            else if (index != -1 && !speciesName.Contains("Sinistea"))
                content[index] = $"{split[0]}_{int.Parse(split[1]) + 1}_{(pk.IsShiny ? "★" + (int.Parse(split[2].Replace("★", "")) + 1).ToString() : split[2])}";
            else if (index != -1 && speciesName.Contains("Sinistea"))
                content[index] = $"{split[0]}_{int.Parse(split[1]) + 1}_{(pk.Form > 0 ? (int.Parse(split[2]) + 1).ToString() : split[2])}_{(pk.IsShiny ? "★" + (int.Parse(split[3].Replace("★", "")) + 1).ToString() : split[3])}";

            content[0] = "Total: " + $"{int.Parse(splitTotal[0].Split(':')[1].Replace(" Pokémon", "")) + 1} Pokémon, " +
                (pk.IsEgg ? $"{int.Parse(splitTotal[1].Replace(" Eggs", "")) + 1} Eggs, " : splitTotal[1].Trim() + ", ") +
                (pk.IsShiny ? $"{int.Parse(splitTotal[2].Replace(" Shiny", "")) + 1} Shiny, " : splitTotal[2].Trim());
            File.WriteAllText("EncounterLog.txt", string.Join("\n", content));
        }

        public static PKM TrashBytes(PKM pkm, LegalityAnalysis? la = null)
        {
            pkm.Nickname = "KOIKOIKOIKOI";
            pkm.IsNicknamed = true;
            if (pkm.Version != (int)GameVersion.GO && !pkm.FatefulEncounter)
                pkm.MetDate = DateTime.Parse("2020/10/20");
            pkm.SetDefaultNickname(la ?? new LegalityAnalysis(pkm));
            return pkm;
        }

        public static string DexFlavor(int species)
        {
            var resourcePath = "SysBot.Pokemon.Helpers.DexFlavor.txt";
            using StreamReader reader = new(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath));
            return reader.ReadToEnd().Split('\n')[species];
        }

        public static PK8 CherishHandler(MysteryGift mg, ITrainerInfo info)
        {
            var mgPkm = mg.ConvertToPKM(info);
            mgPkm = PKMConverter.IsConvertibleToFormat(mgPkm, 8) ? PKMConverter.ConvertToType(mgPkm, typeof(PK8), out _) : mgPkm;
            if (mgPkm != null)
                mgPkm.SetHandlerandMemory(info);
            else return new();

            mgPkm.CurrentLevel = mg.LevelMin;
            if (mg.HeldItem != 0 && ItemRestrictions.IsHeldItemAllowed(mg.HeldItem, 8))
                mgPkm.HeldItem = mg.HeldItem;

            var la = new LegalityAnalysis(mgPkm);
            if (!la.Valid)
            {
                mgPkm.SetRandomIVs(6);
                var showdown = ShowdownParsing.GetShowdownText(mgPkm);
                return (PK8)AutoLegalityWrapper.GetLegal(info, new ShowdownSet(showdown), out _);
            }
            else return (PK8)mgPkm;
        }

        public static T GetRoot<T>(string file, TextReader? textReader = null) where T : new()
        {
            if (textReader == null)
            {
                if (!File.Exists(file))
                {
                    Directory.CreateDirectory(file.Split('\\')[0]);
                    File.Create(file).Close();
                }

                T? root;
                using StreamReader sr = File.OpenText(file);
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new();
                    root = (T?)serializer.Deserialize(reader, typeof(T));
                }
                return root ?? new();
            }
            else
            {
                JsonSerializer serializer = new();
                T? root = (T?)serializer.Deserialize(textReader, typeof(T));
                return root ?? new();
            }
        }

        public static async Task<TCUserInfoRoot.TCUserInfo> GetUserInfo(ulong id, int interval = 0, bool gift = false)
        {
            if (!TCInitialized)
            {
                TCInitialized = true;
                _ = Task.Run(() => SerializationMonitor(interval));
            }

            while (TCRWLockEnable || GiftInProgress.Contains(id) && !gift || CommandInProgress.FindAll(x => x == id).Count > 1 && !gift)
                await Task.Delay(0_100).ConfigureAwait(false);

            if (!gift)
                CommandInProgress.Add(id);
            else if (gift)
                GiftInProgress.Add(id);

            var user = UserInfo.Users.FirstOrDefault(x => x.UserID == id);
            if (user == null)
                user = new TCUserInfoRoot.TCUserInfo { UserID = id };
            return user;
        }

        public static void UpdateUserInfo(TCUserInfoRoot.TCUserInfo info, bool remove = true, bool gift = false)
        {
            UserInfo.Users.RemoveWhere(x => x.UserID == info.UserID);
            UserInfo.Users.Add(info);
            if (remove)
                CommandInProgress.Remove(info.UserID);
            if (gift)
                GiftInProgress.Remove(info.UserID);
        }

        public static void SerializeInfo(object? root, string filePath, bool tc = false)
        {
            while (TCRWLockEnable && tc)
                Thread.Sleep(0_100);

            if (tc)
                TCRWLockEnable = true;

            while (true)
            {
                try
                {
                    JsonSerializer serializer = new();
                    using StreamWriter writer = File.CreateText(filePath);
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(writer, root);
                }
                catch
                {
                    Thread.Sleep(0_100);
                }

                if (TestJsonIntegrity())
                    break;
            }

            if (tc)
                TCRWLockEnable = false;
        }

        private static bool TestJsonIntegrity()
        {
            try
            {
                using StreamReader sr = File.OpenText(InfoPath);
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new();
                    TCUserInfoRoot? rootTest = (TCUserInfoRoot?)serializer.Deserialize(reader, typeof(TCUserInfoRoot));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void TradeStatusUpdate(string id, bool cancelled = false)
        {
            var origPath = TradeCordPath.FirstOrDefault(x => x.Contains(id));
            if (!cancelled && origPath != default)
            {
                var tradedPath = Path.Combine($"TradeCord\\Backup\\{id}", origPath.Split('\\')[2]);
                try
                {
                    File.Move(origPath, tradedPath);
                }
                catch (IOException)
                {
                    File.Move(origPath, tradedPath.Insert(tradedPath.IndexOf(".") - 1, "ex"));
                }
            }

            if (TradeCordPath.FirstOrDefault(x => x.Contains(id)) != default)
            {
                var entries = TradeCordPath.FindAll(x => x.Contains(id));
                for (int i = 0; i < entries.Count; i++)
                    TradeCordPath.Remove(entries[i]);
            }
        }

        public static string PokeImg(PKM pkm, bool canGmax, bool fullSize)
        {
            var alcremieDeco = (uint)(pkm.Species == (int)Species.Alcremie ? pkm.Data[0xE4] : 0);
            bool md = false;
            bool fd = false;
            string[] baseLink;
            if (fullSize)
                baseLink = "https://projectpokemon.org/images/sprites-models/homeimg/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');
            else baseLink = "https://raw.githubusercontent.com/BakaKaito/HomeImages/main/homeimg/128x128/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');

            if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form == 0)
            {
                if (pkm.Gender == 0)
                    md = true;
                else fd = true;
            }

            baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : $"0{pkm.Species}";
            baseLink[3] = pkm.Form < 10 ? $"00{pkm.Form}" : $"0{pkm.Form}";
            baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
            baseLink[5] = canGmax ? "g" : "n";
            baseLink[6] = "0000000" + (pkm.Species == (int)Species.Alcremie ? alcremieDeco : 0);
            baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
            return string.Join("_", baseLink);
        }

        public static TCRng RandomInit()
        {
            var enumVals = (int[])Enum.GetValues(typeof(Gen8Dex));
            return new TCRng()
            {
                CatchRNG = Random.Next(101),
                ShinyRNG = Random.Next(101),
                EggRNG = Random.Next(101),
                EggShinyRNG = Random.Next(101),
                GmaxRNG = Random.Next(101),
                CherishRNG = Random.Next(101),
                SpeciesRNG = enumVals[Random.Next(enumVals.Length)],
                SpeciesBoostRNG = Random.Next(101),
            };
        }
    }
}