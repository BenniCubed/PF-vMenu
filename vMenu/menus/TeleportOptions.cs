using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using CitizenFX.Core;

using MenuAPI;
using vMenuClient.MenuAPIWrapper;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;
using static vMenuShared.PermissionsManager;
using static vMenuShared.ConfigManager;

namespace vMenuClient
{
    public class TeleportOptions
    {
        // Variables
        private WMenu menu;

        private Vector3? prevTpCoords = null;
        private float? prevTpHeading = null;
        private bool prevTpSafe = true;

        private WMenu personalTpLocationsMenu;
        private List<TeleportLocation> personalTpLocations = new List<TeleportLocation>();

        // keybind states
        public bool KbTpToWaypoint { get; private set; } = UserDefaults.KbTpToWaypoint;
        public int KbTpToWaypointKey { get; } = vMenuShared.ConfigManager.GetSettingsInt(vMenuShared.ConfigManager.Setting.vmenu_teleport_to_wp_keybind_key) != -1
            ? vMenuShared.ConfigManager.GetSettingsInt(vMenuShared.ConfigManager.Setting.vmenu_teleport_to_wp_keybind_key)
            : 168; // 168 (F7 by default)

        internal static List<vMenuShared.ConfigManager.TeleportLocation> TpLocations = new List<vMenuShared.ConfigManager.TeleportLocation>();

        public TeleportOptions()
        {
            if (IsAllowed(Permission.TPTeleportToPrev))
            {
                RegisterKeyMapping($"{GetSettingsString(Setting.vmenu_individual_server_id)}vMenu:tpToPrevLocation", "vMenu TP To Prev. Location", "keyboard", "");
                RegisterCommand($"{GetSettingsString(Setting.vmenu_individual_server_id)}vMenu:tpToPrevLocation", new Action<dynamic, List<dynamic>, string>(async (dynamic source, List<dynamic> args, string rawCommand) =>
                {
                    if (!MainMenu.vMenuEnabled)
                        return;

                    await TeleportToPrevTPLocation();
                }), false);
            }

            if(IsAllowed(Permission.TPTeleportToPrev))
            {
                RegisterKeyMapping($"{GetSettingsString(Setting.vmenu_individual_server_id)}vMenu:overridePrevLocation", "vMenu Override Prev. TP Location", "keyboard", "");
                RegisterCommand($"{GetSettingsString(Setting.vmenu_individual_server_id)}vMenu:overridePrevLocation", new Action<dynamic, List<dynamic>, string>((dynamic source, List<dynamic> args, string rawCommand) =>
                {
                    if (!MainMenu.vMenuEnabled)
                        return;

                    OverridePrevLocation();
                }), false);
            }
        }

        public async Task TeleportToWaypoint()
        {
            var coords = await TeleportToWp();
            SetPrevTpLocation(coords, null, false);
        }

        /// <summary>
        /// Set the previous teleport location
        /// </summary>
        private void SetPrevTpLocation(Vector3? coords, float? heading, bool safe = false)
        {
            if (coords == null)
                return;

            prevTpCoords = coords;
            prevTpHeading = heading;
            prevTpSafe = safe;
        }

        /// <summary>
        /// Teleport to the previous TP location.
        /// </summary>
        public async Task TeleportToPrevTPLocation()
        {
            if (prevTpCoords is Vector3 coords)
            {
                await TeleportToCoords(coords, prevTpSafe);
                if (prevTpHeading is float heading)
                {
                    SetEntityHeading(Game.PlayerPed.Handle, heading);
                    SetGameplayCamRelativeHeading(0f);
                }
            }
            else
            {
                Notify.Error("There was no previous teleport location. Teleport somewhere or use ~b~Override Previous Location~s~ to set one.");
            }
        }

        /// <summary>
        /// Set the temporary teleport location to the current location.
        /// </summary>
        public void OverridePrevLocation()
        {
            var coords = Game.PlayerPed.Position;
            var heading = Game.PlayerPed.Heading;

            SetPrevTpLocation(coords, heading, true);

            Notify.Info("Previous TP location overridden.");
        }

        /// <summary>
        /// Load all saved personal teleport locations.
        /// </summary>
        public void LoadPersonalPlayerLocations()
        {
            personalTpLocations.Clear();

            var tpLocStrs = new List<string>();
            var findHandle = StartFindKvp("vmenu_tp_");
            while (true)
            {
                var tpLocStr = FindKvp(findHandle);

                if (tpLocStr is not "" and not null and not "NULL")
                {
                    tpLocStrs.Add(tpLocStr);
                }
                else
                {
                    EndFindKvp(findHandle);
                    break;
                }
            }

            foreach (var tpLocStr in tpLocStrs)
            {
                var tpLocJson = StorageManager.GetJsonData(tpLocStr);
                var tpLoc = JsonConvert.DeserializeObject<TeleportLocation>(tpLocJson);
                personalTpLocations.Add(tpLoc);
                AddPersonalTpLocationMenu(CreatePersonalTpLocationMenu(tpLoc), tpLoc);
            }
        }

        public async Task<string> GetTpLocationName()
        {
            var name = await GetUserInput("Enter location save name", 30);
            if (string.IsNullOrEmpty(name))
            {
                Notify.Error(CommonErrors.InvalidInput);
                return null;
            }
            if (personalTpLocations.Any(loc => loc.name == name))
            {
                Notify.Error("This location name is already used, please use a different name.");
                return null;
            }
            return name;
        }

        public void SavePersonalTpLocation(TeleportLocation tpLoc)
        {
            personalTpLocations.Add(tpLoc);
            var tpLocJson = JsonConvert.SerializeObject(tpLoc);
            StorageManager.SaveJsonData($"vmenu_tp_{tpLoc.name}", tpLocJson, true);
        }

        public void RemovePersonalTpLocation(TeleportLocation tpLoc)
        {
            personalTpLocations.Remove(tpLoc);
            StorageManager.DeleteSavedStorageItem($"vmenu_tp_{tpLoc.name}");
        }

        public void RenamePersonalTpLocation(string newName, ref TeleportLocation tpLoc)
        {
            RemovePersonalTpLocation(tpLoc);
            tpLoc.name = newName;
            SavePersonalTpLocation(tpLoc);
        }

        public void AddPersonalTpLocationMenu(WMenu tpLocMenu, TeleportLocation tpLoc)
        {
            personalTpLocationsMenu.AddSubmenu(tpLocMenu, $"Teleport to ~b~{tpLoc.name}~s~.");
            personalTpLocationsMenu.Menu.SortMenuItems((a, b) => string.Compare(a.Text, b.Text));
        }

        public void RemovePersonalTpLocationMenu(WMenu tpLocMenu)
        {
            tpLocMenu.Menu.GoBack();
            personalTpLocationsMenu.RemoveSubmenu(tpLocMenu);
        }

        public string GetTpToString(TeleportLocation tpLoc)
        {
            var x = Math.Round(tpLoc.coordinates.X, 2);
            var y = Math.Round(tpLoc.coordinates.Y, 2);
            var z = Math.Round(tpLoc.coordinates.Z, 2);
            var h = Math.Round(tpLoc.heading, 2);
            return $"Teleport to~n~x: ~b~{x}~s~~n~y: ~b~{y}~s~~n~z: ~b~{z}~s~~n~heading: ~b~{h}~s~";
        }

        public WMenu CreatePersonalTpLocationMenu(TeleportLocation tpLoc)
        {
            float x = tpLoc.coordinates.X, y = tpLoc.coordinates.Y, z = tpLoc.coordinates.Z;

            var tpLocMenu = new WMenu(MenuTitle, tpLoc.name);

            var tpBtn = new MenuItem("Teleport", GetTpToString(tpLoc)).ToWrapped();
            tpBtn.Selected += async (_s, _args) =>
            {
                    SetPrevTpLocation(tpLoc.coordinates, tpLoc.heading, true);
                    await TeleportToCoords(tpLoc.coordinates, true);
                    SetEntityHeading(Game.PlayerPed.Handle, tpLoc.heading);
                    SetGameplayCamRelativeHeading(0f);
            };

            var renBtn = new MenuItem("Rename", "Rename this teleport location.").ToWrapped();
            renBtn.Selected += async (_s, _args) =>
            {
                var newName = await GetTpLocationName();
                if (string.IsNullOrEmpty(newName))
                    return;

                RenamePersonalTpLocation(newName, ref tpLoc);
                RemovePersonalTpLocationMenu(tpLocMenu);

                var newTpLocMenu = CreatePersonalTpLocationMenu(tpLoc);
                AddPersonalTpLocationMenu(newTpLocMenu, tpLoc);

                MenuController.CloseAllMenus();
                newTpLocMenu.Menu.OpenMenu();
            };

            var delBtn = WMenuItem.CreateConfirmationButton("~r~Delete~s~", "Delete this teleport location. ~y~This cannot be undone.~s~");
            delBtn.Confirmed += (_s, _args) =>
            {
                RemovePersonalTpLocation(tpLoc);
                RemovePersonalTpLocationMenu(tpLocMenu);
            };

            tpLocMenu.AddItem(tpBtn);
            tpLocMenu.AddItem(renBtn);
            tpLocMenu.AddItem(delBtn);

            tpLocMenu.Opened += (_s, _args) => tpLocMenu.Menu.RefreshIndex();
            tpLocMenu.Closed += (_s, _args) => tpLocMenu.Menu.RefreshIndex();

            return tpLocMenu;
        }

        public Vector3? TryParseTpXYZString(string xyzString)
        {
            xyzString = string.Join("", xyzString.Select(c => char.IsDigit(c) || c=='.' ? c : ' '));
            var xyzStrs = xyzString.Split(' ').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            if (xyzStrs.Count != 3)
            {
                Notify.Error("The coordinate input must consist of 3 numbers");
                return null;
            }

            var nums = new float[3];
            for(int i = 0; i < 3; i++)
            {
                if(!float.TryParse(xyzStrs[i], out nums[i]))
                {
                    Notify.Error($"Could not parse \"{xyzStrs[i]}\" as number.");
                    return null;
                }
            }

            return new Vector3(nums[0], nums[1], nums[2]);
        }

        public WMenu CreateServerTeleportLoactionsSubmenu(TpCategory tpCategory)
        {
            var catTpMenu = new WMenu(MenuTitle, tpCategory.name);

            var json = LoadResourceFile(GetCurrentResourceName(), "config/locations/" + tpCategory.JsonName);
            var tpLocs = JsonConvert.DeserializeObject<TeleportLocationsJson>(json);

            foreach (var tpLoc in tpLocs.teleports)
            {
                var x = Math.Round(tpLoc.coordinates.X, 2);
                var y = Math.Round(tpLoc.coordinates.Y, 2);
                var z = Math.Round(tpLoc.coordinates.Z, 2);
                var h = Math.Round(tpLoc.heading, 2);

                var tpBtn = new MenuItem(tpLoc.name, GetTpToString(tpLoc)).ToWrapped();
                tpBtn.Selected += async (_s, _args) =>
                {
                    SetPrevTpLocation(tpLoc.coordinates, tpLoc.heading, true);
                    await TeleportToCoords(tpLoc.coordinates, true);
                    SetEntityHeading(Game.PlayerPed.Handle, tpLoc.heading);
                    SetGameplayCamRelativeHeading(0f);
                };

                catTpMenu.AddItem(tpBtn);
            }

            return catTpMenu;
        }

        public WMenu CreateServerTeleportLoactionsMenu()
        {
            var serverTpMenu = new WMenu(MenuTitle, "Server Teleport Locations");

            var json = LoadResourceFile(GetCurrentResourceName(), "config/TeleportCategories.json");
            var categories = JsonConvert.DeserializeObject<TpCategoriesJson>(json);

            foreach (var category in categories.teleports)
            {
                var submenu = CreateServerTeleportLoactionsSubmenu(category);
                serverTpMenu.AddSubmenu(submenu, $"Teleport to ~b~{category.name}~w~.");
            }

            return serverTpMenu;
        }

        /// <summary>
        /// Creates the menu.
        /// </summary>
        private void CreateMenu()
        {

            menu = new WMenu(MenuTitle, "Teleport");


            var tpItems = new List<WMenuItem>();

            if (IsAllowed(Permission.TPTeleportToWp))
            {
                var tpToWp = new MenuItem("Teleport To Waypoint", "Teleport to the waypoint on your map.").ToWrapped();
                tpToWp.Selected += async (_s, _args) => await TeleportToWaypoint();
                tpItems.Add(tpToWp);
            }

            if (IsAllowed(Permission.TPTeleportToPrev))
            {
                var tpToPrev = new MenuItem("Teleport To Previous Location", "Teleport to the location you last teleported to.").ToWrapped();
                tpToPrev.Selected += async (_s, _args) => await TeleportToPrevTPLocation();

                tpItems.Add(tpToPrev);
            }

            if (IsAllowed(Permission.TPTeleportPersonalLocations))
            {
                personalTpLocationsMenu = new WMenu(MenuTitle, "Personal Teleport Locations");
                personalTpLocationsMenu.Opened += (_s, _args) =>
                {
                    if (personalTpLocationsMenu.Menu.GetMenuItems().Count != 0)
                        return;

                    Notify.Info("You currently do not have any personal teleport locations. Use ~b~Save Personal Teleport Location~s~ to add one.");
                    personalTpLocationsMenu.Menu.GoBack();
                };

                WMenuItem button;
                menu.BindSubmenu(personalTpLocationsMenu, out button, "Teleport to your personal teleport locations.", true);

                tpItems.Add(button);

                LoadPersonalPlayerLocations();
            }

            if (IsAllowed(Permission.TPTeleportLocations))
            {
                var teleportMenu = CreateServerTeleportLoactionsMenu();

                WMenuItem button;
                menu.BindSubmenu(teleportMenu, out button, "Teleport to pre-configured locations, added by the server owner.");

                tpItems.Add(button);
            }

            if (IsAllowed(Permission.TPTeleportToCoord))
            {
                var tpToCoord = new MenuItem("Teleport To Coordinates", "Input X, Y, Z coordinates and you will be teleported to that location.").ToWrapped();
                tpToCoord.Selected += async (_s, _args) =>
                {
                    var input = await GetUserInput("Enter X, Y, and Z coordinates", 60);
                    var coords = TryParseTpXYZString(input);
                    if (coords == null)
                        return;

                    SetPrevTpLocation(coords.Value, null, true);
                    await TeleportToCoords(coords.Value, true);
                };

                tpItems.Add(tpToCoord);
            }


            var setTpSect = new List<WMenuItem>();

            if (IsAllowed(Permission.TPTeleportToPrev))
            {
                var overridePrevBtn = new MenuItem("Override Previous Location", "Overrides the previous teleport location with your current position.").ToWrapped();
                overridePrevBtn.Selected += (_s, _args) => OverridePrevLocation();

                setTpSect.Add(overridePrevBtn);
            }

            if (IsAllowed(Permission.TPTeleportPersonalLocations))
            {
                var savePersonalLocationBtn = new MenuItem("Save Personal Teleport Location", "Adds your current location to your personal teleport locations menu.").ToWrapped();
                savePersonalLocationBtn.Selected += async (_s, _args) =>
                {
                    var name = await GetTpLocationName();
                    if (string.IsNullOrEmpty(name))
                        return;

                    var pos = Game.PlayerPed.Position;
                    var heading = Game.PlayerPed.Heading;

                    var tpLoc = new TeleportLocation(name, pos, heading);

                    SavePersonalTpLocation(tpLoc);

                    var tpLocMenu = CreatePersonalTpLocationMenu(tpLoc);
                    AddPersonalTpLocationMenu(tpLocMenu, tpLoc);
                };

                setTpSect.Add(savePersonalLocationBtn);
            }

            if (IsAllowed(Permission.TPTeleportSaveLocation))
            {
                var saveLocationBtn = new MenuItem("Save Server Teleport Location", "Adds your current location to the server teleport locations menu. ~y~A script restart is required after adding new locations.~s~").ToWrapped();
                saveLocationBtn.Selected += async (_s, _args) => await SavePlayerLocationToLocationsFile();

                setTpSect.Add(saveLocationBtn);
            }


            menu
                .AddItems(tpItems)
                .AddSection("Set Teleports", setTpSect, false);
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
            return menu.Menu;
        }
    }
}
