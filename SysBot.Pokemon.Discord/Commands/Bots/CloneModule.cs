using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [Summary("Queues new Clone trades")]
    public class CloneModule : ModuleBase<SocketCommandContext>
    {
        private static TradeQueueInfo<PK8> Info => SysCordInstance.Self.Hub.Queues.Info;

        [Command("clone")]
        [Alias("c")]
        [Summary("Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesClone))]
        public async Task CloneAsync(int code)
        {
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, new PK8(), PokeRoutineType.Clone, PokeTradeType.Clone).ConfigureAwait(false);
        }

        [Command("clone")]
        [Alias("c")]
        [Summary("Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesClone))]
        public async Task CloneAsync([Summary("Trade Code")][Remainder] string code)
        {
            int tradeCode = Util.ToInt32(code);
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new PK8(), PokeRoutineType.Clone, PokeTradeType.Clone).ConfigureAwait(false);
        }

        [Command("clone")]
        [Alias("c")]
        [Summary("Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesClone))]
        public async Task CloneAsync()
        {
            var code = Info.GetRandomTradeCode();
            await CloneAsync(code).ConfigureAwait(false);
        }

        [Command("powerUp")]
        [Alias("pu", "p")]
        [Summary("Maxes out EXP, dynamax level, and PP ups of a Pokémon you show via Link Trade, teaches all compatible TRs, enables gigantamax if available, and hyper trains all non min/maxed IVs.")]
        [RequireQueueRole(nameof(DiscordManager.RolesPowerUp))]
        public async Task PowerUp([Summary("Trade Code")] int code, [Summary("EVs Line")][Remainder] string EVsContent)
        {
            string[] StatNames = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };
            int[] EVs = { 00, 00, 00, 00, 00, 00 };

            var list = SplitLineStats(EVsContent);

            for (int i = 0; i <= list.Length - 3; i++)
            {
                int pos = i + 1;
                int index = StringUtil.FindIndexIgnoreCase(StatNames, list[pos + 1]);
                if (index >= 0 && ushort.TryParse(list[pos], out var EV))
                {
                    EVs[index] = EV;
                }
            }

            var EVsTotal = EVs[0] + EVs[1] + EVs[2] + EVs[3] + EVs[4] + EVs[5];
            if (EVsTotal > 510 || EVsTotal < 0)
            {
                await ReplyAsync("EVs not legal.");
                return;
            }

            var pkm = new PK8
            {
                EV_HP = EVs[0],
                EV_ATK = EVs[1],
                EV_DEF = EVs[2],
                EV_SPA = EVs[3],
                EV_SPD = EVs[4],
                EV_SPE = EVs[5],
            };

            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, pkm, PokeRoutineType.PowerUp, PokeTradeType.PowerUp).ConfigureAwait(false);
        }

        [Command("powerUp")]
        [Alias("pu", "p")]
        [Summary("Maxes out EXP, dynamax level, and PP ups of a Pokémon you show via Link Trade, teaches all compatible TRs, enables gigantamax if available, and hyper trains all non min/maxed IVs.")]
        [RequireQueueRole(nameof(DiscordManager.RolesPowerUp))]
        public async Task PowerUp([Summary("EVs Line")][Remainder] string EVsContent)
        {
            string[] StatNames = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };
            int[] EVs = { 00, 00, 00, 00, 00, 00 };

            var list = SplitLineStats(EVsContent);

            for (int i = 0; i <= list.Length - 3; i++)
            {
                int pos = i + 1;
                int index = StringUtil.FindIndexIgnoreCase(StatNames, list[pos + 1]);
                if (index >= 0 && ushort.TryParse(list[pos], out var EV))
                {
                    EVs[index] = EV;
                }
            }

            var EVsTotal = EVs[0] + EVs[1] + EVs[2] + EVs[3] + EVs[4] + EVs[5];
            if (EVsTotal > 510 || EVsTotal < 0)
            {
                await ReplyAsync("EVs not legal.");
                return;
            }

            var pkm = new PK8
            {
                EV_HP = EVs[0],
                EV_ATK = EVs[1],
                EV_DEF = EVs[2],
                EV_SPA = EVs[3],
                EV_SPD = EVs[4],
                EV_SPE = EVs[5],
            };

            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, pkm, PokeRoutineType.PowerUp, PokeTradeType.PowerUp).ConfigureAwait(false);
        }

        [Command("powerUp")]
        [Alias("pu", "p")]
        [Summary("Maxes out EXP, dynamax level, and PP ups of a Pokémon you show via Link Trade, teaches all compatible TRs, enables gigantamax if available, and hyper trains all non min/maxed IVs.")]
        [RequireQueueRole(nameof(DiscordManager.RolesPowerUp))]
        public async Task PowerUp([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, new PK8(), PokeRoutineType.PowerUp, PokeTradeType.PowerUp).ConfigureAwait(false);
        }

        [Command("powerUp")]
        [Alias("pu", "p")]
        [Summary("Maxes out EXP, dynamax level, and PP ups of a Pokémon you show via Link Trade, teaches all compatible TRs, enables gigantamax if available, and hyper trains all non min/maxed IVs.")]
        [RequireQueueRole(nameof(DiscordManager.RolesPowerUp))]
        public async Task PowerUp()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await Context.AddToQueueAsync(code, Context.User.Username, sig, new PK8(), PokeRoutineType.PowerUp, PokeTradeType.PowerUp).ConfigureAwait(false);
        }
        [Command("cloneList")]
        [Alias("cl", "cq")]
        [Summary("Prints the users in the Clone queue.")]
        [RequireSudo]
        public async Task GetListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.Clone);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending Trades";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }
        [Command("powerUpList")]
        [Alias("pl", "pq")]
        [Summary("Prints the users in the PowerUp queue.")]
        [RequireSudo]
        public async Task GetPowerUpListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.PowerUp);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending Trades";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        private static string[] SplitLineStats(string line) // from PKHeX.Core
        {
            string[] StatSplitters = { " / ", " " };
            // Because people think they can type sets out...
            return line
                .Replace("SAtk", "SpA").Replace("Sp Atk", "SpA")
                .Replace("SDef", "SpD").Replace("Sp Def", "SpD")
                .Replace("Spd", "Spe").Replace("Speed", "Spe").Split(StatSplitters, StringSplitOptions.None);
        }
    }
}
