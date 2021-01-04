namespace SysBot.Pokemon
{
    public enum EncounterMode
    {
        /// <summary>
        /// Bot will move back and forth in a straight vertical path to encounter Pokémon
        /// </summary>
        VerticalLine,

        /// <summary>
        /// Bot will move back and forth in a straight horizontal path to encounter Pokémon
        /// </summary>
        HorizontalLine,

        /// <summary>
        /// Bot will soft reset Eternatus
        /// </summary>
        Eternatus,

        /// <summary>
        /// Bot will soft reset the Legendary Dogs
        /// </summary>
        LegendaryDogs,

        /// <summary>
        /// Bot will soft reset Regis
        /// </summary>
        SoftReset,

        /// <summary>
        /// Bot will soft reset Regigigas
        /// </summary>
        Regigigas,

        /// <summary>
        /// Bot will soft reset Swords of Justices by entering and leaving camp
        /// </summary>
        SoJCamp,
    }
}