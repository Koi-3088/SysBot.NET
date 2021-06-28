using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class BoolBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BoolSettings Settings;

        public BoolBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = hub.Config.Bool;
        }

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main BoolBot loop.");
            Config.IterateNextRoutine();

            var task = Hub.Config.Bool.BoolType switch
            {
                BoolMode.DexRecSkipper => DexRecSkipper(token),
                BoolMode.DexRecLocationInjector => LocationInjector(token),
                BoolMode.DexRecSpeciesInjector => SpeciesInjector(token),
                BoolMode.ResetLegendaryLairFlags => ResetLegendaryLairFlags(token),
                _ => DexRecSkipper(token),
            };
            await task.ConfigureAwait(false);
        }        
        private async Task ResetLegendaryLairFlags(CancellationToken token)
        {
            uint offset = ResetLegendFlagOffset;
            var enumVal = Enum.GetNames(typeof(LairSpecies));
            Log($"Beginning Flag Reset.");

            for (int i = 1; i < 48; i++)
            {
                var val = BitConverter.ToUInt16(await Connection.ReadBytesAsync(offset, 2, token).ConfigureAwait(false), 0);
                if (val == 1)
                {
                    Log($"Resetting caught flag for {enumVal[i]}.");
                    await Connection.WriteBytesAsync(new byte[] { 0 }, offset, token).ConfigureAwait(false);
                }

                offset += 0x38;
            }
            Log($"Flag Reset Complete.");
            return;

        }
        private async Task LocationInjector(CancellationToken token)
        {
            if (Settings.PokédexRecommendationLocationTarget == DexRecLoc.None)
            {
                Log($"No desired location selected.");
                return;
            }
            ulong location = (ulong)Settings.PokédexRecommendationLocationTarget;

            await Connection.WriteBytesAsync(BitConverter.GetBytes(location), DexRecLocation, token).ConfigureAwait(false);
            Log($"Updating Current Recommendations to Specified location! {Settings.PokédexRecommendationLocationTarget}");
            Log($"Checking Pokédex for Current Recommendations");
            await Click(X, 1_250, token).ConfigureAwait(false);
            await Click(A, 2_000, token).ConfigureAwait(false);
            Log($"Location Update Complete.");
            await Task.Delay(1_500, token).ConfigureAwait(false);
            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Click(B, 0_500, token).ConfigureAwait(false);

            Settings.PokédexRecommendationLocationTarget = DexRecLoc.None;
            return;
        }
        private async Task SpeciesInjector(CancellationToken token)
        {
            uint offset = DexRecMon;
            uint gender = DexRecMonGender;
            var dex = Settings.PokédexRecommendationSpeciesSlots;

            for (int i = 0; i < dex.Length; i++)
            {
                Log($"Changing Slot {i + 1} Species to {dex[i]}.");
                await Connection.WriteBytesAsync(BitConverter.GetBytes((short)dex[i]), offset, token).ConfigureAwait(false);
                switch (dex[i])
                {
                    case DexRecSpecies.Ponyta or DexRecSpecies.Rapidash or DexRecSpecies.Slowpoke or DexRecSpecies.Farfetchd or DexRecSpecies.Weezing or DexRecSpecies.MrMime or
                    DexRecSpecies.Zigzagoon or DexRecSpecies.Linoone or DexRecSpecies.Darumaka or DexRecSpecies.Darmanitan or DexRecSpecies.Yamask or DexRecSpecies.Cofagrigus or DexRecSpecies.Corsola or DexRecSpecies.Shellos or DexRecSpecies.Meowth:
                        await Connection.WriteBytesAsync(BitConverter.GetBytes(dex[i] == DexRecSpecies.Meowth ? (short)0x02 : (short)0x01), offset + 0x04, token).ConfigureAwait(false); break;
                    case DexRecSpecies.NidoranM or DexRecSpecies.Nidorino or DexRecSpecies.Nidoking or DexRecSpecies.Hitmonlee or DexRecSpecies.Hitmonchan or DexRecSpecies.Tauros or DexRecSpecies.Tyrogue or
                    DexRecSpecies.Gallade or DexRecSpecies.Throh or DexRecSpecies.Sawk or DexRecSpecies.Rufflet or DexRecSpecies.Braviary or DexRecSpecies.IndeedeeM or DexRecSpecies.Impidimp or DexRecSpecies.Morgrem or DexRecSpecies.Grimmsnarl:
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((short)0x00), gender, token).ConfigureAwait(false); break;//male only
                    case DexRecSpecies.Magnemite or DexRecSpecies.Magneton or DexRecSpecies.Staryu or DexRecSpecies.Starmie or DexRecSpecies.Ditto or DexRecSpecies.Lunatone or DexRecSpecies.Solrock
                    or DexRecSpecies.Baltoy or DexRecSpecies.Claydol or DexRecSpecies.Beldum or DexRecSpecies.Metang or DexRecSpecies.Metagross or DexRecSpecies.Bronzor or DexRecSpecies.Bronzong or DexRecSpecies.Magnezone
                    or DexRecSpecies.Rotom or DexRecSpecies.Klink or DexRecSpecies.Klang or DexRecSpecies.Klinklang or DexRecSpecies.Cryogonal or DexRecSpecies.Golett or DexRecSpecies.Golurk or DexRecSpecies.Carbink or
                    DexRecSpecies.Dhelmise or DexRecSpecies.Sinistea or DexRecSpecies.Polteageist or DexRecSpecies.Falinks://neutral only
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((short)0x02), gender, token).ConfigureAwait(false); break;
                    default:
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((short)0x00), offset + 0x04, token).ConfigureAwait(false);
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((short)0x01), gender, token).ConfigureAwait(false); break;
                }
                offset += 0x20;
                gender += 0x20;
            }

            Log($"Checking Pokédex for Current Recommendations");
            await Click(X, 1_250, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false);
            Log($"Species Update Complete.");
            await Task.Delay(1_500, token).ConfigureAwait(false);
            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Click(B, 0_500, token).ConfigureAwait(false);

            Settings.PokédexRecommendationLocationTarget = DexRecLoc.None;
            return;
        }
        private async Task DexRecSkipper(CancellationToken token)
        {
            Log("Starting DexRec Day Skipping! Ensure that Date/Time Sync is ON!");
            DexRecSpecies dex = Settings.PokédexRecommendationSpeciesTarget;
            DexRecSpecies species;
            uint offset = DexRecMon;
            Log($"Updating recommendations!");
            while (!token.IsCancellationRequested)
            {
                if (dex == DexRecSpecies.None && Settings.PokédexRecommendationLocationTarget == DexRecLoc.None)
                    Log($"No target set, skipping indefinitely.. When you see a species or location you want, stop the bot.");

                await DaySkip(token).ConfigureAwait(false);
                await Task.Delay(1_500, token).ConfigureAwait(false);
                Log($"Checking Pokédex for Current Recommendations");
                await Click(X, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Task.Delay(3_000, token).ConfigureAwait(false);
                if (dex != DexRecSpecies.None)
                {
                    int i = 0;
                    Log($"Searching for Pokémon: {dex}.");
                    do
                    {
                        byte[] data = await SwitchConnection.ReadBytesAsync(offset, 2, token).ConfigureAwait(false);
                        species = (DexRecSpecies)BitConverter.ToUInt16(data.Slice(0, 2), 0);
                        if (species != 0)
                        {
                            Log($"Current Recommended Species: {species}.");

                            if (species == dex)
                            {
                                Log($"Recommendation Matches Stop Condition Species: {species}.");
                                Settings.PokédexRecommendationLocationTarget = DexRecLoc.None;
                                return;
                            }
                        }
                        offset += 0x20;
                        i++;
                    } while (i < 4);
                }
                if (Settings.PokédexRecommendationLocationTarget != DexRecLoc.None)
                {
                    ulong dexreclocation = BitConverter.ToUInt64(await Connection.ReadBytesAsync(DexRecLocation, 8, token).ConfigureAwait(false), 0);
                    Log($"Searching for location: {Hub.Config.Bool.PokédexRecommendationLocationTarget}.");

                    Log($"Current Recommended Location: {(DexRecLoc)dexreclocation}.");
                    if ((ulong)Settings.PokédexRecommendationLocationTarget == dexreclocation)
                    {
                        Log($"Recommendation Matches Desired Location: {Settings.PokédexRecommendationLocationTarget}.");
                        Settings.PokédexRecommendationLocationTarget = DexRecLoc.None;
                        return;
                    }
                }
                await Task.Delay(3_000, token).ConfigureAwait(false);
                while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
        }
    }
}