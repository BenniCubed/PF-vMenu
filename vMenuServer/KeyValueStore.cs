using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using MySqlConnector;

using Newtonsoft.Json;

using vMenuShared;

using static CitizenFX.Core.Native.API;
using static vMenuServer.KeyValueStore;
using static vMenuShared.KeyValueStoreSync;

namespace vMenuServer
{
    public static class DatabaseKeyValueStore
    {
        public static string Truncate(this string value, int length)
        {
            if (value.Length <= length)
                return value;

            return value.Substring(0, length);
        }

        public const string SERVER_LICENSE = "0000000000000000000000000000000000000000";

        public static string ConnectionString { get; private set; }
        private static MySqlConnection connection = null;


        private static void CreateTable()
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var command = new MySqlCommand
                {
                    Connection = connection,
                    CommandText = "CREATE TABLE IF NOT EXISTS `vMenu` (`PlayerLicense` CHAR(40) NOT NULL, `Key` VARCHAR(64) NOT NULL, `Value` TEXT, `Type` BIT(2) NOT NULL DEFAULT 0, PRIMARY KEY (`PlayerLicense`, `Key`)) CHARACTER SET = utf8mb4 COLLATE = utf8mb4_bin ROW_FORMAT = COMPRESSED"
                };
                command.ExecuteNonQuery();
            }
        }

        public static void Connect()
        {
            SetConvarReplicated(ConfigManager.Setting.vmenu_server_store.ToString(), "false");

            var connectionStringVar = ConfigManager.GetSettingsString(ConfigManager.Setting.vmenu_mysql_connection_string_var);
            if (string.IsNullOrEmpty(connectionStringVar))
            {
                Debug.WriteLine("\"vmenu_mysql_connection_string_var\" not specified or empty. Running without database.");
                return;
            }

            ConnectionString = GetConvar(connectionStringVar, "");
            if (string.IsNullOrEmpty(ConnectionString))
            {
                Debug.WriteLine($"Invalid or missing MySQL connection string convar \"{connectionStringVar}\". Running without database.");
                ConnectionString = null;
                return;
            }

            connection = new MySqlConnection(ConnectionString);

            try
            {
                connection.Open();
            }
            catch (MySqlException)
            {
                Debug.WriteLine($"Could not open connection to database. Running without database.");
                ConnectionString = null;
                connection = null;
                return;
            }

            try
            {
                CreateTable();
            }
            catch (MySqlException e)
            {
                Debug.WriteLine($"Could create key-value store table: {e}");
                ConnectionString = null;
                connection = null;
                return;
            }

            Debug.WriteLine("Successfully connected to database.");
            SetConvarReplicated(ConfigManager.Setting.vmenu_server_store.ToString(), "true");
        }


        public static async Task<Dictionary<string, ValueInfo>> GetAll(string playerLicense)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                var command = new MySqlCommand
                {
                    Connection = connection,
                    CommandText = "SELECT `Key`, `Value`, `Type` FROM `vMenu` WHERE `PlayerLicense`=@playerLicense",
                };
                command.Parameters.AddWithValue("@playerLicense", playerLicense);

                var keyValues = new Dictionary<string, ValueInfo>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var key = reader.GetFieldValue<string>(0);
                        var value = reader.GetFieldValue<string>(1);
                        var type = reader.GetFieldValue<int>(2);
                        keyValues[key] = new ValueInfo(value, (KeyValueStore.ValueType)type);
                    }
                }

                return keyValues;
            }
        }

        public static async Task<Dictionary<string, ValueInfo>> GetAll()
        {
            if (string.IsNullOrEmpty(ConnectionString))
                return new Dictionary<string, ValueInfo>();

            return await GetAll(SERVER_LICENSE);
        }

        public static async Task Remove(string playerLicense, string key)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                var command = new MySqlCommand
                {
                    Connection = connection,
                    CommandText = "DELETE FROM `vMenu` WHERE `PlayerLicense`=@playerLicense AND `Key`=@key"
                };
                command.Parameters.AddWithValue("@playerLicense", playerLicense);
                command.Parameters.AddWithValue("@key", key.Truncate(64));

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task Remove(string key)
        {
            if (string.IsNullOrEmpty(ConnectionString))
                return;

            await Remove(SERVER_LICENSE, key);
        }

        public static async Task Set(string playerLicense, string key, ValueInfo vi)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                var command = new MySqlCommand
                {
                    Connection = connection,
                    CommandText = "INSERT INTO `vMenu` (`PlayerLicense`, `Key`, `Value`, `Type`) VALUES (@playerLicense, @key, @value, @type) ON DUPLICATE KEY UPDATE `Value`=@value, `Type`=@type"
                };
                command.Parameters.AddWithValue("@playerLicense", playerLicense);
                command.Parameters.AddWithValue("@key", key.Truncate(64));
                command.Parameters.AddWithValue("@value", vi.Value);
                command.Parameters.AddWithValue("@type", (int)vi.Type);

                await command.ExecuteNonQueryAsync();
            }
        }

        public static async Task Set(string key, ValueInfo vi)
        {
            if (string.IsNullOrEmpty(ConnectionString))
                return;

            await Set(SERVER_LICENSE, key, vi);
        }

        public static async Task SetAll(string playerLicense, Dictionary<string, ValueInfo> keyValues)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                var command = new MySqlCommand
                {
                    Connection = connection,
                    CommandText = "INSERT INTO `vMenu` (`PlayerLicense`, `Key`, `Value`, `Type`) VALUES (@playerLicense, @key, @value, @type) ON DUPLICATE KEY UPDATE `Value`=@value, `Type`=@type"
                };
                command.Parameters.AddWithValue("@playerLicense", playerLicense);
                command.Parameters.AddWithValue("@key", null);
                command.Parameters.AddWithValue("@value", null);
                command.Parameters.AddWithValue("@type", null);
                command.Prepare();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        command.Transaction = transaction;
                        foreach (var kv in keyValues)
                        {
                            command.Parameters["@key"].Value = kv.Key.Truncate(64);
                            command.Parameters["@value"].Value = kv.Value.Value;
                            command.Parameters["@type"].Value = kv.Value.Type;
                            await command.ExecuteNonQueryAsync();
                            await BaseScript.Delay(0);
                        }
                        transaction.Commit();
                    }
                    catch (Exception e)
                    {
                        transaction.Rollback();
                        throw e;
                    }
                }
            }
        }

        public static async Task SetAll(Dictionary<string, ValueInfo> keyValues)
        {
            if (string.IsNullOrEmpty(ConnectionString))
                return;

            await SetAll(SERVER_LICENSE, keyValues);
        }
    }

    public static class ClientRemoteKeyValueStore
    {
        private static void SendResponse(Player player, Response response)
        {
            player.TriggerLatentEvent("vMenu:ServerKeyValueStoreResponse", 8192, JsonConvert.SerializeObject(response));
        }


        public static async Task HandleRequest(Player player, string json)
        {
            var request = JsonConvert.DeserializeObject<Request>(json);
            var response = await HandleRequest(player, request);
            SendResponse(player, response);
        }

        private static async Task<Response> HandleRequest(Player player, Request request)
        {
            if (string.IsNullOrEmpty(DatabaseKeyValueStore.ConnectionString))
            {
                return new Response
                {
                    Id = request.Id,
                    Type = Response.ResponseType.NoServerStore
                };
            }

            var license = player.Identifiers["license"].Truncate(40);

            Response response;
            try
            {
                switch (request.Type)
                {
                    case Request.RequestType.GetAll:
                        response = await HandleRequestGetAll(license, request);
                        break;
                    case Request.RequestType.Remove:
                        response = await HandleRequestRemove(license, request);
                        break;
                    case Request.RequestType.Set:
                        response = await HandleRequestSet(license, request);
                        break;
                    case Request.RequestType.SetAll:
                        response = await HandleRequestSetAll(license, request);
                        break;
                    default:
                        response = new Response
                        {
                            Type = Response.ResponseType.Error,
                            Error = "The request was invalid"
                        };
                        break;
                }
                response.Type = Response.ResponseType.Ok;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error handling database key-value store request {request.Type} (id {request.Id}) from player {player.Name} ({license}): {e}");
                response = new Response
                {
                    Type = Response.ResponseType.Error,
                    Error = $"request id {request.Id}"
                };
            }
            response.Id = request.Id;

            return response;
        }

        private static async Task<Response> HandleRequestGetAll(string playerLicense, Request _)
        {
            var keyValues = await DatabaseKeyValueStore.GetAll(playerLicense);
            return new Response
            {
                DataGetAll = new Response.ResponseDataGetAll
                {
                    KeyValues = keyValues
                }
            };
        }

        private static async Task<Response> HandleRequestRemove(string playerLicense, Request requestRemove)
        {
            await DatabaseKeyValueStore.Remove(playerLicense, requestRemove.DataRemove?.Key);
            return new Response
            {
                DataRemove = new Response.ResponseDataRemove
                {}
            };
        }

        private static async Task<Response> HandleRequestSet(string playerLicense, Request requestSet)
        {
            var data = requestSet.DataSet.Value;
            await DatabaseKeyValueStore.Set(playerLicense, data.Key, data.ValueInfo);
            return new Response
            {
                DataSet = new Response.ResponseDataSet
                {
                }
            };
        }

        private static async Task<Response> HandleRequestSetAll(string playerLicense, Request requestSetAll)
        {
            await DatabaseKeyValueStore.SetAll(playerLicense, requestSetAll.DataSetAll?.KeyValues);
            return new Response
            {
                DataSetAll = new Response.ResponseDataSetAll
                {
                }
            };
        }
    }

    public static partial class KeyValueStore
    {
        public async static Task SyncWithDatabase()
        {
            var localKvs = GetAll();
            var databaseKvs = await DatabaseKeyValueStore.GetAll();

            var localNonDatabaseKvs = localKvs
                .Where(kv => !databaseKvs.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (var kv in databaseKvs)
            {
                SetLocal(kv.Key, kv.Value);
            }
            try
            {
                await DatabaseKeyValueStore.SetAll(localNonDatabaseKvs);
            }
            catch (MySqlException e)
            {
                Debug.WriteLine($"Error setting multiple keys in database key-value store: {e.Message}");
            }
        }

        public async static Task Remove(string key)
        {
            DeleteResourceKvp(key);
            try
            {
                await DatabaseKeyValueStore.Remove(key);
            }
            catch (MySqlException e)
            {
                Debug.WriteLine($"Error removing key \"{key}\" from database key-value store: {e.Message}");
            }
        }

        private async static Task Set(string key, ValueInfo vi)
        {
            SetLocal(key, vi);
            await DatabaseKeyValueStore.Set(key, vi);
            try
            {
                await DatabaseKeyValueStore.Set(key, vi);
            }
            catch (MySqlException e)
            {
                Debug.WriteLine($"Error setting \"{key}={vi.Value}\" in database key-value store: {e.Message}");
            }
        }

        public async static Task Set(string key, string value) => await Set(key, new ValueInfo(value));
    }
}
