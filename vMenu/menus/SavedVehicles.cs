using System;
using System.Collections.Generic;
using System.Linq;

using CitizenFX.Core;

using MenuAPI;

using vMenuClient.data;
using vMenuClient.MenuAPIWrapper;

using static vMenuClient.CommonFunctions;
using static vMenuShared.ConfigManager;

namespace vMenuClient.menus
{
    public class SavedVehicles
    {
        // Variables
        private WMenu menu;

        private WMenu manageAvailableVehicleMenu = new WMenu(MenuTitle, "CHANGE ME");
        private WMenu manageUnavailableVehicleMenu = new WMenu(MenuTitle, "CHANGE ME");


        private Tuple<string, VehicleInfo> selectedVehicle;

        private struct SavedVehiclesMenuData
        {
            public WMenu Menu;
            public List<WMenuItem> VehicleButtons;
        };

        public struct FilterItems
        {
            public WMenuItem Name;
            public WMenuItem Manufacturer;
            public WMenuItem CustomClass;
            public WMenuItem DefaultClass;

            public int Count => 2 + (CustomClass != null ? 1 : 0) + (DefaultClass != null ? 1 : 0);
        }

        private bool searchingByName = false;


        private SavedVehiclesMenuData availableSavedVehiclesMenuData;
        private VehicleData.VehicleFilter filter;
        private FilterItems filterItems;


        public void ResetAvailableSavedVehiclesFilter()
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

        private int FilterAvailableSavedVehiclesMenu(string name = null)
        {
            availableSavedVehiclesMenuData.Menu.Menu.ResetFilter();
            var countTotal = availableSavedVehiclesMenuData.Menu.Menu.Size;

            if (name != null)
            {
                filter.Name = name;
            }

            filterItems.Name.Label = $"~c~{filter.Name}~s~";

            availableSavedVehiclesMenuData.Menu.Menu.FilterMenuItems(mi =>
            {
                if (mi.ItemData is Tuple<string, VehicleData.VehicleModelInfo> t)
                {
                    return filter.IsMatching(t.Item2, t.Item1);
                }
                else
                {
                    return true;
                }
            });
            var countFiltered = availableSavedVehiclesMenuData.Menu.Menu.Size;

            availableSavedVehiclesMenuData.Menu.ResetIncrement();

            return countFiltered - filterItems.Count;
        }


        private SavedVehiclesMenuData unavailableSavedVehiclesMenuData;


        public WMenu CreateManageVehicleMenu(bool available)
        {
            var manageMenu = new WMenu(MenuTitle, "CHANGE_ME");

            WMenuItem spawnVehicle = null;
            if (available)
            {
                spawnVehicle = new MenuItem("Spawn", "Spawn this saved vehicle.").ToWrapped();
                spawnVehicle.Selected += async (_s, _args) =>
                {
                    if (MainMenu.VehicleSpawnerMenu != null)
                    {
                        await SpawnVehicle(
                            selectedVehicle.Item2.model,
                            MainMenu.VehicleSpawnerMenu.SpawnInVehicle,
                            MainMenu.VehicleSpawnerMenu.ReplaceVehicle,
                            false,
                            vehicleInfo: selectedVehicle.Item2,
                            saveName: selectedVehicle.Item1,
                            destructible: MainMenu.VehicleSpawnerMenu.SpawnDestructible,
                            upgraded: false);
                    }
                    else
                    {
                        await SpawnVehicle(
                            selectedVehicle.Item2.model,
                            true,
                            true,
                            false,
                            vehicleInfo: selectedVehicle.Item2,
                            saveName: selectedVehicle.Item1,
                            destructible: false,
                            upgraded: false);
                    }
                };
            }

            var renameVehicle = new MenuItem("Rename Vehicle", "Rename this saved vehicle.").ToWrapped();
            renameVehicle.Selected += async (s_ , _args) =>
            {
                var newName = await GetUserInput("Enter a new name for this vehicle", 30);
                if (string.IsNullOrEmpty(newName))
                {
                    Notify.Error(CommonErrors.InvalidInput);
                }
                else
                {
                    if (StorageManager.SaveVehicleInfo("veh_" + newName, selectedVehicle.Item2, false))
                    {
                        KeyValueStore.Remove($"veh_{selectedVehicle.Item1}");

                        selectedVehicle = new Tuple<string, VehicleInfo>(newName, selectedVehicle.Item2);
                        manageMenu.Menu.MenuSubtitle = newName;

                        RecreateVehicleMenus();
                    }
                    else
                    {
                        Notify.Error("This name is already in use or something unknown failed. Contact the server owner if you believe something is wrong.");
                    }
                }
            };

            var replaceVehicle = WMenuItem.CreateConfirmationButton("~r~Replace Vehicle~s~", "Replace the saved vehicle with the vehicle you are currently in. ~y~This cannot be undone!~s~");
            replaceVehicle.Confirmed += async (_s, _args) =>
            {
                if (Game.PlayerPed.IsInVehicle())
                {
                    await SaveVehicle(selectedVehicle.Item1);

                    RecreateVehicleMenus();
                    manageMenu.Menu.GoBack();

                    Notify.Success("Your saved vehicle has been replaced with your current vehicle.");
                }
                else
                {
                    Notify.Error("You need to be in a vehicle before you can replace your old vehicle.");
                }
            };

            var deleteVehicle = WMenuItem.CreateConfirmationButton("~r~Delete Vehicle~s~", "This will delete the saved vehicle. ~y~This cannot be undone!~s~");
            deleteVehicle.Confirmed += (_s, _args) =>
            {
                KeyValueStore.Remove($"veh_{selectedVehicle.Item1}");

                RecreateVehicleMenus();
                manageMenu.Menu.GoBack();

                Notify.Success("The saved vehicle has been deleted.");
            };

            manageMenu.AddItems([spawnVehicle, renameVehicle, replaceVehicle, deleteVehicle]);

            manageMenu.Closed += (_s, _args) =>
            {
                manageMenu.Menu.RefreshIndex();
            };

            return manageMenu;
        }

        private WMenuItem CreateAvailableVehicleButton(string name, VehicleInfo info)
        {
            var vi = VehicleData.HashToVehicle[info.model];

            var textColor = !vi.HasProperName ? "~y~" : vi.IsAddon ? "~q~" : "";
            var text = $"{textColor}{name}~s~";

            var manufacturerDescr = vi.Manufacturer != "NULL" ? $"~b~{vi.Manufacturer}~s~ " : "";
            var description = $"Spawn your {manufacturerDescr}~b~{vi.Name}~s~: ~b~{name}~s~.";

            var labelMaxLen = Math.Max(42 - 2 - text.Length, 0);
            string label;
            if (vi.Name.Length <= labelMaxLen)
            {
                label = vi.Name;
            }
            else if (labelMaxLen >= 3)
            {
                label = vi.Name.Substring(0, labelMaxLen - 2).TrimEnd([' ', '-', '/', '(']) + "..";
            }
            else
            {
                label = null;
            }

            var btn = new MenuItem(text, description)
            {
                Label = (label != null ? $"~c~({label})~s~" : "") + " →→→",
                ItemData = vi
            }.ToWrapped();
            btn.Selected += (_s, _args) =>
            {
                selectedVehicle = new Tuple<string, VehicleInfo>(name, info);
                manageAvailableVehicleMenu.Menu.MenuSubtitle = name;
            };

            return btn;
        }

        private WMenuItem CreateUnavailableVehicleButton(string name, VehicleInfo info)
        {
            var btn = new MenuItem($"~c~{name}~s~")
            {
                Label = $"~c~({info.name})~s~ →→→",
            }.ToWrapped();
            btn.Selected += (_s, _args) =>
            {
                selectedVehicle = new Tuple<string, VehicleInfo>(name, info);
                manageUnavailableVehicleMenu.Menu.MenuSubtitle = name;
            };

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
                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(0);
                };

                vehiclesMenu.AddItem(nameFilter);
                filterItems.Name = nameFilter;
            }


            {
                var manufacturers = VehicleData.AllowedVehicles
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

                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(1);
                };
                manufacturerFilter.ListSelected += (_s, _args) =>
                {
                    manufacturerFilter.AsListItem().ListIndex = 0;
                    filter.Manufacturer = null;

                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(1);
                };

                vehiclesMenu.AddItem(manufacturerFilter);
                filterItems.Manufacturer = manufacturerFilter;
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

                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2);
                };
                customClassesFilter.ListSelected += (_s, _args) =>
                {
                    customClassesFilter.AsListItem().ListIndex = 0;
                    filter.CustomClass = null;

                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2);
                };

                vehiclesMenu.AddItem(customClassesFilter);
                filterItems.CustomClass = customClassesFilter;
            }

            if (customClasses.Count == 0 || !GetSettingsBool(Setting.vmenu_only_custom_classes))
            {
                var defaultClasses = VehicleData.AllowedVehicles
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

                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2 + (customClasses.Count > 0 ? 1 : 0));
                };
                defaultClassesFilter.ListSelected += (_s, _args) =>
                {
                    defaultClassesFilter.AsListItem().ListIndex = 0;
                    filter.DefaultClass = null;

                    FilterAvailableSavedVehiclesMenu();
                    vehiclesMenu.Menu.RefreshIndex(2 + (customClasses.Count > 0 ? 1 : 0));
                };

                vehiclesMenu.AddItem(defaultClassesFilter);
                filterItems.DefaultClass = defaultClassesFilter;
            }

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
                        vehiclesMenu.Menu.RefreshIndex(0);
                        vehiclesMenu.ResetIncrement();
                    }
                },
                true));

            vehiclesMenu.AddItem(WMenuItem.CreateSeparatorItem("Vehicles"));
        }

        private SavedVehiclesMenuData CreateVehiclesMenu(string subtitle, bool addFilters = false)
        {
            SavedVehiclesMenuData menuData = new SavedVehiclesMenuData();

            menuData.Menu = new WMenu(MenuTitle, subtitle);
            menuData.VehicleButtons = new List<WMenuItem>();

            if (addFilters)
            {
                AddFilterItems(menuData.Menu);
            }

            menuData.Menu.AddIncrementToggle(Control.NextCamera);

            menuData.Menu.Opened += (s, args) =>
            {
                SetIndexPastFilters(menuData.Menu, filterItems);
            };

            menuData.Menu.Closed += (s, args) =>
            {
                menuData.Menu.ResetIncrement();
                if (searchingByName)
                {
                    ResetAvailableSavedVehiclesFilter();
                    FilterAvailableSavedVehiclesMenu();
                }
                searchingByName = false;
            };

            return menuData;
        }

        private void PopulateVehiclesMenu(SavedVehiclesMenuData menuData, List<Tuple<string, VehicleInfo>> vehicles, bool available)
        {
            foreach (var btn in menuData.VehicleButtons)
            {
                menuData.Menu.RemoveItem(btn);
            }
            menuData.VehicleButtons.Clear();

            foreach(var vehicle in vehicles)
            {
                var btn = available
                    ? CreateAvailableVehicleButton(vehicle.Item1, vehicle.Item2)
                    : CreateUnavailableVehicleButton(vehicle.Item1, vehicle.Item2);
                menuData.Menu.BindSubmenu(manageAvailableVehicleMenu, btn, addLabel: false);
                menuData.Menu.AddItem(btn);
                menuData.VehicleButtons.Add(btn);

                VehicleData.VehicleModelInfo modelInfo;
                if (!VehicleData.HashToVehicle.TryGetValue(vehicle.Item2.model, out modelInfo))
                {
                    modelInfo = new VehicleData.VehicleModelInfo("");
                }

                btn.ItemData = new Tuple<string, VehicleData.VehicleModelInfo>(vehicle.Item1, modelInfo);
            }
        }


        private void RecreateVehicleMenus()
        {
            var savedVehicles = GetSavedVehicles()
                .Select(kv => new Tuple<string, VehicleInfo>(kv.Key.Substring(4), kv.Value))
                .OrderBy(kv => kv.Item1, Comparer<string>.Create(VehicleData.CompareVehicleNames));

            var availableSavedVehicles = savedVehicles
                .Where(t =>
                {
                    if (VehicleData.HashToVehicle.TryGetValue(t.Item2.model, out var vi))
                    {
                        return vi.IsAllowed;
                    }
                    return false;
                })
                .ToList();
            PopulateVehiclesMenu(availableSavedVehiclesMenuData, availableSavedVehicles, true);
            FilterAvailableSavedVehiclesMenu();

            var unavailableSavedVehicles = savedVehicles
                .Where(t =>
                {
                    if (VehicleData.HashToVehicle.TryGetValue(t.Item2.model, out var vi))
                    {
                        return !vi.IsAllowed;
                    }
                    return true;
                })
                .ToList();
            PopulateVehiclesMenu(unavailableSavedVehiclesMenuData, unavailableSavedVehicles, false);
        }

        /// <summary>
        /// Creates the menu.
        /// </summary>
        private void CreateMenu()
        {
            #region Create menus and submenus
            // Create the menu.
            menu = new WMenu(MenuTitle, "Saved Vehicles");

            {
                var saveVehicle = new MenuItem("Save Current Vehicle", "Save the vehicle you are currently in.").ToWrapped();
                saveVehicle.Selected += async (_s, _args) =>
                {
                    var name = await SaveVehicle();
                    RecreateVehicleMenus();
                };
                menu.AddItem(saveVehicle);
            }

            {
                var searchByName = new MenuItem("Search Vehicle By Name", "Search a saved vehicle by its (model) name").ToWrapped();
                searchByName.Selected += async (_s, _args) =>
                {
                    var input = await GetUserInput("Enter search text", 20);
                    if (string.IsNullOrEmpty(input))
                        return;

                    ResetAvailableSavedVehiclesFilter();
                    int count = FilterAvailableSavedVehiclesMenu(input);
                    if (count == 0)
                    {
                        Notify.Info("No vehicles found matching this search.");

                        ResetAvailableSavedVehiclesFilter();
                        FilterAvailableSavedVehiclesMenu();
                    }
                    else
                    {
                        searchingByName = true;
                        MenuController.CloseAllMenus();
                        availableSavedVehiclesMenuData.Menu.Menu.OpenMenu();
                        availableSavedVehiclesMenuData.Menu.Menu.RefreshIndex(filterItems.Count);
                    }
                };

                menu.AddItem(searchByName);
            }

            {
                manageAvailableVehicleMenu = CreateManageVehicleMenu(true);
                availableSavedVehiclesMenuData = CreateVehiclesMenu("Saved Vehicles List", true);

                menu.AddSubmenu(availableSavedVehiclesMenuData.Menu, "A list of all saved vehicles that you can also filter.", true);
            }

            {
                manageUnavailableVehicleMenu = CreateManageVehicleMenu(false);
                unavailableSavedVehiclesMenuData = CreateVehiclesMenu("Unavailable Saved Vehicles", false);

                var btn = new MenuItem("~c~Unavailable Saved Vehicles~s~", "A list of all saved vehicles that are unavailable on this server, either because the vehicle does not exists or you are not allowed to spawn it.");
                menu.BindSubmenu(unavailableSavedVehiclesMenuData.Menu, btn);
                menu.AddItem(btn);
            }

            RecreateVehicleMenus();

            #endregion
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
