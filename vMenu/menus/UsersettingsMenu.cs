using MenuAPI;

using static vMenuClient.data.Usersettings;

using vMenuClient.MenuAPIWrapper;

using CitizenFX.Core;

using System.Linq;
using System.Collections.Generic;
using System;

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

        private void CreateMenu()
        {
            menu = new WMenu(CommonFunctions.MenuTitle, UsersettingsMenuName);

            foreach (var spec in UsersettingsSpecs)
            {
                WMenuItem item = null;
                var setting = UsersettingsDict[spec.key];

                spec.Visit(
                    _ => item = CreateListSpecItem(spec, setting),
                    _ => item = CreateRangeSpecItem(spec, (int)setting),
                    _ => item = CreateToggleSpecItem(spec, (bool)setting));

                item.ItemData = spec;
                usersettingItems.Add(spec.key, item);
                menu.AddItem(item);
            }
        }

        public WMenu GetMenu()
        {
            if (menu == null)
            {
                CreateMenu();
            }
            return menu;
        }

        private WMenu menu;
    }
}
