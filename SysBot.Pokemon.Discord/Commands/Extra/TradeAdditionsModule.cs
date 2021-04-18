using PKHeX.Core;
using Discord;
using Discord.Rest;
using Discord.Commands;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SysBot.Pokemon.Discord
{
    [Summary("Generates and queues various silly trade additions")]
    public class TradeAdditionsModule : ModuleBase<SocketCommandContext>
    {
        private static TradeQueueInfo<PK8> Info => SysCordInstance.Self.Hub.Queues.Info;
        public PokeTradeHub<PK8> Hub = SysCordInstance.Self.Hub;
        private readonly TradeExtensions.TCRng TCRng = new();
        private TradeExtensions.TCUserInfoRoot.TCUserInfo TCInfo = new();
        private MysteryGift? MGRngEvent = default;
        private string EggEmbedMsg = string.Empty;
        private string EventPokeType = string.Empty;
        private string DexMsg = string.Empty;

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
                var test = pool.Files;
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

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Makes the bot trade you a Pokémon holding the requested item, or Ditto if stat spread keyword is provided.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task ItemTrade([Remainder] string item)
        {
            var code = Info.GetRandomTradeCode();
            await ItemTrade(code, item).ConfigureAwait(false);
        }

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Makes the bot trade you a Pokémon holding the requested item.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task ItemTrade([Summary("Trade Code")] int code, [Remainder] string item)
        {
            Species species = Info.Hub.Config.Trade.ItemTradeSpecies == Species.None ? Species.Delibird : Info.Hub.Config.Trade.ItemTradeSpecies;
            var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((int)species, 2, 8)} @ {item.Trim()}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo(8);
            var pkm = sav.GetLegal(template, out var result);
            pkm = PKMConverter.ConvertToType(pkm, typeof(PK8), out _) ?? pkm;
            if (pkm.HeldItem == 0)
            {
                await ReplyAsync($"{Context.User.Username}, the item you entered wasn't recognized.").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);
            if (Info.Hub.Config.Trade.Memes && await TrollAsync(Context, pkm is not PK8 || !la.Valid, template).ConfigureAwait(false))
                return;
            else if (pkm is not PK8 || !la.Valid)
            {
                var reason = result == "Timeout" ? "That set took too long to generate." : "I wasn't able to create something from that.";
                var imsg = $"Oops! {reason} Here's my best attempt for that {species}!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }

            pkm.ResetPartyStats();
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, (PK8)pkm, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }

        [Command("dittoTrade")]
        [Alias("dt", "ditto")]
        [Summary("Makes the bot trade you a Ditto with a requested stat spread and language.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task DittoTrade([Summary("A combination of \"ATK/SPA/SPE\" or \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
        {
            var code = Info.GetRandomTradeCode();
            await DittoTrade(code, keyword, language, nature).ConfigureAwait(false);
        }

        [Command("dittoTrade")]
        [Alias("dt", "ditto")]
        [Summary("Makes the bot trade you a Ditto with a requested stat spread and language.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task DittoTrade([Summary("Trade Code")] int code, [Summary("A combination of \"ATK/SPA/SPE\" or \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
        {
            keyword = keyword.ToLower().Trim();
            language = language.Trim().Substring(0, 1).ToUpper() + language.Trim().Substring(1).ToLower();
            nature = nature.Trim().Substring(0, 1).ToUpper() + nature.Trim().Substring(1).ToLower();
            var set = new ShowdownSet($"{keyword}(Ditto)\nLanguage: {language}\nNature: {nature}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo(8);
            var pkm = sav.GetLegal(template, out var result);
            pkm = PKMConverter.ConvertToType(pkm, typeof(PK8), out _) ?? pkm;
            TradeExtensions.DittoTrade(pkm);

            var la = new LegalityAnalysis(pkm);
            if (Info.Hub.Config.Trade.Memes && await TrollAsync(Context, pkm is not PK8 || !la.Valid, template).ConfigureAwait(false))
                return;
            else if (pkm is not PK8 || !la.Valid)
            {
                var reason = result == "Timeout" ? "That set took too long to generate." : "I wasn't able to create something from that.";
                var imsg = $"Oops! {reason} Here's my best attempt for that Ditto!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }

            pkm.ResetPartyStats();
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, (PK8)pkm, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }

        [Command("TradeCordCatch")]
        [Alias("k", "catch")]
        [Summary("Catch a random Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCord()
        {
            var user = Context.User.Id.ToString();
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            async Task<bool> FuncCatch()
            {
                TCRng.ShinyRNG += TCInfo.DexCompletionCount * 2;
                TCRng.EggShinyRNG += TCInfo.DexCompletionCount * 2;
                if (!SettingsCheck())
                    return false;

                if (!Info.Hub.Config.TradeCord.TradeCordChannels.Contains(Context.Channel.Id.ToString()) && !Info.Hub.Config.TradeCord.TradeCordChannels.Equals(""))
                {
                    await ReplyAsync($"You're typing the command in the wrong channel!").ConfigureAwait(false);
                    return false;
                }
                else if (!TradeCordCanCatch(user, out TimeSpan timeRemaining))
                {
                    var embedTime = new EmbedBuilder { Color = Color.DarkBlue };
                    var timeName = $"{Context.User.Username}, you're too quick!";
                    var timeValue = $"Please try again in {(timeRemaining.TotalSeconds < 1 ? 1 : timeRemaining.TotalSeconds):N0} {(_ = timeRemaining.TotalSeconds > 1 ? "seconds" : "second")}!";
                    await EmbedUtil(embedTime, timeName, timeValue).ConfigureAwait(false);
                    return false;
                }

                TradeCordCooldown(user);
                DateTime.TryParse(Info.Hub.Config.TradeCord.EventEnd, out DateTime endTime);
                bool ended = endTime != default && DateTime.Now > endTime;
                if (Info.Hub.Config.TradeCord.EnableEvent && !ended)
                    EventHandler();
                else
                {
                    while (TCRng.SpeciesRNG == 0)
                        TCRng.SpeciesRNG = SpeciesRand(TradeCordPK(TradeExtensions.Random.Next(1, 899), out string res), res);
                }

                List<string> trainerInfo = new();
                trainerInfo.AddRange(new string[] { TCInfo.OTName == "" ? "" : $"OT: {TCInfo.OTName}", TCInfo.OTGender == "" ? "" : $"OTGender: {TCInfo.OTGender}", TCInfo.TID == 0 ? "" : $"TID: {TCInfo.TID}",
                TCInfo.SID == 0 ? "" : $"SID: {TCInfo.SID}", TCInfo.Language == "" ? "" : $"Language: {TCInfo.Language}" });
                bool egg = CanGenerateEgg(out int evo1, out int evo2) && TCRng.EggRNG > 100 - Info.Hub.Config.TradeCord.EggRate;
                if (egg)
                {
                    if (!await EggHandler(string.Join("\n", trainerInfo), evo1, evo2).ConfigureAwait(false))
                        return false;
                }

                if (TCRng.CatchRNG >= 100 - Info.Hub.Config.TradeCord.CatchRate)
                {
                    var speciesName = SpeciesName.GetSpeciesNameGeneration(TCRng.SpeciesRNG, 2, 8);
                    var mgRng = MGRngEvent == default ? MysteryGiftRng() : MGRngEvent;
                    bool melmetalHack = TCRng.SpeciesRNG == (int)Species.Melmetal && TCRng.GmaxRNG >= 100 - Info.Hub.Config.TradeCord.GmaxRate;
                    if ((TradeExtensions.CherishOnly.Contains(TCRng.SpeciesRNG) || TCRng.CherishRng >= 100 - Info.Hub.Config.TradeCord.CherishRate || MGRngEvent != default || melmetalHack) && mgRng != default)
                    {
                        Enum.TryParse(TCInfo.OTGender, out Gender gender);
                        Enum.TryParse(TCInfo.Language, out LanguageID language);
                        var info = !trainerInfo.Contains("") ? new SimpleTrainerInfo { Gender = (int)gender, Language = (int)language, OT = TCInfo.OTName, TID = TCInfo.TID, SID = TCInfo.SID } : AutoLegalityWrapper.GetTrainerInfo(8);
                        TCRng.CatchPKM = TradeExtensions.CherishHandler(mgRng, info);
                    }

                    if (TCRng.CatchPKM.Species == 0)
                        SetHandler(speciesName, trainerInfo);

                    if (TradeExtensions.TradeEvo.Contains(TCRng.CatchPKM.Species))
                        TCRng.CatchPKM.HeldItem = 229;

                    if (!await CatchHandler(speciesName).ConfigureAwait(false))
                        return false;
                }
                else await FailedCatchHandler().ConfigureAwait(false);

                if (egg || TCRng.CatchRNG >= 100 - Info.Hub.Config.TradeCord.CatchRate)
                    await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
                return true;
            }

            if (!await FuncCatch().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        [Command("TradeCord")]
        [Alias("tc")]
        [Summary("Trade a caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeForTradeCord([Summary("Trade Code")] int code, [Summary("Numerical catch ID")] string id)
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            async Task<bool> TradeFunc()
            {
                if (!int.TryParse(id, out int _id))
                {
                    await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                    return false;
                }

                var match = TCInfo.Catches.FirstOrDefault(x => x.ID == _id && !x.Traded);
                if (match == null)
                {
                    await Context.Message.Channel.SendMessageAsync("There is no Pokémon with this ID.").ConfigureAwait(false);
                    return false;
                }

                var dcfavCheck = TCInfo.Daycare1.ID == _id || TCInfo.Daycare2.ID == _id || TCInfo.Favorites.FirstOrDefault(x => x == _id) != default;
                if (dcfavCheck)
                {
                    await Context.Message.Channel.SendMessageAsync("Please remove your Pokémon from favorites and daycare before trading!").ConfigureAwait(false);
                    return false;
                }

                var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(match.Path));
                if (pkm == null)
                {
                    await Context.Message.Channel.SendMessageAsync("Oops, something happened when converting your Pokémon!").ConfigureAwait(false);
                    return false;
                }

                var la = new LegalityAnalysis(pkm);
                if (!la.Valid || !(pkm is PK8))
                {
                    await Context.Message.Channel.SendMessageAsync("Oops, I cannot trade this Pokémon!").ConfigureAwait(false);
                    return false;
                }

                match.Traded = true;
                TradeExtensions.TradeCordPath.Add(match.Path);
                await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
                var sig = Context.User.GetFavor();
                await Context.AddToQueueAsync(code, Context.User.Username, sig, (PK8)pkm, PokeRoutineType.TradeCord, PokeTradeType.TradeCord).ConfigureAwait(false);
                return true;
            }

            if (!await TradeFunc().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        [Command("TradeCord")]
        [Alias("tc")]
        [Summary("Trade a caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeForTradeCord([Summary("Numerical catch ID")] string id)
        {
            var code = Info.GetRandomTradeCode();
            await TradeForTradeCord(code, id).ConfigureAwait(false);
        }

        [Command("TradeCordCatchList")]
        [Alias("l", "list")]
        [Summary("List user's Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task PokeList([Summary("Species name of a Pokémon")][Remainder] string name)
        {
            await TradeCordParanoiaChecks(Context, false).ConfigureAwait(false);
            var filters = name.Split('=').Length == 3 ? name.Split('=')[1].ToLower().Trim() + " " + name.Split('=')[2].ToLower().Trim() : name.Split('=').Length == 2 ? name.Split('=')[1].ToLower().Trim() : "";
            name = filters != "" ? ListNameSanitize(name.Split('=')[0].Trim()) : ListNameSanitize(name);
            if (name == "")
            {
                await Context.Message.Channel.SendMessageAsync("In order to filter a Pokémon, we need to know which Pokémon to filter.").ConfigureAwait(false);
                return;
            }

            IEnumerable<TradeExtensions.TCUserInfoRoot.Catch> matches;
            var list = TCInfo.Catches.ToList();
            if (filters != "" && !filters.Contains(" ") && !filters.Contains("shiny")) // Look for name and ball
                matches = list.FindAll(x => filters.Contains(x.Ball.ToLower()) && (name == "Shinies" ? x.Shiny : name.Contains(x.Species + x.Form)) && !x.Traded);
            else if (filters != "" && !filters.Contains(" ") && filters.Contains("shiny")) // Look for name and shiny
                matches = list.FindAll(x => x.Shiny && name.Contains(x.Species + x.Form) && !x.Traded);
            else if (filters != "" && filters.Contains(" ")) // Look for name, ball, and shiny
                matches = list.FindAll(x => x.Shiny && filters.Contains(x.Ball.ToLower()) && name.Contains(x.Species + x.Form) && !x.Traded);
            else matches = list.FindAll(x => (name == "All" ? x.Species != "" : name == "Legendaries" ? TradeExtensions.Legends.Contains(SpeciesName.GetSpeciesID(x.Species)) : name == "Egg" ? x.Egg : name == "Shinies" ? x.Shiny : x.Ball == name || x.Species == name || (x.Species + x.Form == name) || x.Form.Replace("-", "") == name) && !x.Traded);

            HashSet<string> count = new(), countSh = new();
            if (name == "Shinies")
            {
                foreach (var result in matches)
                    countSh.Add($"[ID: {result.ID}] {(result.Shiny ? "★" : "")}{result.Species}{result.Form}");
            }
            else
            {
                foreach (var result in matches)
                {
                    var speciesString = $"[ID: {result.ID}] {(result.Shiny ? "★" : "")}{result.Species}{result.Form}";
                    if (result.Shiny)
                        countSh.Add(speciesString);
                    count.Add(speciesString);
                }
            }

            var entry = string.Join(", ", name == "Shinies" ? countSh.OrderBy(x => x.Split(new string[] { "] " }, StringSplitOptions.None)[1].Replace("★", "")) : count.OrderBy(x => x.Split(new string[] { "] " }, StringSplitOptions.None)[1].Replace("★", "")));
            if (entry == "")
            {
                await Context.Message.Channel.SendMessageAsync("No results found.").ConfigureAwait(false);
                return;
            }

            var listName = name == "Shinies" ? "Shiny Pokémon" : name == "All" ? "Pokémon" : name == "Egg" ? "Eggs" : "List For " + name;
            var listCount = name == "Shinies" ? $"★{countSh.Count}" : $"{count.Count}, ★{countSh.Count}";
            var msg = $"{Context.User.Username}'s {listName} [Total: {listCount}]";
            await ListUtil(msg, entry).ConfigureAwait(false);
        }

        [Command("TradeCordInfo")]
        [Alias("i", "info")]
        [Summary("Displays details for a user's Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordInfo([Summary("Numerical catch ID")] string id)
        {
            await TradeCordParanoiaChecks(Context, false).ConfigureAwait(false);
            if (!int.TryParse(id, out int _id))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                return;
            }

            var match = TCInfo.Catches.FirstOrDefault(x => x.ID == _id && !x.Traded);
            if (match == null)
            {
                await Context.Message.Channel.SendMessageAsync("Could not find this ID.").ConfigureAwait(false);
                return;
            }

            var pkm = PKMConverter.GetPKMfromBytes(File.ReadAllBytes(match.Path));
            if (pkm == null)
            {
                await Context.Message.Channel.SendMessageAsync("Oops, something happened when converting your Pokémon!").ConfigureAwait(false);
                return;
            }

            bool canGmax = new ShowdownSet(ShowdownParsing.GetShowdownText(pkm)).CanGigantamax;
            var pokeImg = TradeExtensions.PokeImg(pkm, canGmax);
            var embed = new EmbedBuilder { Color = pkm.IsShiny ? Color.Blue : Color.DarkBlue, ThumbnailUrl = pokeImg }.WithFooter(x => { x.Text = $"\n\n{TradeExtensions.DexFlavor(pkm.Species)}"; x.IconUrl = "https://i.imgur.com/nXNBrlr.png"; });
            var name = $"{Context.User.Username}'s {(match.Shiny ? "★" : "")}{match.Species}{match.Form} [ID: {match.ID}]";
            var value = $"\n\n{ReusableActions.GetFormattedShowdownText(pkm)}";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordMassRelease")]
        [Alias("mr", "massrelease")]
        [Summary("Mass releases every non-shiny and non-Ditto Pokémon or specific species if specified.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task MassRelease([Remainder] string species = "")
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            IEnumerable<TradeExtensions.TCUserInfoRoot.Catch> matches;
            var list = TCInfo.Catches.ToList();
            if (species.ToLower() == "cherish")
                matches = list.FindAll(x => !x.Traded && !x.Shiny && x.Ball == "Cherish" && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);
            else if (species.ToLower() == "shiny")
                matches = list.FindAll(x => !x.Traded && x.Shiny && x.Ball != "Cherish" && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);
            else if (species != "")
            {
                species = ListNameSanitize(species);
                matches = list.FindAll(x => !x.Traded && (species == "Shiny" ? x.Shiny : !x.Shiny) && (species == "Cherish" ? x.Ball == "Cherish" : x.Ball != "Cherish") && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default && $"{x.Species}{x.Form}".Equals(species));
            }
            else matches = list.FindAll(x => !x.Traded && !x.Shiny && x.Ball != "Cherish" && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);

            if (matches.Count() == 0)
            {
                await Context.Message.Channel.SendMessageAsync(species == "" ? "Cannot find any more non-shiny, non-Ditto, non-favorite, non-event Pokémon to release." : "Cannot find anything that could be released with the specified criteria.").ConfigureAwait(false);
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
                return;
            }

            foreach (var val in matches)
            {
                File.Delete(val.Path);
                TCInfo.Catches.Remove(val);
            }

            await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Mass Release";
            var value = species == "" ? "Every non-shiny Pokémon was released, excluding Ditto, favorites, events, and those in daycare." : $"Every {(species.ToLower() == "shiny" ? "shiny Pokémon" : species.ToLower() == "cherish" ? "event Pokémon" : $"non-shiny {species}")} was released, excluding favorites{(species.ToLower() == "cherish" ? "" : ", events,")} and those in daycare.";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordRelease")]
        [Alias("r", "release")]
        [Summary("Releases a user's specific Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Release([Summary("Numerical catch ID")] string id)
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            async Task<bool> FuncRelease()
            {
                if (!int.TryParse(id, out int _id))
                {
                    await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                    return false;
                }

                var match = TCInfo.Catches.FirstOrDefault(x => x.ID == _id && !x.Traded);
                if (match == null)
                {
                    await Context.Message.Channel.SendMessageAsync("Cannot find this Pokémon.").ConfigureAwait(false);
                    return false;
                }

                if (TCInfo.Daycare1.ID == _id || TCInfo.Daycare2.ID == _id || TCInfo.Favorites.FirstOrDefault(x => x == _id) != default)
                {
                    await Context.Message.Channel.SendMessageAsync("Cannot release a Pokémon in daycare or favorites.").ConfigureAwait(false);
                    return false;
                }

                var embed = new EmbedBuilder { Color = Color.DarkBlue };
                var name = $"{Context.User.Username}'s Release";
                var value = $"You release your {(match.Shiny ? "★" : "")}{match.Species}{match.Form}.";
                File.Delete(match.Path);
                TCInfo.Catches.Remove(match);
                await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncRelease().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        [Command("TradeCordDaycare")]
        [Alias("dc")]
        [Summary("Check what's inside the daycare.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task DaycareInfo()
        {
            await TradeCordParanoiaChecks(Context, false).ConfigureAwait(false);
            if (TCInfo.Daycare1.ID == 0 && TCInfo.Daycare2.ID == 0)
            {
                await Context.Message.Channel.SendMessageAsync("You do not have anything in daycare.").ConfigureAwait(false);
                return;
            }

            var msg = string.Empty;
            var dcSpecies1 = TCInfo.Daycare1.ID == 0 ? "" : $"[ID: {TCInfo.Daycare1.ID}] {(TCInfo.Daycare1.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8)}{TCInfo.Daycare1.Form} ({(Ball)TCInfo.Daycare1.Ball})";
            var dcSpecies2 = TCInfo.Daycare2.ID == 0 ? "" : $"[ID: {TCInfo.Daycare2.ID}] {(TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8)}{TCInfo.Daycare2.Form} ({(Ball)TCInfo.Daycare2.Ball})";

            if (TCInfo.Daycare1.ID != 0 && TCInfo.Daycare2.ID != 0)
                msg = $"{dcSpecies1}\n{dcSpecies2}{(CanGenerateEgg(out _, out _) ? "\n\nThey seem to really like each other." : "\n\nThey don't really seem to be fond of each other. Make sure they're of the same evolution tree and can be eggs!")}";
            else if (TCInfo.Daycare1.ID == 0 || TCInfo.Daycare2.ID == 0)
                msg = $"{(TCInfo.Daycare1.ID == 0 ? dcSpecies2 : dcSpecies1)}\n\nIt seems lonely.";

            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Daycare Info";
            await EmbedUtil(embed, name, msg).ConfigureAwait(false);
        }

        [Command("TradeCordDaycare")]
        [Alias("dc")]
        [Summary("Adds (or removes) Pokémon to (from) daycare.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Daycare([Summary("Action to do (withdraw, deposit)")] string action, [Summary("Catch ID or elaborate action (\"All\" if withdrawing")] string id)
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            id = id.ToLower();
            action = action.ToLower();
            async Task<bool> FuncDC()
            {
                if (!int.TryParse(id, out _) && id != "all")
                {
                    await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                    return false;
                }

                string speciesString = string.Empty;
                bool deposit = action == "d" || action == "deposit";
                bool withdraw = action == "w" || action == "withdraw";
                var match = deposit ? TCInfo.Catches.FirstOrDefault(x => x.ID == int.Parse(id) && !x.Traded) : null;
                if (deposit && match == null)
                {
                    await Context.Message.Channel.SendMessageAsync("There is no Pokémon with this ID.").ConfigureAwait(false);
                    return false;
                }

                if (withdraw)
                {
                    if (TCInfo.Daycare1.ID == 0 && TCInfo.Daycare2.ID == 0)
                    {
                        await Context.Message.Channel.SendMessageAsync("You do not have anything in daycare.").ConfigureAwait(false);
                        return false;
                    }

                    if (id != "all")
                    {
                        if (TCInfo.Daycare1.ID.Equals(int.Parse(id)))
                        {
                            speciesString = $"[ID: {TCInfo.Daycare1.ID}] {(TCInfo.Daycare1.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8)}{TCInfo.Daycare1.Form}";
                            TCInfo.Daycare1 = new();
                        }
                        else if (TCInfo.Daycare2.ID.Equals(int.Parse(id)))
                        {
                            speciesString = $"[ID: {TCInfo.Daycare2.ID}] {(TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8)}{TCInfo.Daycare2.Form}";
                            TCInfo.Daycare2 = new();
                        }
                        else
                        {
                            await Context.Message.Channel.SendMessageAsync("You do not have that Pokémon in daycare.").ConfigureAwait(false);
                            return false;
                        }
                    }
                    else
                    {
                        bool fullDC = TCInfo.Daycare1.ID != 0 && TCInfo.Daycare2.ID != 0;
                        speciesString = !fullDC ? $"[ID: {(TCInfo.Daycare1.ID != 0 ? TCInfo.Daycare1.ID : TCInfo.Daycare2.ID)}] {(TCInfo.Daycare1.ID != 0 && TCInfo.Daycare1.Shiny ? "★" : TCInfo.Daycare2.ID != 0 && TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.ID != 0 ? TCInfo.Daycare1.Species : TCInfo.Daycare2.Species, 2, 8)}{(TCInfo.Daycare1.ID != 0 ? TCInfo.Daycare1.Form : TCInfo.Daycare2.Form)}" :
                            $"[ID: {TCInfo.Daycare1.ID}] {(TCInfo.Daycare1.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8)}{TCInfo.Daycare1.Form} and [ID: {TCInfo.Daycare2.ID}] {(TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8)}{TCInfo.Daycare2.Form}";
                        TCInfo.Daycare1 = new();
                        TCInfo.Daycare2 = new();
                    }
                }
                else if (deposit && match != null)
                {
                    if (TCInfo.Daycare1.ID != 0 && TCInfo.Daycare2.ID != 0)
                    {
                        await Context.Message.Channel.SendMessageAsync("Daycare full, please withdraw something first.").ConfigureAwait(false);
                        return false;
                    }

                    var speciesStr = string.Join("", match.Species.Split('-', ' ', '’', '.'));
                    speciesStr += match.Path.Contains("Nidoran-M") ? "M" : match.Path.Contains("Nidoran-F") ? "F" : "";
                    Enum.TryParse(match.Ball, out Ball ball);
                    Enum.TryParse(speciesStr, out Species species);
                    if ((TCInfo.Daycare1.ID == 0 && TCInfo.Daycare2.ID == 0) || (TCInfo.Daycare1.ID == 0 && TCInfo.Daycare2.ID != int.Parse(id)))
                        TCInfo.Daycare1 = new() { Ball = (int)ball, Form = match.Form, ID = match.ID, Shiny = match.Shiny, Species = (int)species };
                    else if (TCInfo.Daycare2.ID == 0 && TCInfo.Daycare1.ID != int.Parse(id))
                        TCInfo.Daycare2 = new() { Ball = (int)ball, Form = match.Form, ID = match.ID, Shiny = match.Shiny, Species = (int)species };
                    else
                    {
                        await Context.Message.Channel.SendMessageAsync("You've already deposited that Pokémon to daycare.").ConfigureAwait(false);
                        return false;
                    }
                }
                else
                {
                    await Context.Message.Channel.SendMessageAsync("Invalid command.").ConfigureAwait(false);
                    return false;
                }

                await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
                var embed = new EmbedBuilder { Color = Color.DarkBlue };
                var name = $"{Context.User.Username}'s Daycare {(deposit ? "Deposit" : "Withdraw")}";
                var results = deposit && match != null ? $"Deposited your {(match.Shiny ? "★" : "")}{match.Species}{match.Form}({match.Ball}) to daycare!" : $"You withdrew your {speciesString} from the daycare.";
                await EmbedUtil(embed, name, results).ConfigureAwait(false);
                return true;
            }

            if (!await FuncDC().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        [Command("TradeCordGift")]
        [Alias("gift", "g")]
        [Summary("Gifts a Pokémon to a mentioned user.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Gift([Summary("Numerical catch ID")] string id, [Summary("User mention")] string _)
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            async Task<bool> FuncGift()
            {
                if (!int.TryParse(id, out int _int))
                {
                    await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                    return false;
                }
                else if (Context.Message.MentionedUsers.Count == 0)
                {
                    await Context.Message.Channel.SendMessageAsync("Please mention a user you're gifting a Pokémon to.").ConfigureAwait(false);
                    return false;
                }
                else if (Context.Message.MentionedUsers.First().Id == Context.User.Id)
                {
                    await Context.Message.Channel.SendMessageAsync("...Why?").ConfigureAwait(false);
                    return false;
                }

                var match = TCInfo.Catches.FirstOrDefault(x => x.ID == int.Parse(id) && !x.Traded);
                var dir = Path.Combine("TradeCord", Context.Message.MentionedUsers.First().Id.ToString());
                if (match == null)
                {
                    await Context.Message.Channel.SendMessageAsync("Cannot find this Pokémon.").ConfigureAwait(false);
                    return false;
                }
                else if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var dcfavCheck = TCInfo.Daycare1.ID == int.Parse(id) || TCInfo.Daycare2.ID == int.Parse(id) || TCInfo.Favorites.FirstOrDefault(x => x == int.Parse(id)) != default;
                if (dcfavCheck)
                {
                    await Context.Message.Channel.SendMessageAsync("Please remove your Pokémon from favorites and daycare before gifting!").ConfigureAwait(false);
                    return false;
                }

                var mentionedUser = Context.Message.MentionedUsers.First().Id;
                var receivingUser = await TradeExtensions.GetUserInfo(mentionedUser, 0, true).ConfigureAwait(false);
                HashSet<int> newIDParse = new();
                foreach (var caught in receivingUser.Catches)
                    newIDParse.Add(caught.ID);

                var newID = Indexing(newIDParse.OrderBy(x => x).ToArray());
                var newPath = $"{dir}\\{match.Path.Split('\\')[2].Replace(match.ID.ToString(), newID.ToString())}";
                File.Move(match.Path, newPath);
                receivingUser.Catches.Add(new() { Ball = match.Ball, Egg = match.Egg, Form = match.Form, ID = newID, Shiny = match.Shiny, Species = match.Species, Path = newPath, Traded = false });
                var specID = SpeciesName.GetSpeciesID(match.Species);
                string dexEntry = "";
                if (receivingUser.DexCompletionCount == 0 && !receivingUser.Dex.Contains(specID))
                {
                    receivingUser.Dex.Add(specID);
                    dexEntry = $"\n{Context.Message.MentionedUsers.First().Username} registered a new entry to the Pokédex!";
                }

                await TradeExtensions.UpdateUserInfo(receivingUser, false, true).ConfigureAwait(false);
                TCInfo.Catches.Remove(match);
                await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);

                var embed = new EmbedBuilder { Color = Color.Purple };
                var name = $"{Context.User.Username}'s Gift";
                var value = $"You gifted your {(match.Shiny ? "★" : "")}{match.Species}{match.Form} to {Context.Message.MentionedUsers.First().Username}. New ID is {newID}.{dexEntry}";
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncGift().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        [Command("TradeCordTrainerInfoSet")]
        [Alias("tis")]
        [Summary("Sets individual trainer info for caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TrainerInfoSet()
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            async Task<bool> FuncTrainerSet()
            {
                var attachments = Context.Message.Attachments;
                if (attachments.Count == 0 || attachments.Count > 1)
                {
                    await Context.Message.Channel.SendMessageAsync($"Please attach a {(attachments.Count == 0 ? "" : "single ")}file.").ConfigureAwait(false);
                    return false;
                }

                var download = await NetUtil.DownloadPKMAsync(attachments.First()).ConfigureAwait(false);
                if (!download.Success)
                {
                    await Context.Message.Channel.SendMessageAsync($"File download failed: \n{download.ErrorMessage}").ConfigureAwait(false);
                    return false;
                }

                var pkm = download.Data!;
                var la = new LegalityAnalysis(pkm);
                if (!la.Valid || !(pkm is PK8))
                {
                    await Context.Message.Channel.SendMessageAsync("Please upload a legal Gen8 Pokémon.").ConfigureAwait(false);
                    return false;
                }

                TCInfo.OTName = pkm.OT_Name;
                TCInfo.OTGender = $"{(Gender)pkm.OT_Gender}";
                TCInfo.TID = pkm.DisplayTID;
                TCInfo.SID = pkm.DisplaySID;
                TCInfo.Language = $"{(LanguageID)pkm.Language}";

                await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
                var embed = new EmbedBuilder { Color = Color.DarkBlue };
                var name = $"{Context.User.Username}'s Trainer Info";
                var value = $"\nYour trainer info was set to the following: \n**OT:** {TCInfo.OTName}\n**OTGender:** {TCInfo.OTGender}\n**TID:** {TCInfo.TID}\n**SID:** {TCInfo.SID}\n**Language:** {TCInfo.Language}";
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncTrainerSet().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        [Command("TradeCordTrainerInfo")]
        [Alias("ti")]
        [Summary("Displays currently set trainer info.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TrainerInfo()
        {
            await TradeCordParanoiaChecks(Context, false).ConfigureAwait(false);
            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s Trainer Info";
            var value = $"\n**OT:** {(TCInfo.OTName == "" ? "Not set." : TCInfo.OTName)}" +
                $"\n**OTGender:** {(TCInfo.OTGender == "" ? "Not set." : TCInfo.OTGender)}" +
                $"\n**TID:** {(TCInfo.TID == 0 ? "Not set." : TCInfo.TID)}" +
                $"\n**SID:** {(TCInfo.SID == 0 ? "Not set." : TCInfo.SID)}" +
                $"\n**Language:** {(TCInfo.Language == "" ? "Not set." : TCInfo.Language)}";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordFavorites")]
        [Alias("fav")]
        [Summary("Display favorites list.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordFavorites()
        {
            await TradeCordParanoiaChecks(Context, false).ConfigureAwait(false);
            if (TCInfo.Favorites.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync($"You don't have anything in favorites yet!").ConfigureAwait(false);
                return;
            }

            List<string> names = new();
            foreach (var fav in TCInfo.Favorites)
            {
                var match = TCInfo.Catches.FirstOrDefault(x => x.ID == fav);
                names.Add($"[ID: {match.ID}] {(match.Shiny ? "★" : "")}{match.Species}{match.Form} ({match.Ball} Ball)");
            }

            var entry = string.Join(", ", names.OrderBy(x => x.Split(new string[] { "] " }, StringSplitOptions.None)[1].Replace("★", "")));
            var msg = $"{Context.User.Username}'s Favorites";
            await ListUtil(msg, entry).ConfigureAwait(false);
        }

        [Command("TradeCordFavorites")]
        [Alias("fav")]
        [Summary("Add/Remove a Pokémon to a favorites list.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordFavorites([Summary("Catch ID")] string id)
        {
            await TradeCordParanoiaChecks(Context).ConfigureAwait(false);
            if (!int.TryParse(id, out int _id))
            {
                await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
                return;
            }

            var match = TCInfo.Catches.FirstOrDefault(x => x.ID == _id && !x.Traded);
            if (match == null)
            {
                await Context.Message.Channel.SendMessageAsync("Cannot find this Pokémon.").ConfigureAwait(false);
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
                return;
            }

            var fav = TCInfo.Favorites.FirstOrDefault(x => x == _id);
            if (fav == default)
            {
                TCInfo.Favorites.Add(_id);
                await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, added your {(match.Shiny ? "★" : "")}{match.Species}{match.Form} to favorites!").ConfigureAwait(false);
            }
            else if (fav == _id)
            {
                TCInfo.Favorites.Remove(fav);
                await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, removed your {(match.Shiny ? "★" : "")}{match.Species}{match.Form} from favorites!").ConfigureAwait(false);
            }
            await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
        }

        [Command("TradeCordDex")]
        [Alias("dex")]
        [Summary("Show missing dex entries and dex stats.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordDex([Summary("Optional parameter \"Missing\" for missing entries.")] string mode = "")
        {
            await TradeCordParanoiaChecks(Context, false).ConfigureAwait(false);
            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            var name = $"{Context.User.Username}'s {(mode.ToLower() == "missing" ? "Missing Entries" : "Dex Info")}";
            var value = $"\n**Pokédex:** {TCInfo.Dex.Count}/664" +
                $"\n**Pokédex Completion Count:** {TCInfo.DexCompletionCount}";
            if (mode.ToLower() == "missing")
            {
                List<string> missing = new();
                var indexes = Zukan8.GetRawIndexes(PersonalTable.SWSH, 3);
                foreach (var entry in indexes)
                {
                    if (!TCInfo.Dex.Contains(entry.Species))
                        missing.Add(SpeciesName.GetSpeciesNameGeneration(entry.Species, 2, 8));
                }

                var missingForeign = TradeExtensions.Foreign.Except(TCInfo.Dex);
                foreach (var entry in missingForeign)
                    missing.Add(SpeciesName.GetSpeciesNameGeneration(entry, 2, 8));

                missing = missing.Distinct().ToList();
                value = string.Join(", ", missing.OrderBy(x => x));
            }
            await ListUtil(name, value).ConfigureAwait(false);
        }

        private void TradeCordDump(string subfolder, PK8 pk, out int index)
        {
            var dir = Path.Combine("TradeCord", subfolder);
            Directory.CreateDirectory(dir);
            var speciesName = SpeciesName.GetSpeciesNameGeneration(pk.Species, 2, 8);
            var form = TradeExtensions.FormOutput(pk.Species, pk.Form, out _);
            if (speciesName.Contains("Nidoran"))
            {
                speciesName = speciesName.Remove(speciesName.Length - 1);
                form = pk.Species == (int)Species.NidoranF ? "-F" : "-M";
            }

            var array = Directory.GetFiles(dir).Where(x => x.Contains(".pk")).Select(x => int.Parse(x.Split('\\')[2].Split('-', '_')[0].Replace("★", "").Trim())).ToArray();
            array = array.OrderBy(x => x).ToArray();
            index = Indexing(array);
            var newname = (pk.IsShiny ? "★" + index.ToString() : index.ToString()) + $"_{(Ball)pk.Ball}" + " - " + speciesName + form + $"{(pk.IsEgg ? " (Egg)" : "")}" + ".pk8";
            var fn = Path.Combine(dir, Util.CleanFileName(newname));
            File.WriteAllBytes(fn, pk.DecryptedPartyData);
            TCInfo.Catches.Add(new() { Species = speciesName, Ball = ((Ball)pk.Ball).ToString(), Egg = pk.IsEgg, Form = form, ID = index, Path = fn, Shiny = pk.IsShiny, Traded = false });
        }

        private int Indexing(int[] array)
        {
            var i = 0;
            return array.Where(x => x > 0).Distinct().OrderBy(x => x).Any(x => x != (i += 1)) ? i : i + 1;
        }

        private void TradeCordCooldown(string id)
        {
            if (Info.Hub.Config.TradeCord.TradeCordCooldown > 0)
            {
                var line = TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(id));
                if (line != default)
                    TradeExtensions.TradeCordCooldown.Remove(TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(id)));
                TradeExtensions.TradeCordCooldown.Add($"{id},{DateTime.Now}");
            }
        }

        private bool TradeCordCanCatch(string user, out TimeSpan timeRemaining)
        {
            if (Info.Hub.Config.TradeCord.TradeCordCooldown < 0)
                Info.Hub.Config.TradeCord.TradeCordCooldown = 0;

            var line = TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(user));
            DateTime.TryParse(line != default ? line.Split(',')[1] : "", out DateTime time);
            var timer = time.AddSeconds(Info.Hub.Config.TradeCord.TradeCordCooldown);
            timeRemaining = timer - DateTime.Now;
            if (DateTime.Now < timer)
                return false;

            return true;
        }

        private async Task TradeCordParanoiaChecks(SocketCommandContext context, bool update = true)
        {
            var user = context.User.Id.ToString();
            if (!Directory.Exists("TradeCord") || !Directory.Exists($"TradeCord\\Backup\\{user}"))
            {
                Directory.CreateDirectory($"TradeCord\\{user}");
                Directory.CreateDirectory($"TradeCord\\Backup\\{user}");
            }

            TCInfo = await TradeExtensions.GetUserInfo(Context.User.Id, Hub.Config.TradeCord.ConfigUpdateInterval).ConfigureAwait(false);
            var traded = TCInfo.Catches.ToList().FindAll(x => x.Traded);
            var tradeSignal = TradeExtensions.TradeCordPath.FirstOrDefault(x => x.Contains(TCInfo.UserID.ToString()));
            if (traded.Count != 0 && tradeSignal == default)
            {
                foreach (var trade in traded)
                {
                    if (!File.Exists(trade.Path))
                        TCInfo.Catches.Remove(trade);
                    else trade.Traded = false;
                }
                await TradeExtensions.UpdateUserInfo(TCInfo).ConfigureAwait(false);
            }

            if (!update)
                TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
        }

        private bool SettingsCheck()
        {
            if (!Hub.Config.Legality.AllowBatchCommands)
                Hub.Config.Legality.AllowBatchCommands = true;

            if (!Hub.Config.Legality.AllowTrainerDataOverride)
                Hub.Config.Legality.AllowTrainerDataOverride = true;

            List<int> rateCheck = new();
            IEnumerable<int> p = new[] { Info.Hub.Config.TradeCord.CatchRate, Info.Hub.Config.TradeCord.CherishRate, Info.Hub.Config.TradeCord.EggRate, Info.Hub.Config.TradeCord.GmaxRate, Info.Hub.Config.TradeCord.SquareShinyRate, Info.Hub.Config.TradeCord.StarShinyRate };
            rateCheck.AddRange(p);
            if (rateCheck.Any(x => x < 0 || x > 100))
            {
                Base.LogUtil.LogInfo("TradeCord settings cannot be less than zero or more than 100.", "Error");
                return false;
            }
            return true;
        }

        private bool CanGenerateEgg(out int evo1, out int evo2)
        {
            evo1 = evo2 = 0;
            if (TCInfo.Daycare1.Species == 0 || TCInfo.Daycare2.Species == 0)
                return false;

            var pkm1 = AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8))), out _);
            evo1 = EvolutionTree.GetEvolutionTree(8).GetValidPreEvolutions(pkm1, 100).LastOrDefault().Species;
            var pkm2 = AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8))), out _);
            evo2 = EvolutionTree.GetEvolutionTree(8).GetValidPreEvolutions(pkm2, 100).LastOrDefault().Species;

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
                if (name.Contains("-"))
                    name = name.Split('-')[0] + "-" + name.Split('-')[1].Substring(0, 1).ToUpper() + name.Split('-')[1].Substring(1);
            }

            return name;
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
            var embed = new EmbedBuilder { Color = Color.DarkBlue }.AddField(x => { x.Name = nameMsg; x.IsInline = false; }).WithFooter(x => { x.IconUrl = "https://i.imgur.com/nXNBrlr.png"; });

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
                    embed.Fields[0].Value = pageContent[page];
                    embed.Footer.Text = $"Page {page + 1} of {pageContent.Count}";
                    await msg.RemoveReactionAsync(reactions[0], UserReactionBack);
                    await msg.ModifyAsync(msg => msg.Embed = embed.Build()).ConfigureAwait(false);
                    sw.Restart();
                }
                else if (UserReactionForward != null && page < pageContent.Count - 1)
                {
                    page++;
                    embed.Fields[0].Value = pageContent[page];
                    embed.Footer.Text = $"Page {page + 1 } of {pageContent.Count}";
                    await msg.RemoveReactionAsync(reactions[1], UserReactionForward);
                    await msg.ModifyAsync(msg => msg.Embed = embed.Build()).ConfigureAwait(false);
                    sw.Restart();
                }
            }
            await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
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

        private void SetHandler(string speciesName, List<string> trainerInfo)
        {
            string formHack = string.Empty;
            var formEdgeCaseRng = TradeExtensions.Random.Next(11);
            string[] poipoleRng = { "Poke", "Beast" };
            string[] mewOverride = { ".Version=34", ".Version=3" };
            int[] ignoreForm = { 382, 383, 646, 649, 716, 717, 773, 778, 800, 845, 875, 877, 888, 889, 890, 898 };
            Shiny shiny = TCRng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.SquareShinyRate ? Shiny.AlwaysSquare : TCRng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.StarShinyRate ? Shiny.AlwaysStar : Shiny.Never;
            string shinyType = shiny == Shiny.AlwaysSquare ? "\nShiny: Square" : shiny == Shiny.AlwaysStar ? "\nShiny: Star" : "";
            string gameVer = TCRng.SpeciesRNG switch
            {
                (int)Species.Exeggutor or (int)Species.Marowak => _ = "\n.Version=33",
                (int)Species.Mew => _ = shinyType != "" ? $"\n{mewOverride[TradeExtensions.Random.Next(2)]}" : "",
                _ => "",
            };

            if (!ignoreForm.Contains(TCRng.SpeciesRNG))
            {
                TradeExtensions.FormOutput(TCRng.SpeciesRNG, 0, out string[] forms);
                formHack = TCRng.SpeciesRNG switch
                {
                    (int)Species.Meowstic or (int)Species.Indeedee => _ = formEdgeCaseRng < 5 ? "-M" : "-F",
                    (int)Species.NidoranF or (int)Species.NidoranM => _ = TCRng.SpeciesRNG == (int)Species.NidoranF ? "-F" : "-M",
                    (int)Species.Sinistea or (int)Species.Polteageist => _ = formEdgeCaseRng < 5 ? "" : "-Antique",
                    (int)Species.Pikachu => _ = formEdgeCaseRng < 5 ? "" : TradeExtensions.PartnerPikachuHeadache[TradeExtensions.Random.Next(TradeExtensions.PartnerPikachuHeadache.Length)],
                    (int)Species.Dracovish or (int)Species.Dracozolt => _ = formEdgeCaseRng < 5 ? "" : "\nAbility: Sand Rush",
                    (int)Species.Arctovish or (int)Species.Arctozolt => _ = formEdgeCaseRng < 5 ? "" : "\nAbility: Slush Rush",
                    (int)Species.Zygarde => "-" + forms[TradeExtensions.Random.Next(forms.Length - 1)],
                    (int)Species.Giratina => _ = formEdgeCaseRng < 5 ? "" : "-Origin @ Griseous Orb",
                    _ => EventPokeType == "" ? "-" + forms[TradeExtensions.Random.Next(forms.Length)] : EventPokeType == "Base" ? "" : "-" + forms[int.Parse(EventPokeType)],
                };
            }

            bool hatchu = TCRng.SpeciesRNG == 25 && formHack != "" && formHack != "-Partner";
            string ballRng = TCRng.SpeciesRNG switch
            {
                (int)Species.Poipole or (int)Species.Naganadel => $"\nBall: {poipoleRng[TradeExtensions.Random.Next(poipoleRng.Length)]}",
                (int)Species.Meltan or (int)Species.Melmetal => $"\nBall: {TradeExtensions.LGPEBalls[TradeExtensions.Random.Next(TradeExtensions.LGPEBalls.Length)]}",
                (int)Species.Dracovish or (int)Species.Dracozolt or (int)Species.Arctovish or (int)Species.Arctozolt => _ = formEdgeCaseRng < 5 ? $"\nBall: Poke" : $"\nBall: {(Ball)TradeExtensions.Random.Next(1, 26)}",
                (int)Species.Treecko or (int)Species.Torchic or (int)Species.Mudkip => $"\nBall: {(Ball)TradeExtensions.Random.Next(2, 27)}",
                (int)Species.Pikachu => "\nBall: Poke",
                _ => TradeExtensions.Pokeball.Contains(TCRng.SpeciesRNG) ? "\nBall: Poke" : $"\nBall: {(Ball)TradeExtensions.Random.Next(1, 27)}",
            };

            ballRng = ballRng.Contains("Cherish") ? ballRng.Replace("Cherish", "Poke") : ballRng;
            if (TradeExtensions.ShinyLockCheck(TCRng.SpeciesRNG, ballRng, formHack != "") || hatchu)
                shinyType = "";

            var set = new ShowdownSet($"{speciesName}{formHack}{ballRng}{shinyType}\n{string.Join("\n", trainerInfo)}{gameVer}");
            if (set.CanToggleGigantamax(set.Species, set.Form) && TCRng.GmaxRNG >= 100 - Info.Hub.Config.TradeCord.GmaxRate)
                set.CanGigantamax = true;

            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo(8);
            TCRng.CatchPKM = (PK8)sav.GetLegal(template, out _);
            TradeExtensions.RngRoutine(TCRng.CatchPKM, template, shiny);
        }

        private async Task<bool> EggHandler(string trainerInfo, int evo1, int evo2)
        {
            bool star = false, square = false;
            if (TCRng.EggShinyRNG + (TCInfo.Daycare1.Shiny && TCInfo.Daycare2.Shiny ? 10 : 0) >= 100 - Info.Hub.Config.TradeCord.SquareShinyRate)
                square = true;
            else if (TCRng.EggShinyRNG + (TCInfo.Daycare1.Shiny && TCInfo.Daycare2.Shiny ? 10 : 0) >= 100 - Info.Hub.Config.TradeCord.StarShinyRate)
                star = true;

            TCRng.EggPKM = (PK8)TradeExtensions.EggRngRoutine(TCInfo, trainerInfo, evo1, evo2, star, square);
            var eggSpeciesName = SpeciesName.GetSpeciesNameGeneration(TCRng.EggPKM.Species, 2, 8);
            var eggForm = TradeExtensions.FormOutput(TCRng.EggPKM.Species, TCRng.EggPKM.Form, out _);
            var laEgg = new LegalityAnalysis(TCRng.EggPKM);
            if (!(TCRng.EggPKM is PK8) || !laEgg.Valid || !TCRng.EggPKM.IsEgg)
            {
                await Context.Channel.SendPKMAsync(TCRng.EggPKM, $"Something went wrong!\n{ReusableActions.GetFormattedShowdownText(TCRng.EggPKM)}").ConfigureAwait(false);
                return false;
            }

            TCRng.EggPKM.ResetPartyStats();
            TCInfo.CatchCount++;
            TradeCordDump(TCInfo.UserID.ToString(), TCRng.EggPKM, out int indexEgg);
            EggEmbedMsg = $"&^&You got " + $"{(TCRng.EggPKM.IsShiny ? "a **shiny egg**" : "an egg")}" +
                        $" from the daycare! Welcome, {(TCRng.EggPKM.IsShiny ? "**" + eggSpeciesName + eggForm + $" [ID: {indexEgg}]**" : eggSpeciesName + eggForm + $" [ID: {indexEgg}]")}!";
            if (TCInfo.DexCompletionCount < 5)
                DexCount(true);

            EggEmbedMsg += DexMsg;
            return true;
        }

        private void EventHandler()
        {
            string type = string.Empty;
            bool match;
            do
            {
                while (TCRng.SpeciesRNG == 0)
                    TCRng.SpeciesRNG = SpeciesRand(TradeCordPK(TradeExtensions.Random.Next(1, 899), out string res), res);

                if (Info.Hub.Config.TradeCord.PokeEventType == PokeEventType.EventPoke)
                    MGRngEvent = MysteryGiftRng();

                if (Info.Hub.Config.TradeCord.PokeEventType != PokeEventType.Legends && Info.Hub.Config.TradeCord.PokeEventType != PokeEventType.EventPoke && Info.Hub.Config.TradeCord.PokeEventType != PokeEventType.PikaClones)
                {
                    var temp = TradeCordPK(TCRng.SpeciesRNG, out _);
                    for (int i = 0; i < temp.PersonalInfo.FormCount; i++)
                    {
                        temp.Form = i;
                        type = GameInfo.Strings.Types[temp.PersonalInfo.Type1] == Info.Hub.Config.TradeCord.PokeEventType.ToString() ? GameInfo.Strings.Types[temp.PersonalInfo.Type1] : GameInfo.Strings.Types[temp.PersonalInfo.Type2] == Info.Hub.Config.TradeCord.PokeEventType.ToString() ? GameInfo.Strings.Types[temp.PersonalInfo.Type2] : "";
                        EventPokeType = type != "" ? $"{temp.Form}" : "";
                        if (EventPokeType != "")
                            break;
                    }
                }

                match = Info.Hub.Config.TradeCord.PokeEventType switch
                {
                    PokeEventType.Legends => TradeExtensions.Legends.Contains(TCRng.SpeciesRNG),
                    PokeEventType.PikaClones => TradeExtensions.PikaClones.Contains(TCRng.SpeciesRNG),
                    PokeEventType.EventPoke => MGRngEvent != default,
                    _ => type == Info.Hub.Config.TradeCord.PokeEventType.ToString(),
                };
                if (!match)
                    TCRng.SpeciesRNG = 0;
            }
            while (!match);
        }

        private MysteryGift? MysteryGiftRng()
        {
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == TCRng.SpeciesRNG).ToList();
            mg.RemoveAll(x => x.GetDescription().Count() < 3);
            MysteryGift? mgRng = default;
            if (mg.Count > 0)
            {
                if (TCRng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.SquareShinyRate || TCRng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.StarShinyRate)
                {
                    var mgSh = mg.FindAll(x => x.IsShiny);
                    mgRng = mgSh.Count > 0 ? mgSh.ElementAt(TradeExtensions.Random.Next(mgSh.Count)) : mg.ElementAt(TradeExtensions.Random.Next(mg.Count));
                }
                else mgRng = mg.ElementAt(TradeExtensions.Random.Next(mg.Count));
            }

            return mgRng;
        }

        private async Task<bool> CatchHandler(string speciesName)
        {
            var la = new LegalityAnalysis(TCRng.CatchPKM);
            var invalid = !(TCRng.CatchPKM is PK8) || !la.Valid || TCRng.SpeciesRNG != TCRng.CatchPKM.Species;
            if (invalid)
            {
                await Context.Channel.SendPKMAsync(TCRng.CatchPKM, $"Something went wrong!\n{ReusableActions.GetFormattedShowdownText(TCRng.CatchPKM)}").ConfigureAwait(false);
                return false;
            }

            var nidoranGender = string.Empty;
            if (TCRng.SpeciesRNG == 32 || TCRng.SpeciesRNG == 29)
            {
                nidoranGender = speciesName.Last().ToString();
                speciesName = speciesName.Remove(speciesName.Length - 1);
            }

            TCInfo.CatchCount++;
            TCRng.CatchPKM.ResetPartyStats();
            TradeCordDump(TCInfo.UserID.ToString(), TCRng.CatchPKM, out int index);
            var form = nidoranGender != string.Empty ? nidoranGender : TradeExtensions.FormOutput(TCRng.CatchPKM.Species, TCRng.CatchPKM.Form, out _);
            var pokeImg = TradeExtensions.PokeImg(TCRng.CatchPKM, TCRng.CatchPKM.CanGigantamax);
            var ballImg = $"https://serebii.net/itemdex/sprites/pgl/" + $"{(Ball)TCRng.CatchPKM.Ball}ball".ToLower() + ".png";
            var embed = new EmbedBuilder { Color = (TCRng.CatchPKM.IsShiny && TCRng.CatchPKM.FatefulEncounter) || TCRng.CatchPKM.ShinyXor == 0 ? Color.Gold : TCRng.CatchPKM.ShinyXor <= 16 ? Color.LightOrange : Color.Teal, ImageUrl = pokeImg, ThumbnailUrl = ballImg };
            var catchName = $"{Context.User.Username}'s Catch [#{TCInfo.CatchCount}]" + "&^&\nResults" + $"{(EggEmbedMsg != string.Empty ? "&^&\nEggs" : "")}";
            var catchMsg = $"You threw {(TCRng.CatchPKM.Ball == 2 ? "an" : "a")} {(Ball)TCRng.CatchPKM.Ball} Ball at a {(TCRng.CatchPKM.IsShiny ? "**shiny** wild **" + speciesName + form + "**" : "wild " + speciesName + form)}..." +
                $"&^&Success! It put up a fight, but you caught {(TCRng.CatchPKM.IsShiny ? "**" + speciesName + form + $" [ID: {index}]**" : speciesName + form + $" [ID: {index}]")}!";
            if (TCInfo.DexCompletionCount < 5)
                DexCount(EggEmbedMsg != "");

            catchMsg += DexMsg;
            catchMsg += EggEmbedMsg;
            await EmbedUtil(embed, catchName, catchMsg).ConfigureAwait(false);
            return true;
        }

        private async Task FailedCatchHandler()
        {
            var spookyRng = TradeExtensions.Random.Next(101);
            var imgRng = TradeExtensions.Random.Next(1, 3);
            string imgGarf = "https://i.imgur.com/BOb6IbW.png";
            string imgConk = "https://i.imgur.com/oSUQhYv.png";
            var ball = (Ball)TradeExtensions.Random.Next(2, 26);
            var embedFail = new EmbedBuilder { Color = Color.Teal, ImageUrl = spookyRng >= 90 && imgRng == 1 ? imgGarf : spookyRng >= 90 && imgRng == 2 ? imgConk : "" };
            var failName = $"{Context.User.Username}'s Catch" + "&^&Results" + $"{(EggEmbedMsg != string.Empty ? "&^&\nEggs" : "")}";
            var failMsg = $"You threw {(ball == Ball.Ultra ? "an" : "a")} {(ball == Ball.Cherish ? Ball.Poke : ball)} Ball at a wild {(spookyRng >= 90 && imgRng != 3 ? "...whatever that thing is" : SpeciesName.GetSpeciesNameGeneration(TCRng.SpeciesRNG, 2, 8))}..." +
                $"&^&{(spookyRng >= 90 && imgRng != 3 ? "One wiggle... Two... It breaks free and stares at you, smiling. You run for dear life." : "...but it managed to escape!")}";
            if (TCInfo.DexCompletionCount < 5)
                DexCount(EggEmbedMsg != "");

            failMsg += DexMsg;
            failMsg += EggEmbedMsg;
            TradeExtensions.CommandInProgress.RemoveAll(x => x == TCInfo.UserID);
            await EmbedUtil(embedFail, failName, failMsg).ConfigureAwait(false);
        }

        private void DexCount(bool egg)
        {
            bool caught = TCRng.CatchRNG >= 100 - Info.Hub.Config.TradeCord.CatchRate && !TCInfo.Dex.Contains(TCRng.CatchPKM.Species) && TCRng.CatchPKM.Species != 0;
            bool hatched = egg && !TCInfo.Dex.Contains(TCRng.EggPKM.Species) && TCRng.EggPKM.Species != 0 && TCRng.EggPKM.Species != TCRng.CatchPKM.Species;
            if (caught)
                TCInfo.Dex.Add(TCRng.CatchPKM.Species);
            if (hatched)
                TCInfo.Dex.Add(TCRng.EggPKM.Species);
            DexMsg = caught || hatched ? " Registered to the Pokédex." : "";
            if (TCInfo.Dex.Count == 664 && TCInfo.DexCompletionCount < 5)
            {
                TCInfo.Dex.Clear();
                TCInfo.DexCompletionCount += 1;
                DexMsg += TCInfo.DexCompletionCount < 5 ? " Shiny Charm improved!" : " Shiny Charm is now fully upgraded!";
            }
        }

        public static async Task<bool> TrollAsync(SocketCommandContext context, bool invalid, IBattleTemplate set)
        {
            var rng = new Random();
            var path = Info.Hub.Config.Trade.MemeFileNames.Split(',');
            if (path.Length == 0)
                path = new string[] { "https://i.imgur.com/qaCwr09.png" }; //If memes enabled but none provided, use a default one.

            if (invalid || !ItemRestrictions.IsHeldItemAllowed(set.HeldItem, 8) || (set.Nickname.ToLower() == "egg" && !TradeExtensions.ValidEgg.Contains(set.Species)))
            {
                var msg = $"Oops! I wasn't able to create that {GameInfo.Strings.Species[set.Species]}. Here's a meme instead!\n";
                await context.Channel.SendMessageAsync($"{(invalid ? msg : "")}{path[rng.Next(path.Length)]}").ConfigureAwait(false);
                return true;
            }
            return false;
        }

        private int SpeciesRand(PK8 pk, string res) => res == "Regenerated" && (pk.FatefulEncounter || !pk.IsNicknamed) ? pk.Species : 0;
        private PK8 TradeCordPK(int species, out string res) => (PK8)AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(species, 2, 8))), out res);
    }
}