﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using Newtonsoft.Json;

using vMenuShared;

using static CitizenFX.Core.Native.API;
using static vMenuServer.DebugLog;
using static vMenuShared.ConfigManager;

namespace vMenuServer
{

    public static class DebugLog
    {
        public enum LogLevel
        {
            error = 1,
            success = 2,
            info = 4,
            warning = 3,
            none = 0
        }

        /// <summary>
        /// Global log data function, only logs when debugging is enabled.
        /// </summary>
        /// <param name="data"></param>
        public static void Log(dynamic data, LogLevel level = LogLevel.none)
        {
            if (MainServer.DebugMode || level == LogLevel.error || level == LogLevel.warning)
            {
                var prefix = "[vMenu] ";
                if (level == LogLevel.error)
                {
                    prefix = "^1[vMenu] [ERROR]^7 ";
                }
                else if (level == LogLevel.info)
                {
                    prefix = "^5[vMenu] [INFO]^7 ";
                }
                else if (level == LogLevel.success)
                {
                    prefix = "^2[vMenu] [SUCCESS]^7 ";
                }
                else if (level == LogLevel.warning)
                {
                    prefix = "^3[vMenu] [WARNING]^7 ";
                }
                Debug.WriteLine($"{prefix}[DEBUG LOG] {data.ToString()}");
            }
        }
    }

    public class MainServer : BaseScript
    {
        #region vars
        // Debug shows more information when doing certain things. Leave it off to improve performance!
        public static bool DebugMode = GetResourceMetadata(GetCurrentResourceName(), "server_debug_mode", 0) == "true";

        public static string Version { get { return GetResourceMetadata(GetCurrentResourceName(), "version", 0); } }

        private readonly List<string> CloudTypes = new()
        {
            "Cloudy 01",
            "RAIN",
            "horizonband1",
            "horizonband2",
            "Puffs",
            "Wispy",
            "Horizon",
            "Stormy 01",
            "Clear 01",
            "Snowy 01",
            "Contrails",
            "altostratus",
            "Nimbus",
            "Cirrus",
            "cirrocumulus",
            "stratoscumulus",
            "horizonband3",
            "Stripey",
            "horsey",
            "shower",
        };
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor.
        /// </summary>
        public MainServer()
        {
            var gamebuild = 2372;
            var gamebuildcurr = GetConvarInt("sv_enforcegamebuild", 0);
            // build check
            if (gamebuildcurr < gamebuild)
            {
                var InvalidGameBuild = new Exception($"\r\n\r\n^1 Wrong game build! Your server's game build is v{gamebuildcurr}! You need atleast the v{gamebuild} or later game builds to use PF-vMenu. Tutorial on how to change this: https://forum.cfx.re/t/tutorial-forcing-gamebuilds-on-fivem/4784977\r\n\r\n\r\n^7");
                try
                {
                    throw InvalidGameBuild;
                }
                catch (Exception e)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Debug.Write(e.Message);
                        System.Threading.Thread.Sleep(5000);

                    }
                    return;
                }
            }
            else
            {
                Debug.WriteLine($"Game build is: v{gamebuildcurr}");
                // id check
                if (GetSettingsString(Setting.vmenu_individual_server_id) == "" || GetSettingsString(Setting.vmenu_individual_server_id) == null || GetSettingsString(Setting.vmenu_individual_server_id) == "null")
                {
                    var InvalidServerId = new Exception("\r\n\r\n^1 Invalid Server ID or Server ID not found! Change or add 'setr vmenu_individual_server_id' to your server.cfg or permissions.cfg. \r\n\r\n\r\n^7");
                    try
                    {
                        throw InvalidServerId;
                    }
                    catch (Exception e)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            Debug.Write(e.Message);
                            System.Threading.Thread.Sleep(5000);

                        }
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine($"Server ID: {GetSettingsString(Setting.vmenu_individual_server_id)}");
                    if (GetCurrentResourceName() != "vMenu")
                    {
                        var InvalidNameException = new Exception("\r\n\r\n^1[vMenu] INSTALLATION ERROR!\r\nThe name of the resource is not valid. " +
                            "Please change the folder name from '^3" + GetCurrentResourceName() + "^1' to '^2vMenu^1' (case sensitive) instead!\r\n\r\n\r\n^7");
                        try
                        {
                            throw InvalidNameException;
                        }
                        catch (Exception e)
                        {
                            Debug.Write(e.Message);
                        }
                    }
                    else
                    {
                        // Add event handlers.
                        EventHandlers.Add("vMenu:GetPlayerIdentifiers", new Action<int, NetworkCallbackDelegate>((TargetPlayer, CallbackFunction) =>
                        {
                            var data = new List<string>();
                            Players[TargetPlayer].Identifiers.ToList().ForEach(e =>
                            {
                                if (!e.Contains("ip:"))
                                {
                                    data.Add(e);
                                }
                            });
                            CallbackFunction(JsonConvert.SerializeObject(data));
                        }));
                        EventHandlers.Add("vMenu:RequestPermissions", new Action<Player>(PermissionsManager.SetPermissionsForPlayer));
                        EventHandlers.Add("vMenu:RequestServerState", new Action<Player>(RequestServerStateFromPlayer));

                        // check addons file for errors
                        var addons = LoadResourceFile(GetCurrentResourceName(), "config/addons.json") ?? "{}";
                        try
                        {
                            JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(addons);
                            // If the above crashes, then the json is invalid and it'll throw warnings in the console.
                        }
                        catch (JsonReaderException ex)
                        {
                            Debug.WriteLine($"\n\n^1[vMenu] [ERROR] ^7Your addons.json file contains a problem! Error details: {ex.Message}\n\n");
                        }

                        // check veh blips file for errors
                        string vehblips = LoadResourceFile(GetCurrentResourceName(), "config/vehblips.json") ?? "{}";
                        try
                        {
                            JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(vehblips);
                            // If the above crashes, then the json is invalid and it'll throw warnings in the console.
                        }
                        catch (JsonReaderException ex)
                        {
                            Debug.WriteLine($"\n\n^1[vMenu] [ERROR] ^7Your vehblips.json file contains a problem! Error details: {ex.Message}\n\n");
                        }

                        // check extras file for errors
                        string extras = LoadResourceFile(GetCurrentResourceName(), "config/extras.json") ?? "{}";
                        try
                        {
                            JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, string>>>(extras);
                            // If the above crashes, then the json is invalid and it'll throw warnings in the console.
                        }
                        catch (JsonReaderException ex)
                        {
                            Debug.WriteLine($"\n\n^1[vMenu] [ERROR] ^7Your extras.json file contains a problem! Error details: {ex.Message}\n\n");
                        }

                        // check if permissions are setup (correctly)
                        if (!GetSettingsBool(Setting.vmenu_use_permissions))
                        {
                            Debug.WriteLine("^3[vMenu] [WARNING] vMenu is set up to ignore permissions!\nIf you did this on purpose then you can ignore this warning.\nIf you did not set this on purpose, then you must have made a mistake while setting up vMenu.\nPlease read the vMenu documentation (^5https://docs.vespura.com/vmenu^3).\nMost likely you are not executing the permissions.cfg (correctly).^7");
                        }

                        Tick += PlayersFirstTick;

                        if (GetSettingsBool(Setting.vmenu_enable_time_weather_sync))
                        {
                            Tick += TimeLoop;
                        }
                    }
                }
            }
        }
        #endregion

        #region command handler
        [Command("vmenuserver", Restricted = true)]
        internal void ServerCommandHandler(int source, List<object> args, string _)
        {
            if (args != null)
            {
                if (args.Count > 0)
                {
                    if (args[0].ToString().ToLower() == "debug")
                    {
                        DebugMode = !DebugMode;
                        if (source < 1)
                        {
                            Debug.WriteLine($"Debug mode is now set to: {DebugMode}.");
                        }
                        else
                        {
                            Players[source].TriggerEvent("chatMessage", $"vMenu Debug mode is now set to: {DebugMode}.");
                        }
                        return;
                    }
                    else if (args[0].ToString().ToLower() == "unban" && (source < 1))
                    {
                        if (args.Count() > 1 && !string.IsNullOrEmpty(args[1].ToString()))
                        {
                            var uuid = args[1].ToString().Trim();
                            var bans = BanManager.GetBanList();
                            var banRecord = bans.Find(b => { return b.uuid.ToString() == uuid; });
                            if (banRecord != null)
                            {
                                BanManager.RemoveBan(banRecord);
                                Debug.WriteLine("Player has been successfully unbanned.");
                            }
                            else
                            {
                                Debug.WriteLine($"Could not find a banned player with the provided uuid '{uuid}'.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("You did not specify a player to unban, you must enter the FULL playername. Usage: vmenuserver unban \"playername\"");
                        }
                        return;
                    }
                    else if (args[0].ToString().ToLower() == "ban" && source < 1)  // only do this via server console (server id < 1)
                    {
                        if (args.Count > 3)
                        {
                            Player p = null;

                            var findByServerId = args[1].ToString().ToLower() == "id";
                            var identifier = args[2].ToString().ToLower();

                            if (findByServerId)
                            {
                                if (Players.Any(player => player.Handle == identifier))
                                {
                                    p = Players.Single(pl => pl.Handle == identifier);
                                }
                                else
                                {
                                    Debug.WriteLine("[vMenu] Could not find this player, make sure they are online.");
                                    return;
                                }
                            }
                            else
                            {
                                if (Players.Any(player => player.Name.ToLower() == identifier.ToLower()))
                                {
                                    p = Players.Single(pl => pl.Name.ToLower() == identifier.ToLower());
                                }
                                else
                                {
                                    Debug.WriteLine("[vMenu] Could not find this player, make sure they are online.");
                                    return;
                                }
                            }

                            var reason = "Banned by staff for:";
                            args.GetRange(3, args.Count - 3).ForEach(arg => reason += " " + arg);

                            if (p != null)
                            {
                                var ban = new BanManager.BanRecord(
                                    BanManager.GetSafePlayerName(p.Name),
                                    p.Identifiers.ToList(),
                                    new DateTime(3000, 1, 1),
                                    reason,
                                    "Server Console",
                                    new Guid()
                                );

                                BanManager.AddBan(ban);
                                BanManager.BanLog($"[vMenu] Player {p.Name}^7 has been banned by Server Console for [{reason}].");
                                TriggerEvent("vMenu:BanSuccessful", JsonConvert.SerializeObject(ban).ToString());
                                var timeRemaining = BanManager.GetRemainingTimeMessage(ban.bannedUntil.Subtract(DateTime.Now));
                                p.Drop($"You are banned from this server. Ban time remaining: {timeRemaining}. Banned by: {ban.bannedBy}. Ban reason: {ban.banReason}. Additional information: {vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_default_ban_message_information)}.");
                            }
                            else
                            {
                                Debug.WriteLine("[vMenu] Player not found, could not ban player.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[vMenu] Not enough arguments, syntax: ^5vmenuserver ban <id|name> <server id|username> <reason>^7.");
                        }
                    }
                    else if (args[0].ToString().ToLower() == "help")
                    {
                        Debug.WriteLine("Available commands:");
                        Debug.WriteLine("(server console only): vmenuserver ban <id|name> <server id|username> <reason> (player must be online!)");
                        Debug.WriteLine("(server console only): vmenuserver unban <uuid>");
                        Debug.WriteLine("vmenuserver weather <new weather type | dynamic <true | false>>");
                        Debug.WriteLine("vmenuserver time <freeze|<hour> <minute>>");
                        Debug.WriteLine("vmenuserver migrate (This copies all banned players in the bans.json file to the new ban system in vMenu v3.3.0, you only need to do this once)");
                    }
                    else if (args[0].ToString().ToLower() == "migrate" && source < 1)
                    {
                        var file = LoadResourceFile(GetCurrentResourceName(), "bans.json");
                        if (string.IsNullOrEmpty(file) || file == "[]")
                        {
                            Debug.WriteLine("&1[vMenu] [ERROR]^7 No bans.json file found or it's empty.");
                            return;
                        }
                        Debug.WriteLine("^5[vMenu] [INFO]^7 Importing all ban records from the bans.json file into the new storage system. ^3This may take some time...^7");
                        var bans = JsonConvert.DeserializeObject<List<BanManager.BanRecord>>(file);
                        bans.ForEach((br) =>
                        {
                            var record = new BanManager.BanRecord(br.playerName, br.identifiers, br.bannedUntil, br.banReason, br.bannedBy, Guid.NewGuid());
                            BanManager.AddBan(record);
                        });
                        Debug.WriteLine("^2[vMenu] [SUCCESS]^7 All ban records have been imported. You now no longer need the bans.json file.");
                    }
                    else
                    {
                        Debug.WriteLine($"vMenu is currently running version: {Version}. Try ^5vmenuserver help^7 for info.");
                    }
                }
                else
                {
                    Debug.WriteLine($"vMenu is currently running version: {Version}. Try ^5vmenuserver help^7 for info.");
                }
            }
            else
            {
                Debug.WriteLine($"vMenu is currently running version: {Version}. Try ^5vmenuserver help^7 for info.");
            }
        }
        #endregion

        #region kick players from personal vehicle
        /// <summary>
        /// Makes the player leave the personal vehicle.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="vehicleNetId"></param>
        /// <param name="playerOwner"></param>
        [EventHandler("vMenu:DelAllVehServ")]
        public void DelAllVehServ([FromSource] Player source)
        {
            var vehdelnum = 0;
            foreach (int veh in GetAllVehicles())
            {
                if (!IsPedAPlayer(GetPedInVehicleSeat(veh, -1)))
                {
                    vehdelnum++;
                    DeleteEntity(veh);
                }
            }
            if (vMenuShared.ConfigManager.GetSettingsBool(vMenuShared.ConfigManager.Setting.pfvmenu_moshnotify_setting))
            {
                source.TriggerEvent("mosh_notify:notify", "SUCCESS", $"<span class=\"text-white\">{vehdelnum} Vehicles Have Been Deleted!</span>", "success", "success", 5000);
            }
            source.TriggerEvent("vMenu:Notify", $"{vehdelnum} Vehicles Have Been Deleted!.", "success");

        }
        [EventHandler("vMenu:GetOutOfCar")]
        internal void GetOutOfCar([FromSource] Player source, int vehicleNetId, int playerOwner)
        {
            if (source != null)
            {
                if (vMenuShared.PermissionsManager.GetPermissionAndParentPermissions(vMenuShared.PermissionsManager.Permission.PVKickPassengers).Any(perm => vMenuShared.PermissionsManager.IsAllowed(perm, source)))
                {
                    TriggerClientEvent("vMenu:GetOutOfCar", vehicleNetId, playerOwner);
                    source.TriggerEvent("vMenu:Notify", "All passengers will be kicked out as soon as the vehicle stops moving, or after 10 seconds if they refuse to stop the vehicle.", "info");
                }
            }
        }
        #endregion

        #region clear area near pos
        /// <summary>
        /// Clear the area near this point for all players.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        [EventHandler("vMenu:ClearArea")]
        internal void ClearAreaNearPos(float x, float y, float z)
        {
            TriggerClientEvent("vMenu:ClearArea", x, y, z);
        }
        #endregion

        #region Manage weather and time changes.
        public void SetServerTime(TimeWeatherCommon.TimeState serverTime)
        {
            var json = JsonConvert.SerializeObject(serverTime);
            SetConvarReplicated(Setting.vmenu_server_time.ToString(), json);
        }

        public void SetServerWeather(TimeWeatherCommon.WeatherState serverWeather)
        {
            var json = JsonConvert.SerializeObject(serverWeather);
            SetConvarReplicated(Setting.vmenu_server_weather.ToString(), json);
        }

        /// <summary>
        /// Loop used for syncing and keeping track of the time in-game.
        /// </summary>
        /// <returns></returns>
        private async Task TimeLoop()
        {
            var ts = TimeWeatherCommon.GetServerTime();
            if (!ts.Frozen)
            {
                if (++ts.Minute == 60)
                {
                    ts.Minute = 0;
                    if (++ts.Hour == 24)
                    {
                        ts.Hour = 0;
                    }
                }
                SetServerTime(ts);
            }
            await Delay(2000);
        }
        #endregion

        #region Sync weather & time with clients
        [EventHandler("vMenu:UpdateOverrideClientTW")]
        internal void UpdateServerWeather(bool override_)
        {
            SetConvarReplicated(Setting.vmenu_override_client_time_weather.ToString(), override_.ToString().ToLower());
        }

        /// <summary>
        /// Set and sync the time to all clients.
        /// </summary>
        [EventHandler("vMenu:UpdateServerTime")]
        internal void UpdateServerTime(string json)
        {
            var value = JsonConvert.DeserializeObject<TimeWeatherCommon.TimeState>(json);
            SetServerTime(value);
        }

        /// <summary>
        /// Update the weather for all clients.
        /// </summary>
        [EventHandler("vMenu:UpdateServerWeather")]
        internal void UpdateServerWeather(string json)
        {
            var value = JsonConvert.DeserializeObject<TimeWeatherCommon.WeatherState>(json);
            SetServerWeather(value);
        }
        #endregion

        #region Online Players Menu Actions
        /// <summary>
        /// Kick a specific player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="kickReason"></param>
        [EventHandler("vMenu:KickPlayer")]
        internal void KickPlayer([FromSource] Player source, int target, string kickReason = "You have been kicked from the server.")
        {
            if (IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.Kick") || IsPlayerAceAllowed(source.Handle, "vMenu.Everything") ||
                IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.All"))
            {
                // If the player is allowed to be kicked.
                var targetPlayer = Players[target];
                if (targetPlayer != null)
                {
                    if (!IsPlayerAceAllowed(targetPlayer.Handle, "vMenu.DontKickMe"))
                    {
                        TriggerEvent("vMenu:KickSuccessful", source.Name, kickReason, targetPlayer.Name);

                        KickLog($"Player: {source.Name} has kicked: {targetPlayer.Name} for: {kickReason}.");
                        TriggerClientEvent(source, "vMenu:Notify", $"The target player (<C>{targetPlayer.Name}</C>) has been kicked.", "info");

                        // Kick the player from the server using the specified reason.
                        DropPlayer(targetPlayer.Handle, kickReason);
                        return;
                    }
                    // Trigger the client event on the source player to let them know that kicking this player is not allowed.
                    TriggerClientEvent(source, "vMenu:Notify", "Sorry, this player can ~r~not ~w~be kicked.", "info");
                    return;
                }
                TriggerClientEvent(source, "vMenu:Notify", "An unknown error occurred. Report it here: vespura.com/vmenu", "info");
            }
            else
            {
                BanManager.BanCheater(source);
            }
        }

        /// <summary>
        /// Kill a specific player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        [EventHandler("vMenu:KillPlayer")]
        internal void KillPlayer([FromSource] Player source, int target)
        {
            if (IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.Kill") || IsPlayerAceAllowed(source.Handle, "vMenu.Everything") ||
                IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.All"))
            {
                var targetPlayer = Players[target];
                if (targetPlayer != null)
                {
                    // Trigger the client event on the target player to make them kill themselves. R.I.P.
                    TriggerClientEvent(player: targetPlayer, eventName: "vMenu:KillMe", args: source.Name);
                    return;
                }
                TriggerClientEvent(source, "vMenu:Notify", "An unknown error occurred. Report it here: vespura.com/vmenu", "info");
            }
            else
            {
                BanManager.BanCheater(source);
            }
        }

        /// <summary>
        /// Teleport a specific player to another player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        [EventHandler("vMenu:SummonPlayer")]
        internal void SummonPlayer([FromSource] Player source, int target)
        {
            if (IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.Summon") || IsPlayerAceAllowed(source.Handle, "vMenu.Everything") ||
                IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.All"))
            {
                // Trigger the client event on the target player to make them teleport to the source player.
                var targetPlayer = Players[target];
                if (targetPlayer != null)
                {
                    TriggerClientEvent(player: targetPlayer, eventName: "vMenu:GoToPlayer", args: source.Handle);
                    return;
                }
                TriggerClientEvent(source, "vMenu:Notify", "An unknown error occurred. Report it here: vespura.com/vmenu", "info");
            }
            else
            {
                BanManager.BanCheater(source);
            }
        }

        [EventHandler("vMenu:SendMessageToPlayer")]
        internal void SendPrivateMessage([FromSource] Player source, int targetServerId, string message)
        {
            var targetPlayer = Players[targetServerId];
            if (targetPlayer != null)
            {
                targetPlayer.TriggerEvent("vMenu:PrivateMessage", source.Handle, message);

                foreach (var p in Players)
                {
                    if (p != source && p != targetPlayer)
                    {
                        if (vMenuShared.PermissionsManager.IsAllowed(vMenuShared.PermissionsManager.Permission.OPSeePrivateMessages, p))
                        {
                            p.TriggerEvent("vMenu:Notify", $"[vMenu Staff Log] <C>{source.Name}</C>~s~ sent a PM to <C>{targetPlayer.Name}</C>~s~: {message}", "");
                        }
                    }
                }
            }
        }

        [EventHandler("vMenu:PmsDisabled")]
        internal void NotifySenderThatDmsAreDisabled([FromSource] Player source, string senderServerId)
        {
            var p = Players[int.Parse(senderServerId)];
            p?.TriggerEvent("vMenu:Notify", $"Sorry, your private message to <C>{source.Name}</C>~s~ could not be delivered because they disabled private messages.", "info");
        }
        #endregion

        #region logging and update checks notifications
        /// <summary>
        /// If enabled using convars, will log all kick actions to the server console as well as an external file.
        /// </summary>
        /// <param name="kickLogMesage"></param>
        private static void KickLog(string kickLogMesage)
        {
            //if (GetConvar("vMenuLogKickActions", "true") == "true")
            if (GetSettingsBool(Setting.vmenu_log_kick_actions))
            {
                var file = LoadResourceFile(GetCurrentResourceName(), "vmenu.log") ?? "";
                var date = DateTime.Now;
                var formattedDate = (date.Day < 10 ? "0" : "") + date.Day + "-" +
                    (date.Month < 10 ? "0" : "") + date.Month + "-" +
                    (date.Year < 10 ? "0" : "") + date.Year + " " +
                    (date.Hour < 10 ? "0" : "") + date.Hour + ":" +
                    (date.Minute < 10 ? "0" : "") + date.Minute + ":" +
                    (date.Second < 10 ? "0" : "") + date.Second;
                var outputFile = file + $"[\t{formattedDate}\t] [KICK ACTION] {kickLogMesage}\n";
                SaveResourceFile(GetCurrentResourceName(), "vmenu.log", outputFile, -1);
                Debug.WriteLine("^3[vMenu] [KICK]^7 " + kickLogMesage + "\n");
            }
        }

        #endregion

        #region Add teleport location
        [EventHandler("vMenu:SaveTeleportLocation")]
        internal void AddTeleportLocation([FromSource] Player _, string locationJson, string jsonname)
        {
            var location = JsonConvert.DeserializeObject<TeleportLocation>(locationJson);
            var jsonFile = LoadResourceFile(GetCurrentResourceName(), "config/locations/" + jsonname);
            var locs = JsonConvert.DeserializeObject<vMenuShared.ConfigManager.Locationsteleport>(jsonFile);
            if (locs.teleports.Any(loc => loc.name == location.name))
            {
                Log("A teleport location with this name already exists, location was not saved.", LogLevel.error);
                return;
            }

            //var locs = GetLocations();
            locs.teleports.Add(location);
            if (!SaveResourceFile(GetCurrentResourceName(), "config/locations/" + jsonname, JsonConvert.SerializeObject(locs, Formatting.Indented), -1))
            {
                Log($"Could not save {jsonname} file, reason unknown.", LogLevel.error);
            }
        }
        #endregion

        #region Infinity bits
        private void RequestServerStateFromPlayer([FromSource] Player player)
        {
            player.TriggerEvent("vMenu:SetServerState", new
            {
                IsInfinity = GetConvar("onesync_enableInfinity", "false") == "true"
            });
        }

        [EventHandler("vMenu:RequestPlayerList")]
        internal void RequestPlayerListFromPlayer([FromSource] Player player)
        {
            player.TriggerEvent("vMenu:ReceivePlayerList", Players.Select(p => new
            {
                n = p.Name,
                s = int.Parse(p.Handle),
            }));
        }

        [EventHandler("vMenu:GetPlayerCoords")]
        internal void GetPlayerCoords([FromSource] Player source, int playerId, NetworkCallbackDelegate callback)
        {
            if (IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.Teleport") || IsPlayerAceAllowed(source.Handle, "vMenu.Everything") ||
                IsPlayerAceAllowed(source.Handle, "vMenu.OnlinePlayers.All"))
            {
                var coords = Players[playerId]?.Character?.Position ?? Vector3.Zero;

                _ = callback(coords);

                return;
            }

            _ = callback(Vector3.Zero);
        }
        #endregion

        #region Player join/quit
        private readonly HashSet<string> joinedPlayers = new();

        private Task PlayersFirstTick()
        {
            Tick -= PlayersFirstTick;

            foreach (var player in Players)
            {
                joinedPlayers.Add(player.Handle);
            }

            return Task.FromResult(0);
        }

        [EventHandler("playerJoining")]
        internal void OnPlayerJoining([FromSource] Player sourcePlayer)
        {
            joinedPlayers.Add(sourcePlayer.Handle);

            foreach (var player in Players)
            {
                if (IsPlayerAceAllowed(player.Handle, "vMenu.MiscSettings.JoinQuitNotifs") ||
                    IsPlayerAceAllowed(player.Handle, "vMenu.MiscSettings.All"))
                {
                    player.TriggerEvent("vMenu:PlayerJoinQuit", sourcePlayer.Name, null);
                }
            }
        }

        [EventHandler("playerDropped")]
        internal void OnPlayerDropped([FromSource] Player sourcePlayer, string reason)
        {
            if (!joinedPlayers.Contains(sourcePlayer.Handle))
            {
                return;
            }

            joinedPlayers.Remove(sourcePlayer.Handle);

            foreach (var player in Players)
            {
                if (IsPlayerAceAllowed(player.Handle, "vMenu.MiscSettings.JoinQuitNotifs") ||
                    IsPlayerAceAllowed(player.Handle, "vMenu.MiscSettings.All"))
                {
                    player.TriggerEvent("vMenu:PlayerJoinQuit", sourcePlayer.Name, reason);
                }
            }
        }
        #endregion

        #region Language template dumper

        [EventHandler("vMenu:DumpLanguageTemplate:Server")]
        private void DumpLangaugeTemplate(string data)
        {
            try
            {
                bool successful = SaveResourceFile(GetCurrentResourceName(), "config/languages/TEMPLATE.json", data, -1);
                if (successful)
                {
                    Debug.WriteLine($"\n\n^2[vMenu] [SUCCESS] ^7Template created successfully!\n\n");
                }
                else
                {
                    Debug.WriteLine($"\n\n^1[vMenu] [ERROR] ^7Could not save the language template!\n\n");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"\n\n^1[vMenu] [ERROR] ^7Your TEMPLATE.json file could not be created or accessed! Error details: {e.Message}\n\n");
            }
        }

        #endregion

        #region Set drift suspension

        [EventHandler("vMenu:SetDriftSuspension")]
        private void SetDriftSuspension(int vehNetId)
        {
            Entity vehEntity = Entity.FromNetworkId(vehNetId);
            if (vehEntity == null) return;

            StateBag vehState = vehEntity.State;
            bool? reduceDriftSuspension = vehState["Set:ReduceDriftSuspension"] ?? false;

            vehEntity.State["Set:ReduceDriftSuspension"] = reduceDriftSuspension.Value ? false : true;
            TriggerClientEvent("vMenu:SetDriftSuspension", vehNetId, vehEntity.State["Set:ReduceDriftSuspension"]);
        }

        #endregion
    }
}
