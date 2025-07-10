using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

using static vMenuShared.ConfigManager;

namespace vMenuClient
{
    public class ValuesUserSettingJson
    {
        public List<string> Display { get; set; }
        public List<string> Keys { get; set; }
        public int DefaultIndex { get; set; } = 0;
    }

    public class CheckboxUserSettingJson
    {
        public bool DefaultValue { get; set; } = false;
    }

    public class RangeUserSetting
    {
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 100;
        public int DefaultValue { get; set; } = 0;
    }

    public class UserSettingJson
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Key { get; set; }

        public ValuesUserSettingJson Values { get; set; }
        public CheckboxUserSettingJson Checkbox { get; set; }
        public RangeUserSetting Range { get; set; }

        public string DataStore { get; set; }
    }

    public class UserSettingsJson
    {
        public string DefaultDataStore { get; set; }
        public List<UserSettingJson> Settings { get; set; } = new List<UserSettingJson>();
    }

    public class UserSettingStore
    {
        public enum Type
        {
            KeyValue,
            Column
        }

        public UserSettingStore(Type type, string table, List<UserSetting> settings)
        {
            if (string.isNullOrEmpty(table))
            {
                throw new ArgumentException($"{@table} cannot be null or empty.");
            }

            if (settings.Count == 0)
            {
                throw new ArgumentException($"{@settings} cannot be empty.");
            }

            Type = type;
            Table = table;
            Settings = settings;
        }

        Type Type { get;}
        public string Table { get; }
        public List<UserSetting> Settings { get; } = new List<UserSetting>();

        public SettingStore FromJson(UserSettingStoreJson json)
        {

        }
    }

        public class UserSettingJson
    {
        public string Name { get; set;}
        public string Key { get; set; }

        public List<string> Values { get; set; }
        public List<string> ValueKeys = Values;

        public int DefaultValue { get; set; } = 0;
    }

    public class UserSettingStoreJson
    {
        public SettingStoreType Type { get; set; } = SettingStoreType.KeyValue;
        public string Table { get; set;}
        public List<SettingJson> Settings { get; set; } = new List<SettingJson>();
    }
}
