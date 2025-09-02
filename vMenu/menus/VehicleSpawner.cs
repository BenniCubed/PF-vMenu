using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

using MenuAPI;

using vMenuClient.data;

using static vMenuShared.ConfigManager;
using static vMenuClient.CommonFunctions;
using static vMenuShared.PermissionsManager;
using vMenuClient.MenuAPIWrapper;

namespace vMenuClient.menus
{
    public class VehicleSpawner
    {
        public struct FilterItems
        {
            public WMenuItem Name;
            public WMenuItem Manufacturer;
            public WMenuItem CustomClass;
            public WMenuItem DefaultClass;

            public int Count => 2 + (CustomClass != null ? 1 : 0) + (DefaultClass != null ? 1 : 0);
        }

        // Variables
        private WMenu menu;


        public WMenu AllVehiclesMenu;
        private VehicleData.VehicleFilter filter;
        FilterItems filterItems;

        private bool searchingByName = false;


        public void ResetAllVehiclesFilter()
        {
            filter = new VehicleData.VehicleFilter();

            filterItems.Name.Label = "";
            filterItems.Manufacturer.AsListItem().ListIndex = 0;
            if (filterItems.CustomClass != null)
            {
                filterItems.CustomClass.AsListItem().ListIndex = 0;
            }
            if (filterItems.DefaultClass != null)
            {
                filterItems.DefaultClass.AsListItem().ListIndex = 0;
            }
        }

        private int FilterAllVehiclesMenu(string name = null)
        {
            AllVehiclesMenu.Menu.ResetFilter();
            var countTotal = AllVehiclesMenu.Menu.Size;

            if (name != null)
            {
                filter.Name = name;
            }

            filterItems.Name.Label = $"~c~{filter.Name}~s~";

            AllVehiclesMenu.Menu.FilterMenuItems(mi =>
                mi.ItemData == null ||
                filter.IsMatching(mi.ItemData as VehicleData.VehicleModelInfo));
            var countFiltered = AllVehiclesMenu.Menu.Size;

            AllVehiclesMenu.ResetIncrement();

            return countFiltered - filterItems.Count;
        }

        public bool SpawnInVehicle { get; private set; } = UserDefaults.VehicleSpawnerSpawnInside;
        public bool SpawnUpgraded { get; private set; } = IsAllowed(Permission.VOMod) ? UserDefaults.VehicleSpawnerSpawnUpgraded : false;
        public bool ReplaceVehicle { get; private set; } = UserDefaults.VehicleSpawnerReplacePrevious;
        public bool SpawnDestructible { get; private set; } = UserDefaults.VehicleSpawnerSpawnDestructible;

        private List<VehicleData.VehicleModelInfo> allowedVehiclesList;


        private WMenuItem CreateSpawnVehicleButton(VehicleData.VehicleModelInfo vi)
        {
            var textColor = !vi.HasProperName ? "~y~" : vi.IsAddon ? "~q~" : "";
            var text = $"{textColor}{vi.Name}~s~";

            var manufacturerDescr = vi.Manufacturer != "NULL" ? $"~b~{vi.Manufacturer}~s~ " : "";
            var description = $"Spawn the {manufacturerDescr}~b~{vi.Name}~s~.";

            var btn = new MenuItem(text, description)
            {
                Label = $"~c~({vi.Shortname})~s~",
                ItemData = vi
            }.ToWrapped();
        btn.Selected += async (_s, _args) => await SpawnVehicle(
            vi.Shortname,
            SpawnInVehicle,
            ReplaceVehicle, destructible: SpawnDestructible,
            upgraded: SpawnUpgraded);

            return btn;
        }

        private static void SetIndexPastFilters(WMenu menu, FilterItems filterItems)
        {
            if (menu.Menu.CurrentIndex < filterItems.Count && menu.Count > filterItems.Count)
            {
                menu.Menu.RefreshIndex(filterItems.Count, 0);
            }
        }

        private void AddFilterItems(WMenu vehiclesMenu)
        {
            filter = new VehicleData.VehicleFilter();
            filterItems = new FilterItems();

            {
                var nameFilter = new MenuItem("~b~Filter By Name~s~", "Filter vehicles by (model) name or reset the filter.").ToWrapped();
                nameFilter.Selected += async (_s, _args) =>
                {
                    var input = await GetUserInput("Enter filter text. Leave empty to reset the filter", 20);
                    if (input == null)
                        return;

                    filter.Name = input;
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(0);
                };

                filterItems.Name = nameFilter;
                vehiclesMenu.AddItem(nameFilter);
            }


            {
                var manufacturers = VehicleData.DisplayVehicles
                    .Select(veh => VehicleData.AllVehicles[veh].Manufacturer)
                    .Distinct()
                    .OrderBy(s => s, Comparer<string>.Create(VehicleData.CompareManufacturers))
                    .Select(s => s == "NULL" ? "~italic~Unknown~italic~" : s);

                var manufacturerFilterOptions = Enumerable.Concat(["~italic~All~italic~"], manufacturers).ToList();
                var manufacturerFilter = new MenuListItem("~b~Filter By Manufacturer~s~", manufacturerFilterOptions, 0, "Filter vehicles by manufacturer. Click to reset the filter.").ToWrapped();
                manufacturerFilter.ListChanged += (_s, args) =>
                {
                    if (args.ListIndexNew == 0)
                    {
                        filter.Manufacturer = null;
                    }
                    else if (args.ListIndexNew == manufacturerFilterOptions.Count - 1)
                    {
                        filter.Manufacturer = "NULL";
                    }
                    else
                    {
                        filter.Manufacturer = manufacturerFilterOptions[args.ListIndexNew];
                    }
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(1);
                };
                manufacturerFilter.ListSelected += (_s, _args) =>
                {
                    manufacturerFilter.AsListItem().ListIndex = 0;
                    filter.Manufacturer = null;
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(1);
                };

                filterItems.Manufacturer = manufacturerFilter;
                vehiclesMenu.AddItem(manufacturerFilter);
            }


            bool customClassesOnly = GetSettingsBool(Setting.vmenu_only_custom_classes);

            var customClasses = VehicleData.CustomVehiclesClasses.Select(c => c.Name).ToList();
            if (customClasses.Count > 0)
            {
                var customClassesOptions = Enumerable.Concat(["~italic~All~italic~"], customClasses).ToList();
                var customClassesFilter = new MenuListItem(
                    $"~b~Filter By {(customClassesOnly ? "" : "Custom ")}Class~s~",
                    customClassesOptions,
                    0,
                    "Filter vehicles by custom class. Click to reset the filter.").ToWrapped();
                customClassesFilter.ListChanged += (_s, args) =>
                {
                    if (args.ListIndexNew == 0)
                    {
                        filter.CustomClass = null;
                    }
                    else
                    {
                        filter.CustomClass = customClassesOptions[args.ListIndexNew];
                    }
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2);
                };
                customClassesFilter.ListSelected += (_s, _args) =>
                {
                    customClassesFilter.AsListItem().ListIndex = 0;
                    filter.CustomClass = null;
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2);
                };

                filterItems.CustomClass = customClassesFilter;
                vehiclesMenu.AddItem(customClassesFilter);
            }

            if (customClasses.Count == 0 || !GetSettingsBool(Setting.vmenu_only_custom_classes))
            {
                var defaultClasses = VehicleData.DisplayVehicles
                    .Select(veh => VehicleData.AllVehicles[veh].Class)
                    .OrderBy(c => c, Comparer<int>.Create(VehicleData.CompareClasses))
                    .Distinct()
                    .Select(c => VehicleData.ClassIdToName[c]);

                var defaultClassesOptions = Enumerable.Concat(["~italic~All~italic~"], defaultClasses).ToList();
                var defaultClassesFilter = new MenuListItem(
                    $"~b~Filter By {(customClasses.Count == 0 ? "" : "Default ")}Class~s~",
                    defaultClassesOptions,
                    0,
                    "Filter vehicles by default class. Click to reset the filter.").ToWrapped();
                defaultClassesFilter.ListChanged += (_s, args) =>
                {
                    if (args.ListIndexNew == 0)
                    {
                        filter.DefaultClass = null;
                    }
                    else
                    {
                        filter.DefaultClass = defaultClassesOptions[args.ListIndexNew];
                    }
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2 + (customClasses.Count > 0 ? 1 : 0));
                };
                defaultClassesFilter.ListSelected += (_s, _args) =>
                {
                    defaultClassesFilter.AsListItem().ListIndex = 0;
                    filter.DefaultClass = null;
                    FilterAllVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2 + (customClasses.Count > 0 ? 1 : 0));
                };

                filterItems.DefaultClass = defaultClassesFilter;
                vehiclesMenu.AddItem(defaultClassesFilter);
            }

            vehiclesMenu.AddItem(WMenuItem.CreateSeparatorItem("Vehicles"));

            vehiclesMenu.Menu.InstructionalButtons.Add(Control.SelectWeapon, "Filter Vehicles");
            vehiclesMenu.Menu.ButtonPressHandlers.Add(new Menu.ButtonPressHandler(
                Control.SelectWeapon,
                Menu.ControlPressCheckType.JUST_RELEASED,
                (m, _c) =>
                {
                    if (vehiclesMenu.CurrentIndex < filterItems.Count)
                    {
                        SetIndexPastFilters(vehiclesMenu, filterItems);
                    }
                    else
                    {
                        vehiclesMenu.Menu.RefreshIndex();
                        vehiclesMenu.ResetIncrement();
                    }
                },
                true));
        }

        private WMenu CreateVehiclesMenu(string subtitle, List<VehicleData.VehicleModelInfo> vehicles, bool addFilters = false, bool showDisabled = false)
        {
            var vehiclesMenu = new WMenu(MenuTitle, subtitle);

            if (!showDisabled)
            {
                vehicles = vehicles.Where(vi => VehicleData.DisplayVehicles.Contains(vi.Shortname)).ToList();
            }

            if (addFilters)
            {
                AddFilterItems(vehiclesMenu);
            }

            if (vehicles.Count > 10)
            {
                vehiclesMenu.AddIncrementToggle(Control.NextCamera);

                vehiclesMenu.Closed += (_s, _args) => vehiclesMenu.ResetIncrement();
            }

            foreach(var vehicle in vehicles)
            {
                var btn = CreateSpawnVehicleButton(vehicle);
                vehiclesMenu.AddItem(btn);
            }

            vehiclesMenu.Opened += (s, args) =>
            {
                SetIndexPastFilters(vehiclesMenu, filterItems);
            };

            vehiclesMenu.Closed += (_s, _args) =>
            {
                if (searchingByName)
                {
                    ResetAllVehiclesFilter();
                    FilterAllVehiclesMenu();
                }
                searchingByName = false;
            };

            return vehiclesMenu;
        }

        private Random random = new Random();

        private List<string> randomVehiclesList;
        public async Task SpawnRandomVehicle()
        {
            if (randomVehiclesList.Count == 0)
            {
                Notify.Error("You are not able to spawn any random vehicles, sorry");
                return;
            }
            var veh = randomVehiclesList[random.Next(0, randomVehiclesList.Count)];
            await SpawnVehicle(
                veh,
                SpawnInVehicle,
                ReplaceVehicle,
                destructible: SpawnDestructible,
                upgraded: SpawnUpgraded);
        }

        private List<string> randomSportyVehiclesList;
        public async Task SpawnRandomSportyVehicle()
        {
            if (randomSportyVehiclesList.Count == 0)
            {
                Notify.Error("You are not able to spawn any random sporty vehicles, sorry");
                return;
            }
            var veh = randomSportyVehiclesList[random.Next(0, randomSportyVehiclesList.Count)];
            await SpawnVehicle(
                veh,
                SpawnInVehicle,
                ReplaceVehicle,
                destructible: SpawnDestructible,
                upgraded: SpawnUpgraded);
        }


        private void CreateMenu()
        {
            allowedVehiclesList = VehicleData.AllVehicles.Values
                        .Where(vi => vi.IsAllowed)
                        .OrderBy(vi => vi.Name, Comparer<string>.Create(VehicleData.CompareVehicleNames))
                        .ToList();

            randomVehiclesList = VehicleData.DisplayVehicles.Where(veh =>
                {
                    var hash = (uint)GetHashKey(veh);
                    return
                        IsThisModelABicycle(hash) ||
                        IsThisModelABike(hash) ||
                        IsThisModelACar(hash) ||
                        IsThisModelAnAmphibiousCar(hash) ||
                        IsThisModelAnAmphibiousQuadbike((int)hash) ||
                        IsThisModelAQuadbike(hash);
                }).ToList();

            randomSportyVehiclesList = randomVehiclesList.Where(veh =>
            {
                var vi = VehicleData.AllVehicles[veh];

                if (VehicleData.CustomVehicleClassesDict.TryGetValue("Sporty", out var _))
                {
                    return vi.CustomVehicleClasses.Contains("Sporty");
                }

                // 5-7 = Sports Classics, Sports, Super
                return vi.Class >= 5 && vi.Class <= 7 && !vi.CustomVehicleClasses.Contains("Meme");
            }).ToList();

            // Create the menu.
            menu = new WMenu(MenuTitle, "Spawn Vehicles");

            if (IsAllowed(Permission.VSSpawnByName))
            {
                var spawnVehicleByName = new MenuItem("Spawn Vehicle By Model Name", "Spawn a vehicle by its exact model name.").ToWrapped();
                spawnVehicleByName.Selected += async (_s, _args) => await SpawnVehicle("custom", SpawnInVehicle, ReplaceVehicle, SpawnDestructible);

                menu.AddItem(spawnVehicleByName);
            }

            {
                var searchByName = new MenuItem("Search Vehicle By Name", "Search a vehicle by its (model) name").ToWrapped();
                searchByName.Selected += async (_s, _args) =>
                {
                    var input = await GetUserInput("Enter search text", 20);
                    if (string.IsNullOrEmpty(input))
                        return;

                    ResetAllVehiclesFilter();
                    int count = FilterAllVehiclesMenu(input);
                    if (count == 0)
                    {
                        Notify.Info("No vehicles found matching this search.");

                        ResetAllVehiclesFilter();
                        FilterAllVehiclesMenu();
                    }
                    else
                    {
                        searchingByName = true;
                        MenuController.CloseAllMenus();
                        MenuController.AddSubmenu(menu.Menu, AllVehiclesMenu.Menu);
                        AllVehiclesMenu.Menu.OpenMenu();
                    }
                };

                menu.AddItem(searchByName);
            }


            {
                AllVehiclesMenu = CreateVehiclesMenu("Vehicles List", allowedVehiclesList, addFilters: true);
                menu.AddSubmenu(AllVehiclesMenu, "A list of all vehicles that you can also filter.");
            }


            {
                var spawnRandom = new MenuItem("Spawn Random Vehicle", "Spawn a random land-based vehicle.").ToWrapped();
                spawnRandom.Selected += async (_s, _args) => await SpawnRandomVehicle();

                menu.AddItem(spawnRandom);
            }

            {
                var spawnRandomSporty = new MenuItem("Spawn Random Sporty Vehicle", "Spawn a random, but sporty land-based vehicle.").ToWrapped();
                spawnRandomSporty.Selected += async (_s, _args) => await SpawnRandomSportyVehicle();

                menu.AddItem(spawnRandomSporty);
            }


            {
                var spawnOptionsMenu = new Menu(MenuTitle, "Spawn Options");
                var spawnOptionsBtn = new MenuItem("Spawn Options", "Change vehicle spawn options.");

                var spawnInVeh = new MenuCheckboxItem("Spawn Inside Vehicle", "This will teleport you into the vehicle when you spawn it.", SpawnInVehicle);
                var replacePrev = new MenuCheckboxItem("Replace Previous Vehicle", "This will automatically delete your previously spawned vehicle when you spawn a new vehicle.", ReplaceVehicle);
                var spawnUpgraded = new MenuCheckboxItem("Spawn Upgraded Vehicle", "This will spawn the vehicle with performance upgrades applied.", SpawnUpgraded);
                var spawnDestructible = new MenuCheckboxItem("Spawn Traffic-Style Vehicle", "This will make it so the spawned vehicle can despawn when too far away and explode on impact.", SpawnDestructible);

                spawnOptionsMenu.AddMenuItem(spawnInVeh);
                if (IsAllowed(Permission.VOMod))
                {
                    spawnOptionsMenu.AddMenuItem(spawnUpgraded);
                }
                if (IsAllowed(Permission.VSDisableReplacePrevious))
                {
                    spawnOptionsMenu.AddMenuItem(replacePrev);
                }
                else
                {
                    replacePrev = null;
                    ReplaceVehicle = true;
                }
                spawnOptionsMenu.AddMenuItem(spawnDestructible);

                menu.AddSubmenu(spawnOptionsMenu);

                spawnOptionsMenu.OnCheckboxChange += (sender, item, index, _checked) =>
                {
                    if (item == spawnInVeh)
                    {
                        SpawnInVehicle = _checked;
                    }
                    else if (item == spawnUpgraded)
                    {
                        UserDefaults.VehicleSpawnerSpawnUpgraded = SpawnUpgraded = _checked;
                    }
                    else if (item == replacePrev)
                    {
                        ReplaceVehicle = _checked;
                    }
                    else if (item == spawnDestructible)
                    {
                        SpawnDestructible = _checked;
                    }
                };
            }

            if (VehicleData.VehicleDisablelist.Count > 0 && IsAllowed(Permission.VODisableFromDefaultList))
            {
                var allowedDisabledVehicles = allowedVehiclesList
                    .Where(vi => VehicleData.VehicleDisablelist.Contains(vi.Shortname))
                    .ToList();

                if (allowedDisabledVehicles.Count > 0)
                {
                    var disabledVehiclesMenu = CreateVehiclesMenu("Hidden Vehicles", allowedDisabledVehicles, addFilters: false, showDisabled: true);

                    WMenuItem button = new MenuItem(
                        "~y~Hidden Vehicles~s~",
                        "Vehicles in ~b~addons.json > disablefromdefaultlist~s~. ~y~These vehicles will not show in other vehicle lists and can only be spawned by players with the ~o~VODisableFromDefaultList~y~ permission.~s~")
                        .ToWrapped();
                    menu.BindSubmenu(disabledVehiclesMenu, button);

                    menu.AddItem(button);
                }
            }

            if (VehicleData.VehicleBlacklist.Count > 0 && IsAllowed(Permission.VOVehiclesBlacklist))
            {
                var allowedBlacklistedVehicles = allowedVehiclesList
                    .Where(vi => VehicleData.VehicleBlacklist.Contains(vi.Shortname))
                    .ToList();

                if (allowedBlacklistedVehicles.Count > 0)
                {
                    var disabledVehiclesMenu = CreateVehiclesMenu("Blacklisted Vehicles", allowedBlacklistedVehicles, addFilters: false, showDisabled: false);

                    WMenuItem button = new MenuItem(
                        "~y~Blacklisted Vehicles~s~",
                        "Vehicles in ~b~addons.json > vehicleblacklist~s~. ~y~These vehicles ~italic~will~italic~ show in other vehicle lists, but can only be spawned by players with the ~o~VOVehiclesBlacklist~y~ permission.~s~")
                        .ToWrapped();
                    menu.BindSubmenu(disabledVehiclesMenu, button);

                    menu.AddItem(button);
                }
            }
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
