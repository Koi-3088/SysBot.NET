using System;
using System.ComponentModel;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class BoolSettings
    {
        private const string Bool = nameof(Bool);
        public override string ToString() => "Bool Bot Settings";

        [Category(Bool), Description("The method by which the bot will reset bools of Pokémon. If you are skipping for a Location, select a desired location under PokédexRecommendationLocation then run DexRecSkipper.  If you are skipping for a Species, select it as your StopOnSpecies under StopConditions then run DexRecSkipper. Make sure your Menu Icon is hovered over the Pokédex.")]
        public BoolMode BoolType { get; set; } = BoolMode.DexRecSkipper;

        [Category(Bool), Description("When set to DexRecSkipper and Location is not None, will skip until target is found. Otherwise, set BoolMode to DexRecLocation and choose a location of choice to have it injected. Make sure your Menu Icon is hovered over the Pokédex.")]
        public DexRecLoc PokédexRecommendationLocationTarget { get; set; } = DexRecLoc.None;

        [Category(Bool), Description("When set to DexRecSkipper and Species is not None, will skip until target is found. Make sure your Menu Icon is hovered over the Pokédex.")]
        public DexRecSpecies PokédexRecommendationSpeciesTarget { get; set; } = DexRecSpecies.None;

        [Category(Bool), Description("Set BoolMode to DexRecSpecies. If set to None, will the slot. Make sure your Menu Icon is hovered over the Pokédex.")]
        public DexRecSpecies[] PokédexRecommendationSpeciesSlots { get; set; } = { DexRecSpecies.None, DexRecSpecies.None, DexRecSpecies.None, DexRecSpecies.None };
    }
}