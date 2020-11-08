using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysBot.Pokemon
{
    public class BotCompleteCounts
    {
        private readonly CountSettings Config;

        private int CompletedTrades;
        private int CompletedEggs;
        private int CompletedFossils;
        private int CompletedEncounters;
        private int CompletedLegends;
        private int CompletedSeedChecks;
        private int CompletedSurprise;
        private int CompletedDistribution;
        private int CompletedClones;
        private int CompletedFixOTs;
        private int CompletedPowerUps;
        private int CompletedEggRolls;
        private int CompletedDumps;
        private int CompletedRaids;

        public BotCompleteCounts(CountSettings config)
        {
            Config = config;
            LoadCountsFromConfig();
        }

        public void LoadCountsFromConfig()
        {
            CompletedTrades = Config.CompletedTrades;
            CompletedEggs = Config.CompletedEggs;
            CompletedFossils = Config.CompletedFossils;
            CompletedEncounters = Config.CompletedEncounters;
            CompletedLegends = Config.CompletedLegends;
            CompletedSeedChecks = Config.CompletedSeedChecks;
            CompletedSurprise = Config.CompletedSurprise;
            CompletedDistribution = Config.CompletedDistribution;
            CompletedClones = Config.CompletedClones;
            CompletedFixOTs = Config.CompletedFixOTs;
            CompletedPowerUps = Config.CompletedPowerUps;
            CompletedEggRolls = Config.CompletedEggRolls;
            CompletedDumps = Config.CompletedDumps;
            CompletedRaids = Config.CompletedRaids;
        }

        public void AddCompletedTrade()
        {
            Interlocked.Increment(ref CompletedTrades);
            Config.CompletedTrades = CompletedTrades;
        }

        public void AddCompletedEggs()
        {
            Interlocked.Increment(ref CompletedEggs);
            Config.CompletedEggs = CompletedEggs;
        }

        public void AddCompletedFossils()
        {
            Interlocked.Increment(ref CompletedFossils);
            Config.CompletedFossils = CompletedFossils;
        }

        public void AddCompletedEncounters()
        {
            Interlocked.Increment(ref CompletedEncounters);
            Config.CompletedEncounters = CompletedEncounters;
        }

        public void AddCompletedLegends()
        {
            Interlocked.Increment(ref CompletedLegends);
            Config.CompletedLegends = CompletedLegends;
        }

        public void AddCompletedSeedCheck()
        {
            Interlocked.Increment(ref CompletedSeedChecks);
            Config.CompletedSeedChecks = CompletedSeedChecks;
        }

        public void AddCompletedSurprise()
        {
            Interlocked.Increment(ref CompletedSurprise);
            Config.CompletedSurprise = CompletedSurprise;
        }

        public void AddCompletedDistribution()
        {
            Interlocked.Increment(ref CompletedDistribution);
            Config.CompletedDistribution = CompletedDistribution;
        }

        public void AddCompletedClones()
        {
            Interlocked.Increment(ref CompletedClones);
            Config.CompletedClones = CompletedClones;
        }

        public void AddCompletedFixOTs()
        {
            Interlocked.Increment(ref CompletedFixOTs);
            Config.CompletedFixOTs = CompletedFixOTs;
        }

        public void AddCompletedPowerUps()
        {
            Interlocked.Increment(ref CompletedPowerUps);
            Config.CompletedPowerUps = CompletedPowerUps;
        }

        public void AddCompletedEggRolls()
        {
            Interlocked.Increment(ref CompletedEggRolls);
            Config.CompletedEggRolls = CompletedEggRolls;
        }

        public void AddCompletedRaids()
        {
            Interlocked.Increment(ref CompletedRaids);
            Config.CompletedRaids = CompletedRaids;
        }

        public void AddCompletedDumps()
        {
            Interlocked.Increment(ref CompletedDumps);
            Config.CompletedDumps = CompletedDumps;
        }

        public IEnumerable<string> Summary()
        {
            if (CompletedSeedChecks != 0)
                yield return $"Seed Check Trades: {CompletedSeedChecks}";
            if (CompletedClones != 0)
                yield return $"Clone Trades: {CompletedClones}";
            if (CompletedFixOTs != 0)
                yield return $"FixOT Trades: {CompletedFixOTs}";
            if (CompletedPowerUps != 0)
                yield return $"PowerUp Trades: {CompletedPowerUps}";
            if (CompletedEggRolls != 0)
                yield return $"EggRoll Trades: {CompletedEggRolls}";
            if (CompletedDumps != 0)
                yield return $"Dump Trades: {CompletedDumps}";
            if (CompletedTrades != 0)
                yield return $"Link Trades: {CompletedTrades}";
            if (CompletedDistribution != 0)
                yield return $"Distribution Trades: {CompletedDistribution}";
            if (CompletedSurprise != 0)
                yield return $"Surprise Trades: {CompletedSurprise}";
            if (CompletedEggs != 0)
                yield return $"Eggs Received: {CompletedEggs}";
            if (CompletedRaids != 0)
                yield return $"Completed Raids: {CompletedRaids}";
            if (CompletedFossils != 0)
                yield return $"Completed Fossils: {CompletedFossils}";
            if (CompletedEncounters != 0)
                yield return $"Wild Encounters: {CompletedEncounters}";
            if (CompletedLegends != 0)
                yield return $"Legendary Encounters: {CompletedLegends}";
        }

        public void AddEncounteredSpecies(PKHeX.Core.PK8 pkm)
        {
            var file = "EncounteredSpecies.txt";
            if (pkm.IsEgg)
            {
                file = "EggLog.txt";
                if (!System.IO.File.Exists(file))
                    System.IO.File.AppendAllText(file, $"Total = 0 Eggs, 0 Shiny\n--------------------------------{System.Environment.NewLine}");
            }
            else if (!System.IO.File.Exists(file))
                System.IO.File.AppendAllText(file, $"Total = 0 Pokémon, 0 Shiny\n--------------------------------{System.Environment.NewLine}");

            var name = PKHeX.Core.SpeciesName.GetSpeciesName(pkm.Species, pkm.Language);
            var newname = PKHeX.Core.SpeciesName.GetSpeciesName(pkm.Species, pkm.Language);
            var duplicate = name.Contains(newname) && System.IO.File.ReadAllText(file).Contains(newname);
            string sinistea = "-Antique";
            string shiny = "-Shiny";
            var fullPattern = @"(^" + $"{newname}" + @"\W*)+(\d*)\.+(\d*)\D*\.+(\d*)\D*$";
            var total = @"(^Total\W*)+(\d*\d)+(\s\D*)+(\d*\d)+(\s\w*)";
            var countSpecies = @"\=\s(\d*)\W*\d*\.\.\.\d*";
            var countShiny = @"\.(\d*)\*";

            if (name == "Sinistea" && pkm.AltForm != 0)
                name = $"{name + sinistea}";
            if (pkm.IsShiny)
                name = $"{name + shiny}";

            if (!duplicate)
            {
                if (!name.Contains(shiny) && name.Contains("Sinistea"))
                {
                    if (name == "Sinistea")
                        System.IO.File.AppendAllText(file, $"{newname} = {1}...{0}...{0}*\n");
                    if (name.Contains(sinistea))
                        System.IO.File.AppendAllText(file, "ANTIQUE JACKPOT!\n");
                }
                else if (name.Contains(shiny) && name.Contains("Sinistea"))
                {
                    if (name == "Sinistea")
                        System.IO.File.AppendAllText(file, $"{newname} = {1}...{0}...{1}*\n");
                    if (name.Contains(sinistea))
                        System.IO.File.AppendAllText(file, $"{newname} = {1}...{1}...{1}*\n");
                }
                else if (!name.Contains(shiny) && !name.Contains("Sinistea"))
                    System.IO.File.AppendAllText(file, $"{newname} = {1}...{0}*\n");
                else if (name.Contains(shiny) && !name.Contains("Sinistea"))
                    System.IO.File.AppendAllText(file, $"{newname} = {1}...{1}*\n");
            }

            System.IO.StreamReader reader = new System.IO.StreamReader(file);
            var content = reader.ReadToEnd();
            reader.Close();

            if (duplicate)
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, fullPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
                var speciesname = match.Groups[1].Value;
                var speciescount = int.Parse(match.Groups[2].Value);
                int.TryParse(match.Groups[3].Value, out int antiquecount);
                var shinycount = int.Parse(match.Groups[4].Value);

                if (!name.Contains(shiny))
                {
                    if (name.Contains(newname) && name == "Sinistea")
                        content = System.Text.RegularExpressions.Regex.Replace(content, fullPattern, $"{speciesname}{speciescount + 1}...{antiquecount}...{shinycount}*", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();
                    if (name.Contains(newname) && name.Contains(sinistea))
                        content = System.Text.RegularExpressions.Regex.Replace(content, fullPattern, $"{speciesname}{speciescount + 1}...{antiquecount + 1}...{shinycount}*", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();
                    if (name.Contains(newname) && !name.Contains("Sinistea"))
                        content = System.Text.RegularExpressions.Regex.Replace(content, fullPattern, $"{speciesname}{speciescount + 1}...{shinycount}*", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();
                }
                else if (name.Contains(shiny))
                {
                    if (name.Contains(newname) && name.Contains("Sinistea"))
                        content = System.Text.RegularExpressions.Regex.Replace(content, fullPattern, $"{speciesname}{speciescount + 1}...{antiquecount}...{shinycount + 1}*", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();
                    if (name.Contains(newname) && name.Contains(sinistea))
                        System.IO.File.AppendAllText(file, "ANTIQUE JACKPOT!\n");
                    if (name.Contains(newname) && !name.Contains("Sinistea"))
                        content = System.Text.RegularExpressions.Regex.Replace(content, fullPattern, $"{speciesname}{speciescount + 1}...{shinycount + 1}*", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();
                }
            }

            var totalSpecies = System.Text.RegularExpressions.Regex.Matches(content, countSpecies, System.Text.RegularExpressions.RegexOptions.Multiline).OfType<System.Text.RegularExpressions.Match>().Select(countSpecies => int.Parse(countSpecies.Groups[1].Value)).Sum();
            var totalShiny = System.Text.RegularExpressions.Regex.Matches(content, countShiny, System.Text.RegularExpressions.RegexOptions.Multiline).OfType<System.Text.RegularExpressions.Match>().Select(countShiny => int.Parse(countShiny.Groups[1].Value)).Sum();

            if (pkm.IsEgg)
                content = System.Text.RegularExpressions.Regex.Replace(content, total, $"Total = {totalSpecies} Eggs, {totalShiny} Shiny", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();
            else content = System.Text.RegularExpressions.Regex.Replace(content, total, $"Total = {totalSpecies} Pokémon, {totalShiny} Shiny", System.Text.RegularExpressions.RegexOptions.Multiline).TrimEnd();

            System.IO.StreamWriter writer = new System.IO.StreamWriter(file);
            writer.WriteLine(content);
            writer.Close();
        }
    }
}