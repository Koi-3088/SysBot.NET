using Discord;
using Discord.Commands;
using Discord.Rest;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [Summary("Generates and queues various silly trade additions")]
    public class TradeAdditionsModule : ModuleBase<SocketCommandContext>
    {
        private static TradeQueueInfo<PK8> Info => SysCordInstance.Self.Hub.Queues.Info;

        [Command("giveawayqueue")]
        [Alias("gaq")]
        [Summary("Prints the users in the giveway queues.")]
        [RequireSudo]
        public async Task GetGiveawayListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending Giveaways";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("giveawaypool")]
        [Alias("gap")]
        [Summary("Show a list of Pokémon available for giveaway.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task DisplayGiveawayPoolCountAsync()
        {
            var pool = Info.Hub.Ledy.Pool;
            if (pool.Count > 0)
            {
                var lines = pool.Files.Select((z, i) => $"{i + 1}: {z.Key} = {(Species)z.Value.RequestInfo.Species}");
                var msg = string.Join("\n", lines);
                await ListUtil("Giveaway Pool Details", msg).ConfigureAwait(false);
            }
            else await ReplyAsync($"Giveaway pool is empty.").ConfigureAwait(false);
        }

        [Command("giveaway")]
        [Alias("ga", "giveme", "gimme")]
        [Summary("Makes the bot trade you the specified giveaway Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task GiveawayAsync([Remainder] string content)
        {
            var code = Info.GetRandomTradeCode();
            await GiveawayAsync(code, content).ConfigureAwait(false);
        }
        
        [Command("giveaway")]
        [Alias("ga", "giveme", "gimme")]
        [Summary("Makes the bot trade you the specified giveaway Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task GiveawayAsync([Summary("Giveaway Code")] int code, [Remainder] string content)
        {
            var pk = new PK8();
            content = ReusableActions.StripCodeBlock(content);
            pk.Nickname = content;
            var pool = Info.Hub.Ledy.Pool;

            if (pool.Count == 0)
            {
                await ReplyAsync($"Giveaway pool is empty.").ConfigureAwait(false);
                return;
            }
            else if (pk.Nickname.ToLower() == "random") // Request a random giveaway prize.
                pk = Info.Hub.Ledy.Pool.GetRandomSurprise();
            else
            {
                var trade = Info.Hub.Ledy.GetLedyTrade(pk);
                if (trade != null)
                    pk = trade.Receive;
                else
                {
                    await ReplyAsync($"Requested Pokémon not available, use \"{Info.Hub.Config.Discord.CommandPrefix}giveawaypool\" for a full list of available giveaways!").ConfigureAwait(false);
                    return;
                }
            }

            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Giveaway, Context.User).ConfigureAwait(false);
        }

        [Command("fixOT")]
        [Alias("fix", "f")]
        [Summary("Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
        [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
        public async Task FixAdOT()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, new PK8(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [Command("fixOT")]
        [Alias("fix", "f")]
        [Summary("Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
        [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
        public async Task FixAdOT([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, new PK8(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [Command("fixOTList")]
        [Alias("fl", "fq")]
        [Summary("Prints the users in the FixOT queue.")]
        [RequireSudo]
        public async Task GetFixListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.FixOT);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending Trades";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("TradeCordList")]
        [Alias("tcl", "tcq")]
        [Summary("Prints users in the TradeCord queue.")]
        [RequireSudo]
        public async Task GetTradeCordListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.TradeCord);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending TradeCord Trades";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("TradeCordCatch")]
        [Alias("k", "catch")]
        [Summary("Catch a random Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCord()
        {
            var user = Context.User.Id.ToString();
            TradeCordParanoiaChecks(Context);
            if (!Info.Hub.Config.Trade.TradeCordChannels.Contains(Context.Channel.Id.ToString()) && !Info.Hub.Config.Trade.TradeCordChannels.Equals(""))
            {
                await ReplyAsync($"You're typing the command in the wrong channel!").ConfigureAwait(false);
                return;
            }
            else if (!TradeCordCanCatch(user, out TimeSpan timeRemaining))
            {
                var embedTime = new EmbedBuilder { Color = Color.DarkBlue };
                var timeName = $"{Context.User.Username}, you're too quick!";
                var timeValue = $"Please try again in {(timeRemaining.Seconds < 1 ? 1 : timeRemaining.Seconds):N0} {(_ = timeRemaining.Seconds > 1 ? "seconds" : "second")}!";
                await EmbedUtil(embedTime, timeName, timeValue).ConfigureAwait(false);
                return;
            }

            var rng = new Random();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            var speciesZukan = Zukan8.GetRawIndexes(PersonalTable.SWSH, 2);
            var speciesRng = speciesZukan[rng.Next(speciesZukan.Count)].Species;
            var catchRng = rng.Next(101);
            var eggRng = rng.Next(101);
            PKM eggPkm = new PK8();
            int form1 = 0, form2 = 0, evo1 = 0, evo2 = 0;
            bool egg = content[1] != "0-0" && content[2] != "0-0" && CanGenerateEgg(content, out _, out _, out form1, out form2, out evo1, out evo2) && eggRng > 25;
            List<string> trainerInfo = new();

            for (int i = 3; i < 8; i++)
            {
                if (content[i] != "0")
                    trainerInfo.Add(i == 3 ? $"OT: {content[3]}" : i == 4 ? $"OTGender: {content[4]}" : i == 5 ? $"TID: {content[5]}" : i == 6 ? $"SID: {content[6]}" : i == 7 ? $"Language: {content[7]}" : "");
            }

            if (egg)
            {
                eggPkm = TradeExtensions.EggRngRoutine(content, trainerInfo, form1, form2, evo1, evo2);
                var laEgg = new LegalityAnalysis(eggPkm);
                var invalidEgg = !(eggPkm is PK8) || (!laEgg.Valid && SysCordInstance.Self.Hub.Config.Legality.VerifyLegality);
                if (invalidEgg)
                {
                    await Context.Channel.SendPKMAsync(eggPkm, $"Something went wrong!\n{ReusableActions.GetFormattedShowdownText(eggPkm)}").ConfigureAwait(false);
                    return;
                }

                eggPkm.ResetPartyStats();
            }

            if (catchRng > 10)
            {
                var speciesName = SpeciesName.GetSpeciesNameGeneration(speciesRng, 2, 8);
                var nidoranGender = string.Empty;
                var shinyRng = rng.Next(101);
                string shinyType = shinyRng > 98 ? "\nShiny: Square" : shinyRng > 95 ? "\nShiny: Star" : "";
                var gmaxRng = rng.Next(101);
                var formHack = FormHack(speciesRng, shinyRng, gmaxRng);
                var ballRng = formHack.Item2;

                if (speciesRng == 32 || speciesRng == 29)
                {
                    nidoranGender = speciesName.Last().ToString();
                    speciesName = speciesName.Remove(speciesName.Length - 1);
                }

                if (((speciesRng == (int)Species.Mew && shinyRng > 95) || ballRng.Contains("Cherish")) && trainerInfo.Count == 5)
                    trainerInfo.RemoveAt(4);

                if (TradeExtensions.ShinyLock.Contains(speciesRng) || (ballRng.Contains("Cherish") && !TradeExtensions.CanBeShinyCherish.Contains(speciesRng) && !TradeExtensions.CherishShinyOnly.Contains(speciesRng)) ||
                   (speciesRng == (int)Species.Pikachu && formHack.Item1 != "-Partner" && formHack.Item1 != "") || ((speciesRng == (int)Species.Poipole || speciesRng == (int)Species.Naganadel) && ballRng.Contains("Beast")) ||
                   ((speciesRng == (int)Species.Zapdos || speciesRng == (int)Species.Articuno || speciesRng == (int)Species.Moltres) && formHack.Item1 != "") || (ballRng.Contains("Cherish") && speciesRng == (int)Species.Melmetal))
                    shinyType = "";

                var set = new ShowdownSet($"{speciesName}{formHack.Item1}{ballRng}{shinyType}\n{string.Join("\n", trainerInfo)}");
                bool canGmax = set.CanToggleGigantamax(set.Species, set.FormIndex) && gmaxRng > 60 && !ballRng.Contains("Cherish");
                if (canGmax)
                    set.CanGigantamax = true;

                var template = AutoLegalityWrapper.GetTemplate(set);
                var sav = AutoLegalityWrapper.GetTrainerInfo(8);
                var pkm = sav.GetLegal(template, out _);

                TradeExtensions.RngRoutine(pkm);
                var form = nidoranGender != string.Empty ? nidoranGender : TradeExtensions.FormOutput(pkm.Species, pkm.AltForm, out _);

                if (pkm.Species == (int)Species.Pikachu && pkm.AltForm == 0 && shinyRng > 95)
                    CommonEdits.SetShiny(pkm, Shiny.Random);

                var la = new LegalityAnalysis(pkm);
                var invalid = !(pkm is PK8) || (!la.Valid && SysCordInstance.Self.Hub.Config.Legality.VerifyLegality);
                if (invalid)
                {
                    await Context.Channel.SendPKMAsync(pkm, $"Something went wrong!\n{ReusableActions.GetFormattedShowdownText(pkm)}").ConfigureAwait(false);
                    return;
                }

                CatchIncrement(content, user, out int catchID);
                pkm.ResetPartyStats();
                TradeCordDump("TradeCord", user, pkm, out int index);

                var pokeImg = PokeImg(pkm, set.CanGigantamax, pkm.Species == (int)Species.Alcremie ? pkm.Data[0xE4] : (uint)0);
                var ballImg = $"https://serebii.net/itemdex/sprites/pgl/" + $"{(Ball)pkm.Ball}ball".ToLower() + ".png";
                var embed = new EmbedBuilder { Color = pkm.IsShiny && pkm.ShinyXor == 0 ? Color.Gold : pkm.IsShiny ? Color.LightOrange : Color.Teal, ImageUrl = pokeImg, ThumbnailUrl = ballImg };
                var catchName = $"{Context.User.Username}'s Catch [#{catchID}]" + "&^&" + "\nResults";
                var catchMsg = $"You threw {(pkm.Ball == 2 ? "an" : "a")} {(Ball)pkm.Ball} Ball at a {(pkm.IsShiny ? "**shiny** wild **" + speciesName + form + "**" : "wild " + speciesName + form)}..." + "&^&" + 
                    $"Success! It put up a fight, but you caught {(pkm.IsShiny ? "**" + speciesName + form + $" [ID: {index}]**" : speciesName + form + $" [ID: {index}]")}!";

                if (egg)
                {
                    var eggSpeciesName = SpeciesName.GetSpeciesNameGeneration(eggPkm.Species, 2, 8);
                    var eggForm = TradeExtensions.FormOutput(eggPkm.Species, eggPkm.AltForm, out _);
                    CatchIncrement(content, user, out _);
                    TradeCordDump("TradeCord", user, eggPkm, out int indexEgg);
                    catchName = catchName + "&^&" + "\nEggs";
                    catchMsg = catchMsg + "&^&" + $"You got " + $"{(eggPkm.IsShiny ? "a **shiny egg**" : "an egg")}" + 
                        $" from the daycare! Welcome, {(eggPkm.IsShiny ? "**" + eggSpeciesName + eggForm + $" [ID: {indexEgg}]**" : eggSpeciesName + eggForm + $" [ID: {indexEgg}]")}!";
                }

                TradeCordCooldown(user);
                await EmbedUtil(embed, catchName, catchMsg).ConfigureAwait(false);
            }
            else
            {
                var spookyRng = rng.Next(101);
                var imgRng = rng.Next(1, 3);
                string imgGarf = "https://i.imgur.com/BOb6IbW.png";
                string imgConk = "https://i.imgur.com/oSUQhYv.png";
                var ball = (Ball)rng.Next(2, 26);
                var embedFail = new EmbedBuilder { Color = Color.Teal, ImageUrl = spookyRng > 90 && imgRng == 1 ? imgGarf : spookyRng > 90 && imgRng == 2 ? imgConk : string.Empty };
                var failName = $"{Context.User.Username}'s Catch" + "&^&" + "Results";
                var failMsg = $"You threw {(ball == Ball.Ultra ? "an" : "a")} {(ball == Ball.Cherish ? Ball.Poke : ball)} Ball at a wild {(spookyRng > 90 && imgRng != 3 ? "...whatever that thing is" : SpeciesName.GetSpeciesNameGeneration(speciesRng, 2, 8))}..." + "&^&" +
                    $"{(spookyRng > 90 && imgRng != 3 ? "One wiggle... Two... It breaks free and stares at you, smiling. You run for dear life." : "...but it managed to escape!")}";

                if (egg)
                {
                    var eggSpeciesName = SpeciesName.GetSpeciesNameGeneration(eggPkm.Species, 2, 8);
                    var eggForm = TradeExtensions.FormOutput(eggPkm.Species, eggPkm.AltForm, out _);
                    CatchIncrement(content, user, out _);
                    TradeCordDump("TradeCord", user, eggPkm, out int indexEgg);
                    failName = failName + "&^&" + "\nEggs";
                    failMsg = failMsg + "&^&" + $"You got " + $"{(eggPkm.IsShiny ? "a **shiny egg**" : "an egg")}" +
                        $" from the daycare! Welcome, {(eggPkm.IsShiny ? "**" + eggSpeciesName + eggForm + $" [ID: {indexEgg}]**" : eggSpeciesName + eggForm + $" [ID: {indexEgg}]")}!";
                }

                TradeCordCooldown(user);
                await EmbedUtil(embedFail, failName, failMsg).ConfigureAwait(false);
                return;
            }
        }

        [Command("TradeCord")]
        [Alias("tc")]
        [Summary("Trade a caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeForTradeCord([Summary("Trade Code")] int code, [Summary("Numerical catch ID")] string id)
        {
            var user = Context.User.Id.ToString();
            TradeCordParanoiaChecks(Context);

            if (!int.TryParse(id, out _))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("There is no Pokémon with this ID.").ConfigureAwait(false);
                return;
            }

            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            string? idCheck = FavoritesCheck(content).Find(x => x.Replace("★", "").Equals(id));
            var dcCheck = content[1].Split('_')[content[1].Contains("★") ? 1 : 0].Equals(id) || content[2].Split('_')[content[2].Contains("★") ? 1 : 0].Equals(id);
            if (idCheck != null || dcCheck)
            {
                await Context.Message.Channel.SendMessageAsync("Please remove your Pokémon from favorites and daycare before trading!").ConfigureAwait(false);
                return;
            }

            var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(path[0]));
            if (pkm == null)
            {
                await Context.Message.Channel.SendMessageAsync("Oops, something happened when converting your Pokémon!").ConfigureAwait(false);
                return;
            }

            TradeExtensions.TradeCordPath.Add(path[0]);
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, (PK8)pkm, PokeRoutineType.TradeCord, PokeTradeType.TradeCord).ConfigureAwait(false);
        }

        [Command("TradeCord")]
        [Alias("tc")]
        [Summary("Trade a caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeForTradeCord([Summary("Numerical catch ID")] string id)
        {
            var user = Context.User.Id.ToString();
            TradeCordParanoiaChecks(Context);

            if (!int.TryParse(id, out _))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            var code = Info.GetRandomTradeCode();
            List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("There is no Pokémon with this ID.").ConfigureAwait(false);
                return;
            }

            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            string? idCheck = FavoritesCheck(content).Find(x => x.Replace("★", "").Equals(id));
            var dcCheck = content[1].Split('_')[content[1].Contains("★") ? 1 : 0].Equals(id) || content[2].Split('_')[content[2].Contains("★") ? 1 : 0].Equals(id);
            if (idCheck != null || dcCheck)
            {
                await Context.Message.Channel.SendMessageAsync("Please remove your Pokémon from favorites and daycare before trading!").ConfigureAwait(false);
                return;
            }

            var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(path[0]));
            if (pkm == null)
            {
                await Context.Message.Channel.SendMessageAsync("Oops, something happened when converting your Pokémon!").ConfigureAwait(false);
                return;
            }

            TradeExtensions.TradeCordPath.Add(path[0]);
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, (PK8)pkm, PokeRoutineType.TradeCord, PokeTradeType.TradeCord).ConfigureAwait(false);
        }

        [Command("TradeCordCatchList")]
        [Alias("l", "list")]
        [Summary("List a user's Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task PokeList([Summary("Species name of a Pokémon")] [Remainder] string name)
        {
            TradeCordParanoiaChecks(Context);
            var filters = name.Split('=').Length == 3 ? name.Split('=')[1].ToLower().Trim() + " " + name.Split('=')[2].ToLower().Trim() : name.Split('=').Length == 2 ? name.Split('=')[1].ToLower().Trim() : "";
            name = filters != "" ? ListNameSanitize(name.Split('=')[0].Trim()) : ListNameSanitize(name);
            if (name == "")
            {
                await Context.Message.Channel.SendMessageAsync("In order to filter a Pokémon, we need to know which Pokémon to filter.").ConfigureAwait(false);
                return;
            }

            var user = Context.User.Id.ToString();
            var list = new List<string>();
            List<string> templist = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Contains(".pk8")).ToList();
            foreach (var line in templist)
            {
                var sanitize = line.Split('\\')[2].Trim();
                list.Add(sanitize.Remove(sanitize.IndexOf(".pk8")));
            }

            List<string> countTemp = new();
            string[] splitter = new string[] { " - " };
            if (filters != "" && !filters.Contains(" ") && !filters.Contains("shiny")) // Look for name and ball
                countTemp = list.FindAll(x => x.Split(' ')[0].Contains(filters.Substring(0, 1).ToUpper() + filters.Substring(1)) && (name == "Egg" ? x.Split(splitter, StringSplitOptions.None)[1].Contains(name) : x.Split(splitter, StringSplitOptions.None)[1].Replace(" (Egg)", "").Equals(name)));
            else if (filters != "" && !filters.Contains(" ") && filters.Contains("shiny")) // Look for name and shiny
                countTemp = list.FindAll(x => x.Contains("★") && (name == "Egg" ? x.Split(splitter, StringSplitOptions.None)[1].Contains(name) : x.Split(splitter, StringSplitOptions.None)[1].Replace(" (Egg)", "").Equals(name)));
            else if (filters != "" && filters.Contains(" ")) // Look for name, ball, and shiny
            {
                filters = filters.Replace("shiny", "").Trim();
                countTemp = list.FindAll(x => x.Contains("★") && x.Split(' ')[0].Contains(filters.Substring(0, 1).ToUpper() + filters.Substring(1)) && (name == "Egg" ? x.Split(splitter, StringSplitOptions.None)[1].Contains(name) : x.Split(splitter, StringSplitOptions.None)[1].Replace(" (Egg)", "").Equals(name)));
            }
            else countTemp = list.FindAll(x => name == "Egg" ? x.Split(splitter, StringSplitOptions.None)[1].Contains(name) : (x.Split(' ')[0].Contains(name) || x.Split(splitter, StringSplitOptions.None)[1].Replace(" (Egg)", "").Equals(name)));

            var count = new List<string>();
            var countAll = new List<string>();
            var countSh = new List<string>();
            var countShNonDupe = new List<string>();

            if (name == "Shinies")
            {
                foreach (var line in list)
                {
                    var sort = line.Split('-').Length > 2 ? line.Split('-')[1].Trim() + "-" + line.Split('-')[2].Trim() : line.Split('-')[1].Trim();
                    if (line.Contains("★"))
                        countSh.Add(sort);
                }
            }
            else if (name == "All")
            {
                foreach (var line in list)
                {
                    var sort = line.Split('-').Length > 2 ? line.Split('-')[1].Trim() + "-" + line.Split('-')[2].Trim() : line.Split('-')[1].Trim();
                    sort = sort.Replace("(Egg)", "").Trim();

                    if (!countAll.Contains(sort) && !line.Contains("★"))
                        countAll.Add(sort);

                    if (line.Contains("★") && !countShNonDupe.Contains("★" + sort))
                        countShNonDupe.Add("★" + sort);

                    if (line.Contains("★"))
                        countSh.Add(sort);
                }

                countAll.RemoveAll(x => countSh.Contains(x));
                countAll.AddRange(countShNonDupe);
            }
            else
            {
                foreach (var line in countTemp)
                {
                    var sort = line.Split('-', '_')[0].Trim();
                    if (sort.Contains("★"))
                        countSh.Add(sort);
                    count.Add(sort);
                }
            }

            var entry = string.Join(", ", name == "Shinies" ? countSh.OrderBy(x => x.Contains('★') ? x.Substring(1, 2) : x.Substring(0, 1)) : name == "All" ? countAll.OrderBy(x => x.Contains('★') ? x.Substring(1, 2) : x.Substring(0, 1)) : count.OrderBy(x => x.Contains('★') ? int.Parse(x.Split('★')[1].Split('_')[0]) : int.Parse(x.Split('_')[0])));
            if (entry == "")
            {
                await Context.Message.Channel.SendMessageAsync("No results found.").ConfigureAwait(false);
                return;
            }

            var listName = name == "Shinies" ? "Shiny Pokémon" : name == "All" ? "Pokémon" : name == "Egg" ? "Eggs" : "List For " + name;
            var listCount = name == "Shinies" ? $"★{countSh.Count}" : name == "All" ? $"{list.Count}, ★{countSh.Count}" : $"{count.Count}, ★{countSh.Count}";
            var msg = $"{Context.User.Username}'s {listName} [Total: {listCount}]";
            await ListUtil(msg, entry).ConfigureAwait(false);
        }

        [Command("TradeCordInfo")]
        [Alias("i", "info")]
        [Summary("Displays details for a user's Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordInfo([Summary("Numerical catch ID")] string id)
        {
            TradeCordParanoiaChecks(Context);
            if (!int.TryParse(id, out _))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            var user = Context.User.Id.ToString();
            List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("Could not find this ID.").ConfigureAwait(false);
                return;
            }

            var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(path[0]));
            if (pkm == null)
            {
                await Context.Message.Channel.SendMessageAsync("Oops, something happened when converting your Pokémon!").ConfigureAwait(false);
                return;
            }

            bool canGmax = new ShowdownSet(ShowdownSet.GetShowdownText(pkm)).CanGigantamax;
            var pokeImg = PokeImg(pkm, canGmax, pkm.Species == (int)Species.Alcremie ? pkm.Data[0xE4] : (uint)0);
            var form = TradeExtensions.FormOutput(pkm.Species, pkm.AltForm, out _);
            var embed = new EmbedBuilder { Color = pkm.IsShiny ? Color.Blue : Color.DarkBlue, ThumbnailUrl = pokeImg };
            var name = $"{Context.User.Username}'s {(pkm.IsShiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(pkm.Species, 2, 8)}{form} [ID: {id}]";
            var value = $"\n{ReusableActions.GetFormattedShowdownText(pkm)}";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordMassRelease")]
        [Alias("mr", "massrelease")]
        [Summary("Mass releases every non-shiny and non-Ditto Pokémon or specific species if specified.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task MassRelease([Remainder] string species = "")
        {
            TradeCordParanoiaChecks(Context);
            var user = Context.User.Id.ToString();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            var id1 = content[1].Split('_')[content[1].Contains("★") ? 1 : 0];
            var id2 = content[2].Split('_')[content[2].Contains("★") ? 1 : 0];
            var favorites = FavoritesCheck(content);
            List<string> path = new();

            if (species != "")
            {
                species = ListNameSanitize(species);
                path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => !x.Split('\\')[2].Contains("★") && !x.Split('\\')[2].Contains("Ditto") &&
                !x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id1) && !x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id2) &&
                !x.Split('\\')[2].Split(' ')[0].Split('_')[1].Equals("Cherish") && x.Split(new string[] { " - " }, StringSplitOptions.None)[1].Split('.')[0].Replace(" (Egg)", "").Equals(species)).ToList();
            }
            else path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => !x.Split('\\')[2].Contains("★") && !x.Split('\\')[2].Contains("Ditto") &&
            !x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id1) && !x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id2) && !x.Split('\\')[2].Split(' ')[0].Split('_')[1].Equals("Cherish")).ToList();

            foreach (var fav in favorites)
            {
                var match = path.Find(x => x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim() == fav.Replace("★", "").Trim());
                path.Remove(match);
            }

            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync(species == "" ? "Cannot find any more non-shiny, non-Ditto, non-favorite, non-event Pokémon to release." : "Cannot find specified species that could be released.").ConfigureAwait(false);
                return;
            }

            foreach (var line in path)
                File.Delete(line);

            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Mass Release";
            var value = species == "" ? "Every non-shiny Pokémon was released, excluding Ditto, favorites, events, and those in daycare." : $"Every non-shiny {species} was released, excluding favorites, events, and those in daycare.";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordRelease")]
        [Alias("r", "release")]
        [Summary("Releases a user's specific Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Release([Summary("Numerical catch ID")] string id)
        {
            TradeCordParanoiaChecks(Context);
            if (!int.TryParse(id, out _))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            var user = Context.User.Id.ToString();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("Cannot find this Pokémon.").ConfigureAwait(false);
                return;
            }

            if (content[1] != "0-0" || content[2] != "0-0")
            {
                var shiny1 = content[1].Contains("★");
                var shiny2 = content[2].Contains("★");
                if (content[1].Split('_')[content[1].Contains("★") ? 1 : 0].Equals(id) || content[2].Split('_')[content[2].Contains("★") ? 1 : 0].Equals(id))
                {
                    await Context.Message.Channel.SendMessageAsync("Cannot release a Pokémon in daycare.").ConfigureAwait(false);
                    return;
                }
            }

            var favorites = FavoritesCheck(content);
            string? fav = favorites.Count > 0 ? favorites.Find(x => x.Replace("★", "").Trim().Equals(id)) : "";
            fav = fav != null ? fav.Replace("★", "").Trim() : fav;
            if (fav == id)
            {
                await Context.Message.Channel.SendMessageAsync("Cannot release a Pokémon added to favorites.").ConfigureAwait(false);
                return;
            }

            File.Delete(path[0]);
            var sanitize = path[0].Split('-').Length > 2 ? (path[0].Split('-')[1] + "-" + path[0].Split('-')[2].Replace(".pk8", "")).Trim() : path[0].Split('-')[1].Replace(".pk8", "").Trim();
            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Release";
            var value = $"You release your {(path[0].Contains("★") ? "★" + sanitize : sanitize)}.";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordDaycare")]
        [Alias("dc")]
        [Summary("Check what's inside the daycare.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task DaycareInfo()
        {
            TradeCordParanoiaChecks(Context);
            var content = File.ReadAllText($"TradeCord\\{Context.User.Id}.txt").Split(',').ToList();
            if (content[1] == "0-0" && content[2] == "0-0")
            {
                await Context.Message.Channel.SendMessageAsync("You do not have anything in daycare.").ConfigureAwait(false);
                return;
            }

            bool canGenerateEgg = CanGenerateEgg(content, out string[] split1, out string[] split2, out int form1, out int form2, out _, out _);
            var shiny1 = split1.Contains("★");
            var shiny2 = split2.Contains("★");
            var species1 = content[1] != "0-0" ? split1[shiny1 ? 2 : 1].Split('-')[0] : "";
            var species2 = content[2] != "0-0" ? split2[shiny2 ? 2 : 1].Split('-')[0] : "";
            string dcSpecies1 = "";
            string dcSpecies2 = "";
            string msg = "";

            if (content[1] != "0-0")
                dcSpecies1 = $"[ID: {split1[shiny1 ? 1 : 0]}] {(shiny1 ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(int.Parse(species1), 2, 8)}{TradeExtensions.FormOutput(int.Parse(species1), form1, out _)} ({(Ball)int.Parse(split1[shiny1 ? 3 : 2])})";

            if (content[2] != "0-0")
                dcSpecies2 = $"[ID: {split2[shiny2 ? 1 : 0]}] {(shiny2 ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(int.Parse(species2), 2, 8)}{TradeExtensions.FormOutput(int.Parse(species2), form2, out _)} ({(Ball)int.Parse(split2[shiny2 ? 3 : 2])})";

            if (content[1] != "0-0" && content[2] != "0-0")
                msg = $"{dcSpecies1}\n{dcSpecies2}{(canGenerateEgg ? "\n\nThey seem to really like each other." : "\n\nThey don't really seem to be fond of each other. Make sure they're of the same evolution tree and can be eggs!")}";
            else if (content[1] == "0-0" && content[2] != "0-0")
                msg = $"{dcSpecies2}\n\nIt seems lonely.";
            else if (content[2] == "0-0" && content[1] != "0-0")
                msg = $"{dcSpecies1}\n\nIt seems lonely.";

            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Daycare Info";
            await EmbedUtil(embed, name, msg).ConfigureAwait(false);
        }

        [Command("TradeCordDaycare")]
        [Alias("dc")]
        [Summary("Adds (or removes) Pokémon to daycare.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Daycare([Summary("Action to do (withdraw, deposit)")] string action, [Summary("Catch ID or elaborate action (\"All\" if withdrawing")] string id)
        {
            TradeCordParanoiaChecks(Context);
            id = id.Substring(0, 1).ToUpper() + id.Substring(1);
            if (!int.TryParse(id, out _) && id != "All")
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            var user = Context.User.Id.ToString();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            action = action.ToLower();
            if (action == "w" || action == "withdraw")
            {
                if (content[1] == "0-0" && content[2] == "0-0")
                {
                    await Context.Message.Channel.SendMessageAsync("You do not have anything in daycare.").ConfigureAwait(false);
                    return;
                }

                bool shiny1 = content[1].Contains("★");
                bool shiny2 = content[2].Contains("★");
                var id1 = content[1] != "0-0" ? content[1].Split('_')[shiny1 ? 1 : 0].Split('-')[0].Trim() : "";
                var id2 = content[2] != "0-0" ? content[2].Split('_')[shiny2 ? 1 : 0].Split('-')[0].Trim() : "";
                List<string> species = new();
                if (id != "All")
                {
                    if (id1.Equals(id))
                    {
                        species.Add(content[1].Split('_')[shiny1 ? 2 : 1].Trim());
                        content[1] = "0-0";
                    }
                    else if (id2.Equals(id))
                    {
                        species.Add(content[2].Split('_')[shiny2 ? 2 : 1].Trim());
                        content[2] = "0-0";
                    }
                }
                else
                {
                    if (content[1] != "0-0")
                        species.Add(content[1].Split('_')[shiny1 ? 2 : 1].Trim());
                    
                    if (content[2] != "0-0")
                        species.Add(content[2].Split('_')[shiny2 ? 2 : 1].Trim());

                    content[1] = content[2] = "0-0";
                }

                if (species.Count < 1)
                {
                    await Context.Message.Channel.SendMessageAsync(id == "All" ? "You do not have anything in daycare." : "You do not have that Pokémon in daycare.").ConfigureAwait(false);
                    return;
                }
                else File.WriteAllText($"TradeCord\\{user}.txt", string.Join(",", content));

                var species1 = (shiny1 ? "★" : "") + SpeciesName.GetSpeciesNameGeneration(int.Parse(species[0].Split('-')[0].Trim()), 2, 8);
                var form1 = TradeExtensions.FormOutput(int.Parse(species[0].Split('-')[0].Trim()), int.Parse(species[0].Split('-')[1].Trim()), out _);
                var species2 = species.Count > 1 ? (shiny2 ? "★" : "") + SpeciesName.GetSpeciesNameGeneration(int.Parse(species[1].Split('-')[0].Trim()), 2, 8) : "";
                var form2 = species.Count > 1 ? TradeExtensions.FormOutput(int.Parse(species[1].Split('-')[0].Trim()), int.Parse(species[1].Split('-')[1].Trim()), out _) : "";
                var embedWithdraw = new EmbedBuilder { Color = Color.DarkBlue };
                var nameWithdraw = $"{Context.User.Username}'s Daycare Withdraw";
                var valueWithdraw = $"{(id != "All" ? $"You withdrew your [ID: {id}] {species1}{form1} from the daycare." : $"You withdrew your [ID: {(id1 != "" ? id1 : id2)}] {species1}{form1}{(species.Count > 1 ? $" and [ID: {id2}] {species2}{form2}" : "")} from the daycare.")}";
                await EmbedUtil(embedWithdraw, nameWithdraw, valueWithdraw).ConfigureAwait(false);
                return;
            }
            else if (action == "d" || action == "deposit")
            {
                List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
                if (path.Count == 0)
                {
                    await Context.Message.Channel.SendMessageAsync("There is no Pokémon with this ID.").ConfigureAwait(false);
                    return;
                }
                else if (content[1] != "0-0" && content[2] != "0-0")
                {
                    await Context.Message.Channel.SendMessageAsync("Daycare full, please withdraw something first.").ConfigureAwait(false);
                    return;
                }

                var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(path[0]));
                if (pkm == null)
                {
                    await Context.Message.Channel.SendMessageAsync("Oops, something happened when converting your Pokémon!").ConfigureAwait(false);
                    return;
                }

                if (content[1] == "0-0" && content[2] != "0-0" && !content[2].Split('_')[content[2].Contains("★") ? 1 : 0].Equals(id))
                    content[1] = (pkm.IsShiny ? "★_" : "") + id + "_" + pkm.Species + "-" + pkm.AltForm + "_" + pkm.Ball;
                else if (content[2] == "0-0" && content[1] != "0-0" && !content[1].Split('_')[content[1].Contains("★") ? 1 : 0].Equals(id))
                    content[2] = (pkm.IsShiny ? "★_" : "") + id + "_" + pkm.Species + "-" + pkm.AltForm + "_" + pkm.Ball;
                else if (content[1] == "0-0" && content[2] == "0-0")
                    content[1] = (pkm.IsShiny ? "★_" : "") + id + "_" + pkm.Species + "-" + pkm.AltForm + "_" + pkm.Ball;
                else
                {
                    await Context.Message.Channel.SendMessageAsync("You've already deposited that Pokémon to daycare.").ConfigureAwait(false);
                    return;
                }

                File.WriteAllText($"TradeCord\\{user}.txt", string.Join(",", content));
                var speciesStr = $"{SpeciesName.GetSpeciesNameGeneration(pkm.Species, 2, 8)}{TradeExtensions.FormOutput(pkm.Species, pkm.AltForm, out _)}({(Ball)pkm.Ball})";
                var embedDeposit = new EmbedBuilder { Color = Color.DarkBlue };
                var nameDeposit = $"{Context.User.Username}'s Daycare Deposit";
                var valueDeposit = $"Deposited your {(pkm.IsShiny ? "★" + speciesStr : speciesStr)} to daycare!";
                await EmbedUtil(embedDeposit, nameDeposit, valueDeposit).ConfigureAwait(false);
                return;
            }
        }

        [Command("TradeCordGift")]
        [Alias("gift", "g")]
        [Summary("Gifts a Pokémon to a mentioned user.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Gift([Summary("Numerical catch ID")] string id, [Summary("User mention")] string _)
        {
            TradeCordParanoiaChecks(Context);
            var user = Context.User.Id.ToString();
            List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => !x.Contains("★") ? x.Split('\\')[2].Split('_')[0].Trim().Equals(id) : x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
            if (!int.TryParse(id, out int _int))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }
            else if (Context.Message.MentionedUsers.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("Please mention a user you're gifting a Pokémon to.").ConfigureAwait(false);
                return;
            }
            else if (Context.Message.MentionedUsers.First().Id == Context.User.Id)
            {
                await Context.Message.Channel.SendMessageAsync("...Why?").ConfigureAwait(false);
                return;
            }

            var dir = Path.Combine("TradeCord", Context.Message.MentionedUsers.First().Id.ToString());
            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("Cannot find this Pokémon.").ConfigureAwait(false);
                return;
            }
            else if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            string? idCheck = FavoritesCheck(content).Find(x => x.Replace("★", "").Equals(id));
            var dcCheck = content[1].Split('_')[content[1].Contains("★") ? 1 : 0].Equals(id) || content[2].Split('_')[content[2].Contains("★") ? 1 : 0].Equals(id);
            if (idCheck != null || dcCheck)
            {
                await Context.Message.Channel.SendMessageAsync("Please remove your Pokémon from favorites and daycare before gifting!").ConfigureAwait(false);
                return;
            }

            var split = path[0].Split('\\')[2].Split('_');
            var oldname = split[0].Replace("★", "").Trim().Substring(0, id.Length);
            var newIDparse = Directory.GetFiles(dir).Where(x => x.Contains(".pk8")).Select(x => x.Split('\\')[2].Split(' ', '_')[0].Replace("★", "").Trim()).ToArray();
            var newID = newIDparse.OrderBy(x => int.Parse(x)).ToArray();
            var sanitize = split[1].Split(new string[] { " - " }, StringSplitOptions.None)[1].Replace(".pk8", "").Trim();
            var embed = new EmbedBuilder { Color = Color.Purple };
            var name = $"{Context.User.Username}'s Gift";
            var value = $"You gifted your {(path[0].Contains("★") ? "★**" + sanitize + "**" : sanitize)} to {Context.Message.MentionedUsers.First().Username}.";
            File.Move(path[0], $"{dir}\\{path[0].Split('\\')[2].Replace(oldname, Indexing(newID).ToString())}");
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordTrainerInfoSet")]
        [Alias("tis")]
        [Summary("Sets individual trainer info for caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TrainerInfoSet([Remainder] string info)
        {
            TradeCordParanoiaChecks(Context);
            var user = Context.User.Id.ToString();
            List<string> trainerInfo = info.Split('\n', ',').ToList();

            if (trainerInfo.Count < 5)
            {
                await Context.Message.Channel.SendMessageAsync("Please enter all required info!").ConfigureAwait(false);
                return;
            }
            else if (trainerInfo.Any(x => !x.Contains(":")))
            {
                await Context.Message.Channel.SendMessageAsync("Please check the input format!").ConfigureAwait(false);
                return;
            }

            for (int i = 0; i < trainerInfo.Count; i++)
                trainerInfo[i] = trainerInfo[i].Split(':')[0].Trim().ToLowerInvariant().Replace(" ", "") + ":" + trainerInfo[i].Split(':')[1].Trim();

            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',');
            content[3] = trainerInfo.Find(x => x.Contains("ot:")).Split(':')[1].Trim();
            content[4] = trainerInfo.Find(x => x.Contains("otgender:")).Split(':')[1].Trim().Substring(0, 1).ToUpper() + trainerInfo.Find(x => x.Contains("otgender:")).Split(':')[1].Trim().Substring(1);
            content[5] = trainerInfo.Find(x => x.Contains("tid:")).Split(':')[1].Trim();
            content[6] = trainerInfo.Find(x => x.Contains("sid:")).Split(':')[1].Trim();
            content[7] = trainerInfo.Find(x => x.Contains("language:")).Split(':')[1].Trim().Substring(0, 1).ToUpper() + trainerInfo.Find(x => x.Contains("language:")).Split(':')[1].Trim().Substring(1);

            if (content[3].Length > 12)
            {
                await Context.Message.Channel.SendMessageAsync("OT name too long, has to be 12 characters or fewer!").ConfigureAwait(false);
                return;
            }
            else if (content[3].Length > 6 && (content[7].Contains("Japanese") || content[7].Contains("Korean")))
            {
                await Context.Message.Channel.SendMessageAsync("Japanese and Korean have a lower limit for OT of 6 characters or fewer!").ConfigureAwait(false);
                return;
            }
            else if (content[4] != "Male" && content[4] != "Female")
            {
                await Context.Message.Channel.SendMessageAsync("Enter your in-game OT gender as \"Male\" or \"Female\"!").ConfigureAwait(false);
                return;
            }
            else if (!int.TryParse(content[5], out _))
            {
                await Context.Message.Channel.SendMessageAsync("TID has to be 6 digits long and only contain numbers!").ConfigureAwait(false);
                return;
            }
            else if (!int.TryParse(content[6], out _))
            {
                await Context.Message.Channel.SendMessageAsync("SID has to be 4 digits long and only contain numbers!").ConfigureAwait(false);
                return;
            }
            else if (!Enum.TryParse(content[7], out LanguageID _))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a valid in-game language!").ConfigureAwait(false);
                return;
            }

            File.WriteAllText($"TradeCord\\{Context.User.Id}.txt", string.Join(",", content));
            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Trainer Info";
            var value = $"\nYou've set your trainer info as the following: \n**OT:** {content[3]}\n**OTGender:** {content[4]}\n**TID:** {content[5]}\n**SID:** {content[6]}\n**Language:** {content[7]}";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordTrainerInfo")]
        [Alias("ti")]
        [Summary("Displays currently set trainer info.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TrainerInfo()
        {
            TradeCordParanoiaChecks(Context);
            var user = Context.User.Id.ToString();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',');
            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Trainer Info";
            var value = $"\n**OT:** {(content[3] == "0" ? "Not set." : content[3])}" +
                $"\n**OTGender:** {(content[4] == "0" ? "Not set." : content[4])}" +
                $"\n**TID:** {(content[5] == "0" ? "Not set." : content[5])}" +
                $"\n**SID:** {(content[6] == "0" ? "Not set." : content[6])}" +
                $"\n**Language:** {(content[7] == "0" || int.TryParse(content[7], out _) ? "Not set." : content[7])}";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordFavorites")]
        [Alias("fav")]
        [Summary("Display favorites list.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordFavorites()
        {
            TradeCordParanoiaChecks(Context);
            var user = Context.User.Id.ToString();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            var favorites = FavoritesCheck(content);
            if (favorites.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync($"You don't have anything in favorites yet!").ConfigureAwait(false);
                return;
            }

            List<string> names = new();
            foreach (var fav in favorites)
            {
                List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => !x.Contains("★") ? x.Split('\\')[2].Split('_')[0].Trim().Equals(fav) : x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(fav.Replace("★", "").Trim())).ToList();
                var split = path[0].Split('\\')[2].Split('-');
                names.Add(split.Length > 2 ? $"[ID: {fav}] " + split[1].Trim() + "-" + split[2].Replace(".pk8", "").Replace("(Egg)", "").Trim() :
                    $"[ID: {fav}] " + split[1].Replace(".pk8", "").Replace("(Egg)", "").Trim());
            }

            var entry = string.Join(", ", names);
            var msg = $"{Context.User.Username}'s Favorites";
            await ListUtil(msg, entry).ConfigureAwait(false);
        }

        [Command("TradeCordFavorites")]
        [Alias("fav")]
        [Summary("Add/Remove a Pokémon to a favorites list.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordFavorites([Summary("Catch ID")] string id)
        {
            TradeCordParanoiaChecks(Context);
            if (!int.TryParse(id, out int _int))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            var user = Context.User.Id.ToString();
            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            List<string> path = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => !x.Contains("★") ? x.Split('\\')[2].Split('_')[0].Trim().Equals(id) : x.Split('\\')[2].Split('_')[0].Replace("★", "").Trim().Equals(id)).ToList();
            if (path.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("Cannot find this Pokémon.").ConfigureAwait(false);
                return;
            }

            var favorites = FavoritesCheck(content);
            string? fav = favorites.Count > 0 ? favorites.Find(x => x.Replace("★", "").Trim().Equals(id)) : "";
            fav = fav != null ? fav.Replace("★", "").Trim() : fav;
            var split = path[0].Split('\\')[2].Split('-');
            var name = split.Length > 2 ? split[1].Trim() + "-" + split[2].Replace(".pk8", "").Trim() : split[1].Replace(".pk8", "").Trim();

            if (fav != id)
            {
                content.Add(split[0].Contains("★") ? "★" + id : id);
                File.WriteAllText($"TradeCord\\{Context.User.Id}.txt", string.Join(",", content));
                await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, added your {(split[0].Contains("★") ? "★**" + name + "**" : name)} to favorites!").ConfigureAwait(false);
                return;
            }
            else if (fav == id)
            {
                for (int i = 8; i < content.Count; i++)
                {
                    if (content[i].Replace("★", "").Trim() == id)
                        content.RemoveAt(i);
                }

                File.WriteAllText($"TradeCord\\{Context.User.Id}.txt", string.Join(",", content));
                await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, removed your {(split[0].Contains("★") ? "★**" + name + "**" : name)} from favorites!").ConfigureAwait(false);
                return;
            }
        }

        private void TradeCordDump(string folder, string subfolder, PKM pkm, out int index)
        {
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            var speciesName = SpeciesName.GetSpeciesNameGeneration(pkm.Species, 2, 8);
            var form = TradeExtensions.FormOutput(pkm.Species, pkm.AltForm, out _);
            if (speciesName.Contains("Nidoran"))
            {
                speciesName = speciesName.Remove(speciesName.Length - 1);
                form = pkm.Species == (int)Species.NidoranF ? "-F" : "-M";
            }

            var array = Directory.GetFiles(dir).Where(x => x.Contains(".pk8")).Select(x => x.Split('\\')[2].Split('-', '_')[0].Replace("★", "").Trim()).ToArray();
            array = array.OrderBy(x => int.Parse(x)).ToArray();
            index = Indexing(array);
            var newname = (pkm.IsShiny ? "★" + index.ToString() : index.ToString()) + $"_{(Ball)pkm.Ball}" + " - " + speciesName + form  + $"{(pkm.IsEgg ? " (Egg)" : "")}" + ".pk8";
            var fn = Path.Combine(dir, Util.CleanFileName(newname));
            File.WriteAllBytes(fn, pkm.DecryptedPartyData);
        }

        private int Indexing(string[] array)
        {
            var i = 0;
            return array.Where(x => int.Parse(x) > 0).Distinct().OrderBy(x => int.Parse(x)).Any(x => int.Parse(x) != (i += 1)) ? i : i + 1;
        }

        private void TradeCordCooldown(string id)
        {
            if (Info.Hub.Config.Trade.TradeCordCooldown > 0)
            {
                var line = TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(id));
                if (line != null)
                    TradeExtensions.TradeCordCooldown.Remove(TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(id)));
                TradeExtensions.TradeCordCooldown.Add($"{id},{DateTime.Now}");
            }
        }

        private bool TradeCordCanCatch(string user, out TimeSpan timeRemaining)
        {
            if (Info.Hub.Config.Trade.TradeCordCooldown < 0)
                Info.Hub.Config.Trade.TradeCordCooldown = default;

            var line = TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(user));
            DateTime.TryParse(line != null ? line.Split(',')[1] : string.Empty, out DateTime time);
            var timer = time.AddSeconds(Info.Hub.Config.Trade.TradeCordCooldown);
            timeRemaining = timer - DateTime.Now;

            if (DateTime.Now < timer)
                return false;

            return true;
        }

        private void TradeCordParanoiaChecks(SocketCommandContext context)
        {
            var user = context.User.Id.ToString();
            if (!Directory.Exists("TradeCord") || !Directory.Exists($"TradeCord\\Backup\\{user}"))
            {
                Directory.CreateDirectory($"TradeCord\\{user}");
                Directory.CreateDirectory($"TradeCord\\Backup\\{user}");
            }

            if (!File.Exists($"TradeCord\\{user}.txt"))
            {
                File.Create($"TradeCord\\{user}.txt").Close();
                File.WriteAllText($"TradeCord\\{user}.txt", "0,0-0,0-0,0,0,0,0,0");
            }

            var content = File.ReadAllText($"TradeCord\\{user}.txt").Split(',').ToList();
            if (content.Count < 8)
            {
                content.Add("0");
                File.WriteAllText($"TradeCord\\{user}.txt", string.Join(",", content));
            }

            if (content[7] != "0" && int.TryParse(content[7], out _))
            {
                content.Insert(7, "0");
                File.WriteAllText($"TradeCord\\{user}.txt", string.Join(",", content));
            }

            var parseForm = content[1].Contains("-") && int.TryParse(content[1].Split('-')[1].Split('_')[0], out _) && content[2].Contains("-") && int.TryParse(content[2].Split('-')[1].Split('_')[0], out _);
            if (!parseForm)
            {
                for (int i = 1; i < 3; i++)
                {
                    var split = content[i].Split('_');
                    if (split.Length > 1)
                    {
                        var splitID = split[content[i].Contains("★") ? 1 : 0];
                        var splitSpecies = split[content[i].Contains("★") ? 2 : 1].Split('-')[0];
                        var splitForm = content[i].Contains("-") ? content[i].Split('-')[1].Split('_')[0] : "";
                        var splitBall = split[content[i].Contains("★") ? 3 : 2];
                        TradeExtensions.FormOutput(int.Parse(splitSpecies), 0, out string[] formArray);
                        var form = formArray.ToList().FindIndex(x => x.Contains(splitForm));
                        content[i] = (content[i].Contains("★") ? "★_" : "") + splitID + "_" + splitSpecies + "-" + (int.TryParse(splitForm, out _) ? splitForm : (form == -1 ? 0 : form).ToString()) + "_" + splitBall;
                    }
                    else content[i] = "0-0";
                }
                File.WriteAllText($"TradeCord\\{user}.txt", string.Join(",", content));
            }

            var parseBall = Directory.GetFiles(Path.Combine("TradeCord", user)).Where(x => x.Contains(".pk8")).ToList();
            if (!parseBall.All(x => x.Contains('_')))
            {
                foreach (var line in parseBall)
                {
                    if (!line.Contains('_'))
                    {
                        var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(line));
                        if (pkm == null)
                            break;

                        var ball = $"_{(Ball)pkm.Ball}" ?? "_";
                        var newName = line.Replace(line.Split(' ')[0].Trim(), $" {line.Split(' ')[0] + ball} ".Trim());
                        File.Move(line, newName);
                    }                    
                }
            }
        }

        private bool CanGenerateEgg(List<string> content, out string[] split1, out string[] split2, out int form1, out int form2, out int evo1, out int evo2)
        {
            split1 = content[1].Split('_');
            split2 = content[2].Split('_');
            form1 = form2 = 0;
            evo1 = evo2 = 0;

            if (content[1] != "0-0")
            {
                form1 = int.Parse(content[1].Split('-')[1].Split('_')[0]);
                int speciesID1 = int.Parse(content[1].Split('-')[0].Split('_').LastOrDefault());
                var pkm1 = AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(speciesID1, 2, 8))), out _);
                evo1 = EvolutionTree.GetEvolutionTree(8).GetValidPreEvolutions(pkm1, 100).LastOrDefault().Species;
            }

            if (content[2] != "0-0")
            {
                form2 = int.Parse(content[2].Split('-')[1].Split('_')[0]);
                int speciesID2 = int.Parse(content[2].Split('-')[0].Split('_').LastOrDefault());
                var pkm2 = AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(speciesID2, 2, 8))), out _);
                evo2 = EvolutionTree.GetEvolutionTree(8).GetValidPreEvolutions(pkm2, 100).LastOrDefault().Species;
            }

            if (evo1 == 132 && evo2 == 132)
                return true;
            else if (evo1 == evo2 && TradeExtensions.ValidEgg.Contains(evo1))
                return true;
            else if ((evo1 == 132 || evo2 == 132) && (TradeExtensions.ValidEgg.Contains(evo1) || TradeExtensions.ValidEgg.Contains(evo2)))
                return true;
            else if ((evo1 == 29 && evo2 == 32) || (evo1 == 32 && evo2 == 29))
                return true;
            else return false;
        }

        private List<string> FavoritesCheck(List<string> content)
        {
            List<string> favorites = new();
            if (content.Count > 8)
            {
                for (int i = 8; i < content.Count; i++)
                    favorites.Add(content[i]);
            }

            return favorites;
        }

        private string ListNameSanitize(string name)
        {
            if (name == "")
                return name;

            name = name.Substring(0, 1).ToUpper().Trim() + name.Substring(1).ToLower().Trim();
            if (name.Contains("'"))
                name = name.Replace("'", "’");

            if (name.Contains('-'))
            {
                var split = name.Split('-');
                bool exceptions = split[1] == "z" || split[1] == "m" || split[1] == "f";
                name = split[0] + "-" + (split[1].Length < 2 && !exceptions ? split[1] : split[1].Substring(0, 1).ToUpper() + split[1].Substring(1).ToLower() + (split.Length > 2 ? "-" + split[2].ToUpper() : ""));
            }

            if (name.Contains(' '))
            {
                var split = name.Split(' ');
                name = split[0] + " " + split[1].Substring(0, 1).ToUpper() + split[1].Substring(1).ToLower();
            }

            return name;
        }

        private Tuple<string,string> FormHack(int speciesRng, int shinyRng, int gmaxRng)
        {
            var rng = new Random();
            string formHack;
            var formEdgeCaseRng = rng.Next(2);
            string[] poipoleRng = { "Poke", "Beast", "Cherish" };
            var eventRng = rng.Next(101);
            string ballRng = (eventRng > 75 && TradeExtensions.Cherish.Contains(speciesRng)) || (speciesRng == (int)Species.Melmetal && gmaxRng > 60) || TradeExtensions.CherishOnly.Contains(speciesRng) || (TradeExtensions.CherishShinyOnly.Contains(speciesRng) && shinyRng > 95) ? "\nBall: Cherish" : "\nBall: Poke";

            if (ballRng.Contains("Cherish"))
            {
                switch (speciesRng)
                {
                    case (int)Species.Sandshrew: formHack = "-Alola"; break;
                    case (int)Species.Vulpix: formHack = "-Alola"; break;
                    case (int)Species.Diglett: formHack = "-Alola"; break;
                    case (int)Species.Ponyta: formHack = "-Galar"; break;
                    case (int)Species.Exeggutor: formHack = "-Alola"; break;
                    case (int)Species.MrMime: formHack = "-Galar"; break;
                    case (int)Species.Corsola: formHack = "-Galar"; break;
                    case (int)Species.Rockruff: formHack = "-Dusk"; break;
                    case (int)Species.Lycanroc: formHack = "-Midnight"; break;
                    case (int)Species.Gastrodon: _ = shinyRng > 95 ? formHack = "-West" : formHack = "-East"; break;
                    case (int)Species.Pumpkaboo: formHack = "-Super"; break;
                    case (int)Species.Meowth: _ = formEdgeCaseRng == 1 ? formHack = $"{(gmaxRng > 60 ? "-Gmax" : "")}" : formHack = "-Galar"; break;
                    case (int)Species.Magearna: _ = formEdgeCaseRng == 1 ? formHack = "" : formHack = "-Original"; break;
                    case (int)Species.Raikou: _ = shinyRng > 95 ? formHack = "\nRash Nature" : formHack = ""; break;
                    case (int)Species.Entei: _ = shinyRng > 95 ? formHack = "\nAdamant Nature" : formHack = ""; break;
                    case (int)Species.Suicune: _ = shinyRng > 95 ? formHack = "\nRelaxed Nature" : formHack = ""; break;
                    case (int)Species.Tangrowth: formHack = "\nBrave Nature"; break;
                    case (int)Species.Pichu: formHack = "\nJolly Nature"; break;
                    case (int)Species.Octillery: formHack = "\nSerious Nature"; break;
                    case (int)Species.Melmetal: formHack = "-Gmax"; break;
                    case (int)Species.Feebas: formHack = "\nCalm Nature"; break;
                    case (int)Species.Whiscash: formHack = "\nGentle Nature"; break;
                    default: formHack = ""; break;
                };
            }
            else
            {
                switch (speciesRng)
                {
                    case (int)Species.Meowstic: case (int)Species.Indeedee: _ = formEdgeCaseRng == 1 ? formHack = "-M" : formHack = "-F"; break;
                    case (int)Species.NidoranF: case (int)Species.NidoranM: _ = speciesRng == (int)Species.NidoranF ? formHack = "-F" : formHack = "-M"; break;
                    case (int)Species.Sinistea: case (int)Species.Polteageist: _ = formEdgeCaseRng == 1 ? formHack = "" : formHack = "-Antique"; break;
                    case (int)Species.Pikachu: _ = formEdgeCaseRng == 1 ? formHack = "" : formHack = TradeExtensions.PartnerPikachuHeadache[rng.Next(TradeExtensions.PartnerPikachuHeadache.Length)]; break;
                    case (int)Species.Exeggutor: _ = formEdgeCaseRng == 1 ? formHack = "" : formHack = "-Alola"; break;
                    default: formHack = ""; break;
                };

                int[] alreadyChecked = { 854, 855, 25, 103 };
                int[] ignore = { 382, 383, 487, 647, 649, 716, 717, 718, 773, 893 };
                if (formHack == "" && !alreadyChecked.Contains(speciesRng) && !ignore.Contains(speciesRng))
                {
                    var strings = GameInfo.GetStrings(LanguageID.English.GetLanguage2CharName());
                    var formString = FormConverter.GetFormList(speciesRng, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, 8);
                    formHack = formString.Length > 1 ? "-" + formString[rng.Next(formString.Length)] : "";
                }
            }

            switch (speciesRng)
            {
                case (int)Species.Poipole: case (int)Species.Naganadel: ballRng = $"\nBall: {poipoleRng[rng.Next(poipoleRng.Length)]}"; break;
                case (int)Species.Meltan: ballRng = $"\nBall: {TradeExtensions.LGPEBalls[rng.Next(TradeExtensions.LGPEBalls.Length)]}"; break;
                case (int)Species.Melmetal: ballRng = gmaxRng > 60 ? ballRng : $"\nBall: {TradeExtensions.LGPEBalls[rng.Next(TradeExtensions.LGPEBalls.Length)]}"; break;
                case (int)Species.Marowak: ballRng = $"\nBall: Premier"; break;
            };

            return new Tuple<string, string>(formHack, ballRng);
        }

        private async Task ListUtil(string nameMsg, string entry)
        {
            var index = 0;
            List<string> pageContent = new();
            var emptyList = "No results found.";
            bool canReact = Context.Guild.CurrentUser.GetPermissions(Context.Channel as IGuildChannel).AddReactions;
            var round = Math.Round((decimal)entry.Length / 1024, MidpointRounding.AwayFromZero);

            if (entry.Length > 1024)
            {
                for (int i = 0; i <= round; i++)
                {
                    var splice = TradeExtensions.SpliceAtWord(entry, index, 1024);
                    index += splice.Count;
                    if (splice.Count == 0)
                        break;

                    pageContent.Add(string.Join(entry.Contains(",") ? ", " : "\n", splice));
                }
            }
            else pageContent.Add(entry == "" ? emptyList : entry);

            var embed = new EmbedBuilder { Color = Color.DarkBlue }.AddField(x =>
            {
                x.Name = nameMsg;
                x.Value = pageContent[0];
                x.IsInline = false;
            }).WithFooter(x =>
            {
                x.IconUrl = "https://i.imgur.com/nXNBrlr.png";
                x.Text = $"Page 1 of {pageContent.Count}";
            });

            if (!canReact && pageContent.Count > 1)
            {
                embed.AddField(x =>
                {
                    x.Name = "Missing \"Add Reactions\" Permission";
                    x.Value = "Displaying only the first page of the list due to embed field limits.";
                });
            }

            var msg = await Context.Message.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
            if (pageContent.Count > 1 && canReact)
                _ = Task.Run(async () => await ReactionAwait(msg, nameMsg, pageContent).ConfigureAwait(false));
        }

        private async Task ReactionAwait(RestUserMessage msg, string nameMsg, List<string> pageContent)
        {
            int page = 0;
            var userId = Context.User.Id;
            IEmote[] reactions = { new Emoji("⬅️"), new Emoji("➡️") };
            await msg.AddReactionsAsync(reactions).ConfigureAwait(false);
            var sw = new Stopwatch();
            sw.Start();

            while (sw.ElapsedMilliseconds < 20_000)
            {
                var collectorBack = await msg.GetReactionUsersAsync(reactions[0], 100).FlattenAsync().ConfigureAwait(false);
                var collectorForward = await msg.GetReactionUsersAsync(reactions[1], 100).FlattenAsync().ConfigureAwait(false);
                IUser? UserReactionBack = collectorBack.FirstOrDefault(x => x.Id == userId && !x.IsBot);
                IUser? UserReactionForward = collectorForward.FirstOrDefault(x => x.Id == userId && !x.IsBot);

                if (UserReactionBack != null && page > 0)
                {
                    page--;
                    var embedBack = new EmbedBuilder { Color = Color.DarkBlue }.AddField(x =>
                    {
                        x.Name = nameMsg;
                        x.Value = pageContent[page];
                        x.IsInline = false;
                    }).WithFooter(x =>
                    {
                        x.IconUrl = "https://i.imgur.com/nXNBrlr.png";
                        x.Text = $"Page {page + 1 } of {pageContent.Count}";
                    }).Build();

                    await msg.RemoveReactionAsync(reactions[0], UserReactionBack);
                    await msg.ModifyAsync(msg => msg.Embed = embedBack).ConfigureAwait(false);
                    sw.Restart();
                }
                else if (UserReactionForward != null && page < pageContent.Count - 1)
                {
                    page++;
                    var embedForward = new EmbedBuilder { Color = Color.DarkBlue }.AddField(x =>
                    {
                        x.Name = nameMsg;
                        x.Value = pageContent[page];
                        x.IsInline = false;
                    }).WithFooter(x =>
                    {
                        x.IconUrl = "https://i.imgur.com/nXNBrlr.png";
                        x.Text = $"Page {page + 1} of {pageContent.Count}";
                    }).Build();

                    await msg.RemoveReactionAsync(reactions[1], UserReactionForward);
                    await msg.ModifyAsync(msg => msg.Embed = embedForward).ConfigureAwait(false);
                    sw.Restart();
                }
            }

            await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
        }

        private string PokeImg(PKM pkm, bool canGmax, uint alcremieDeco)
        {
            bool md = false;
            bool fd = false;
            if (TradeExtensions.GenderDependent.Contains(pkm.Species) && !canGmax && pkm.AltForm == 0)
            {
                if (pkm.Gender == 0)
                    md = true;
                else fd = true;
            }

            var baseLink = "https://projectpokemon.org/images/sprites-models/homeimg/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');
            baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : $"0{pkm.Species}";
            baseLink[3] = pkm.AltForm < 10 ? $"00{pkm.AltForm}" : $"0{pkm.AltForm}";
            baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
            baseLink[5] = canGmax ? "g" : "n";
            baseLink[6] = "0000000" + (pkm.Species == (int)Species.Alcremie ? alcremieDeco : 0);
            baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";

            return string.Join("_", baseLink);
        }

        private async Task EmbedUtil(EmbedBuilder embed, string name, string value)
        {
            var splitName = name.Split(new string[] { "&^&" }, StringSplitOptions.None);
            var splitValue = value.Split(new string[] { "&^&" }, StringSplitOptions.None);
            for (int i = 0; i < splitName.Length; i++)
            {
                embed.AddField(x =>
                {
                    x.Name = splitName[i];
                    x.Value = splitValue[i];
                    x.IsInline = false;
                });
            }

            await Context.Message.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        private void CatchIncrement(List<string> content, string user, out int catchID)
        {
            int.TryParse(content[0].Split('_')[0], out catchID);
            catchID++;
            content[0] = catchID.ToString();
            File.WriteAllText($"TradeCord\\{user}.txt", string.Join(",", content));
        }
    }
}
