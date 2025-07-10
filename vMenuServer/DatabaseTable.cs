using System;
using System.Collections.Generic;

using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using MySqlConnector;

using vMenuShared;

namespace vMenuServer
{
    public static class DatabaseStringExtensions
    {
        public static bool IsValidDbIdentifier(this string value) => value.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    public abstract class DatabaseKeyValueStore
    {
        public string ConnectionString { get; }

        public DatabaseKeyValueStore(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException($"{nameof(connectionString)} cannot be null or empty.");
            }

            ConnectionString = connectionString;
        }

        private async Task<T> RunWithConnection<T>(Func<MySqlConnection, Task<T>> func)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                return await func(connection);
            }
        }

        private async Task RunWithConnection(Func<MySqlConnection, Task> func)
        {
            await RunWithConnection(async c =>
            {
                await func(c);
                return new {};
            });
        }

        public async Task Create() => await RunWithConnection(CreateImpl);

        public async Task Set(string license, string key, DynamicValue value)
        {
            await RunWithConnection(c => SetImpl(c, license, key, value));
        }
        public async Task Set(string license, string key, string value) =>
            await Set(license, key, new DynamicValue(value));
        public async Task Set(string license, string key, float value) =>
            await Set(license, key, new DynamicValue(value));
        public async Task Set(string license, string key, int value) =>
            await Set(license, key, new DynamicValue(value));
        public async Task Set(string license, string key, bool value) =>
            await Set(license, key, new DynamicValue(value));

        public async Task SetMany(string license, Dictionary<string, DynamicValue> keyValues) =>
            await RunWithConnection(c => SetManyImpl(c, license, keyValues));

        public async Task<Dictionary<string, DynamicValue>> GetAll(string license) =>
            await RunWithConnection(c => GetAllImpl(c, license));

        public async Task Delete(string license, string key) =>
            await RunWithConnection(c => DeleteImpl(c, license, key));

        protected abstract Task CreateImpl(MySqlConnection connection);
        protected abstract Task SetImpl(MySqlConnection connection, string license, string key, DynamicValue value);
        protected abstract Task SetManyImpl(MySqlConnection connection, string license, Dictionary<string, DynamicValue> keyValues);
        protected abstract Task<Dictionary<string, DynamicValue>> GetAllImpl(MySqlConnection connection, string license);
        protected abstract Task DeleteImpl(MySqlConnection connection, string license, string key);
    }

    public class KeyValueTable : DatabaseKeyValueStore
    {
        private readonly string tableName;
        private readonly string licenseColumnName;
        private readonly string keyColumnName;
        private readonly string valueColumnName;
        private readonly string typeColumnName;

        public KeyValueTable(string connectionString, string tableName, string licenseColumnName, string keyColumnName, string valueColumnName, string typeColumnName)
            : base(connectionString)
        {
            if (!tableName.IsValidDbIdentifier())
            {
                throw new ArgumentException("Table name must be a valid SQL identifier.");
            }

            if (!licenseColumnName.IsValidDbIdentifier() || !keyColumnName.IsValidDbIdentifier() || !valueColumnName.IsValidDbIdentifier() || !typeColumnName.IsValidDbIdentifier())
            {
                throw new ArgumentException("Column names must be valid SQL identifiers.");
            }

            this.tableName = tableName;
            this.licenseColumnName = licenseColumnName;
            this.keyColumnName = keyColumnName;
            this.valueColumnName = valueColumnName;
            this.typeColumnName = typeColumnName;
        }

        protected override async Task CreateImpl(MySqlConnection connection)
        {
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"CREATE TABLE IF NOT EXISTS `{tableName}` (`{licenseColumnName}` VARCHAR(128) NOT NULL, `{keyColumnName}` VARCHAR(128) NOT NULL, `{valueColumnName}` TEXT, `{typeColumnName}` BIT(2) NOT NULL DEFAULT 0, PRIMARY KEY (`{licenseColumnName}`, `{keyColumnName}`)) CHARACTER SET = utf8mb4 COLLATE = utf8mb4_bin ROW_FORMAT = COMPRESSED"
            };
            await command.ExecuteNonQueryAsync();
        }

        protected override async Task SetImpl(MySqlConnection connection, string license, string key, DynamicValue value)
        {
            var type = (int)value.Type;
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"INSERT INTO `{tableName}` (`{licenseColumnName}`, `{keyColumnName}`, `{valueColumnName}`, `{typeColumnName}`) VALUES (@license, @key, @value, @type) ON DUPLICATE KEY UPDATE `{valueColumnName}`=@value"
            };
            command.Parameters.AddWithValue("@license", license);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value.AsObject());
            command.Parameters.AddWithValue("@type", type);

            await command.ExecuteNonQueryAsync();
        }

        protected override async Task SetManyImpl(MySqlConnection connection, string license, Dictionary<string, DynamicValue> keyValues)
        {
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"INSERT INTO `{tableName}` (`{licenseColumnName}`, `{keyColumnName}`, `{valueColumnName}`, `{typeColumnName}`) VALUES (@license, @key, @value, @type) ON DUPLICATE KEY UPDATE `{valueColumnName}`=@value"
            };
            command.Parameters.AddWithValue("@license", license);
            command.Parameters.AddWithValue("@key", null);
            command.Parameters.AddWithValue("@value", null);
            command.Parameters.AddWithValue("@type", null);
            command.Prepare();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var kv in keyValues)
                    {
                        command.Transaction = transaction;
                        command.Parameters["@key"].Value = kv.Key;
                        command.Parameters["@value"].Value = kv.Value.AsObject();
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

        private static DynamicValue ParseValue(string value, DynamicValueType type)
        {
            switch (type)
            {
                case DynamicValueType.String:
                    return new DynamicValue(value);
                case DynamicValueType.Float:
                    return new DynamicValue(float.TryParse(value, out var floatValue) ? floatValue : 0f);
                case DynamicValueType.Int:
                    return new DynamicValue(int.TryParse(value, out var intValue) ? intValue : 0);
                case DynamicValueType.Bool:
                    return new DynamicValue(bool.TryParse(value, out var boolValue) ? boolValue : false);
            }
            throw new ArgumentOutOfRangeException(nameof(type), type, "Invalid DynamicValueType.");
        }

        protected override async Task<Dictionary<string, DynamicValue>> GetAllImpl(MySqlConnection connection, string license)
        {
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"SELECT `Key, Value, Type` FROM `{tableName}` WHERE `{licenseColumnName}`=@license"
            };
            command.Parameters.AddWithValue("@license", license);

            var keyValues = new Dictionary<string, DynamicValue>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var key = reader.GetString("Key");
                    var value = reader.GetString("Value");
                    var type = reader.GetInt32(2);

                    keyValues[key] = ParseValue(value, (DynamicValueType)type);
                }
            }
            return keyValues;
        }

        protected override async Task DeleteImpl(MySqlConnection connection, string license, string key)
        {
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"DELETE FROM `{tableName}` WHERE `{licenseColumnName}`=@license AND `{keyColumnName}`=@key"
            };
            command.Parameters.AddWithValue("@license", license);
            command.Parameters.AddWithValue("@key", key);

            await command.ExecuteNonQueryAsync();
        }
    }

    public class ColumnarTable : DatabaseKeyValueStore
    {
        private readonly string tableName;
        private readonly string licenseColumnName;
        private readonly Dictionary<string, DynamicValueType> columns;

        public ColumnarTable(string connectionString, string tableName, string licenseColumnName, Dictionary<string, DynamicValueType> columns)
            : base(connectionString)
        {
            if (!tableName.IsValidDbIdentifier())
            {
                throw new ArgumentException("Table name must be a valid SQL identifier.");
            }

            if (!licenseColumnName.IsValidDbIdentifier())
            {
                throw new ArgumentException("Column names must be a valid SQL identifier.");
            }

            foreach (var column in columns.Keys)
            {
                if (!column.IsValidDbIdentifier())
                {
                    throw new ArgumentException("Column names must be valid SQL identifiers.");
                }
            }

            this.tableName = tableName;
            this.licenseColumnName = licenseColumnName;
            this.columns = columns;
        }

        private static string GetSqlType(DynamicValueType type)
        {
            return type switch
            {
                DynamicValueType.String => "TEXT",
                DynamicValueType.Float => "FLOAT",
                DynamicValueType.Int => "INT",
                DynamicValueType.Bool => "BIT(1)",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Invalid DynamicValueType.")
            };
        }

        protected override async Task CreateImpl(MySqlConnection connection)
        {
            var columnDefinitions = string.Join(", ", columns.Select(kvp => $"`{kvp.Key}` {GetSqlType(kvp.Value)}"));
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"CREATE TABLE IF NOT EXISTS `{tableName}` (`{licenseColumnName}` VARCHAR(128) NOT NULL, {columnDefinitions}, PRIMARY KEY (`{licenseColumnName}`)) CHARACTER SET = utf8mb4 COLLATE = utf8mb4_bin"
            };
            await command.ExecuteNonQueryAsync();
        }

        protected override async Task SetImpl(MySqlConnection connection, string license, string key, DynamicValue value)
        {
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"INSERT INTO `{tableName}` (`{licenseColumnName}`, `{key}`) VALUES (@license, @value) ON DUPLICATE KEY UPDATE `{key}`=@value"
            };
            command.Parameters.AddWithValue("@license", license);
            command.Parameters.AddWithValue("@value", value.AsObject());

            await command.ExecuteNonQueryAsync();
        }

        protected override async Task SetManyImpl(MySqlConnection connection, string license, Dictionary<string, DynamicValue> keyValues)
        {
            var keyPlaceholders = keyValues.Keys
                .Select((key, i) => new Tuple<string, string>($"`{key}`", $"@value{i}"))
                .ToList();

            var columnNames = string.Join(", ", keyPlaceholders.Select(x => x.Item1));
            var valueParams = string.Join(", ", keyPlaceholders.Select(x => x.Item2));
            var updateString = string.Join(", ", keyPlaceholders.Select(x => $"{x.Item1}={x.Item2}"));

            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"INSERT INTO `{tableName}` (`{licenseColumnName}`, {columnNames}) VALUES (@license, {valueParams}) ON DUPLICATE KEY UPDATE {updateString}"
            };
            command.Parameters.AddWithValue("@license", license);

            foreach (var kv in keyValues)
            {
                command.Parameters.AddWithValue($"@{kv.Key}", kv.Value.AsObject());
            }

            await command.ExecuteNonQueryAsync();
        }

        private string ColumnNames => string.Join(", ", columns.Keys.Select(key => $"`{key}`"));

        protected override async Task<Dictionary<string, DynamicValue>> GetAllImpl(MySqlConnection connection, string license)
        {
            var columnNames = string.Join(", ", columns.Keys.Select(key => $"`{key}`"));
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"SELECT {ColumnNames} FROM `{tableName}` WHERE `{licenseColumnName}`=@license"
            };
            command.Parameters.AddWithValue("@license", license);

            var keyValues = new Dictionary<string, DynamicValue>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                foreach (var column in columns)
                {
                    var columnName = column.Key;
                    var columnType = column.Value;
                    keyValues[columnName] = DynamicValue.FromObject(reader[columnName], columnType);
                }
            }
            return keyValues;
        }

        protected override async Task DeleteImpl(MySqlConnection connection, string license, string key)
        {
            var command = new MySqlCommand
            {
                Connection = connection,
                CommandText = $"INSERT INTO `{tableName}` (`{licenseColumnName}`, `{key}`) VALUES (@license, DEFAULT) ON DUPLICATE KEY UPDATE `{key}`=DEFAULT"
            };
            command.Parameters.AddWithValue("@license", license);
            command.Parameters.AddWithValue("@key", key);

            await command.ExecuteNonQueryAsync();
        }
    }
}
