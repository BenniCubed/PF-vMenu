using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using CitizenFX.Core;

using MenuAPI;

namespace vMenuClient
{
    namespace MenuAPIWrapper
    {
        public class Section : Tuple<string, IEnumerable<WMenuItem>>
        {
            public Section(string name, IEnumerable<WMenuItem> items) : base(name, items) {}
        }

        public static class Extensions
        {
            public static WMenuItem ToWrapped(this MenuItem menuItem)
            {
                return new WMenuItem(menuItem);
            }
        }

        public class EventArgs : System.EventArgs
        {
            public WMenu Menu { get; set; }
        }

        public class WMenuItem
        {
            #region EventArgs
            public class EventArgs<ItemType> : EventArgs
            {
                public int ItemIndex { get; set; }
                public ItemType Item { get; set; }
            }

            public class DynamicListEventArgs : EventArgs
            {
                public MenuDynamicListItem Item { get; set; }
            }

            public class SelectedEventArgs : EventArgs<MenuItem> { }

            public class CheckboxChangedEventArgs : EventArgs<MenuCheckboxItem>
            {
                public bool Checked { get; set; }
            }

            public class DynamicListChangedEventArgs : DynamicListEventArgs
            {
                public string ValueOld { get; set; }
                public string ValueNew { get; set; }
            }

            public class DynamicListSelectedEventArgs : DynamicListEventArgs
            {
                public string Value { get; set; }
            }

            public class ListChangedEventArgs : EventArgs<MenuListItem>
            {
                public int ListIndexOld { get; set; }
                public int ListIndexNew { get; set; }
            }

            public class ListSelectedEventArgs : EventArgs<MenuListItem>
            {
                public int ListIndex { get; set; }
            }

            public class SliderChangedEventArgs : EventArgs<MenuSliderItem>
            {
                public int PositionOld { get; set; }
                public int PositionNew { get; set; }
            }

            public class SliderSelectedEventArgs : EventArgs<MenuSliderItem>
            {
                public int Position { get; set; }
            }
            #endregion

            #region Events
            public event EventHandler<SelectedEventArgs> Selected;
            public event EventHandler<CheckboxChangedEventArgs> CheckboxChanged;
            public event EventHandler<DynamicListChangedEventArgs> DynamicListChanged;
            public event EventHandler<DynamicListSelectedEventArgs> DynamicListSelected;
            public event EventHandler<ListChangedEventArgs> ListChanged;
            public event EventHandler<ListSelectedEventArgs> ListSelected;
            public event EventHandler<SliderChangedEventArgs> SliderChanged;
            public event EventHandler<SliderSelectedEventArgs> SliderSelected;

            public event EventHandler<SelectedEventArgs> Confirmed;

            public event EventHandler<WMenu.IndexChangedEvenArgs> MenuIndexChanged;
            public event EventHandler<WMenu.OpenedEvenArgs> MenuOpened;
            public event EventHandler<WMenu.ClosedEvenArgs> MenuClosed;


            internal void OnSelected(SelectedEventArgs args) => Selected?.Invoke(this, args);
            internal void OnCheckboxChanged(CheckboxChangedEventArgs args) => CheckboxChanged?.Invoke(this, args);
            internal void OnDynamicListChanged(DynamicListChangedEventArgs args) => DynamicListChanged?.Invoke(this, args);
            internal void OnDynamicListSelected(DynamicListSelectedEventArgs args) => DynamicListSelected?.Invoke(this, args);
            internal void OnListChanged(ListChangedEventArgs args) => ListChanged?.Invoke(this, args);
            internal void OnListSelected(ListSelectedEventArgs args) => ListSelected?.Invoke(this, args);
            internal void OnSliderChanged(SliderChangedEventArgs args) => SliderChanged?.Invoke(this, args);
            internal void OnSliderSelected(SliderSelectedEventArgs args) => SliderSelected?.Invoke(this, args);

            internal void OnMenuIndexChanged(WMenu.IndexChangedEvenArgs args) => MenuIndexChanged?.Invoke(this, args);
            internal void OnMenuOpened(WMenu.OpenedEvenArgs args) => MenuOpened?.Invoke(this, args);
            internal void OnMenuClosed(WMenu.ClosedEvenArgs args) => MenuClosed?.Invoke(this, args);
            #endregion

            public MenuItem MenuItem { get; private set; }

            public MenuCheckboxItem AsCheckboxItem() => (MenuCheckboxItem)MenuItem;
            public MenuDynamicListItem AsDynamicListItem() => (MenuDynamicListItem)MenuItem;
            public MenuListItem AsListItem() => (MenuListItem)MenuItem;
            public MenuSliderItem AsSliderItem() => (MenuSliderItem)MenuItem;

            public int Index => MenuItem.Index;
            public string Label
            {
                get => MenuItem.Label;
                set => MenuItem.Label = value;
            }
            public string Text
            {
                get => MenuItem.Text;
                set => MenuItem.Text = value;
            }
            public dynamic ItemData
            {
                get => MenuItem.ItemData;
                set => MenuItem.ItemData = value;
            }

            public WMenuItem(MenuItem menuItem)
            {
                if (menuItem == null)
                    throw new ArgumentNullException(nameof(menuItem));
                MenuItem = menuItem;
            }

            public static WMenuItem CreateConfirmationButton(string text, string description, int neededConfirmations = 2)
            {
                var button = new MenuItem(text, description).ToWrapped();

                int confirmations;

                void Reset()
                {
                    confirmations = -1;
                    button.Label = $"~c~{neededConfirmations} Confirm{(neededConfirmations > 1 ? "s": "")}~s~";
                }

                Reset();

                button.Selected += (sender, args) =>
                {
                    ++confirmations;
                    if (confirmations == neededConfirmations)
                    {
                        button.Confirmed?.Invoke(sender, args);
                        Reset();
                        return;
                    }

                    var qmarks = Enumerable.Range(0, neededConfirmations - confirmations).Select(_ => "?");
                    button.Label = $"~o~~h~SURE{string.Join("", qmarks)}~h~~s~";
                };

                button.MenuIndexChanged += (_s, args) =>
                {
                    if (args.IndexOld == button.Index)
                        Reset();
                };

                button.MenuOpened += (_s, _args) => Reset();
                button.MenuClosed += (_s, _args) => Reset();

                return button;
            }

            public static WMenuItem CreateSeparatorItem(string _)
            {
                return null;
            }
        }

        public class WMenu
        {
            public class IndexChangedEvenArgs : EventArgs
            {
                public WMenuItem ItemOld { get; set; }
                public WMenuItem ItemNew { get; set; }

                public int IndexOld { get; set; }
                public int IndexNew { get; set; }
            }

            public class OpenedEvenArgs : EventArgs { }
            public class ClosedEvenArgs : EventArgs { }

            public event EventHandler<IndexChangedEvenArgs> IndexChanged;
            public event EventHandler<OpenedEvenArgs> Opened;
            public event EventHandler<ClosedEvenArgs> Closed;

            private Dictionary<MenuItem, WMenuItem> itemsDict = new Dictionary<MenuItem, WMenuItem>();
            private Dictionary<MenuItem, Menu> submenuButtons = new Dictionary<MenuItem, Menu>();

            public int Count => Menu.GetMenuItems().Count;
            public int CurrentIndex => Menu.CurrentIndex;
            public WMenuItem this[int index] => itemsDict[Menu.GetMenuItems()[index]];

            public Menu Menu { get; private set; }


            private int PerformIncrement(int initialIndex, bool down)
            {
                if (down && initialIndex == Count - 1)
                {
                    ResetIncrement();
                    return 0;
                }

                if (!down && initialIndex == 0)
                {
                    ResetIncrement();
                    return Count - 1;
                }

                if (down && initialIndex + Increment >= Count)
                {
                    ResetIncrement();
                    return Count - 1;
                }

                if (!down && initialIndex - Increment < 0)
                {
                    ResetIncrement();
                    return 0;
                }

                return initialIndex + (down ? Increment : -Increment);
            }


            public WMenu(string title, string subtitle)
            {
                Menu = new Menu(title, subtitle);

                Menu.OnIndexChange += (menu, itemOld, itemNew, indexOld, indexNew) =>
                {
                    indexNew = PerformIncrement(indexOld, indexNew == indexOld + 1 || (indexNew == 0 && indexOld != 1));
                    if (indexNew == indexOld)
                        return;

                    int newViewIndexOffset = Menu.ViewIndexOffset;
                    if (indexNew < Menu.ViewIndexOffset)
                    {
                        newViewIndexOffset = indexNew;
                    }
                    else if (indexNew >= Menu.ViewIndexOffset + Menu.MaxItemsOnScreen)
                    {
                        newViewIndexOffset = indexNew - Menu.MaxItemsOnScreen + 1;
                    }

                    Menu.RefreshIndex(indexNew, newViewIndexOffset);


                    var args = new IndexChangedEvenArgs
                    {
                        ItemOld = itemsDict[itemOld],
                        ItemNew = itemsDict[Menu.GetMenuItems()[indexNew]],

                        IndexOld = indexOld,
                        IndexNew = indexNew,
                    };


                    IndexChanged?.Invoke(this, args);

                    var witemOld = itemsDict[itemOld];
                    var witemNew= itemsDict[itemNew];

                    witemOld?.OnMenuIndexChanged(args);
                    witemNew?.OnMenuIndexChanged(args);
                };

                Menu.OnMenuOpen += (menu) =>
                {
                    var args = new OpenedEvenArgs { Menu = this };

                    Opened?.Invoke(this, args);
                    foreach (var item in itemsDict.ToList())
                    {
                        item.Value.OnMenuOpened(args);
                    }
                };

                Menu.OnMenuClose += (_menu) =>
                {
                    var args = new ClosedEvenArgs { Menu = this };

                    Closed?.Invoke(this, args);
                    foreach (var item in itemsDict.ToList())
                    {
                        item.Value.OnMenuClosed(args);
                    }
                };

                Menu.OnItemSelect += (_menu, item, index) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnSelected(new WMenuItem.SelectedEventArgs
                    {
                        Menu = this,
                        Item = item,

                    });
                };

                Menu.OnCheckboxChange += (_menu, item, index, checked_) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnCheckboxChanged(new WMenuItem.CheckboxChangedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        ItemIndex = index,
                        Checked = checked_,
                    });
                };

                Menu.OnDynamicListItemCurrentItemChange += (_menu, item, valueOld, valueNew) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnDynamicListChanged(new WMenuItem.DynamicListChangedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        ValueOld = valueOld,
                        ValueNew = valueNew,
                    });
                };

                Menu.OnDynamicListItemSelect += (_menu, item, value) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnDynamicListSelected(new WMenuItem.DynamicListSelectedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        Value = value,
                    });
                };

                Menu.OnListIndexChange += (_menu, item, listIndexOld, listIndexNew, itemIndex) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnListChanged(new WMenuItem.ListChangedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        ItemIndex = itemIndex,
                        ListIndexOld = listIndexOld,
                        ListIndexNew = listIndexNew,
                    });
                };

                Menu.OnListItemSelect += (_menu, item, listIndex, itemIndex) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnListSelected(new WMenuItem.ListSelectedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        ItemIndex = itemIndex,
                        ListIndex = listIndex,
                    });
                };

                Menu.OnSliderPositionChange += (_menu, item, positionOld, positionNew, itemIndex) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnSliderChanged(new WMenuItem.SliderChangedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        ItemIndex = itemIndex,
                        PositionOld = positionOld,
                        PositionNew = positionNew,
                    });
                };

                Menu.OnSliderItemSelect += (_menu, item, position, itemIndex) =>
                {
                    var witem = itemsDict[item];
                    witem?.OnSliderSelected(new WMenuItem.SliderSelectedEventArgs
                    {
                        Menu = this,
                        Item = item,
                        ItemIndex = itemIndex,
                        Position = position,
                    });
                };
            }

            public WMenu AddItem(MenuItem menuItem)
            {
                return AddItem(menuItem.ToWrapped());
            }
            public WMenu AddItem(WMenuItem menuItem)
            {
                if (menuItem == null)
                    return this;

                itemsDict[menuItem.MenuItem] = menuItem;
                Menu.AddMenuItem(menuItem.MenuItem);
                return this;
            }

            public WMenu RemoveItem(MenuItem menuItem)
            {
                Menu.RemoveMenuItem(menuItem);
                itemsDict.Remove(menuItem);
                submenuButtons.Remove(menuItem);
                return this;
            }
            public WMenu RemoveItem(WMenuItem menuItem) => RemoveItem(menuItem.MenuItem);

            public WMenu AddItems(IEnumerable<WMenuItem> menuItems)
            {
                foreach (var menuItem in menuItems)
                {
                    if (menuItem != null)
                        AddItem(menuItem);
                }
                return this;
            }

            public WMenu AddSection(string _1, IEnumerable<WMenuItem> menuItems, bool _2 = true)
            {
                if (menuItems.Count() == 0)
                    return this;

                AddItems(menuItems);

                return this;
            }

            public WMenu AddSections(IEnumerable<Tuple<string, IEnumerable<WMenuItem>>> sections, bool singleSectionHeading = false)
            {
                sections = sections.Where(s => s.Item2.Count() > 0);

                if (sections.Count() == 0)
                    return this;

                if (sections.Count() == 1)
                {
                    var section = sections.ElementAt(0);
                    if (singleSectionHeading)
                    {
                        AddSection(section.Item1, section.Item2);
                    }
                    else
                    {
                        AddItems(section.Item2);
                    }
                }
                else
                {
                    foreach (var section in sections)
                    {
                        AddSection(section.Item1, section.Item2);
                    }
                }

                return this;
            }

            public WMenu RegisterSubmenu(Menu submenu)
            {
                MenuController.AddSubmenu(Menu, submenu);
                return this;
            }
            public WMenu RegisterSubmenu(WMenu submenu) => RegisterSubmenu(submenu.Menu);

            public WMenu BindSubmenu(Menu submenu, MenuItem button, bool addLabel = true)
            {
                if (addLabel)
                {
                    button.Label = "→→→";
                }

                // This already calls MenuController.AddSubmenu(), so no need to call RegisterSubmenu()
                MenuController.BindMenuItem(Menu, submenu, button);
                submenuButtons[button] = submenu;

                return this;
            }
            public WMenu BindSubmenu(WMenu submenu, MenuItem button, bool addLabel = true) =>
                BindSubmenu(submenu.Menu, button, addLabel);
            public WMenu BindSubmenu(Menu submenu, WMenuItem button, bool addLabel = true) =>
                BindSubmenu(submenu, button.MenuItem, addLabel);
            public WMenu BindSubmenu(WMenu submenu, WMenuItem button, bool addLabel = true) =>
                BindSubmenu(submenu.Menu, button.MenuItem, addLabel);

            public WMenu BindSubmenu(Menu submenu, out MenuItem button, string description = "", bool addEmpty = false)
            {
                if (submenu.GetMenuItems().Count == 0 && !addEmpty)
                {
                    button = null;
                    return this;
                }

                button = new MenuItem(submenu.MenuSubtitle, description);
                return BindSubmenu(submenu, button, true);
            }
            public WMenu BindSubmenu(WMenu submenu, out MenuItem button, string description = "", bool addEmpty = false) =>
                BindSubmenu(submenu.Menu, out button, description, addEmpty);
            public WMenu BindSubmenu(Menu submenu, out WMenuItem button, string description = "", bool addEmpty = false)
            {
                MenuItem nonWrapperButton;
                BindSubmenu(submenu, out nonWrapperButton, description, addEmpty);
                button = nonWrapperButton?.ToWrapped();
                return this;
            }

            public WMenu BindSubmenu(WMenu submenu, out WMenuItem button, string description = "", bool addEmpty = false) =>
                BindSubmenu(submenu.Menu, out button, description, addEmpty);

            public WMenu AddSubmenu(Menu submenu, string description = "", bool addEmpty = false)
            {
                if (submenu.GetMenuItems().Count == 0 && !addEmpty)
                    return this;

                WMenuItem button;
                BindSubmenu(submenu, out button, description, addEmpty);
                AddItem(button);

                return this;
            }
            public WMenu AddSubmenu(WMenu submenu, string description = "", bool addEmpty = false) =>
                AddSubmenu(submenu.Menu, description, addEmpty);

            public WMenu RemoveSubmenu(Menu submenu)
            {
                if (submenu == null)
                    return this;

                var removeButtons = submenuButtons.Where(kv => kv.Value == submenu).Select(kv => kv.Key);
                foreach (var button in removeButtons.ToList())
                {
                    RemoveItem(button);
                }
                return this;
            }
            public WMenu RemoveSubmenu(WMenu submenu) => RemoveSubmenu(submenu.Menu);

            public WMenu ClearItems()
            {
                itemsDict.Clear();
                submenuButtons.Clear();
                Menu.ClearMenuItems();

                return this;
            }


            Menu.ButtonPressHandler? incrementBtnPressHandler = null;
            Control? incrementBtnPressHandlerControl = null;
            private int _increment = 1;
            public int Increment
            {
                get => _increment;
                set
                {
                    _increment = value;
                    if (incrementBtnPressHandlerControl is Control control)
                    {
                        Menu.InstructionalButtons.Remove(control);
                        Menu.InstructionalButtons.Add(control, $"Increment: {_increment}");
                    }
                }
            }

            public void ResetIncrement()
            {
                Increment = 1;
            }

            public void NextIncrement()
            {
                Increment = Increment == 1 ? 10 : 1;
            }

            public WMenu AddIncrementToggle(Control control)
            {
                if (incrementBtnPressHandler is Menu.ButtonPressHandler handler)
                {
                    Menu.ButtonPressHandlers.Remove(handler);
                }

                incrementBtnPressHandlerControl = control;
                Increment = 1;

                incrementBtnPressHandler = new Menu.ButtonPressHandler(
                    control,
                    Menu.ControlPressCheckType.JUST_RELEASED,
                    (m, _c) => NextIncrement(),
                    true);
                Menu.ButtonPressHandlers.Add(incrementBtnPressHandler.Value);

                return this;
            }
        }
    }
}
