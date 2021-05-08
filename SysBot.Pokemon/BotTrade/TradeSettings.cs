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

        [Category(TradeCode), Description("Select default species for \"ItemTrade\", if configured.")]
        public Species ItemTradeSpecies { get; set; } = Species.None;

        [Category(TradeCode), Description("Silly, useless feature to post a meme when certain illegal or disallowed trade requests are made.")]
        public bool Memes { get; set; } = false;

        [Category(TradeCode), Description("Enter either direct picture or gif links, or file names with extensions. For example, file1.png, file2.jpg, etc.")]
        public string MemeFileNames { get; set; } = string.Empty;

        /// <summary>
        /// Gets a random trade code based on the range settings.
        /// </summary>
        public int GetRandomTradeCode() => Util.Rand.Next(MinTradeCode, MaxTradeCode + 1);
    }
}
