﻿﻿using System.Collections.Generic;

using CitizenFX.Core;

namespace vMenuClient.data
{
    public static class RadioData
    {
        public static Dictionary<RadioStation, string> RadioStationToGameName => new Dictionary<RadioStation, string>()
        {
            [RadioStation.LosSantosRockRadio] = "RADIO_01_CLASS_ROCK",
            [RadioStation.NonStopPopFM] = "RADIO_02_POP",
            [RadioStation.RadioLosSantos] = "RADIO_03_HIPHOP_NEW",
            [RadioStation.ChannelX] = "RADIO_04_PUNK",
            [RadioStation.WestCoastTalkRadio] = "RADIO_05_TALK_01",
            [RadioStation.RebelRadio] = "RADIO_06_COUNTRY",
            [RadioStation.SoulwaxFM] = "RADIO_07_DANCE_01",
            [RadioStation.EastLosFM] = "RADIO_08_MEXICAN",
            [RadioStation.WestCoastClassics] = "RADIO_09_HIPHOP_OLD",
            [RadioStation.BlaineCountyRadio] = "RADIO_11_TALK_02",
            [RadioStation.TheBlueArk] = "RADIO_12_REGGAE",
            [RadioStation.WorldWideFM] = "RADIO_13_JAZZ",
            [RadioStation.FlyloFM] = "RADIO_14_DANCE_02",
            [RadioStation.TheLowdown] = "RADIO_15_MOTOWN",
            [RadioStation.RadioMirrorPark] = "RADIO_16_SILVERLAKE",
            [RadioStation.Space] = "RADIO_17_FUNK",
            [RadioStation.VinewoodBoulevardRadio] = "RADIO_18_90S_ROCK",
            [RadioStation.SelfRadio] = "RADIO_19_USER",
            [RadioStation.TheLab] = "RADIO_20_THELAB",
            [RadioStation.BlondedLosSantos] = "RADIO_21_DLC_XM17",
            [RadioStation.LosSantosUndergroundRadio] = "RADIO_22_DLC_BATTLE_MIX1_RADIO",
            [RadioStation.RadioOff] = "OFF",
        };
    }
}
