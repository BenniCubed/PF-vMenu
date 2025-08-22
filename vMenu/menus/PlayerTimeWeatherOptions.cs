using System.Threading.Tasks;

using MenuAPI;

using vMenuShared;

using static vMenuClient.CommonFunctions;

namespace vMenuClient
{
    public class PlayerTimeWeatherOptions
    {
        // Variables
        private Menu menu;

        private MenuCheckboxItem overrideServer;
        private MenuListItem timeList;
        private MenuCheckboxItem freezeTime;
        private MenuListItem weatherTypeList;
        private MenuCheckboxItem snowEnabled;
        private MenuListItem blackoutList;

        public bool OverrideServer
        {
            get => overrideServer.Enabled && overrideServer.Checked;
        }
        public TimeWeatherCommon.TimeState ClientTime { get; private set; } =
            new TimeWeatherCommon.TimeState()
            {
                Hour = 12,
                Minute = 0,
                Frozen = false,
            };
        public TimeWeatherCommon.WeatherState ClientWeather { get; private set; } =
            new TimeWeatherCommon.WeatherState();

        bool enabled = true;
        public bool Enabled
        {
            get => enabled && MainMenu.vMenuEnabled;
            set
            {
                if (value && !enabled)
                {
                    enabled = true;

                    overrideServer.Enabled = true;
                }
                else if (!value && enabled)
                {
                    enabled = false;

                    overrideServer.Enabled = false;
                    overrideServer.Checked = false;
                }
            }
        }

        /// <summary>
        /// Creates the menu.
        /// </summary>
        private void CreateMenu()
        {
            menu = new Menu(MenuTitle, "Local Time & Weather");

            overrideServer = new MenuCheckboxItem("Enable", "Enable client-sided time and weather.", false);
            menu.AddMenuItem(overrideServer);

            timeList = new MenuListItem(
                "Time",
                TimeWeatherCommon.TimeListOptions,
                TimeWeatherCommon.TimeListOptions.Count / 2,
                "Select the time of day.");
            menu.AddMenuItem(timeList);

            freezeTime = new MenuCheckboxItem("Freeze Time", "Keep the clock frozen at the selected time.", false);
            menu.AddMenuItem(freezeTime);

            weatherTypeList = new MenuListItem(
                "Weather Type",
                TimeWeatherCommon.WeatherTypeOptions,
                0,
                "Select the weather type.");
            menu.AddMenuItem(weatherTypeList);

            snowEnabled = new MenuCheckboxItem(
                "Snow",
                "Enable or disable snow textures (snow is always enabled with ~b~Xmas~s~ weather).",
                false);
            menu.AddMenuItem(snowEnabled);

            blackoutList = new MenuListItem(
                "Blackout",
                TimeWeatherCommon.BlackoutStateOptionsList,
                0,
                "Select the blackout state.");
            menu.AddMenuItem(blackoutList);

            menu.OnListIndexChange += (_menu, item, _oldIx, _newIx, _itemIx) => {
                if (item == timeList)
                {
                    ClientTime.Hour = timeList.ListIndex / 2;
                    ClientTime.Minute = 30 * (timeList.ListIndex % 2);
                }
                else if (item == weatherTypeList)
                {
                    ClientWeather.WeatherType =
                        TimeWeatherCommon.WeatherTypeOptionsIndexToType(weatherTypeList.ListIndex);

                    switch(ClientWeather.WeatherType)
                    {
                        case TimeWeatherCommon.WeatherType.SnowHalloween:
                        case TimeWeatherCommon.WeatherType.Snow:
                        case TimeWeatherCommon.WeatherType.Blizzard:
                        case TimeWeatherCommon.WeatherType.SnowLight:
                        case TimeWeatherCommon.WeatherType.Xmas:
                            snowEnabled.Checked = true;
                            ClientWeather.Snow = true;
                            break;
                        default:
                            snowEnabled.Checked = false;
                            ClientWeather.Snow = false;
                            break;
                    }
                }
                else if (item == blackoutList)
                {
                    ClientWeather.Blackout = (TimeWeatherCommon.BlackoutState)blackoutList.ListIndex;
                }
            };

            menu.OnCheckboxChange += (_menu, item, _ix, check) => {
                if (item == freezeTime)
                {
                    ClientTime.Frozen = check;
                }
                else if (item == snowEnabled)
                {
                    ClientWeather.Snow = check;
                }
            };
        }

        /// <summary>
        /// Create the menu if it doesn't exist, and then returns it.
        /// </summary>
        /// <returns>The Menu</returns>
        public Menu GetMenu()
        {
            if (menu == null)
            {
                CreateMenu();
            }
            return menu;
        }

        public async Task Sync()
        {
            bool overrideClient = TimeWeatherCommon.GetOverrideClientTW();
            if (overrideClient)
                overrideServer.Checked = false;

            overrideServer.Enabled = !overrideClient;

            await Delay(100);
        }
    }
}
