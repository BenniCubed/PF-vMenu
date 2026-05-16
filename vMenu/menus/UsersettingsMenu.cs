using MenuAPI;

using static vMenuClient.data.Usersettings;

using vMenuClient.MenuAPIWrapper;

using System.Linq;
using System.Collections.Generic;

using vMenuShared;

using CitizenFX.Core;

namespace vMenuClient.menus
{
    public class Usersettings
    {
        private Dictionary<string, WMenuItem> usersettingItems = new Dictionary<string, WMenuItem>();

        public void UpdateUsersettingItemState(string key, object value)
        {
            var ok = usersettingItems.TryGetValue(key, out var item);
            if (!ok)
            {
                return;
            }

            var spec = item.ItemData as UsersettingSpec;
            spec.Visit(
                s => item.AsListItem().ListIndex = s.GetKeyIndex(value),
                s => item.AsListItem().ListIndex = s.GetValueIndex((int)value),
                s => item.AsCheckboxItem().Checked = (bool)value);
        }

        private void UpdateUsersetting(string key, object value)
        {
            TryUpdateUsersetting(key, value, true, true);
        }

        private WMenuItem CreateListSpecItem(UsersettingSpec spec, object initialKey)
        {
            var key = spec.key;
            var listSpec = spec.listSpec;

            var items = listSpec.items;

            var listItems = items.Select(i => i.name).ToList();
            var index = listSpec.GetKeyIndex(initialKey);
            var menuItem = new MenuListItem(spec.name, listItems, index, spec.description).ToWrapped();
            menuItem.ListChanged += (_, e) =>
            {
                UpdateUsersetting(key, items[e.ListIndexNew].key);
            };

            return menuItem;
        }

        private WMenuItem CreateRangeSpecItem(UsersettingSpec spec, int initialValue)
        {
            var key = spec.key;
            var rangeSpec = spec.rangeSpec;
            var begin = rangeSpec.begin;
            var step = rangeSpec.step;

            var listItems = new List<string>();
            for (int i = begin; i <= rangeSpec.end; i += step)
            {
                listItems.Add($"{i}");
            }

            var index = rangeSpec.GetValueIndex(initialValue);
            var menuItem = new MenuListItem(spec.name, listItems, index, spec.description).ToWrapped();
            menuItem.ListChanged += (_, e) =>
            {
                UpdateUsersetting(key, begin + e.ListIndexNew * step);
            };

            return menuItem;
        }

        private WMenuItem CreateToggleSpecItem(UsersettingSpec spec, bool initialState)
        {
            var toggleSpec = spec.toggleSpec;

            var menuItem = new MenuCheckboxItem(spec.name, spec.description, initialState).ToWrapped();
            menuItem.CheckboxChanged += (_, e) =>
            {
                UpdateUsersetting(spec.key, e.Checked);
            };

            return menuItem;
        }

        private WMenu CreateMenu(UsersettingsMenuSpec menuSpec)
        {
            var menu = new WMenu(CommonFunctions.MenuTitle, menuSpec.menuName);

            foreach (var spec in menuSpec.deserializedSpecs)
            {
                var submenuSpec = spec as UsersettingsMenuSpec;
                var usersettingSpec = spec as UsersettingSpec;

                var permission = submenuSpec != null
                    ? submenuSpec.permission
                    : usersettingSpec.permission;
                if (!string.IsNullOrEmpty(permission) && !PermissionsManager.IsAllowed(permission))
                {
                    continue;
                }

                if (submenuSpec != null)
                {
                    var submenu = CreateMenu(submenuSpec);
                    menu.AddSubmenu(submenu, submenuSpec.menuDescription ?? "");
                }
                else if (usersettingSpec != null)
                {
                    WMenuItem item = null;
                    var setting = UsersettingsDict[usersettingSpec.key];

                    usersettingSpec.Visit(
                        _ => item = CreateListSpecItem(usersettingSpec, setting),
                        _ => item = CreateRangeSpecItem(usersettingSpec, (int)setting),
                        _ => item = CreateToggleSpecItem(usersettingSpec, (bool)setting));

                    item.ItemData = spec;
                    usersettingItems.Add(usersettingSpec.key, item);
                    menu.AddItem(item);
                }
                else
                {
                    Debug.WriteLine($"[ERROR] Unknown usersetting menu item spec");
                }
            }

            return menu;
        }

        public WMenu GetMenu()
        {
            if (menu == null)
            {
                menu = CreateMenu(MenuSpec);
            }
            return menu;
        }

        private WMenu menu;
    }
}
