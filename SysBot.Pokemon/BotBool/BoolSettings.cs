using System;
using System.ComponentModel;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class BoolSettings
    {
        private const string Bool = nameof(Bool);
        public override string ToString() => "Bool Bot Settings";

        [Category(Bool), Description("The method by which the bot will update certain Bools. ")]
        public BoolMode BoolType { get; set; } = BoolMode.DexRecSkipper;

        [Category(Bool), Description("Extra Conditions"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public DexRecConditionsCategory DexRecConditions { get; set; } = new();

        [Category(Bool)]
        [TypeConverter(typeof(DexRecConditionsCategoryConverter))]
        public class DexRecConditionsCategory
        {
            public override string ToString() => "DexRec Conditions";

            [Category(Bool), Description("When set to DexRecSkipper and Location is not None, will skip indefinitely. You must manually stop when you see a desired location you want. Otherwise, set BoolMode to DexRecLocation and choose a location of choice to have it injected.")]
            public DexRecLoc PokédexRecommendationLocationTarget { get; set; } = DexRecLoc.None;

            [Category(Bool), Description("When set to DexRecSkipper and Species is not None, will skip indefinitely. You must manually stop when you see a desired species you want.")]
            public DexRecSpecies PokédexRecommendationSpeciesTarget { get; set; } = DexRecSpecies.None;

            [Category(Bool), Description("Set BoolMode to DexRecSpeciesInjector. If set to None, will empty the slot.")]
            public DexRecSpecies[] PokédexRecommendationSpeciesSlots { get; set; } = { DexRecSpecies.None, DexRecSpecies.None, DexRecSpecies.None, DexRecSpecies.None };
        }
        public class DexRecConditionsCategoryConverter : TypeConverter
        {
            public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes) => TypeDescriptor.GetProperties(typeof(DexRecConditionsCategory));

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
        }

    }
}