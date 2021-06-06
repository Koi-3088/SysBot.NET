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
        private readonly TradeExtensions.TCRng Rng = TradeExtensions.RandomInit();
        private TradeExtensions.TCUserInfoRoot.TCUserInfo TCInfo = new();
        private MysteryGift? MGRngEvent = default;
        private string EggEmbedMsg = string.Empty;
        private string EventPokeType = string.Empty;
        private string DexMsg = string.Empty;
        private int EggIndex = -1;

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
            if (pkm.HeldItem == 0 && !Info.Hub.Config.Trade.Memes)
            {
                await ReplyAsync($"{Context.User.Username}, the item you entered wasn't recognized.").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);
            if (Info.Hub.Config.Trade.Memes && await TrollAsync(Context, pkm is not PK8 || !la.Valid, template, true).ConfigureAwait(false))
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
            language = language.Trim().Substring(0, 1).ToUpper() + language.Trim()[1..].ToLower();
            nature = nature.Trim().Substring(0, 1).ToUpper() + nature.Trim()[1..].ToLower();
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

        [Command("screenOff")]
        [Alias("off")]
        [Summary("Turn off the console screen for specified bot(s).")]
        [RequireOwner]
        public async Task ScreenOff([Remainder] string addressesCommaSeparated)
        {
            var address = addressesCommaSeparated.Replace(" ", "").Split(',');
            var source = new System.Threading.CancellationTokenSource();
            var token = source.Token;

            foreach (var adr in address)
            {
                var bot = SysCordInstance.Runner.GetBot(adr);
                if (bot == null)
                {
                    await ReplyAsync($"No bot found with the specified address ({adr}).").ConfigureAwait(false);
                    return;
                }

                var c = bot.Bot.Connection;
                bool crlf = bot.Bot.Config.Connection.UseCRLF;
                await c.SendAsync(Base.SwitchCommand.ScreenOff(crlf), token).ConfigureAwait(false);
                await ReplyAsync($"Turned screen off for {bot.Bot.Connection.Label}.").ConfigureAwait(false);
            }
        }

        [Command("screenOn")]
        [Alias("on")]
        [Summary("Turn on the console screen for specified bot(s).")]
        [RequireOwner]
        public async Task ScreenOn([Remainder] string addressesCommaSeparated)
        {
            var address = addressesCommaSeparated.Replace(" ", "").Split(',');
            var source = new System.Threading.CancellationTokenSource();
            var token = source.Token;

            foreach (var adr in address)
            {
                var bot = SysCordInstance.Runner.GetBot(adr);
                if (bot == null)
                {
                    await ReplyAsync($"No bot found with the specified address ({adr}).").ConfigureAwait(false);
                    return;
                }

                var c = bot.Bot.Connection;
                bool crlf = bot.Bot.Config.Connection.UseCRLF;
                await c.SendAsync(Base.SwitchCommand.ScreenOn(crlf), token).ConfigureAwait(false);
                await ReplyAsync($"Turned screen on for {bot.Bot.Connection.Label}.").ConfigureAwait(false);
            }
        }

        [Command("TradeCordCatch")]
        [Alias("k", "catch")]
        [Summary("Catch a random Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCord()
        {
            async Task<bool> FuncCatch()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false) || !SettingsCheck())
                    return false;

                var userID = TCInfo.UserID.ToString();
                if (!TradeCordCanCatch(userID, out TimeSpan timeRemaining))
                {
                    var embedTime = new EmbedBuilder { Color = Color.DarkBlue };
                    var timeName = $"{Context.User.Username}, you're too quick!";
                    var timeValue = $"Please try again in {(timeRemaining.TotalSeconds < 2 ? 1 : timeRemaining.TotalSeconds):N0} {(_ = timeRemaining.TotalSeconds < 2 ? "second" : "seconds")}!";
                    await EmbedUtil(embedTime, timeName, timeValue).ConfigureAwait(false);
                    return false;
                }

                TradeCordCooldown(userID);
                if (Info.Hub.Config.TradeCord.TradeCordCooldown > 0)
                {
                    if (TradeExtensions.UserCommandTimestamps.ContainsKey(TCInfo.UserID))
                        TradeExtensions.UserCommandTimestamps[TCInfo.UserID].Add(DateTime.UtcNow);
                    else TradeExtensions.UserCommandTimestamps.Add(TCInfo.UserID, new List<DateTime> { DateTime.UtcNow });

                    if (TradeExtensions.SelfBotScanner(TCInfo.UserID, Info.Hub.Config.TradeCord.TradeCordCooldown))
                    {
                        if (await ReactionVerification().ConfigureAwait(false))
                            return false;
                    }
                }

                DateTime.TryParse(Info.Hub.Config.TradeCord.EventEnd, out DateTime endTime);
                bool ended = endTime != default && DateTime.Now > endTime;
                PerkBoostApplicator();
                bool boostProc = TCInfo.SpeciesBoost != 0 && Rng.SpeciesBoostRNG >= 100 - TCInfo.ActivePerks.FindAll(x => x == DexPerks.SpeciesBoost).Count;

                if (Info.Hub.Config.TradeCord.EnableEvent && !ended)
                    EventHandler();
                else if (boostProc)
                    Rng.SpeciesRNG = TCInfo.SpeciesBoost;

                List<string> trainerInfo = new();
                trainerInfo.AddRange(new string[] { TCInfo.OTName == "" ? "" : $"OT: {TCInfo.OTName}", TCInfo.OTGender == "" ? "" : $"OTGender: {TCInfo.OTGender}", TCInfo.TID == 0 ? "" : $"TID: {TCInfo.TID}",
                TCInfo.SID == 0 ? "" : $"SID: {TCInfo.SID}", TCInfo.Language == "" ? "" : $"Language: {TCInfo.Language}" });
                bool egg = CanGenerateEgg(out int evo1, out int evo2) && Rng.EggRNG >= 100 - Info.Hub.Config.TradeCord.EggRate;
                if (egg)
                {
                    if (!await EggHandler(string.Join("\n", trainerInfo), evo1, evo2).ConfigureAwait(false))
                        return false;
                }

                if (Rng.CatchRNG >= 100 - Info.Hub.Config.TradeCord.CatchRate)
                {
                    var speciesName = SpeciesName.GetSpeciesNameGeneration(Rng.SpeciesRNG, 2, 8);
                    var mgRng = MGRngEvent == default ? MysteryGiftRng() : MGRngEvent;
                    bool melmetalHack = Rng.SpeciesRNG == (int)Species.Melmetal && Rng.GmaxRNG >= 100 - Info.Hub.Config.TradeCord.GmaxRate;
                    if ((TradeExtensions.CherishOnly.Contains(Rng.SpeciesRNG) || Rng.CherishRNG >= 100 - Info.Hub.Config.TradeCord.CherishRate || MGRngEvent != default || melmetalHack) && mgRng != default)
                    {
                        Enum.TryParse(TCInfo.OTGender, out Gender gender);
                        Enum.TryParse(TCInfo.Language, out LanguageID language);
                        var info = !trainerInfo.Contains("") ? new SimpleTrainerInfo { Gender = (int)gender, Language = (int)language, OT = TCInfo.OTName, TID = TCInfo.TID, SID = TCInfo.SID } : AutoLegalityWrapper.GetTrainerInfo(8);
                        Rng.CatchPKM = TradeExtensions.CherishHandler(mgRng, info);
                    }

                    if (Rng.CatchPKM.Species == 0)
                        SetHandler(speciesName, trainerInfo);

                    if (TradeExtensions.TradeEvo.Contains(Rng.CatchPKM.Species))
                        Rng.CatchPKM.HeldItem = 229;

                    if (!await CatchHandler(speciesName).ConfigureAwait(false))
                        return false;
                }
                else
                {
                    await FailedCatchHandler().ConfigureAwait(false);
                    return false;
                }

                if (egg || Rng.CatchRNG >= 100 - Info.Hub.Config.TradeCord.CatchRate)
                    TradeExtensions.UpdateUserInfo(TCInfo);
                return true;
            }

            if (!await FuncCatch().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCord")]
        [Alias("tc")]
        [Summary("Trade a caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeForTradeCord([Summary("Trade Code")] int code, [Summary("Numerical catch ID")] string id)
        {
            async Task<bool> TradeFunc()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

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
                TradeExtensions.UpdateUserInfo(TCInfo);
                var sig = Context.User.GetFavor();
                await Context.AddToQueueAsync(code, Context.User.Username, sig, (PK8)pkm, PokeRoutineType.TradeCord, PokeTradeType.TradeCord).ConfigureAwait(false);
                return true;
            }

            if (!await TradeFunc().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
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
            if (!await TradeCordParanoiaChecks(false).ConfigureAwait(false))
                return;

            List<string> filters = name.Contains("=") ? name.Split('=').ToList() : new();
            if (filters.Count > 0)
            {
                filters.RemoveAt(0);
                name = name.Split('=')[0].Trim();
            }

            for (int i = 0; i < filters.Count; i++)
                filters[i] = filters[i].ToLower().Trim();

            name = ListNameSanitize(name);
            if (name == "")
            {
                await Context.Message.Channel.SendMessageAsync("In order to filter a Pokémon, we need to know which Pokémon to filter.").ConfigureAwait(false);
                return;
            }

            var catches = TCInfo.Catches.ToList();
            var ball = filters.FirstOrDefault(x => x != "shiny");
            bool shiny = filters.FirstOrDefault(x => x == "shiny") != default;
            IEnumerable<TradeExtensions.TCUserInfoRoot.Catch> matches = filters.Count switch
            {
                1 => catches.FindAll(x => (name == "All" ? x.Species != "" : name == "Legendaries" ? Enum.IsDefined(typeof(Legends), SpeciesName.GetSpeciesID(x.Species)) : name == "Egg" ? x.Egg : name == "Shinies" ? x.Shiny : (x.Species == name || (x.Species + x.Form == name) || x.Form.Replace("-", "") == name)) && (ball != default ? ball == x.Ball.ToLower() : x.Shiny) && !x.Traded),
                2 => catches.FindAll(x => (name == "All" ? x.Species != "" : name == "Legendaries" ? Enum.IsDefined(typeof(Legends), SpeciesName.GetSpeciesID(x.Species)) : name == "Egg" ? x.Egg : x.Species == name || (x.Species + x.Form == name) || x.Form.Replace("-", "") == name) && x.Shiny && ball == x.Ball.ToLower() && !x.Traded),
                _ => catches.FindAll(x => (name == "All" ? x.Species != "" : name == "Legendaries" ? Enum.IsDefined(typeof(Legends), SpeciesName.GetSpeciesID(x.Species)) : name == "Egg" ? x.Egg : name == "Shinies" ? x.Shiny : x.Ball == name || x.Species == name || (x.Species + x.Form == name) || x.Form.Replace("-", "") == name) && !x.Traded),
            };

            HashSet<string> count = new(), countSh = new();
            if (name == "Shinies")
            {
                foreach (var result in matches)
                    countSh.Add($"(__{result.ID}__) {result.Species}{result.Form}");
            }
            else
            {
                foreach (var result in matches)
                {
                    var speciesString = result.Shiny ? $"(__{result.ID}__) {result.Species}{result.Form}" : $"({result.ID}) {result.Species}{result.Form}";
                    if (result.Shiny)
                        countSh.Add(speciesString);
                    count.Add(speciesString);
                }
            }

            var entry = string.Join(", ", name == "Shinies" ? countSh.OrderBy(x => int.Parse(x.Split(' ')[0].Trim(new char[] { '(', '_', ')' }))) : count.OrderBy(x => int.Parse(x.Split(' ')[0].Trim(new char[] { '(', '_', ')' }))));
            if (entry == "")
            {
                await Context.Message.Channel.SendMessageAsync("No results found.").ConfigureAwait(false);
                return;
            }

            var listName = name == "Shinies" ? "Shiny Pokémon" : name == "All" ? "Pokémon" : name == "Egg" ? "Eggs" : $"List For {name}";
            var listCount = name == "Shinies" ? $"★{countSh.Count}" : $"{count.Count}, ★{countSh.Count}";
            var msg = $"{Context.User.Username}'s {listName} (Total: {listCount})";
            await ListUtil(msg, entry).ConfigureAwait(false);
        }

        [Command("TradeCordInfo")]
        [Alias("i", "info")]
        [Summary("Displays details for a user's Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordInfo([Summary("Numerical catch ID")] string id)
        {
            if (!await TradeCordParanoiaChecks(false).ConfigureAwait(false))
                return;

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
            var pokeImg = TradeExtensions.PokeImg(pkm, canGmax, Hub.Config.TradeCord.UseFullSizeImages);
            var embed = new EmbedBuilder { Color = pkm.IsShiny ? Color.Blue : Color.DarkBlue, ThumbnailUrl = pokeImg }.WithFooter(x => { x.Text = $"\n\n{TradeExtensions.DexFlavor(pkm.Species)}"; x.IconUrl = "https://i.imgur.com/nXNBrlr.png"; });
            var name = $"{Context.User.Username}'s {(match.Shiny ? "★" : "")}{match.Species}{match.Form} (ID: {match.ID})";
            var value = $"\n\n{ReusableActions.GetFormattedShowdownText(pkm)}";
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordMassRelease")]
        [Alias("mr", "massrelease")]
        [Summary("Mass releases every non-shiny and non-Ditto Pokémon or specific species if specified.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task MassRelease([Remainder] string species = "")
        {
            async Task<bool> FuncMassRelease()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

                IEnumerable<TradeExtensions.TCUserInfoRoot.Catch> matches;
                var list = TCInfo.Catches.ToList();
                var ballStr = species != "" ? species.Substring(0, 1).ToUpper() + species[1..].ToLower() : "None";
                bool ballRelease = Enum.TryParse(ballStr, out Ball ball);

                if (ballRelease && ball != Ball.None)
                    matches = list.FindAll(x => !x.Traded && !x.Shiny && x.Ball == ball.ToString() && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);
                else if (species.ToLower() == "shiny")
                    matches = list.FindAll(x => !x.Traded && x.Shiny && x.Ball != "Cherish" && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);
                else if (species != "")
                {
                    species = ListNameSanitize(species);
                    matches = list.FindAll(x => !x.Traded && !x.Shiny && x.Ball != "Cherish" && $"{x.Species}{x.Form}".Equals(species) && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);
                }
                else matches = list.FindAll(x => !x.Traded && !x.Shiny && x.Ball != "Cherish" && x.Species != "Ditto" && x.ID != TCInfo.Daycare1.ID && x.ID != TCInfo.Daycare2.ID && TCInfo.Favorites.FirstOrDefault(z => z == x.ID) == default);

                if (matches.Count() == 0)
                {
                    await Context.Message.Channel.SendMessageAsync(species == "" ? "Cannot find any more non-shiny, non-Ditto, non-favorite, non-event Pokémon to release." : "Cannot find anything that could be released with the specified criteria.").ConfigureAwait(false);
                    return false;
                }

                foreach (var val in matches)
                {
                    File.Delete(val.Path);
                    TCInfo.Catches.Remove(val);
                }

                if (ballRelease && ball != Ball.None)
                    species = $"Pokémon in {ball} Ball";

                TradeExtensions.UpdateUserInfo(TCInfo);
                var embed = new EmbedBuilder { Color = Color.DarkBlue };
                var name = $"{Context.User.Username}'s Mass Release";
                var value = species == "" ? "Every non-shiny Pokémon was released, excluding Ditto, favorites, events, and those in daycare." : $"Every {(species.ToLower() == "shiny" ? "shiny Pokémon" : ballStr == "Cherish" ? "non-shiny event Pokémon" : $"non-shiny {species}")} was released, excluding favorites{(ballStr == "Cherish" ? "" : ", events,")} and those in daycare.";
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncMassRelease().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordRelease")]
        [Alias("r", "release")]
        [Summary("Releases a user's specific Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Release([Summary("Numerical catch ID")] string id)
        {
            async Task<bool> FuncRelease()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

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
                TradeExtensions.UpdateUserInfo(TCInfo);
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncRelease().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordDaycare")]
        [Alias("dc")]
        [Summary("Check what's inside the daycare.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task DaycareInfo()
        {
            if (!await TradeCordParanoiaChecks(false).ConfigureAwait(false))
                return;

            if (TCInfo.Daycare1.ID == 0 && TCInfo.Daycare2.ID == 0)
            {
                await Context.Message.Channel.SendMessageAsync("You do not have anything in daycare.").ConfigureAwait(false);
                return;
            }

            var msg = string.Empty;
            var dcSpecies1 = TCInfo.Daycare1.ID == 0 ? "" : $"(ID: {TCInfo.Daycare1.ID}) {(TCInfo.Daycare1.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8)}{TCInfo.Daycare1.Form} ({(Ball)TCInfo.Daycare1.Ball})";
            var dcSpecies2 = TCInfo.Daycare2.ID == 0 ? "" : $"(ID: {TCInfo.Daycare2.ID}) {(TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8)}{TCInfo.Daycare2.Form} ({(Ball)TCInfo.Daycare2.Ball})";

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
            async Task<bool> FuncDC()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

                id = id.ToLower();
                action = action.ToLower();
                if (!int.TryParse(id, out int _id) && id != "all")
                {
                    await Context.Message.Channel.SendMessageAsync("Please enter a numerical catch ID.").ConfigureAwait(false);
                    return false;
                }

                string speciesString = string.Empty;
                bool deposit = action == "d" || action == "deposit";
                bool withdraw = action == "w" || action == "withdraw";
                var match = deposit ? TCInfo.Catches.FirstOrDefault(x => x.ID == _id && !x.Traded) : null;
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
                            speciesString = $"(ID: {TCInfo.Daycare1.ID}) {(TCInfo.Daycare1.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8)}{TCInfo.Daycare1.Form}";
                            TCInfo.Daycare1 = new();
                        }
                        else if (TCInfo.Daycare2.ID.Equals(int.Parse(id)))
                        {
                            speciesString = $"(ID: {TCInfo.Daycare2.ID}) {(TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8)}{TCInfo.Daycare2.Form}";
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
                        speciesString = !fullDC ? $"(ID: {(TCInfo.Daycare1.ID != 0 ? TCInfo.Daycare1.ID : TCInfo.Daycare2.ID)}) {(TCInfo.Daycare1.ID != 0 && TCInfo.Daycare1.Shiny ? "★" : TCInfo.Daycare2.ID != 0 && TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.ID != 0 ? TCInfo.Daycare1.Species : TCInfo.Daycare2.Species, 2, 8)}{(TCInfo.Daycare1.ID != 0 ? TCInfo.Daycare1.Form : TCInfo.Daycare2.Form)}" :
                            $"(ID: {TCInfo.Daycare1.ID}) {(TCInfo.Daycare1.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8)}{TCInfo.Daycare1.Form} and (ID: {TCInfo.Daycare2.ID}) {(TCInfo.Daycare2.Shiny ? "★" : "")}{SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8)}{TCInfo.Daycare2.Form}";
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

                TradeExtensions.UpdateUserInfo(TCInfo);
                var embed = new EmbedBuilder { Color = Color.DarkBlue };
                var name = $"{Context.User.Username}'s Daycare {(deposit ? "Deposit" : "Withdraw")}";
                var results = deposit && match != null ? $"Deposited your {(match.Shiny ? "★" : "")}{match.Species}{match.Form}({match.Ball}) to daycare!" : $"You withdrew your {speciesString} from the daycare.";
                await EmbedUtil(embed, name, results).ConfigureAwait(false);
                return true;
            }

            if (!await FuncDC().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordGift")]
        [Alias("gift", "g")]
        [Summary("Gifts a Pokémon to a mentioned user.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task Gift([Summary("Numerical catch ID")] string id, [Summary("User mention")] string _)
        {
            async Task<bool> FuncGift()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

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
                var dex = (int[])Enum.GetValues(typeof(Gen8Dex));
                var missingEntries = GetMissingDexEntries(dex, receivingUser).Count;

                if (receivingUser.DexCompletionCount == 0 || (receivingUser.DexCompletionCount < 30 && missingEntries <= 50))
                    DexCount(receivingUser, false, true, specID);

                TradeExtensions.UpdateUserInfo(receivingUser, false, true);
                TCInfo.Catches.Remove(match);
                TradeExtensions.UpdateUserInfo(TCInfo);

                var embed = new EmbedBuilder { Color = Color.Purple };
                var name = $"{Context.User.Username}'s Gift";
                var value = $"You gifted your {(match.Shiny ? "★" : "")}{match.Species}{match.Form} to {Context.Message.MentionedUsers.First().Username}. New ID is {newID}.{DexMsg}";
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncGift().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordTrainerInfoSet")]
        [Alias("tis")]
        [Summary("Sets individual trainer info for caught Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TrainerInfoSet()
        {
            async Task<bool> FuncTrainerSet()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

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

                TradeExtensions.UpdateUserInfo(TCInfo);
                var embed = new EmbedBuilder { Color = Color.DarkBlue };
                var name = $"{Context.User.Username}'s Trainer Info";
                var value = $"\nYour trainer info was set to the following: \n**OT:** {TCInfo.OTName}\n**OTGender:** {TCInfo.OTGender}\n**TID:** {TCInfo.TID}\n**SID:** {TCInfo.SID}\n**Language:** {TCInfo.Language}";
                await EmbedUtil(embed, name, value).ConfigureAwait(false);
                return true;
            }

            if (!await FuncTrainerSet().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordTrainerInfo")]
        [Alias("ti")]
        [Summary("Displays currently set trainer info.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TrainerInfo()
        {
            if (!await TradeCordParanoiaChecks(false).ConfigureAwait(false))
                return;

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
            if (!await TradeCordParanoiaChecks(false).ConfigureAwait(false))
                return;

            if (TCInfo.Favorites.Count == 0)
            {
                await Context.Message.Channel.SendMessageAsync("You don't have anything in favorites yet!").ConfigureAwait(false);
                return;
            }

            List<string> names = new();
            foreach (var fav in TCInfo.Favorites)
            {
                var match = TCInfo.Catches.FirstOrDefault(x => x.ID == fav);
                names.Add(match.Shiny ? $"(__{match.ID}__) {match.Species}{match.Form}" : $"({match.ID}) {match.Species}{match.Form}");
            }

            var entry = string.Join(", ", names.OrderBy(x => int.Parse(x.Split(' ')[0].Trim(new char[] { '(', '_', ')' }))));
            var msg = $"{Context.User.Username}'s Favorites";
            await ListUtil(msg, entry).ConfigureAwait(false);
        }

        [Command("TradeCordFavorites")]
        [Alias("fav")]
        [Summary("Add/Remove a Pokémon to a favorites list.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordFavorites([Summary("Catch ID")] string id)
        {
            async Task<bool> FuncFav()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

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
                TradeExtensions.UpdateUserInfo(TCInfo);
                return true;
            }

            if (!await FuncFav().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordDex")]
        [Alias("dex")]
        [Summary("Show missing dex entries, dex stats, boosted species.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordDex([Summary("Optional parameter \"missing\" for missing entries.")] string input = "")
        {
            if (!await TradeCordParanoiaChecks(false).ConfigureAwait(false))
                return;

            var embed = new EmbedBuilder { Color = Color.DarkBlue };
            if (TCInfo.DexCompletionCount >= 1)
                embed.WithFooter(new EmbedFooterBuilder { Text = $"You have {TCInfo.DexCompletionCount} unused {(TCInfo.DexCompletionCount == 1 ? "perk" : "perks")}!\nType \"{Info.Hub.Config.Discord.CommandPrefix}perks\" to view available perk names!" });

            var entries = (int[])Enum.GetValues(typeof(Gen8Dex));
            var name = $"{Context.User.Username}'s {(input.ToLower() == "missing" ? "Missing Entries" : "Dex Info")}";
            var speciesBoost = TCInfo.SpeciesBoost != 0 ? $"\n**Pokémon Boost:** {SpeciesName.GetSpeciesNameGeneration(TCInfo.SpeciesBoost, 2, 8)}" : "\n**Pokémon Boost:** N/A";
            var value = $"\n**Pokédex:** {TCInfo.Dex.Count}/{entries.Length}\n**Level:** {TCInfo.DexCompletionCount + TCInfo.ActivePerks.Count}{speciesBoost}";

            if (input.ToLower() == "missing")
            {
                List<string> missing = GetMissingDexEntries(entries, TCInfo);
                value = string.Join(", ", missing.OrderBy(x => x));
                await ListUtil(name, value).ConfigureAwait(false);
                return;
            }
            await EmbedUtil(embed, name, value).ConfigureAwait(false);
        }

        [Command("TradeCordDexPerks")]
        [Alias("dexperks", "perks")]
        [Summary("Display and use available Dex completion perks.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordDexPerks([Summary("Optional perk name(s) to add, or \"clear\" to remove all perks.")][Remainder] string input = "")
        {
            async Task<bool> FuncDexPerks()
            {
                if (!await TradeCordParanoiaChecks(input != "").ConfigureAwait(false))
                    return false;

                if (input == "" && (TCInfo.DexCompletionCount > 0 || TCInfo.ActivePerks.Count > 0))
                {
                    var embed = new EmbedBuilder { Color = Color.DarkBlue };
                    if (TCInfo.DexCompletionCount >= 1)
                        embed.WithFooter(new EmbedFooterBuilder { Text = $"You have {TCInfo.DexCompletionCount} unused {(TCInfo.DexCompletionCount == 1 ? "perk" : "perks")}!" });

                    var embedName = $"{Context.User.Username}'s Perk List";
                    var msg = $"**CatchBoost:** {TCInfo.ActivePerks.FindAll(x => x == DexPerks.CatchBoost).Count}\n" +
                              $"**EggBoost:** {TCInfo.ActivePerks.FindAll(x => x == DexPerks.EggRateBoost).Count}\n" +
                              $"**ShinyBoost:** {TCInfo.ActivePerks.FindAll(x => x == DexPerks.ShinyBoost).Count}\n" +
                              $"**SpeciesBoost:** {TCInfo.ActivePerks.FindAll(x => x == DexPerks.SpeciesBoost).Count}\n" +
                              $"**GmaxBoost:** {TCInfo.ActivePerks.FindAll(x => x == DexPerks.GmaxBoost).Count}\n" +
                              $"**CherishBoost:** {TCInfo.ActivePerks.FindAll(x => x == DexPerks.CherishBoost).Count}";

                    await EmbedUtil(embed, embedName, msg).ConfigureAwait(false);
                    return true;
                }
                else if (input.ToLower() == "clear")
                {
                    TCInfo.DexCompletionCount += TCInfo.ActivePerks.Count;
                    TCInfo.ActivePerks = new();
                    TCInfo.SpeciesBoost = 0;
                    TradeExtensions.UpdateUserInfo(TCInfo);
                    await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, all active perks cleared!").ConfigureAwait(false);
                    return true;
                }

                if (TCInfo.DexCompletionCount == 0)
                {
                    await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, no perks available. Unassign a perk or complete the Dex to get more!").ConfigureAwait(false);
                    return false;
                }

                List<string> perkList = input.Split(',', ' ').ToList();
                perkList.RemoveAll(x => x == "");
                if (perkList.Count > TCInfo.DexCompletionCount)
                {
                    await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, not enough points available to assign all requested perks.").ConfigureAwait(false);
                    return false;
                }

                var enumVals = (DexPerks[])Enum.GetValues(typeof(DexPerks));
                List<DexPerks> comp = new();
                for (int i = 0; i < perkList.Count; i++)
                {
                    var comparison = perkList[i].ToLower();
                    if (comparison.Contains("catch"))
                        comp.Add(DexPerks.CatchBoost);
                    else if (comparison.Contains("egg"))
                        comp.Add(DexPerks.EggRateBoost);
                    else if (comparison.Contains("shiny"))
                        comp.Add(DexPerks.ShinyBoost);
                    else if (comparison.Contains("species"))
                        comp.Add(DexPerks.SpeciesBoost);
                    else if (comparison.Contains("gmax"))
                        comp.Add(DexPerks.GmaxBoost);
                    else if (comparison.Contains("cherish"))
                        comp.Add(DexPerks.CherishBoost);
                }

                if (comp.Count == 0)
                {
                    await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, perk input was not recognized.").ConfigureAwait(false);
                    return false;
                }

                for (int i = 0; i < comp.Count; i++)
                {
                    if (TCInfo.ActivePerks.FindAll(x => x == comp[i]).Count == 5)
                    {
                        await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, {comp[i]} is maxed out. Clear your active perks to re-assign them.").ConfigureAwait(false);
                        return false;
                    }

                    TCInfo.ActivePerks.Add(comp[i]);
                    TCInfo.DexCompletionCount -= 1;
                }

                TradeExtensions.UpdateUserInfo(TCInfo);
                await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, {(perkList.Count > 1 ? "added all requested perks!" : "requested perk added!")}").ConfigureAwait(false);
                return true;
            }

            if (!await FuncDexPerks().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordSpeciesBoost")]
        [Alias("boost", "b")]
        [Summary("If set as an active perk, enter Pokémon species to boost appearance of.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTradeCord))]
        public async Task TradeCordSpeciesBoost([Remainder] string input)
        {
            async Task<bool> FuncBoost()
            {
                if (!await TradeCordParanoiaChecks().ConfigureAwait(false))
                    return false;

                if (!TCInfo.ActivePerks.Contains(DexPerks.SpeciesBoost))
                {
                    await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, the SpeciesBoost perk isn't active.").ConfigureAwait(false);
                    return false;
                }

                input = ListNameSanitize(input).Replace("'", "").Replace("-", "").Replace(" ", "");
                if (!Enum.TryParse(input, out Gen8Dex species))
                {
                    await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, entered species was not recognized.").ConfigureAwait(false);
                    return false;
                }

                TCInfo.SpeciesBoost = (int)species;
                TradeExtensions.UpdateUserInfo(TCInfo);
                await Context.Message.Channel.SendMessageAsync($"{Context.User.Username}, catch chance for {species} was slightly boosted!").ConfigureAwait(false);
                return true;
            }

            if (!await FuncBoost().ConfigureAwait(false))
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
        }

        [Command("TradeCordCommandClear")]
        [Alias("cc")]
        [Summary("Clear the mentioned user's command queue and entry in the ignore list.")]
        [RequireSudo]
        public async Task TradeCordCommandClear([Remainder] string _)
        {
            if (Context.Message.MentionedUsers.Count == 0)
            {
                await ReplyAsync("Please mention a user.").ConfigureAwait(false);
                return;
            }
            else if (Context.Message.MentionedUsers.Count > 1)
            {
                await ReplyAsync("Please mention a single user.").ConfigureAwait(false);
                return;
            }

            var usr = Context.Message.MentionedUsers.First();
            bool command = TradeExtensions.CommandInProgress.Remove(usr.Id);
            bool ignore = TradeExtensions.IgnoreList.Remove(usr.Id);
            var msg = command || ignore ? $"Removed {(command && ignore ? "a queued command and ignore entry" : command ? "a queued command" : "an ignore entry")} for {usr.Username}." : $"{usr.Username} has no queued commands or ignore entries.";
            await ReplyAsync(msg).ConfigureAwait(false);
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
                    TradeExtensions.TradeCordCooldown.RemoveWhere(x => x.Contains(id));
                TradeExtensions.TradeCordCooldown.Add($"{id},{DateTime.Now}");
            }
        }

        private bool TradeCordCanCatch(string userID, out TimeSpan timeRemaining)
        {
            var line = TradeExtensions.TradeCordCooldown.FirstOrDefault(z => z.Contains(userID));
            DateTime.TryParse(line != default ? line.Split(',')[1] : "", out DateTime time);
            var timer = time.AddSeconds(Info.Hub.Config.TradeCord.TradeCordCooldown);
            timeRemaining = timer - DateTime.Now;
            if (DateTime.Now < timer)
                return false;

            return true;
        }

        private async Task<bool> TradeCordParanoiaChecks(bool update = true)
        {
            if (!Info.Hub.Config.TradeCord.TradeCordChannels.Contains(Context.Channel.Id.ToString()) && !Info.Hub.Config.TradeCord.TradeCordChannels.Equals(""))
            {
                await ReplyAsync("You're typing the command in the wrong channel!").ConfigureAwait(false);
                return false;
            }

            var id = Context.User.Id;
            if (!Directory.Exists("TradeCord") || !Directory.Exists($"TradeCord\\Backup\\{id}"))
            {
                Directory.CreateDirectory($"TradeCord\\{id}");
                Directory.CreateDirectory($"TradeCord\\Backup\\{id}");
            }
            else if (TradeExtensions.IgnoreList.Contains(id))
            {
                await ReplyAsync("Command ignored due to suspicion of you running a script. Contact the bot owner if this is a false-positive.").ConfigureAwait(false);
                return false;
            }

            TCInfo = await TradeExtensions.GetUserInfo(id, Hub.Config.TradeCord.ConfigUpdateInterval).ConfigureAwait(false);
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
                TradeExtensions.UpdateUserInfo(TCInfo, false);
            }

            if (!update)
                TradeExtensions.CommandInProgress.Remove(TCInfo.UserID);
            return true;
        }

        private bool SettingsCheck()
        {
            if (!Hub.Config.Legality.AllowBatchCommands)
                Hub.Config.Legality.AllowBatchCommands = true;

            if (!Hub.Config.Legality.AllowTrainerDataOverride)
                Hub.Config.Legality.AllowTrainerDataOverride = true;

            if (Hub.Config.TradeCord.ConfigUpdateInterval < 30)
                Hub.Config.TradeCord.ConfigUpdateInterval = 60;

            List<int> rateCheck = new();
            IEnumerable<int> p = new[] { Info.Hub.Config.TradeCord.TradeCordCooldown, Info.Hub.Config.TradeCord.CatchRate, Info.Hub.Config.TradeCord.CherishRate, Info.Hub.Config.TradeCord.EggRate, Info.Hub.Config.TradeCord.GmaxRate, Info.Hub.Config.TradeCord.SquareShinyRate, Info.Hub.Config.TradeCord.StarShinyRate };
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
            if (TCInfo.Daycare1.ID == 0 || TCInfo.Daycare2.ID == 0)
                return false;

            var pkm1 = AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare1.Species, 2, 8))), out _);
            evo1 = EvolutionTree.GetEvolutionTree(8).GetValidPreEvolutions(pkm1, 100).LastOrDefault().Species;
            var pkm2 = AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(TCInfo.Daycare2.Species, 2, 8))), out _);
            evo2 = EvolutionTree.GetEvolutionTree(8).GetValidPreEvolutions(pkm2, 100).LastOrDefault().Species;

            if (evo1 == 132 && evo2 == 132)
                return true;
            else if (evo1 == evo2 && Enum.IsDefined(typeof(ValidEgg), evo1))
                return true;
            else if ((evo1 == 132 || evo2 == 132) && (Enum.IsDefined(typeof(ValidEgg), evo1) || Enum.IsDefined(typeof(ValidEgg), evo2)))
                return true;
            else if ((evo1 == 29 && evo2 == 32) || (evo1 == 32 && evo2 == 29))
                return true;
            else return false;
        }

        private string ListNameSanitize(string name)
        {
            if (name == "")
                return name;

            name = name.Substring(0, 1).ToUpper().Trim() + name[1..].ToLower().Trim();
            if (name.Contains("'"))
                name = name.Replace("'", "’");

            if (name.Contains('-'))
            {
                var split = name.Split('-');
                bool exceptions = split[1] == "z" || split[1] == "m" || split[1] == "f";
                name = split[0] + "-" + (split[1].Length < 2 && !exceptions ? split[1] : split[1].Substring(0, 1).ToUpper() + split[1][1..].ToLower() + (split.Length > 2 ? "-" + split[2].ToUpper() : ""));
            }

            if (name.Contains(' '))
            {
                var split = name.Split(' ');
                name = split[0] + " " + split[1].Substring(0, 1).ToUpper() + split[1][1..].ToLower();
                if (name.Contains("-"))
                    name = name.Split('-')[0] + "-" + name.Split('-')[1].Substring(0, 1).ToUpper() + name.Split('-')[1][1..];
            }

            return name;
        }

        private async Task ListUtil(string nameMsg, string entry)
        {
            List<string> pageContent = TradeExtensions.ListUtilPrep(entry);
            bool canReact = Context.Guild.CurrentUser.GetPermissions(Context.Channel as IGuildChannel).AddReactions;
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
            IEmote[] reactions = { new Emoji("⬅️"), new Emoji("➡️"), new Emoji("⬆️"), new Emoji("⬇️") };
            await msg.AddReactionsAsync(reactions).ConfigureAwait(false);
            var sw = new Stopwatch();
            var embed = new EmbedBuilder { Color = Color.DarkBlue }.AddField(x => { x.Name = nameMsg; x.IsInline = false; }).WithFooter(x => { x.IconUrl = "https://i.imgur.com/nXNBrlr.png"; });
            sw.Start();

            while (sw.ElapsedMilliseconds < 20_000)
            {
                await msg.UpdateAsync().ConfigureAwait(false);
                var react = msg.Reactions.FirstOrDefault(x => x.Value.ReactionCount > 1 && x.Value.IsMe);
                if (react.Key == default)
                    continue;

                if (react.Key.Name == reactions[0].Name || react.Key.Name == reactions[1].Name)
                {
                    var reactUsers = await msg.GetReactionUsersAsync(reactions[react.Key.Name == reactions[0].Name ? 0 : 1], 100).FlattenAsync().ConfigureAwait(false);
                    var usr = reactUsers.FirstOrDefault(x => x.Id == userId && !x.IsBot);
                    if (usr == default)
                        continue;

                    if (react.Key.Name == reactions[0].Name)
                    {
                        if (page == 0)
                            page = pageContent.Count - 1;
                        else page--;
                    }
                    else
                    {
                        if (page + 1 == pageContent.Count)
                            page = 0;
                        else page++;
                    }

                    embed.Fields[0].Value = pageContent[page];
                    embed.Footer.Text = $"Page {page + 1} of {pageContent.Count}";
                    await msg.RemoveReactionAsync(reactions[react.Key.Name == reactions[0].Name ? 0 : 1], usr);
                    await msg.ModifyAsync(msg => msg.Embed = embed.Build()).ConfigureAwait(false);
                    sw.Restart();
                }
                else if (react.Key.Name == reactions[2].Name || react.Key.Name == reactions[3].Name)
                {
                    var reactUsers = await msg.GetReactionUsersAsync(reactions[react.Key.Name == reactions[2].Name ? 2 : 3], 100).FlattenAsync().ConfigureAwait(false);
                    var usr = reactUsers.FirstOrDefault(x => x.Id == userId && !x.IsBot);
                    if (usr == default)
                        continue;

                    List<string> tempList = new();
                    foreach (var p in pageContent)
                    {
                        var split = p.Replace(", ", ",").Split(',');
                        tempList.AddRange(split);
                    }

                    var tempEntry = string.Join(", ", react.Key.Name == reactions[2].Name ? tempList.OrderBy(x => x.Split(' ')[1]) : tempList.OrderByDescending(x => x.Split(' ')[1]));
                    pageContent = TradeExtensions.ListUtilPrep(tempEntry);
                    embed.Fields[0].Value = pageContent[page];
                    embed.Footer.Text = $"Page {page + 1} of {pageContent.Count}";
                    await msg.RemoveReactionAsync(reactions[react.Key.Name == reactions[2].Name ? 2 : 3], usr);
                    await msg.ModifyAsync(msg => msg.Embed = embed.Build()).ConfigureAwait(false);
                    sw.Restart();
                }
            }
            await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
        }

        private async Task<bool> ReactionVerification()
        {
            var sw = new Stopwatch();
            IEmote[] reaction = { new Emoji("👍") };
            var msg = await Context.Channel.SendMessageAsync($"{Context.User.Username}, please react to the attached emoji in order to confirm you're not using a script.").ConfigureAwait(false);
            await msg.AddReactionsAsync(reaction).ConfigureAwait(false);
            
            sw.Start();
            while (sw.ElapsedMilliseconds < 20_000)
            {
                await msg.UpdateAsync().ConfigureAwait(false);
                var react = msg.Reactions.FirstOrDefault(x => x.Value.ReactionCount > 1 && x.Value.IsMe);
                if (react.Key == default)
                    continue;

                if (react.Key.Name == reaction[0].Name)
                {
                    var reactUsers = await msg.GetReactionUsersAsync(reaction[0], 100).FlattenAsync().ConfigureAwait(false);
                    var usr = reactUsers.FirstOrDefault(x => x.Id == Context.User.Id && !x.IsBot);
                    if (usr == default)
                        continue;

                    await msg.AddReactionAsync(new Emoji("✅")).ConfigureAwait(false);
                    return false;
                }
            }
            await msg.AddReactionAsync(new Emoji("❌")).ConfigureAwait(false);
            TradeExtensions.IgnoreList.Add(Context.User.Id);
            return true;
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
            Shiny shiny = Rng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.SquareShinyRate ? Shiny.AlwaysSquare : Rng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.StarShinyRate ? Shiny.AlwaysStar : Shiny.Never;
            string shinyType = shiny == Shiny.AlwaysSquare ? "\nShiny: Square" : shiny == Shiny.AlwaysStar ? "\nShiny: Star" : "";
            string gameVer = Rng.SpeciesRNG switch
            {
                (int)Species.Exeggutor or (int)Species.Marowak => _ = "\n.Version=33",
                (int)Species.Mew => _ = shinyType != "" ? $"\n{mewOverride[TradeExtensions.Random.Next(2)]}" : "",
                _ => "",
            };

            if (Rng.SpeciesRNG == (int)Species.NidoranF || Rng.SpeciesRNG == (int)Species.NidoranM)
                speciesName = speciesName.Remove(speciesName.Length - 1);

            if (!ignoreForm.Contains(Rng.SpeciesRNG))
            {
                TradeExtensions.FormOutput(Rng.SpeciesRNG, 0, out string[] forms);
                formHack = Rng.SpeciesRNG switch
                {
                    (int)Species.Meowstic or (int)Species.Indeedee => _ = formEdgeCaseRng < 5 ? "-M" : "-F",
                    (int)Species.NidoranF or (int)Species.NidoranM => _ = Rng.SpeciesRNG == (int)Species.NidoranF ? "-F (F)" : "-M (M)",
                    (int)Species.Sinistea or (int)Species.Polteageist => _ = formEdgeCaseRng < 5 ? "" : "-Antique",
                    (int)Species.Pikachu => _ = formEdgeCaseRng < 5 ? "" : TradeExtensions.PartnerPikachuHeadache[TradeExtensions.Random.Next(TradeExtensions.PartnerPikachuHeadache.Length)],
                    (int)Species.Dracovish or (int)Species.Dracozolt => _ = formEdgeCaseRng < 5 ? "" : "\nAbility: Sand Rush",
                    (int)Species.Arctovish or (int)Species.Arctozolt => _ = formEdgeCaseRng < 5 ? "" : "\nAbility: Slush Rush",
                    (int)Species.Zygarde => "-" + forms[TradeExtensions.Random.Next(forms.Length - 1)],
                    (int)Species.Giratina => _ = formEdgeCaseRng < 5 ? "" : "-Origin @ Griseous Orb",
                    (int)Species.Keldeo => "-Resolute",
                    _ => EventPokeType == "" ? "-" + forms[TradeExtensions.Random.Next(forms.Length)] : EventPokeType == "Base" ? "" : "-" + forms[int.Parse(EventPokeType)],
                };
            }

            bool hatchu = Rng.SpeciesRNG == 25 && formHack != "" && formHack != "-Partner";
            string ballRng = Rng.SpeciesRNG switch
            {
                (int)Species.Poipole or (int)Species.Naganadel => $"\nBall: {poipoleRng[TradeExtensions.Random.Next(poipoleRng.Length)]}",
                (int)Species.Meltan or (int)Species.Melmetal => $"\nBall: {TradeExtensions.LGPEBalls[TradeExtensions.Random.Next(TradeExtensions.LGPEBalls.Length)]}",
                (int)Species.Dracovish or (int)Species.Dracozolt or (int)Species.Arctovish or (int)Species.Arctozolt => _ = formEdgeCaseRng < 5 ? $"\nBall: Poke" : $"\nBall: {(Ball)TradeExtensions.Random.Next(1, 26)}",
                (int)Species.Treecko or (int)Species.Torchic or (int)Species.Mudkip => $"\nBall: {(Ball)TradeExtensions.Random.Next(2, 27)}",
                (int)Species.Pikachu or (int)Species.Victini or (int)Species.Celebi or (int)Species.Jirachi or (int)Species.Genesect => "\nBall: Poke",
                _ => TradeExtensions.Pokeball.Contains(Rng.SpeciesRNG) ? "\nBall: Poke" : $"\nBall: {(Ball)TradeExtensions.Random.Next(1, 27)}",
            };

            ballRng = ballRng.Contains("Cherish") ? ballRng.Replace("Cherish", "Poke") : ballRng;
            if (TradeExtensions.ShinyLockCheck(Rng.SpeciesRNG, ballRng, formHack != "") || hatchu)
            {
                shinyType = "";
                shiny = Shiny.Never;
            }

            var set = new ShowdownSet($"{speciesName}{formHack}{ballRng}{shinyType}\n{string.Join("\n", trainerInfo)}{gameVer}");
            if (set.CanToggleGigantamax(set.Species, set.Form) && Rng.GmaxRNG >= 100 - Info.Hub.Config.TradeCord.GmaxRate)
                set.CanGigantamax = true;

            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo(8);
            Rng.CatchPKM = (PK8)sav.GetLegal(template, out _);
            TradeExtensions.RngRoutine(Rng.CatchPKM, template, shiny);
        }

        private async Task<bool> EggHandler(string trainerInfo, int evo1, int evo2)
        {
            bool star = false, square = false;
            if (Rng.EggShinyRNG + (TCInfo.Daycare1.Shiny && TCInfo.Daycare2.Shiny ? 5 : 0) >= 100 - Info.Hub.Config.TradeCord.SquareShinyRate)
                square = true;
            else if (Rng.EggShinyRNG + (TCInfo.Daycare1.Shiny && TCInfo.Daycare2.Shiny ? 5 : 0) >= 100 - Info.Hub.Config.TradeCord.StarShinyRate)
                star = true;

            Rng.EggPKM = (PK8)TradeExtensions.EggRngRoutine(TCInfo, trainerInfo, evo1, evo2, star, square);
            var eggSpeciesName = SpeciesName.GetSpeciesNameGeneration(Rng.EggPKM.Species, 2, 8);
            var eggForm = TradeExtensions.FormOutput(Rng.EggPKM.Species, Rng.EggPKM.Form, out _);
            var finalEggName = eggSpeciesName + eggForm;
            var laEgg = new LegalityAnalysis(Rng.EggPKM);
            if (!(Rng.EggPKM is PK8) || !laEgg.Valid || !Rng.EggPKM.IsEgg)
            {
                await Context.Channel.SendPKMAsync(Rng.EggPKM, $"Something went wrong!\n{ReusableActions.GetFormattedShowdownText(Rng.EggPKM)}").ConfigureAwait(false);
                return false;
            }

            Rng.EggPKM.ResetPartyStats();
            TCInfo.CatchCount++;
            TradeCordDump(TCInfo.UserID.ToString(), Rng.EggPKM, out int indexEgg);
            EggIndex = indexEgg;
            EggEmbedMsg = $"&^&You got {(Rng.EggPKM.IsShiny ? "a **shiny egg**" : "an egg")} from the daycare! Welcome, {(Rng.EggPKM.IsShiny ? $"**{finalEggName}**" : $"{finalEggName}")}!";
            if (TCInfo.DexCompletionCount < 30)
                DexCount(TCInfo, true);

            EggEmbedMsg += $"\n{DexMsg}";
            return true;
        }

        private void EventHandler()
        {
            string type = string.Empty;
            var enumVals = (int[])Enum.GetValues(typeof(Gen8Dex));
            bool match;
            do
            {
                if (Info.Hub.Config.TradeCord.PokeEventType == PokeEventType.EventPoke)
                    MGRngEvent = MysteryGiftRng();

                if (Info.Hub.Config.TradeCord.PokeEventType != PokeEventType.Legends && Info.Hub.Config.TradeCord.PokeEventType != PokeEventType.EventPoke && Info.Hub.Config.TradeCord.PokeEventType != PokeEventType.PikaClones)
                {
                    var temp = TradeCordPK(Rng.SpeciesRNG);
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
                    PokeEventType.Legends => Enum.IsDefined(typeof(Legends), Rng.SpeciesRNG),
                    PokeEventType.PikaClones => TradeExtensions.PikaClones.Contains(Rng.SpeciesRNG),
                    PokeEventType.EventPoke => MGRngEvent != default,
                    _ => type == Info.Hub.Config.TradeCord.PokeEventType.ToString(),
                };
                if (!match)
                    Rng.SpeciesRNG = enumVals[TradeExtensions.Random.Next(enumVals.Length)];
            }
            while (!match);
        }

        private MysteryGift? MysteryGiftRng()
        {
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == Rng.SpeciesRNG).ToList();
            mg.RemoveAll(x => x.GetDescription().Count() < 3);
            MysteryGift? mgRng = default;
            if (mg.Count > 0)
            {
                if (Rng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.SquareShinyRate || Rng.ShinyRNG >= 100 - Info.Hub.Config.TradeCord.StarShinyRate)
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
            var la = new LegalityAnalysis(Rng.CatchPKM);
            var invalid = !(Rng.CatchPKM is PK8) || !la.Valid || Rng.SpeciesRNG != Rng.CatchPKM.Species;
            if (invalid)
            {
                await Context.Channel.SendPKMAsync(Rng.CatchPKM, $"Something went wrong!\n{ReusableActions.GetFormattedShowdownText(Rng.CatchPKM)}").ConfigureAwait(false);
                return false;
            }

            var nidoranGender = string.Empty;
            if (Rng.SpeciesRNG == 32 || Rng.SpeciesRNG == 29)
            {
                nidoranGender = speciesName.Last().ToString();
                speciesName = speciesName.Remove(speciesName.Length - 1);
            }

            TCInfo.CatchCount++;
            Rng.CatchPKM.ResetPartyStats();
            TradeCordDump(TCInfo.UserID.ToString(), Rng.CatchPKM, out int index);
            var form = nidoranGender != string.Empty ? nidoranGender : TradeExtensions.FormOutput(Rng.CatchPKM.Species, Rng.CatchPKM.Form, out _);
            var finalName = speciesName + form;
            var pokeImg = TradeExtensions.PokeImg(Rng.CatchPKM, Rng.CatchPKM.CanGigantamax, Hub.Config.TradeCord.UseFullSizeImages);
            var ballImg = $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/{((Ball)Rng.CatchPKM.Ball).ToString().ToLower()}ball.png";
            var desc = $"You threw {(Rng.CatchPKM.Ball == 2 ? "an" : "a")} {(Ball)Rng.CatchPKM.Ball} Ball at a {(Rng.CatchPKM.IsShiny ? $"**shiny** wild **{finalName}**" : $"wild {finalName}")}...";
            var catchName = $"Results{(EggEmbedMsg != string.Empty ? "&^&\nEggs" : "")}";
            var catchMsg = $"It put up a fight, but you caught {(Rng.CatchPKM.IsShiny ? $"**{finalName}**" : $"{finalName}")}!";
            if (TCInfo.DexCompletionCount < 30)
                DexCount(TCInfo, EggEmbedMsg != "");

            catchMsg += $"\n{DexMsg}";
            catchMsg += $"\n{EggEmbedMsg}";
            var author = new EmbedAuthorBuilder { Name = $"{Context.User.Username}'s Catch" };
            if (!Hub.Config.TradeCord.UseLargerPokeBalls)
            {
                author.IconUrl = ballImg;
                ballImg = "";
            }

            var footer = new EmbedFooterBuilder { Text = $"Catch {TCInfo.CatchCount} | Pokémon ID {index}{(EggIndex == -1 ? "" : $" | Egg ID {EggIndex}")}" };
            var embed = new EmbedBuilder
            {
                Color = (Rng.CatchPKM.IsShiny && Rng.CatchPKM.FatefulEncounter) || Rng.CatchPKM.ShinyXor == 0 ? Color.Gold : Rng.CatchPKM.ShinyXor <= 16 ? Color.LightOrange : Color.Teal,
                ImageUrl = pokeImg,
                ThumbnailUrl = ballImg,
                Description = desc,
                Author = author,
                Footer = footer,
            };
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
            var desc = $"You threw {(ball == Ball.Ultra ? "an" : "a")} {(ball == Ball.Cherish ? Ball.Poke : ball)} Ball at a wild {(spookyRng >= 90 ? "...whatever that thing is" : SpeciesName.GetSpeciesNameGeneration(Rng.SpeciesRNG, 2, 8))}...";
            var failName = $"Results{(EggEmbedMsg != string.Empty ? "&^&\nEggs" : "")}";
            var failMsg =  $"{(spookyRng >= 90 ? "One wiggle... Two... It breaks free and stares at you, smiling. You run for dear life." : "...but it managed to escape!")}";
            if (TCInfo.DexCompletionCount < 30)
                DexCount(TCInfo, EggEmbedMsg != "");

            failMsg += $"\n{DexMsg}";
            failMsg += $"\n{EggEmbedMsg}";
            var author = new EmbedAuthorBuilder { Name = $"{Context.User.Username}'s Catch"};
            var footer = new EmbedFooterBuilder { Text = $"{(spookyRng >= 90 ? $"But deep inside you know there is no escape... {(EggIndex != -1 ? $"Egg ID {EggIndex}" : "")}" : EggIndex != -1 ? $"Egg ID {EggIndex}" : "")}" };
            var embedFail = new EmbedBuilder
            {
                Color = Color.Teal,
                ImageUrl = spookyRng >= 90 && imgRng == 1 ? imgGarf : spookyRng >= 90 && imgRng == 2 ? imgConk : "",
                Description = desc,
                Author = author,
                Footer = footer,
            };

            await EmbedUtil(embedFail, failName, failMsg).ConfigureAwait(false);
        }

        private void DexCount(TradeExtensions.TCUserInfoRoot.TCUserInfo info, bool egg, bool gift = false, int giftSpecies = -1)
        {
            bool caught = Rng.CatchRNG >= 100 - Info.Hub.Config.TradeCord.CatchRate && !info.Dex.Contains(Rng.CatchPKM.Species) && Rng.CatchPKM.Species != 0 && !gift;
            bool hatched = egg && !info.Dex.Contains(Rng.EggPKM.Species) && Rng.EggPKM.Species != 0 && Rng.EggPKM.Species != Rng.CatchPKM.Species && !gift;
            gift = !info.Dex.Contains(giftSpecies) && giftSpecies > 0 && gift;
            if (caught)
                info.Dex.Add(Rng.CatchPKM.Species);
            if (hatched)
                info.Dex.Add(Rng.EggPKM.Species);
            if (gift)
                info.Dex.Add(giftSpecies);

            DexMsg = caught || hatched ? "Registered to the Pokédex." : gift ? $"\n{Context.Message.MentionedUsers.First().Username} registered a new entry to the Pokédex!" : "";
            if (info.Dex.Count >= 664 && info.DexCompletionCount < 30)
            {
                info.Dex.Clear();
                info.DexCompletionCount += 1;
                DexMsg += info.DexCompletionCount < 30 ? " Level increased!" : " Highest level achieved!";
            }
        }

        private void PerkBoostApplicator()
        {
            Rng.CatchRNG += TCInfo.ActivePerks.FindAll(x => x == DexPerks.CatchBoost).Count;
            Rng.CherishRNG += TCInfo.ActivePerks.FindAll(x => x == DexPerks.CherishBoost).Count * 2;
            Rng.GmaxRNG += TCInfo.ActivePerks.FindAll(x => x == DexPerks.GmaxBoost).Count * 2;
            Rng.EggRNG += TCInfo.ActivePerks.FindAll(x => x == DexPerks.EggRateBoost).Count * 2;
            Rng.ShinyRNG += TCInfo.ActivePerks.FindAll(x => x == DexPerks.ShinyBoost).Count * 2;
            Rng.EggShinyRNG += TCInfo.ActivePerks.FindAll(x => x == DexPerks.ShinyBoost).Count * 2;
        }

        public static async Task<bool> TrollAsync(SocketCommandContext context, bool invalid, IBattleTemplate set, bool itemTrade = false)
        {
            var rng = new Random();
            bool noItem = set.HeldItem == 0 && itemTrade;
            var path = Info.Hub.Config.Trade.MemeFileNames.Split(',');
            if (Info.Hub.Config.Trade.MemeFileNames == "" || path.Length == 0)
                path = new string[] { "https://i.imgur.com/qaCwr09.png" }; //If memes enabled but none provided, use a default one.

            if (invalid || !ItemRestrictions.IsHeldItemAllowed(set.HeldItem, 8) || noItem || (set.Nickname.ToLower() == "egg" && !Enum.IsDefined(typeof(ValidEgg), set.Species)))
            {
                var msg = $"{(noItem ? $"{context.User.Username}, the item you entered wasn't recognized." : $"Oops! I wasn't able to create that {GameInfo.Strings.Species[set.Species]}.")} Here's a meme instead!\n";
                await context.Channel.SendMessageAsync($"{(invalid || noItem ? msg : "")}{path[rng.Next(path.Length)]}").ConfigureAwait(false);
                return true;
            }
            return false;
        }

        private List<string> GetMissingDexEntries(int[] entries, TradeExtensions.TCUserInfoRoot.TCUserInfo info)
        {
            List<string> missing = new();
            foreach (var entry in entries)
            {
                if (!info.Dex.Contains(entry))
                    missing.Add(SpeciesName.GetSpeciesNameGeneration(entry, 2, 8));
            }
            return missing;
        }

        private PK8 TradeCordPK(int species) => (PK8)AutoLegalityWrapper.GetTrainerInfo(8).GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(SpeciesName.GetSpeciesNameGeneration(species, 2, 8))), out _);
    }
}