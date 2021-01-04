using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Linq;

namespace SysBot.Pokemon.Discord
{
    public class DiscordTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
    {
        private T Data { get; }
        private PokeTradeTrainerInfo Info { get; }
        private int Code { get; }
        private SocketUser Trader { get; }
        private SocketCommandContext Context { get; }
        public Action<PokeRoutineExecutor>? OnFinish { private get; set; }
        public readonly PokeTradeHub<PK8> Hub = SysCordInstance.Self.Hub;

        public DiscordTradeNotifier(T data, PokeTradeTrainerInfo info, int code, SocketUser trader, SocketCommandContext context)
        {
            Data = data;
            Info = info;
            Code = code;
            Trader = trader;
            Context = context;
        }

        public void TradeInitialize(PokeRoutineExecutor routine, PokeTradeDetail<T> info)
        {
            var receive = Data.Species == 0 ? string.Empty : $" ({Data.Nickname})";
            Trader.SendMessageAsync($"Initializing trade{receive}. Please be ready. Your code is **{Code:0000 0000}**.").ConfigureAwait(false);
        }

        public void TradeSearching(PokeRoutineExecutor routine, PokeTradeDetail<T> info)
        {
            var name = Info.TrainerName;
            var trainer = string.IsNullOrEmpty(name) ? string.Empty : $", {name}";
            Trader.SendMessageAsync($"I'm waiting for you{trainer}! Your code is **{Code:0000 0000}**. My IGN is **{routine.InGameName}**.").ConfigureAwait(false);
        }

        public void TradeCanceled(PokeRoutineExecutor routine, PokeTradeDetail<T> info, PokeTradeResult msg)
        {
            OnFinish?.Invoke(routine);
            Trader.SendMessageAsync($"Trade canceled: {msg}").ConfigureAwait(false);
            if (info.Type == PokeTradeType.TradeCord)
            {
                var user = Trader.Id.ToString();
                var path = TradeExtensions.TradeCordPath.FirstOrDefault(x => x.Contains(user));
                TradeExtensions.TradeCordPath.Remove(path);
            }
        }

        public void TradeFinished(PokeRoutineExecutor routine, PokeTradeDetail<T> info, T result)
        {
            OnFinish?.Invoke(routine);
            var tradedToUser = Data.Species;
            var message = tradedToUser != 0 ? $"Trade finished. Enjoy your {(Species)tradedToUser}!" : "Trade finished!";
            Trader.SendMessageAsync(message).ConfigureAwait(false);
            if (result.Species != 0 && Hub.Config.Discord.ReturnPK8s)
                Trader.SendPKMAsync(result, "Here's what you traded me!").ConfigureAwait(false);

            if (info.Type == PokeTradeType.TradeCord)
            {
                var user = Trader.Id.ToString();
                var original = TradeExtensions.TradeCordPath.FirstOrDefault(x => x.Contains(user));
                TradeExtensions.TradeCordPath.Remove(original);
                try
                {
                    System.IO.File.Move(original, System.IO.Path.Combine($"TradeCord\\Backup\\{user}", original.Split('\\')[2]));
                }
                catch (Exception ex)
                {
                    Base.LogUtil.LogText("Error occurred: " + ex.InnerException);
                    TradeExtensions.TradeCordPath.RemoveAll(x => x.Contains(user));
                }
            }
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, string message)
        {
            Trader.SendMessageAsync(message).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, PokeTradeSummary message)
        {
            if (message.ExtraInfo is SeedSearchResult r)
            {
                SendNotificationZ3(r);
                return;
            }

            var msg = message.Summary;
            if (message.Details.Count > 0)
                msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
            Trader.SendMessageAsync(msg).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, T result, string message)
        {
            if (result.Species != 0 && (Hub.Config.Discord.ReturnPK8s || info.Type == PokeTradeType.Dump))
                Trader.SendPKMAsync(result, message).ConfigureAwait(false);
        }

        private void SendNotificationZ3(SeedSearchResult r)
        {
            var lines = r.ToString();
            var embed = new EmbedBuilder { Color = Color.LighterGrey };
            embed.AddField(x =>
            {
                x.Name = $"Seed: {r.Seed:X16}";
                x.Value = lines;
                x.IsInline = false;
            });
            var msg = $"Here are the details for `{r.Seed:X16}`:";
            if (Hub.Config.SeedCheck.PostResultToChannel && !Hub.Config.SeedCheck.PostResultToBoth)
                Context.Channel.SendMessageAsync(Trader.Mention + " - " + msg, embed: embed.Build()).ConfigureAwait(false);
            else if (Hub.Config.SeedCheck.PostResultToBoth)
            {
                Context.Channel.SendMessageAsync(Trader.Username + " - " + msg, embed: embed.Build()).ConfigureAwait(false);
                Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
            }
            else Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
