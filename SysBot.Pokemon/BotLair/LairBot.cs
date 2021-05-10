using System;
using PKHeX.Core;
using SysBot.Base;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    // Thanks to Anubis and Zyro for providing offsets and ideas for LairBot, and Elvis for endless testing with PinkBot!
    public class LairBot : PokeRoutineExecutor
    {
        private StopConditionSettings NewSCSettings = new();
        private readonly BotCompleteCounts Counts;
        private readonly PokeTradeHub<PK8> Hub;
        private readonly LairSettings Settings;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        private byte[] OtherItemsPouch = { 0 };
        private byte[] BallPouch = { 0 };
        private ulong MainNsoBase;
        private bool StopBot;
        private bool Upgrade;
        private bool Dmax;
        private bool Lost;
        private int Caught;
        private int DmaxEnd = -1;
        private int OldMoveIndex = 0;
        private int LairEncounterCount;
        private int CatchCount = -1;
        private int ResetCount;
        private readonly LairCount AdventureCounts = new();
        private readonly KeepPathTotals KeepPathCounts = new();
        private PK8 LairBoss = new();

        private class LairCount
        {
            public double AdventureCount { get; set; }
            public double WinCount { get; set; }
        }

        private class KeepPathTotals
        {
            public int KeepPathAdventures { get; set; }
            public int KeepPathWins { get; set; }
        }

        public LairBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            Settings = hub.Config.Lair;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
        }

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);
            Log("It's adventure time! Starting main LairBot loop.");
            Config.IterateNextRoutine();
            MainNsoBase = await SwitchConnection.GetMainNsoBaseAsync(token).ConfigureAwait(false);
            if (Settings.EnableOHKO)
                _ = Task.Run(async () => await CancellationMonitor(token).ConfigureAwait(false));
            await DoLairBot(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }

        private async Task DoLairBot(CancellationToken token)
        {
            int raidCount = 1;
            while (!token.IsCancellationRequested)
            {
                Lost = false;
                Upgrade = false;
                Dmax = false;
                if (raidCount == 1)
                {
                    Caught = 0;
                    while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                        await Click(A, 0_500, token).ConfigureAwait(false);

                    Log($"{(StopBot ? "Waiting for next Legendary Adventure... Use \"$hunt (Species)\" to select a new Legendary!" : $"Starting a new Adventure...")}");
                    if (StopBot)
                    {
                        StopBot = false;
                        return;
                    }

                    if (!await SettingsCheck(token).ConfigureAwait(false))
                        return;

                    await SetHuntedPokemon(token).ConfigureAwait(false);
                    if (!await LairSeedInjector(token).ConfigureAwait(false))
                        return;

                    ulong seed = BitConverter.ToUInt64(await Connection.ReadBytesAsync(AdventureSeedOffset, 8, token).ConfigureAwait(false), 0);
                    Log($"Here is your current Lair Seed: {seed:X16}");
                    while (true)
                    {
                        if (await LairStatusCheck(LairMenuBytes, CurrentScreenLairOffset, token).ConfigureAwait(false))
                            break;
                        await Click(A, 0_600 + Settings.MashDelay, token).ConfigureAwait(false);
                    }

                    var huntedSpecies = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNote1, 2, token).ConfigureAwait(false), 0);
                    var winRate = AdventureCounts.AdventureCount > 0 ? $" {AdventureCounts.WinCount}/{AdventureCounts.AdventureCount} adventures won." : "";

                    Log($"Starting a Solo Adventure for {(huntedSpecies == 0 ? "a random Legendary" : (LairSpecies)huntedSpecies)}!{winRate}");
                    await Task.Delay(1_500).ConfigureAwait(false);
                    await RentalRoutine(token).ConfigureAwait(false); // Select a rental.
                }

                while (!await LairStatusCheck(LairAdventurePathBytes, CurrentScreenLairOffset, token).ConfigureAwait(false)) // Delay until in path select screen.
                    await Task.Delay(0_500).ConfigureAwait(false);

                await Task.Delay(raidCount == 1 ? 9_000 : 3_000).ConfigureAwait(false); // Because map scroll is slow.

                if (Settings.EnableOHKO) // Enable dirty OHKO.
                    await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7900E81F), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);

                switch (Hub.Config.Lair.SelectPath) // Choose a path to take.
                {
                    case SelectPath.GoLeft: await Click(A, 1_000, token).ConfigureAwait(false); break;
                    case SelectPath.GoRight: await Click(DRIGHT, 1_000, token).ConfigureAwait(false); break;
                };

                while (!await IsInBattle(token).ConfigureAwait(false))
                {
                    await Click(A, 0_500, token).ConfigureAwait(false);
                    if (LairBoss.Species == 0)
                        LairBoss = await ReadUntilPresentAbsolute(await ParsePointer("[[[[[[main+26365B8]+68]+78]+88]+D08]+950]+D0", token).ConfigureAwait(false), 0_500, 0_200, token).ConfigureAwait(false);
                }

                if (raidCount == 1 && Settings.UseStopConditionsPathReset)
                {
                    if (!await LegendReset(token).ConfigureAwait(false))
                        continue;
                }

                var lairPk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (lairPk == null)
                    lairPk = new();

#pragma warning disable CS8601 // Possible null reference assignment.
                var party = new PK8[] { await ReadUntilPresent(LairPartyP1Offset, 0_500, 0_200, token).ConfigureAwait(false), await ReadUntilPresent(LairPartyP2Offset, 0_500, 0_200, token).ConfigureAwait(false), await ReadUntilPresent(LairPartyP3Offset, 0_500, 0_200, token).ConfigureAwait(false), await ReadUntilPresent(LairPartyP4Offset, 0_500, 0_200, token).ConfigureAwait(false) };
#pragma warning restore CS8601 // Possible null reference assignment.

                if (party.Any(x => x == null))
                    party = new PK8[4];

                LairEncounterCount++;
                Log($"Raid Battle {raidCount}. Encounter {LairEncounterCount}: {SpeciesName.GetSpeciesNameGeneration(lairPk.Species, 2, 8)}{TradeExtensions.FormOutput(lairPk.Species, lairPk.Form, out _)}.");
                if (party[0] != null)
                    Log($"Sending out: {SpeciesName.GetSpeciesNameGeneration(party[0].Species, 2, 8)}{TradeExtensions.FormOutput(party[0].Species, party[0].Form, out _)}.");
                else party[0] = new();

                await BattleRoutine(party, lairPk, token).ConfigureAwait(false);
                LairBotUtil.TerrainDur = -1;
                OldMoveIndex = 0;
                DmaxEnd = -1;
                Dmax = false;

                while (!await LairStatusCheck(LairCatchScreenBytes, LairMiscScreenOffset, token).ConfigureAwait(false) && !Lost)
                    await Task.Delay(1_000).ConfigureAwait(false);

                if (raidCount == 4 || Lost)
                {
                    AdventureCounts.AdventureCount++;
                    if (!Settings.InjectSeed && !Settings.EnableOHKO && !Settings.CatchLairPokémon && Settings.KeepPath)
                        KeepPathCounts.KeepPathAdventures++;
                }

                if (Lost) // We've lost the battle, exit back to main loop.
                {
                    Log($"Lost Adventure #{AdventureCounts.AdventureCount}.");
                    if (Caught > 0)
                        await Results(token).ConfigureAwait(false);
                    raidCount = 1;
                    continue;
                }

                await CatchRoutine(raidCount, token).ConfigureAwait(false);
                Log($"{(raidCount == 4 || Settings.CatchLairPokémon || Upgrade ? "Caught" : "Defeated")} {SpeciesName.GetSpeciesNameGeneration(lairPk.Species, 2, 8)}{TradeExtensions.FormOutput(lairPk.Species, lairPk.Form, out _)}.");
                if (raidCount == 4) // Final raid complete.
                {
                    if (!Settings.InjectSeed && !Settings.EnableOHKO && !Settings.CatchLairPokémon && Settings.KeepPath)
                        KeepPathCounts.KeepPathWins++;

                    AdventureCounts.WinCount++;
                    Log($"Adventure #{AdventureCounts.AdventureCount} completed.");
                    await Results(token).ConfigureAwait(false);
                    raidCount = 1;
                    continue;
                }
                raidCount++;
            }
        }

        private async Task RentalRoutine(CancellationToken token)
        {
            List<uint> RentalOfsList = new() { RentalMon1, RentalMon2, RentalMon3 };
            List<int> speedStat = new();
            List<PK8> pkList = new();
            int monIndex = -1;
            int moveIndex = -1;
            await Click(DDOWN, 0_250, token).ConfigureAwait(false);
            await Click(A, 2_000, token).ConfigureAwait(false);

            for (int i = 0; i < RentalOfsList.Count; i++)
            {
                var pk = await ReadUntilPresent(RentalOfsList[i], 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    Log("Entered the lobby too fast, correcting...");
                    await CodePink(token).ConfigureAwait(false);
                    return;
                }
                else
                {
                    pkList.Add(pk);
                    moveIndex = LairBotUtil.PriorityIndex(pk);
                    if (moveIndex != -1) // Add Ditto override because Imposter is fun?
                    {
                        monIndex = i;
                        break;
                    }
                    else if (Settings.EnableOHKO)
                        speedStat.Add(LairBotUtil.CalculateEffectiveStat(pk.IV_SPE, pk.EV_SPE, pk.PersonalInfo.SPE, pk.CurrentLevel));
                    else
                    {

                    }
                }
            }
            var speedIndex = speedStat.Count > 0 ? speedStat.IndexOf(speedStat.Max()) : 0;
            if (monIndex == -1)
                Log($"Selecting {SpeciesName.GetSpeciesNameGeneration(pkList[speedIndex].Species, 2, 8)}{TradeExtensions.FormOutput(pkList[speedIndex].Species, pkList[speedIndex].Form, out _)}.");
            else Log($"Selecting {SpeciesName.GetSpeciesNameGeneration(pkList[monIndex].Species, 2, 8)}{TradeExtensions.FormOutput(pkList[monIndex].Species, pkList[monIndex].Form, out _)} with {(Move)pkList[monIndex].Moves[moveIndex]}");
            await MoveAndRentalClicks(monIndex == -1 ? speedIndex : monIndex, token).ConfigureAwait(false);
        }

        private async Task CodePink(CancellationToken token)
        {
            await Click(B, 0_500, token).ConfigureAwait(false);
            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Task.Delay(1_000).ConfigureAwait(false);
            for (int p = 0; p < 15; p++)
            {
                if (await LairStatusCheck(LairMenuBytes, CurrentScreenLairOffset, token).ConfigureAwait(false))
                    break;
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
            await RentalRoutine(token).ConfigureAwait(false);
        }

        private async Task SelectMove(PK8[] party, PK8 lairMon, bool stuck, int turn, CancellationToken token)
        { // to-do: select certain status moves (healing/guard) based on health or condition, random otherwise if no good moves available
            var pk = party[0];
            bool dmaxEnded = Dmax && DmaxEnd == -1;
            if (dmaxEnded)
                Dmax = false;

            if (!Settings.EnableOHKO && !Dmax && pk.Species != 132)
            {
                await Click(DLEFT, 0_400, token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);
                if (await LairStatusCheck(LairDmaxMovesBytes, LairMiscScreenOffset, token).ConfigureAwait(false))
                {
                    Log(pk.CanGigantamax ? "Gigantamaxing..." : "Dynamaxing...");
                    Dmax = true;
                    DmaxEnd = turn + 3;
                }
                else await Click(DLEFT, 0_400, token).ConfigureAwait(false);
            }

            List<int> statusMoves = new();
            List<int> ppCount = new();
            var priorityMove = pk.Moves.ToList().IndexOf(pk.Moves.Intersect((IEnumerable<int>)Enum.GetValues(typeof(PriorityMoves))).FirstOrDefault());
            var dmgWeight = LairBotUtil.WeightedDamage(party, lairMon, Dmax).ToList();
            bool priority = Settings.EnableOHKO && priorityMove != -1 && dmgWeight[priorityMove] > 0 && lairMon.Ability != (int)Ability.PsychicSurge && lairMon.Ability != (int)Ability.QueenlyMajesty && lairMon.Ability != (int)Ability.Dazzling;
            for (int i = 0; i < pk.Moves.Length; i++)
            {
                var move = LairBotUtil.MoveRoot.Moves.FirstOrDefault(x => x.MoveID == pk.Moves[i]);
                ppCount.Add(await GetPPCount(priority ? priorityMove : i, token).ConfigureAwait(false));
                if (move.Category == MoveCategory.Status)
                    statusMoves.Add(i);
            }

            var bestMove = dmgWeight.IndexOf(dmgWeight.Max());
            bool movePass = false;
            while (!movePass)
            {
                var move = LairBotUtil.MoveRoot.Moves.FirstOrDefault(x => x.MoveID == pk.Moves[priority ? priorityMove : bestMove]);
                var currentPP = ppCount[priority ? priorityMove : bestMove];
                bool recoil = move.Recoil >= 206 && move.EffectSequence >= 48;
                if (currentPP == 0 || (stuck && (OldMoveIndex == (priority ? priorityMove : bestMove))) || Settings.EnableOHKO && (recoil || move.Charge) || move.MoveID == (int)Move.Belch)
                {
                    dmgWeight[priority ? priorityMove : bestMove] = 0.0;
                    bestMove = dmgWeight.Max() < 0.0 ? statusMoves[new Random().Next(statusMoves.Count)] : dmgWeight.IndexOf(dmgWeight.Max());
                    priority = false;
                    stuck = false;
                    continue;
                }
                else if (priority)
                    bestMove = priorityMove;
                movePass = true;
            }

            var finalMove = LairBotUtil.MoveRoot.Moves.FirstOrDefault(x => x.MoveID == pk.Moves[bestMove]);
            int dmaxMove = finalMove.Category != MoveCategory.Status ? (int)finalMove.Type : 18;
            Log($"Turn #{turn}: Selecting {(Dmax ? (DmaxMoves)dmaxMove : (Move)pk.Moves[bestMove])}.");
            var index = bestMove - OldMoveIndex;
            if (dmaxEnded)
                index = bestMove;
            else if (index < 0)
                index = index + OldMoveIndex + 1;

            await MoveAndRentalClicks(index, token).ConfigureAwait(false);
            OldMoveIndex = bestMove;
        }

        private async Task CheckIfUpgrade(PK8[] party, PK8 lairPk, CancellationToken token)
        {
            var playerPk = party[0];
            if (Settings.EnableOHKO)
            {
                var ourSpeed = LairBotUtil.CalculateEffectiveStat(playerPk.IV_SPE, playerPk.EV_SPE, playerPk.PersonalInfo.SPE, playerPk.CurrentLevel);
                bool noPriority = LairBotUtil.PriorityIndex(playerPk) == -1;
                var lairPkSpeed = LairBotUtil.CalculateEffectiveStat(lairPk.IV_SPE, lairPk.EV_SPE, lairPk.PersonalInfo.SPE, lairPk.CurrentLevel);
                bool lairPkPriority = LairBotUtil.PriorityIndex(lairPk) != -1;
                if ((noPriority && (lairPkSpeed > ourSpeed)) || (lairPkPriority && noPriority))
                    Upgrade = true;
            }
            else if (!Settings.EnableOHKO)
            {
                var dmgWeightPlayer = LairBotUtil.WeightedDamage(party, LairBoss, false);
                var dmgWeightLair = LairBotUtil.WeightedDamage(new PK8[] { lairPk, new() }, LairBoss, false);
                for (int i = 0; i < playerPk.Moves.Length; i++)
                {
                    if (await GetPPCount(i, token).ConfigureAwait(false) == 0)
                        dmgWeightPlayer[i] = 0.0;
                }

                if (dmgWeightLair.Max() > dmgWeightPlayer.Max())
                    Upgrade = true;
            }

            if (Upgrade && lairPk.Species != LairBoss.Species)
                Log($"Lair encounter is better than our current Pokémon. If we win the battle, going to catch and upgrade.");
        }

        private async Task BattleRoutine(PK8[] party, PK8 lairPk, CancellationToken token)
        {
            var pk = party[0];
            if (pk.Species == 132 && pk.Ability == (int)Ability.Imposter)
                pk = lairPk;
            else if (lairPk.Species == 132 && lairPk.Ability == (int)Ability.Imposter)
                lairPk = pk;

            int turn = 1;
            bool stuck = false;

            if (Settings.UpgradePokemon)
                await CheckIfUpgrade(party, lairPk, token).ConfigureAwait(false);

            while (true)
            {
                while (!await LairStatusCheck(LairMovesBytes, CurrentScreenLairOffset2, token).ConfigureAwait(false))
                {
                    Lost = await LairStatusCheck(LairRewardsScreenBytes, CurrentScreenLairOffset2, token).ConfigureAwait(false) || await LairStatusCheck(LairDialogueBytes, LairDialogueOffset, token).ConfigureAwait(false);
                    if (await LairStatusCheck(LairCatchScreenBytes, LairMiscScreenOffset, token).ConfigureAwait(false) || Lost)
                        return;

                    if (await LairStatusCheck(LairBattleMenuBytes, CurrentScreenLairOffset2, token).ConfigureAwait(false) && await IsInBattle(token).ConfigureAwait(false))
                        await Click(A, 1_000, token).ConfigureAwait(false);
                }

                if (stuck)
                    Log($"{(Move)pk.Moves[OldMoveIndex]} cannot be executed, trying to select a different move.");
                else
                {
                    var newPlayerPk = await ReadUntilPresent(LairPartyP1Offset, 2_000, 0_200, token).ConfigureAwait(false);
                    var newLairPk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                    if (newPlayerPk != null && newLairPk != null)
                    {
                        pk = newPlayerPk;
                        lairPk = newLairPk;
                    }
                }

                await SelectMove(party, lairPk, stuck, turn, token).ConfigureAwait(false);
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);

                if (await LairStatusCheck(LairMovesBytes, CurrentScreenLairOffset2, token).ConfigureAwait(false))
                    stuck = true;
                else
                {
                    stuck = false;
                    turn++;
                }

                if (turn == DmaxEnd)
                    DmaxEnd = -1;
            }
        }

        private async Task CatchRoutine(int raidCount, CancellationToken token)
        {
            await Task.Delay(1_000).ConfigureAwait(false);
            if (Settings.CatchLairPokémon || Upgrade || raidCount == 4) // We want to catch the legendary regardless of settings for catching.
            {
                await SelectCatchingBall(token).ConfigureAwait(false); // Select ball to catch with.
                Log($"Catching {(raidCount < 4 ? "encounter" : "legendary")}...");
                if (raidCount < 4)
                {
                    while (!await LairStatusCheck(LairMonSelectScreenBytes, CurrentScreenLairOffset, token).ConfigureAwait(false)) // Spam A until we're back in a menu.
                        await Task.Delay(1_000).ConfigureAwait(false);

                    if (!Upgrade)
                        await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                    await Click(A, 1_000, token).ConfigureAwait(false);
                }
                CatchCount--;
                Caught++;
            }
            else
            {
                await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
        }

        private async Task Results(CancellationToken token)
        {
            Counts.AddCompletedAdventures();
            int index = -1;
            int legendSpecies = 0;
            bool stopCond = false;
            bool caughtLegend = false;

            while (!await LairStatusCheck(LairRewardsScreenBytes, CurrentScreenLairOffset2, token).ConfigureAwait(false))
                await Task.Delay(6_000).ConfigureAwait(false);

            for (int i = 0; i < Caught; i++)
            {
                var jumpAdj = i == 0 ? 0 : i == 1 ? 2 : i == 2 ? 10 : 12;
                var pointer = $"[[[[[main+28F4060]+1B0]+68]+{58 + jumpAdj}]+58]";
                var pk = await ReadUntilPresentAbsolute(await ParsePointer(pointer, token), 2_000, 0_200, token).ConfigureAwait(false);
                if (pk != null)
                {
                    if (pk.IsShiny)
                        index = Settings.CatchLairPokémon ? i : ++index;

                    caughtLegend = (Caught - 1 == index && pk.IsShiny && !Lost) || (Settings.CatchLairPokémon && index == 3 && pk.IsShiny);
                    bool caughtRegular = !caughtLegend && pk.IsShiny;
                    legendSpecies = caughtLegend ? pk.Species : 0;
                    if (caughtLegend && StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, NewSCSettings) && Settings.UseStopConditionsPathReset)
                        stopCond = true;

                    if (stopCond || (caughtLegend && Settings.StopOnLegendary))
                        StopBot = true;

                    TradeExtensions.EncounterLogs(pk);
                    if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                        DumpPokemon(DumpSetting.DumpFolder, "lairs", pk);

                    if (Settings.AlwaysOutputShowdown)
                        Log($"Adventure #{AdventureCounts.AdventureCount}.{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");

                    if (LairBotUtil.EmbedsInitialized && Settings.ResultsEmbedChannels != string.Empty && (caughtLegend || caughtRegular))
                        LairBotUtil.EmbedMon = (pk, caughtLegend);
                    else
                    {
                        if (caughtLegend)
                            EchoUtil.Echo($"{(!NewSCSettings.PingOnMatch.Equals(string.Empty) ? $"<@{NewSCSettings.PingOnMatch}>\n" : "")}Shiny Legendary found!\nEncounter #{LairEncounterCount}. Adventure #{AdventureCounts.AdventureCount}.{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");
                        else if (caughtRegular)
                            EchoUtil.Echo($"{(!NewSCSettings.PingOnMatch.Equals(string.Empty) ? $"<@{NewSCSettings.PingOnMatch}>\n" : "")}Found a shiny, but it's not quite legendary...\nEncounter #{LairEncounterCount}. Adventure #{AdventureCounts.AdventureCount}.{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");
                    }
                }
            }

            if (Settings.EnableOHKO)
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7900E808), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);

            if (Settings.LairBall != Ball.None && CatchCount < 5 && CatchCount != -1)
            {
                Log("Restoring original ball pouch...");
                await Connection.WriteBytesAsync(BallPouch, PokeBallOffset, token).ConfigureAwait(false);
                CatchCount = await GetPokeBallCount(token).ConfigureAwait(false);
            }

            if (!Settings.InjectSeed && !Settings.EnableOHKO && Settings.KeepPath && !Settings.CatchLairPokémon && !caughtLegend)
            {
                double winRate = KeepPathCounts.KeepPathWins / KeepPathCounts.KeepPathAdventures;
                if (KeepPathCounts.KeepPathAdventures < 5 || (KeepPathCounts.KeepPathAdventures >= 5 && winRate >= 0.5))
                {
                    Log($"{(Lost ? "" : "No shiny legendary found. ")}Resetting the game to keep the seed.");
                    await GameRestart(token).ConfigureAwait(false);
                    if (await GetDyniteCount(token).ConfigureAwait(false) < 10)
                    {
                        Log("Restoring Dynite Ore...");
                        await SwitchConnection.WriteBytesAsync(OtherItemsPouch, OtherItemAddress, token).ConfigureAwait(false);
                    }
                }
                else if (KeepPathCounts.KeepPathAdventures >= 5 && winRate < 0.5)
                {
                    KeepPathCounts.KeepPathWins = 0;
                    KeepPathCounts.KeepPathAdventures = 0;
                    await Click(B, 1_000, token).ConfigureAwait(false);
                    Log("Our win ratio isn't looking too good... Rolling our path.");
                }
                return;
            }

            if (index == -1)
            {
                await Click(B, 1_000, token).ConfigureAwait(false);
                Log("No results found... Going deeper into the lair...");
                return;
            }

            if (index > -1)
            {
                for (int y = 0; y < index; y++)
                    await Click(DDOWN, 0_250, token).ConfigureAwait(false);

                if (Hub.Config.StopConditions.CaptureVideoClip)
                {
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                    await Click(A, 2_000, token).ConfigureAwait(false);
                    await PressAndHold(CAPTURE, 2_000, 10_000, token).ConfigureAwait(false);
                    await Click(B, 4_000, token).ConfigureAwait(false);
                }

                if (Settings.ResetLegendaryCaughtFlag && caughtLegend)
                {
                    Log("Resetting Legendary Flag!");
                    await ResetLegendaryFlag(legendSpecies, token).ConfigureAwait(false);
                }
                else if (!Settings.ResetLegendaryCaughtFlag && !StopBot && caughtLegend)
                    Settings.LairSpecies = LairSpecies.None;
            }
        }

        private async Task<bool> LegendReset(CancellationToken token)
        {
            ResetCount++;
            var originalSetting = NewSCSettings.ShinyTarget;
            NewSCSettings.ShinyTarget = TargetShinyType.DisableOption;
            Log("Reading legendary Pokémon offset...");
            TradeExtensions.EncounterLogs(LairBoss);
            Log($"Reset #{ResetCount} {Environment.NewLine}{ShowdownParsing.GetShowdownText(LairBoss)}{Environment.NewLine}");
            if (!StopConditionSettings.EncounterFound(LairBoss, DesiredMinIVs, DesiredMaxIVs, NewSCSettings))
            {
                Log("No match found, restarting the game...");
                await GameRestart(token).ConfigureAwait(false);
                if (await GetDyniteCount(token).ConfigureAwait(false) < 10)
                {
                    Log("Restoring Dynite Ore...");
                    await SwitchConnection.WriteBytesAsync(OtherItemsPouch, OtherItemAddress, token).ConfigureAwait(false);
                }
                return false;
            }
            Log("Stats match conditions, now let's continue the adventure and check if it's shiny...");
            NewSCSettings.ShinyTarget = originalSetting;
            return true;
        }

        private async Task<int> GetDyniteCount(CancellationToken token)
        {
            OtherItemsPouch = await Connection.ReadBytesAsync(OtherItemAddress, 2184, token).ConfigureAwait(false);
            var pouch = new InventoryPouch8(InventoryType.Items, LairBotUtil.Pouch_Regular_SWSH, 999, 0, 546);
            pouch.GetPouch(OtherItemsPouch);
            return pouch.Items.FirstOrDefault(x => x.Index == 1604).Count;
        }

        private async Task<int> GetPokeBallCount(CancellationToken token)
        {
            BallPouch = await Connection.ReadBytesAsync(PokeBallOffset, 116, token).ConfigureAwait(false);
            var counts = EncounterCount.GetBallCounts(BallPouch);
            return counts.PossibleCatches(Settings.LairBall);
        }

        private async Task SelectCatchingBall(CancellationToken token)
        {
            if (Settings.LairBall == Ball.None || (!Settings.CatchLairPokémon && Upgrade))
            {
                for (int i = 0; i < 3; i++)
                    await Click(A, 1_000, token).ConfigureAwait(false);
                return;
            }

            Log($"Selecting {Settings.LairBall} Ball...");
            await Click(A, 1_000, token).ConfigureAwait(false);
            var index = EncounterCount.BallIndex((int)Settings.LairBall);
            var ofs = await ParsePointer("[[[[[[main+2951270]+1D8]+818]+2B0]+2E0]+200]", token).ConfigureAwait(false);
            while (true)
            {
                int ball = BitConverter.ToInt32(await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 4, token).ConfigureAwait(false), 0);
                if (ball == index)
                    break;
                if (Settings.LairBall.IsApricornBall())
                    await Click(DLEFT, 0_050, token).ConfigureAwait(false);
                else await Click(DRIGHT, 0_050, token).ConfigureAwait(false);
            }
            await Click(A, 1_000, token).ConfigureAwait(false);
        }

        private async Task ResetLegendaryFlag(int species, CancellationToken token)
        {
            if (species == 0)
                return;

            while (!await LairStatusCheck(LairDialogueBytes, CurrentScreenLairOffset, token).ConfigureAwait(false))
                await Click(A, 0_400, token).ConfigureAwait(false);
            await Connection.WriteBytesAsync(new byte[1], GetFlagOffset(species), token).ConfigureAwait(false);
        }

        private uint GetFlagOffset(int species)
        {
            if (species == 0)
                return 0;

            var index = Array.IndexOf(Enum.GetValues(typeof(LairSpeciesBlock)), Enum.Parse(typeof(LairSpeciesBlock), $"{(Species)species}"));
            return (uint)(ResetLegendFlagOffset + (index * 0x38));
        }

        private async Task<int> GetPPCount(int move, CancellationToken token) => BitConverter.ToInt32(await Connection.ReadBytesAsync((uint)(LairMove1Offset + (move * 0xC)), 4, token).ConfigureAwait(false), 0);

        private async Task<bool> LairSeedInjector(CancellationToken token)
        {
            if (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Click(A, 2_000, token).ConfigureAwait(false);
            if (!Settings.InjectSeed || Settings.SeedToInject == string.Empty)
                return true;

            Log("Injecting specified Lair Seed...");
            if (!ulong.TryParse(Settings.SeedToInject, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong seedInj))
            {
                Log("Entered seed is invalid, stopping LairBot.");
                return false;
            }
            await Connection.WriteBytesAsync(BitConverter.GetBytes(seedInj), AdventureSeedOffset, token).ConfigureAwait(false);
            return true;
        }

        private async Task SetHuntedPokemon(CancellationToken token)
        {
            byte[] note = await Connection.ReadBytesAsync(LairSpeciesNote1, 2, token).ConfigureAwait(false);
            byte[] wanted = BitConverter.GetBytes((ushort)Settings.LairSpecies);
            if (LairBotUtil.NoteRequest.Count > 0)
            {
                for (int i = 0; i < LairBotUtil.NoteRequest.Count; i++)
                {
                    var caughtFlag = await Connection.ReadBytesAsync(GetFlagOffset((int)LairBotUtil.NoteRequest[i]), 2, token).ConfigureAwait(false);
                    if (caughtFlag[0] != 0)
                    {
                        Log($"{LairBotUtil.NoteRequest[i]} was already caught prior, skipping!");
                        LairBotUtil.NoteRequest.Remove(LairBotUtil.NoteRequest[i]);
                        --i;
                        continue;
                    }
                    else await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)LairBotUtil.NoteRequest[i]), i == 0 ? LairSpeciesNote1 : i == 1 ? LairSpeciesNote2 : LairSpeciesNote3, token);
                }
                Log($"Lair Notes set to {string.Join(", ", LairBotUtil.NoteRequest)}!");
                LairBotUtil.NoteRequest = new();
                return;
            }
            else if (!note.SequenceEqual(wanted) && Settings.LairSpecies != LairSpecies.None)
            {
                var caughtFlag = await Connection.ReadBytesAsync(GetFlagOffset((int)Settings.LairSpecies), 2, token).ConfigureAwait(false);
                if (caughtFlag[0] != 0)
                {
                    Log($"{Settings.LairSpecies} was already caught prior, ignoring the request.");
                    Settings.LairSpecies = LairSpecies.None;
                    return;
                }
                await Connection.WriteBytesAsync(wanted, BitConverter.ToUInt16(note, 0) > 0 ? LairSpeciesNote1 : LairSpeciesNote2, token);
                Log($"{Settings.LairSpecies} is ready to be hunted.");
            }
        }

        private async Task MoveAndRentalClicks(int clicks, CancellationToken token)
        {
            for (int i = 0; i < clicks; i++)
                await Click(DDOWN, 0_300, token);

            await Click(A, 1_000, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
        }

        private async Task GameRestart(CancellationToken token)
        {
            await CloseGame(Hub.Config, token).ConfigureAwait(false);
            await StartGame(Hub.Config, token, false, true).ConfigureAwait(false);
        }

        private async Task<bool> SettingsCheck(CancellationToken token)
        {
            NewSCSettings = Hub.Config.StopConditions;
            if (NewSCSettings.ShinyTarget == TargetShinyType.SquareOnly)
                NewSCSettings.ShinyTarget = TargetShinyType.AnyShiny;
            if (NewSCSettings.MarkOnly)
                NewSCSettings.MarkOnly = false;

            if (BallPouch.Length == 1 && Settings.LairBall != Ball.None)
            {
                Log("Checking Poké Ball pouch...");
                CatchCount = await GetPokeBallCount(token).ConfigureAwait(false);
                if (CatchCount <= 4)
                {
                    Log($"Insufficient {Settings.LairBall} Ball count.");
                    return false;
                }
            }

            if (OtherItemsPouch.Length == 1 && Settings.UseStopConditionsPathReset)
            {
                Log("Checking Dynite Ore count...");
                var dyniteCount = await GetDyniteCount(token).ConfigureAwait(false);
                if (dyniteCount < 10)
                {
                    Log($"{(dyniteCount == 0 ? "No" : $"Only {dyniteCount}")} Dynite Ore found. To be on the safe side, obtain more and restart the bot.");
                    return false;
                }
            }
            return true;
        }

        private async Task CancellationMonitor(CancellationToken oldToken)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;
            while (!oldToken.IsCancellationRequested)
                await Task.Delay(1_000).ConfigureAwait(false);

            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7900E808), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);
            source.Cancel();
        }
    }
}