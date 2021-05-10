﻿using System;

namespace SysBot.Pokemon
{
    public static class PokeDataOffsets
    {
        public const uint BoxStartOffset = 0x45075880;
        public const uint CurrentBoxOffset = 0x450C680E;
        public const uint TrainerDataOffset = 0x45068F18;
        public const uint SoftBanUnixTimespanOffset = 0x450C89E8;
        public const uint IsConnectedOffset = 0x30c7cca8;
        public const uint TextSpeedOffset = 0x450690A0;
        public const uint ItemTreasureAddress = 0x45068970;
        public const uint LastUsedBallOffset = 0x4C428C80;
        public const uint PokeBallOffset = 0x45067B88; // 0x74 size
        public const uint XYCoordinates = 0x1D5B690; // +0x8 for Y
        public const uint DenOffset = 0x450C8A70;

        // Raid Offsets
        // The dex number of the Pokémon the host currently has chosen. 
        // Details for each player span 0x30, so add 0x30 to get to the next offset.
        public const uint RaidP0PokemonOffset = 0x8398A294;
        // Add to each Pokémon offset.  AltForm used.
        public const uint RaidAltFormInc = 0x4;
        // Add to each Pokémon offset.  0 = male, 1 = female, 2 = genderless.
        public const uint RaidGenderIncr = 0x8;
        // Add to each Pokémon offset.  Bool for whether the Pokémon is shiny.
        public const uint RaidShinyIncr = 0xC;
        // Add to each Pokémon offset.  Bool for whether they have locked in their Pokémon.
        public const uint RaidLockedInIncr = 0x1C;
        public const uint RaidBossOffset = 0x8398A25C;

        // 0 when not in a battle or raid, 0x40 or 0x41 otherwise.
        public const uint InBattleRaidOffsetSW = 0x3F128624;
        public const uint InBattleRaidOffsetSH = 0x3F128626;

        // Pokémon Encounter Offsets
        public const uint WildPokemonOffset = 0x8FEA3648;
        public const uint RaidPokemonOffset = 0x886A95B8;
        public const uint LegendaryPokemonOffset = 0x886BC348;

        // Link Trade Offsets
        public const uint LinkTradePartnerPokemonOffset = 0xAF286078;
        public const uint LinkTradePartnerNameOffset = 0xAF28384C;
        public const uint LinkTradeSearchingOffset = 0x2F76C3C8;

        // Suprise Trade Offsets
        public const uint SurpriseTradePartnerPokemonOffset = 0x450675a0;

        public const uint SurpriseTradeLockSlot = 0x450676fc;
        public const uint SurpriseTradeLockBox = 0x450676f8;

        public const uint SurpriseTradeSearchOffset = 0x45067704;
        public const uint SurpriseTradeSearch_Empty = 0x00000000;
        public const uint SurpriseTradeSearch_Searching = 0x01000000;
        public const uint SurpriseTradeSearch_Found = 0x0200012C;
        public const uint SurpriseTradePartnerNameOffset = 0x45067708;

        /* Wild Area Daycare */
        public const uint DayCare_Wildarea_Step_Counter = 0x4511FC54;
        public const uint DayCare_Wildarea_Egg_Is_Ready = 0x4511FC60;

        /* Route 5 Daycare */
        public const uint DayCare_Route5_Step_Counter = 0x4511F99C;
        public const uint DayCare_Route5_Egg_Is_Ready = 0x4511F9A8;

        public const int BoxFormatSlotSize = 0x158;
        public const int TrainerDataLength = 0x110;

        // Lair offsets
        public const uint CurrentScreenLairOffset = 0x6B30FAC0;
        public const uint CurrentScreenLairOffset2 = 0x6B582760;
        public const uint LairMiscScreenOffset = 0x6B38B5D0;
        public const uint LairDialogueOffset = 0x6B38B610;

        public const uint LairMenuBytes = 0xFFAC2CC4; // 1st offset
        public const uint LairCatchScreenBytes = 0xFBFEFEFE; // MiscScreen
        public const uint LairDialogueBytes = 0xC8D8DADF; // Dialogue
        public const uint LairBattleMenuBytes = 0xFFFFFFFF; // 2nd offset
        public const uint LairMovesBytes = 0xDF6C6C6C; // 2nd offset
        public const uint LairDmaxMovesBytes = 0xFF5D23EF; // MiscScreen
        public const uint LairMonSelectScreenBytes = 0xD79E2DBB; // 1st offset
        public const uint LairRewardsScreenBytes = 0xFFAE2FC6; // 2nd offset
        public const uint LairAdventurePathBytes = 0xFFFFFFFF; // 1st offset

        public const uint AdventureSeedOffset = 0x4514A4B0;
        public const uint ResetLegendFlagOffset = 0x50AD76B8;

        public const uint LairPartyP1Offset = 0x886B67C8;
        public const uint LairPartyP2Offset = 0x886BC348;
        public const uint LairPartyP3Offset = 0x886B9588;
        public const uint LairPartyP4Offset = 0x886BF108;

        public const uint RentalMon1 = 0x83E93070;
        public const uint RentalMon2 = 0x83E93300;
        public const uint RentalMon3 = 0x83E93590;

        public const uint DamageOutputOffset = 0x007E37F0;
        public const uint OtherItemAddress = 0x45067D90;
        public const uint LairMove1Offset = 0x840A5B10;

        public const uint LairSpeciesNote1 = 0x50B12278;
        public const uint LairSpeciesNote2 = 0x50B122B0;
        public const uint LairSpeciesNote3 = 0x50B122E8;

        #region ScreenDetection
        // CurrentScreenOffset can be unreliable for Overworld; this one is 1 on Overworld and 0 otherwise.
        // Varies based on console language which is configured in Hub.
        // Default setting works for English, Dutch, Portuguese, and Russian
        public const uint OverworldOffset = 0x2F770638;
        public const uint OverworldOffsetFrench = 0x2F770828;
        public const uint OverworldOffsetGerman = 0x2F770908;
        public const uint OverworldOffsetSpanish = 0x2F7707F8;
        public const uint OverworldOffsetItalian = 0x2F7705B8;
        public const uint OverworldOffsetJapanese = 0x2F770798;
        public const uint OverworldOffsetChineseT = 0x2F76F7D8;
        public const uint OverworldOffsetChineseS = 0x2F76F838;
        public const uint OverworldOffsetKorean = 0x2F76FC38;

        // For detecting when we're on the in-battle menu. 
        public const uint BattleMenuOffset = 0x6B578EDC;

        // Original screen detection offset.
        public const uint CurrentScreenOffset = 0x6B30FA00;

        // Value goes between either of these; not game or area specific.
        public const uint CurrentScreen_Overworld1 = 0xFFFF5127;
        public const uint CurrentScreen_Overworld2 = 0xFFFFFFFF;

        public const uint CurrentScreen_Box1 = 0xFF00D59B;
        public const uint CurrentScreen_Box2 = 0xFF000000;
        public const uint CurrentScreen_Box_WaitingForOffer = 0xC800B483;
        public const uint CurrentScreen_Box_ConfirmOffer = 0xFF00B483;

        public const uint CurrentScreen_Softban = 0xFF000000;

        //public const uint CurrentScreen_YMenu = 0xFFFF7983;
        public const uint CurrentScreen_RaidParty = 0xFF1461DB;
        #endregion

        public static uint GetTrainerNameOffset(TradeMethod tradeMethod)
        {
            return tradeMethod switch
            {
                TradeMethod.LinkTrade => LinkTradePartnerNameOffset,
                TradeMethod.SupriseTrade => SurpriseTradePartnerNameOffset,
                _ => throw new ArgumentException(nameof(tradeMethod)),
            };
        }

        public static uint GetDaycareStepCounterOffset(SwordShieldDaycare daycare)
        {
            return daycare switch
            {
                SwordShieldDaycare.WildArea => DayCare_Wildarea_Step_Counter,
                SwordShieldDaycare.Route5 => DayCare_Route5_Step_Counter,
                _ => throw new ArgumentException(nameof(daycare)),
            };
        }

        public static uint GetDaycareEggIsReadyOffset(SwordShieldDaycare daycare)
        {
            return daycare switch
            {
                SwordShieldDaycare.WildArea => DayCare_Wildarea_Egg_Is_Ready,
                SwordShieldDaycare.Route5 => DayCare_Route5_Egg_Is_Ready,
                _ => throw new ArgumentException(nameof(daycare)),
            };
        }
    }
}