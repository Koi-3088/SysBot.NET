using PKHeX.Core;
using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class TradeSettings
    {
        private const string TradeCode = nameof(TradeCode);
        private const string TradeConfig = nameof(TradeConfig);
        private const string Dumping = nameof(Dumping);
        public override string ToString() => "Trade Bot Settings";

        [Category(TradeConfig), Description("Time to wait for a trade partner in seconds.")]
        public int TradeWaitTime { get; set; } = 45;

        [Category(TradeCode), Description("Minimum Link Code.")]
        public int MinTradeCode { get; set; } = 8180;

        [Category(TradeCode), Description("Maximum Link Code.")]
        public int MaxTradeCode { get; set; } = 8199;

        [Category(Dumping), Description("Link Trade: Dumping routine will stop after a maximum number of dumps from a single user.")]
        public int MaxDumpsPerTrade { get; set; } = 20;

        [Category(Dumping), Description("Link Trade: Dumping routine will stop after spending x seconds in trade.")]
        public int MaxDumpTradeTime { get; set; } = 180;

        [Category(TradeCode), Description("Spin while waiting for trade partner. Currently needs USB-Botbase.")]
        public bool SpinTrade { get; set; } = false;

        [Category(TradeCode), Description("Link Trade: Will restrict trading to a single non-shiny species. Useful for item trades in servers (such as raid servers) that don't want full-on genning.")]
        public Species ItemMuleSpecies { get; set; } = Species.None;

        [Category(TradeCode), Description("Custom message to display if a non-ItemMule species is requested via $trade.")]
        public string ItemMuleCustomMessage { get; set; } = string.Empty;

        [Category(TradeCode), Description("Toggle Ditto trades for breeding. Can be used with \"ItemMule\".")]
        public bool DittoTrade { get; set; } = false;

        [Category(TradeCode), Description("Toggle Egg trades. Can be used with \"ItemMule\".")]
        public bool EggTrade { get; set; } = false;

        [Category(TradeCode), Description("Silly, useless feature to post a meme if someone requests an illegal item for \"ItemMule\".")]
        public bool Memes { get; set; } = false;

        [Category(TradeCode), Description("Enter either direct picture or gif links, or file names with extensions. For example, file1.png, file2.jpg, etc.")]
        public string MemeFileNames { get; set; } = string.Empty;

        [Category(TradeCode), Description("Enter Channel ID(s) where TradeCord should be active, or leave blank.")]
        public string TradeCordChannels { get; set; } = string.Empty;

        [Category(TradeCode), Description("Enter the amount of time in seconds until a user can catch again.")]
        public double TradeCordCooldown { get; set; } = 60;

        /// <summary>
        /// Gets a random trade code based on the range settings.
        /// </summary>
        public int GetRandomTradeCode() => Util.Rand.Next(MinTradeCode, MaxTradeCode + 1);
    }
}
