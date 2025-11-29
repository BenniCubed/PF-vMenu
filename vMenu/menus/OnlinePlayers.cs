using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using MenuAPI;

using Newtonsoft.Json;

using vMenuClient.MenuAPIWrapper;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;
using static vMenuShared.PermissionsManager;

namespace vMenuClient.menus
{
    public class OnlinePlayers
    {
        public List<int> PlayersWaypointList = new();
        public Dictionary<int, int> PlayerCoordWaypoints = new();

        // Menu variable, will be defined in CreateMenu()
        private WMenu menu;

        private WMenu playerMenu;
        private IPlayer currentPlayer = null;

        private async Task TryTeleport(bool inVehicle)
        {
            if (!currentPlayer.IsLocal)
            {
                await TeleportToPlayer(currentPlayer, inVehicle);
            }
            else
            {
                Notify.Error("You cannot teleport to yourself!");
            }
        }

        private async Task ToggleGPS()
        {
            var selectedPedRouteAlreadyActive = false;
            if (PlayersWaypointList.Count > 0)
            {
                if (PlayersWaypointList.Contains(currentPlayer.ServerId))
                {
                    selectedPedRouteAlreadyActive = true;
                }
                foreach (var serverId in PlayersWaypointList)
                {
                    // remove any coord blip
                    if (PlayerCoordWaypoints.TryGetValue(serverId, out var wp))
                    {
                        SetBlipRoute(wp, false);
                        RemoveBlip(ref wp);

                        PlayerCoordWaypoints.Remove(serverId);
                    }

                    // remove any entity blip
                    var playerId = GetPlayerFromServerId(serverId);

                    if (playerId < 0)
                    {
                        continue;
                    }

                    var playerPed = GetPlayerPed(playerId);
                    if (DoesEntityExist(playerPed) && DoesBlipExist(GetBlipFromEntity(playerPed)))
                    {
                        var oldBlip = GetBlipFromEntity(playerPed);
                        SetBlipRoute(oldBlip, false);
                        RemoveBlip(ref oldBlip);
                        Notify.Custom($"~g~GPS route to ~s~<C>{GetSafePlayerName(currentPlayer.Name)}</C>~g~ is now disabled.");
                    }
                }
                PlayersWaypointList.Clear();
            }

            if (!selectedPedRouteAlreadyActive)
            {
                if (currentPlayer.ServerId != Game.Player.ServerId)
                {
                    int blip;

                    if (currentPlayer.IsActive && currentPlayer.Character != null)
                    {
                        var ped = GetPlayerPed(currentPlayer.Handle);
                        blip = GetBlipFromEntity(ped);
                        if (!DoesBlipExist(blip))
                        {
                            blip = AddBlipForEntity(ped);
                        }
                    }
                    else
                    {
                        if (!PlayerCoordWaypoints.TryGetValue(currentPlayer.ServerId, out blip))
                        {
                            var coords = await MainMenu.RequestPlayerCoordinates(currentPlayer.ServerId);
                            blip = AddBlipForCoord(coords.X, coords.Y, coords.Z);
                            PlayerCoordWaypoints[currentPlayer.ServerId] = blip;
                        }
                    }

                    SetBlipColour(blip, 58);
                    SetBlipRouteColour(blip, 58);
                    SetBlipRoute(blip, true);

                    PlayersWaypointList.Add(currentPlayer.ServerId);
                    Notify.Custom($"~g~GPS route to ~s~<C>{GetSafePlayerName(currentPlayer.Name)}</C>~g~ is now active, press the ~s~Toggle GPS Route~g~ button again to disable the route.");
                }
                else
                {
                    Notify.Error("You can not set a waypoint to yourself.");
                }
            }
        }

        private void PrintIdentifiers()
        {
            Action<List<string>> CallbackFunction = (data) =>
            {
                foreach (var id in data)
                {
                    Debug.WriteLine(id);
                }
                var ids = string.Join("~n~", data);
                Notify.Custom($"~b~{GetSafePlayerName(currentPlayer.Name)}~s~'s Identifiers:~n~{ids}", false);
            };
            BaseScript.TriggerServerEvent("vMenu:GetPlayerIdentifiers", CallbackFactory.Create(CallbackFunction));
        }

        private void TryBanPlayer(IPlayer player, bool forever)
        {
            if (player.Handle != Game.Player.Handle)
            {
                BanPlayer(player, forever);
            }
            else
            {
                Notify.Error("You cannot ban yourself!");
            }
        }

        /// <summary>
        /// Creates the menu.
        /// </summary>
        private void SetupPlayerMenu()
        {
            playerMenu = new WMenu(MenuTitle, "CHANGEME");
            playerMenu.Closed += (_s, _args) =>
            {
                playerMenu.Menu.MenuSubtitle = "CHANGEME";
                playerMenu.Menu.RefreshIndex();
                currentPlayer = null;
            };

            {
                var sendMessage = new MenuItem("Send Private Message", "Sends a private message to this player. ~y~Staff is able to see all PMs.~s~").ToWrapped();
                sendMessage.Selected += async (_s, _args) =>
                {
                    if (MainMenu.MiscSettingsMenu != null && !MainMenu.MiscSettingsMenu.MiscDisablePrivateMessages)
                    {
                        var message = await GetUserInput($"Private Message To {currentPlayer.Name}", 200);
                        if (string.IsNullOrEmpty(message))
                        {
                            Notify.Error(CommonErrors.InvalidInput);
                        }
                        else
                        {
                            TriggerServerEvent("vMenu:SendMessageToPlayer", currentPlayer.ServerId, message);
                            PrivateMessage(currentPlayer.ServerId.ToString(), message, true);
                        }
                    }
                    else
                    {
                        Notify.Error("You can't send a private message if you have private messages disabled yourself. Enable them in the Misc Settings menu and try again.");
                    }
                };

                playerMenu.AddItem(sendMessage);
            }

            if (IsAllowed(Permission.OPTeleport))
            {
                var teleport = new MenuItem("Teleport To Player", "Teleport to this player.").ToWrapped();
                teleport.Selected += async (_s, _args) => await TryTeleport(false);

                var teleportVeh = new MenuItem("Teleport Into Player Vehicle", "Teleport into the vehicle of the player.").ToWrapped();
                teleportVeh.Selected += async (_s, _args) => await TryTeleport(true);


                playerMenu.AddItems([teleport, teleportVeh]);
            }
            if (IsAllowed(Permission.OPSummon))
            {
                var summon = new MenuItem("Summon Player", "Teleport the player to you.").ToWrapped();
                summon.Selected += (_s, _args) =>
                {
                    if (Game.Player.Handle != currentPlayer.Handle)
                    {
                        SummonPlayer(currentPlayer);
                    }
                    else
                    {
                        Notify.Error("You can't summon yourself.");
                    }
                };

                playerMenu.AddItem(summon);
            }
            if (IsAllowed(Permission.OPSpectate))
            {
                var spectate = new MenuItem("Spectate Player", "Spectate this player. Click this button again to stop spectating.").ToWrapped();
                spectate.Selected += (_s, _args) => SpectatePlayer(currentPlayer);

                playerMenu.AddItem(spectate);
            }
            if (IsAllowed(Permission.OPWaypoint))
            {
                var toggleGPS = new MenuItem("Toggle GPS", "Enables or disables the GPS route on your radar to this player.").ToWrapped();
                toggleGPS.Selected += async (_s, _args) => await ToggleGPS();

                playerMenu.AddItem(toggleGPS);
            }
            if (IsAllowed(Permission.OPIdentifiers))
            {
                var printIdentifiers = new MenuItem("Print Identifiers", "This will print the player's identifiers to the client console (F8). And also save it to the CitizenFX.log file.").ToWrapped();
                printIdentifiers.Selected += (_s, _args) => PrintIdentifiers();


                playerMenu.AddItem(printIdentifiers);
            }


            var dangerZone = new List<WMenuItem>();

            if (IsAllowed(Permission.OPKill))
            {
                var kill = WMenuItem.CreateConfirmationButton("~r~Kill Player~s", "Kill this player, note they will receive a notification saying that you killed them. It will also be logged in the Staff Actions log.", 3);
                kill.Confirmed += (_s, _args) => KillPlayer(currentPlayer);

                dangerZone.Add(kill);
            }
            if (IsAllowed(Permission.OPKick))
            {
                var kick = WMenuItem.CreateConfirmationButton("~r~Kick Player~s", "Kick the player from the server.", 5);
                kick.Confirmed += (_s, _args) =>
                {
                    if (currentPlayer.Handle != Game.Player.Handle)
                    {
                        KickPlayer(currentPlayer, true);
                    }
                    else
                    {
                        Notify.Error("You cannot kick yourself!");
                    }
                };

                dangerZone.Add(kick);
            }
            if (IsAllowed(Permission.OPTempBan))
            {
                var tempban = WMenuItem.CreateConfirmationButton("~r~Ban Player Temporarily ~s", "Give this player a tempban of up to 30 days (max). You can specify duration and ban reason after clicking this button.", 5);
                tempban.Confirmed += (_s, _args) => TryBanPlayer(currentPlayer, false);

                dangerZone.Add(tempban);
            }
            if (IsAllowed(Permission.OPPermBan))
            {
                var ban = WMenuItem.CreateConfirmationButton("~r~Ban Player Permanently~s", "Ban this player permanently from the server. Are you sure you want to do this? You can specify the ban reason after clicking this button.", 5);
                ban.Confirmed += (_s, _args) => TryBanPlayer(currentPlayer, true);

                dangerZone.Add(ban);
            }

            playerMenu.AddSection("Danger Zone", dangerZone);
        }

        private void CreateMenu()
        {
            // Create the menu.
            menu = new WMenu(MenuTitle, "Online Players");
            SetupPlayerMenu();
        }

        /// <summary>
        /// Updates the player items.
        /// </summary>
        public async Task UpdatePlayerlist()
        {
            void UpdateStuff()
            {
                menu.ClearItems();

                foreach (var p in MainMenu.PlayersList.OrderBy(a => a.Name))
                {
                    var name = GetSafePlayerName(p.Name);
                    var playerButton = new MenuItem(name, $"Click to view options for ~b~{name}~s~.~n~Server ID: ~b~{p.ServerId}~s~~n~Local ID: ~b~{p.Handle}~s~").ToWrapped();
                    playerButton.Selected += (_s, _args) =>
                    {
                        playerMenu.Menu.MenuSubtitle = name;
                        currentPlayer = p;
                    };

                    menu.BindSubmenu(playerMenu, playerButton, true).AddItem(playerButton);
                }

                menu.Menu.RefreshIndex();
                playerMenu.Menu.RefreshIndex();
            }

            // First, update *before* waiting - so we get all local players.
            UpdateStuff();
            await MainMenu.PlayersList.WaitRequested();

            // Update *after* waiting too so we have all remote players.
            // We have to do this, because we cannot await this function in GetMenu(), so we might access this menu
            // before WaitRequested() has finished.
            UpdateStuff();
        }

        /// <summary>
        /// Checks if the menu exists, if not then it creates it first.
        /// Then returns the menu.
        /// </summary>
        /// <returns>The Online Players Menu</returns>
        public Menu GetMenu()
        {
            if (menu == null)
            {
                CreateMenu();
            }

            _ = UpdatePlayerlist();

            return menu.Menu;
        }
    }
}
