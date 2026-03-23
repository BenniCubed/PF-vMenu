using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using CitizenFX.Core;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace vMenuClient.data
{
    public static class Usersettings
    {
        public class UsersettingListSpec
        {
            public class Item
            {
                public object key;
                public string name;
            }

            public List<Item> items;
            public object defaultKey;

            public int GetKeyIndex(object key)
            {
                return items.FindIndex(i => Equals(key, i.key));
            }

            public bool IsValidValue(object key)
            {
                var normKey = NormalizeValue(key);
                return normKey != null && GetKeyIndex(normKey) != -1;
            }

            public object NormalizeValue(object value)
            {
                var strValue = value as string;
                if (strValue != null)
                {
                    return strValue;
                }

                var intOk = int.TryParse($"{value}", out var intValue);
                if (intOk)
                {
                    return intValue;
                }

                var boolValue = value as bool?;
                return boolValue;
            }

            public bool Verify(string key)
            {
                var writeError = (string error) =>
                {
                    Debug.WriteLine($"Invalid list spec for usersetting \"{key}\": {error}");
                };

                if (items == null || items.Count == 0)
                {
                    writeError("items is null or empty");
                    return false;
                }

                var keys = new HashSet<object>();
                Type lastItemKeyType = null;
                foreach (var item in items)
                {
                    var normKey = NormalizeValue(item.key);
                    if (normKey == null)
                    {
                        writeError("invalid item key found");
                        return false;
                    }

                    var ikey = item.key = normKey;
                    var keyType = ikey.GetType();

                    if (keys.Contains(ikey))
                    {
                        writeError($"duplicate item key \"{ikey}\"");
                        return false;
                    }

                    if (lastItemKeyType != null && lastItemKeyType != keyType)
                    {
                        writeError($"item key \"{ikey}\" has inconsistent type");
                        return false;
                    }

                    keys.Add(ikey);
                    lastItemKeyType = keyType;
                }

                if (defaultKey == null)
                {
                    defaultKey = items[0].key;
                }
                else
                {
                    defaultKey = NormalizeValue(defaultKey);
                    if (!IsValidValue(defaultKey))
                    {
                        writeError($"invalid default key \"{defaultKey}\"");
                        return false;
                    }
                }

                return true;
            }
        }

        public class UsersettingRangeSpec
        {
            public int begin = 0;
            public int end = 1;
            public int step = 1;
            public int defaultValue = 0;

            public int GetValueIndex(int value)
            {
                var offset = value - begin;
                if (begin > value || end < value || offset % step != 0)
                {
                    return -1;
                }

                return offset / step;
            }

            public bool IsValidValue(object value)
            {
                var ok = int.TryParse($"{value}", out var intValue) && value is not string;
                return ok && GetValueIndex(intValue) != -1;
            }

            public int? NormalizeValue(object value)
            {
                var ok = int.TryParse($"{value}", out var intValue) && value is not string;
                return ok ? intValue : null;
            }

            public bool Verify(string key)
            {
                var writeError = (string error) =>
                {
                    Debug.WriteLine($"Invalid range spec for usersetting \"{key}\": {error}");
                };

                if (begin > end)
                {
                    writeError("begin > end");
                    return false;
                }

                if (step < 1)
                {
                    writeError("step < 1");
                    return false;
                }

                if (!IsValidValue(end))
                {
                    writeError("end is not a valid value based on begin and step");
                    return false;
                }

                if (!IsValidValue(defaultValue))
                {
                    writeError("defaultVal is not a valid value based on begin and step");
                    return false;
                }

                return true;
            }
        }

        public class UsersettingToggleSpec
        {
            public bool defaultState = false;

            public bool IsValidValue(object value)
            {
                return NormalizeValue(value) != null;
            }

            public bool? NormalizeValue(object value)
            {
                return value as bool?;
            }

            public bool Verify(string key)
            {
                return true;
            }
        }

        public class UsersettingSpec
        {
            public string key;
            public string name;
            public string description = "";

            public string type;
            public JToken spec;

            public UsersettingListSpec listSpec;
            public UsersettingRangeSpec rangeSpec;
            public UsersettingToggleSpec toggleSpec;

            public void Visit(
                Action<UsersettingListSpec> listSpecAction,
                Action<UsersettingRangeSpec> rangeSpecAction,
                Action<UsersettingToggleSpec> toggleSpecAction)
            {
                switch (type)
                {
                    case "list":
                        listSpecAction(listSpec);
                        break;
                    case "range":
                        rangeSpecAction(rangeSpec);
                        break;
                    case "toggle":
                        toggleSpecAction(toggleSpec);
                        break;
                    default:
                        throw new InvalidOperationException($"Usersetting spec \"{key}\" with invalid type \"{type}\"");
                }
            }

            public bool DeserializeSpec()
            {
                try
                {
                    Visit(
                        _ => listSpec = spec.ToObject<UsersettingListSpec>(),
                        _ => rangeSpec = spec.ToObject<UsersettingRangeSpec>(),
                        _ => toggleSpec = spec.ToObject<UsersettingToggleSpec>());
                }
                catch (InvalidOperationException e)
                {
                    Debug.WriteLine($"[WARNING] {e.Message}");
                    return false;
                }

                return true;
            }

            public bool Verify()
            {
                var verified = false;
                Visit(
                    s => verified = s.Verify(key),
                    s => verified = s.Verify(key),
                    s => verified = s.Verify(key));

                return verified;
            }

            public bool IsValidValue(object value)
            {
                if (value == null)
                {
                    return false;
                }

                var isValidValue = false;
                Visit(
                    s => isValidValue = s.IsValidValue(value),
                    s => isValidValue = s.IsValidValue(value),
                    s => isValidValue = s.IsValidValue(value));

                return isValidValue;
            }

            public object NormalizeValue(object value)
            {
                if (value == null)
                {
                    return null;
                }

                try
                {
                    object normalizedValue = null;
                    Visit(
                        s => normalizedValue = s.NormalizeValue(value),
                        s => normalizedValue = s.NormalizeValue(value),
                        s => normalizedValue = s.NormalizeValue(value)
                    );
                    return normalizedValue;
                }
                catch
                {
                    return null;
                }
            }

            public object DefaultValue()
            {
                object value = null;
                Visit(
                    s => value = s.defaultKey,
                    s => value = s.defaultValue,
                    s => value = s.defaultState);

                return value;
            }
        }

        class UsersettingsInfo
        {
            public string menuName = "User Settings";
            public string menuDescription = "User Settings";
            public List<UsersettingSpec> specs = new List<UsersettingSpec>();
        }

        public static string UsersettingsMenuName { get; private set; } = "User Settings";
        public static string UsersettingsMenuDescription { get; private set; } = "User Settings";

        public static List<UsersettingSpec> UsersettingsSpecs { get; private set; }
            = new List<UsersettingSpec>();

        public static Dictionary<string, UsersettingSpec> UsersettingsSpecsDict { get; private set; }
            = new Dictionary<string, UsersettingSpec>();

        public static Dictionary<string, object> UsersettingsDict { get; private set; }
            = new Dictionary<string, object>();

        public static void InitUsersettingsInfo(string usersettingsInfoJson)
        {
            if (string.IsNullOrEmpty(usersettingsInfoJson))
                return;

            var info = JsonConvert.DeserializeObject<UsersettingsInfo>(usersettingsInfoJson);

            UsersettingsMenuName = info.menuName;
            UsersettingsMenuDescription = info.menuDescription;

            var specs = info.specs;
            foreach (var spec in specs)
            {
                var key = spec.key;

                if (UsersettingsSpecsDict.ContainsKey(key))
                {
                    Debug.WriteLine($"[WARNING] Usersetting spec with duplicate key \"{key}\"");
                    continue;
                }

                if (!spec.DeserializeSpec() || !spec.Verify())
                {
                    continue;
                }

                UsersettingsSpecsDict.Add(key, spec);
                UsersettingsSpecs.Add(spec);
                UsersettingsDict.Add(key, spec.DefaultValue());
            }
        }

        public static void InitUsersettings(string usersettingsJson)
        {
            if (string.IsNullOrEmpty(usersettingsJson))
                return;

            var usersettingsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(usersettingsJson);

            foreach (var setting in usersettingsDict)
            {
                var key = setting.Key;
                var value = setting.Value;

                var specExists = UsersettingsSpecsDict.TryGetValue(key, out var spec);
                if (!specExists)
                {
                    if (!key.StartsWith("__"))
                    {
                        Debug.WriteLine($"[WARNING] Usersetting spec \"{key}\" does not exist");
                    }
                    continue;
                }

                if (value != null && !spec.IsValidValue(value))
                {
                    Debug.WriteLine($"[WARNING] Invalid value \"{value}\" for usersetting \"{key}\"");
                    value = spec.DefaultValue();
                }
                else if (value == null)
                {
                    value = spec.DefaultValue();
                }

                UsersettingsDict[key] = spec.NormalizeValue(value);
            }
        }

        private static readonly Dictionary<string, object> updatedUsersettings = new Dictionary<string, object>();

        public static bool CheckCanUpdateUsersetting(string key, object value)
        {
            if (UsersettingsSpecsDict.Count == 0)
            {
                return false;
            }

            UsersettingsSpecsDict.TryGetValue(key, out var spec);
            if (spec == null)
            {
                Debug.WriteLine($"[WARNING] Usersetting spec \"{key}\" does not exist");
                return false;
            }

            if (!spec.IsValidValue(value))
            {
                Debug.WriteLine($"[WARNING] Invalid value \"{value}\" for usersetting \"{key}\"");
                return false;
            }

            return true;
        }

        public static bool TryUpdateUsersetting(string key, object value, bool sendEvent, bool sync)
        {
            var specOk = UsersettingsSpecsDict.TryGetValue(key, out var spec);
            if (!specOk)
            {
                Debug.WriteLine($"[WARNING] Usersetting spec \"{key}\" does not exist");
                return false;
            }

            var normValue = spec.NormalizeValue(value);
            if (!spec.IsValidValue(normValue))
            {
                Debug.WriteLine($"[WARNING] Invalid value \"{value}\" for usersetting \"{key}\"");
                return false;
            }

            UpdateUsersetting(key, value, sendEvent, sync);
            return true;
        }

        private static void UpdateUsersetting(string key, object value, bool sendEvent, bool sync)
        {
            UsersettingsDict[key] = value;

            if (sendEvent)
            {
                BaseScript.TriggerEvent($"vMenu:UsersettingUpdated:{key}", value);
                BaseScript.TriggerEvent($"vMenu:UsersettingUpdated", key, value);
            }

            if (sync)
            {
                updatedUsersettings[key] = value;
            }
        }

        public static void SyncUpdatedUsersettings()
        {
            if (updatedUsersettings.Count == 0)
                return;

            BaseScript.TriggerServerEvent(
                "vMenu:SyncUpdatedUsersettings",
                JsonConvert.SerializeObject(updatedUsersettings));

            updatedUsersettings.Clear();
        }
    }
}
