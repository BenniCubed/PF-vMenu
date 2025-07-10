using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;

namespace vMenuShared
{
    public abstract class DataStore
    {
        public abstract Task Init();

        public void Set(string key, string value)
        {
            SetImpl(key, new DynamicValue(value));
        }
        public void Set(string key, float value)
        {
            SetImpl(key, new DynamicValue(value));
        }
        public void Set(string key, int value)
        {
            SetImpl(key, new DynamicValue(value));
        }
        public void Set(string key, bool value)
        {
            SetImpl(key, new DynamicValue(value));
        }
        public void Set(string key, DynamicValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "DynamicValue cannot be null.");
            }
            SetImpl(key, value);
        }

        public string GetString(string key)
        {
            return GetImpl(key).AsString();
        }
        public float GetFloat(string key)
        {
            return GetImpl(key).AsFloat();
        }
        public int GetInt(string key)
        {
            return GetImpl(key).AsInt();
        }
        public bool GetBool(string key)
        {
            return GetImpl(key).AsBool();
        }
        public DynamicValue Get(string key)
        {
            return GetImpl(key);
        }

        public Dictionary<string, string> GetAllString(string prefix = null)
        {
            return GetAllImpl(prefix, DynamicValueType.String)
                .ToDictionary(kv => kv.Key, kv => kv.Value.AsString());
        }
        public Dictionary<string, float> GetAllFloat(string prefix = null)
        {
            return GetAllImpl(prefix, DynamicValueType.Float)
                .ToDictionary(kv => kv.Key, kv => kv.Value.AsFloat());
        }
        public Dictionary<string, int> GetAllInt(string prefix = null)
        {
            return GetAllImpl(prefix, DynamicValueType.Int)
                .ToDictionary(kv => kv.Key, kv => kv.Value.AsInt());
        }
        public Dictionary<string, bool> GetAllBool(string prefix = null)
        {
            return GetAllImpl(prefix, DynamicValueType.Bool)
                .ToDictionary(kv => kv.Key, kv => kv.Value.AsBool());
        }
        public Dictionary<string, DynamicValue> GetAll(string prefix = null, DynamicValueType? type = null)
        {
            return GetAllImpl(prefix, type);
        }

        public void Delete(string key)
        {
            DeleteImpl(key);
        }

        protected abstract void SetImpl(string key, DynamicValue value);
        protected abstract DynamicValue GetImpl(string key);
        protected abstract Dictionary<string, DynamicValue> GetAllImpl(
            string prefix = null,
            DynamicValueType? type = null);
        protected abstract void DeleteImpl(string key);
    }

    public class ModifyEventFiringDataStore : DataStore
    {
        public ModifyEventFiringDataStore(string name, DataStore dataStore)
        {
            this.name = name;
            this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore), "DataStore cannot be null.");
        }

        public override async Task Init()
        {
            await dataStore.Init();
        }

        protected override void SetImpl(string key, DynamicValue value)
        {
            dataStore.Set(key, value);
            BaseScript.TriggerEvent("vMenu:DataStore:Set", name, key, value);
        }

        protected override DynamicValue GetImpl(string key)
        {
            return dataStore.Get(key);
        }

        protected override Dictionary<string, DynamicValue> GetAllImpl(string prefix = null, DynamicValueType? type = null)
        {
            return dataStore.GetAll(prefix, type);
        }

        protected override void DeleteImpl(string key)
        {
            dataStore.Delete(key);
            BaseScript.TriggerEvent("vMenu:DataStore:Delete", name, key);
        }

        private readonly string name;
        private readonly DataStore dataStore;
    }

    public class ResourceDataStore : DataStore
    {
        public ResourceDataStore(string name = null)
        {
            prefix = string.IsNullOrWhiteSpace(name) ? "" : $"{name}:";
        }

        public override Task Init()
        {
            return Task.FromResult(0);
        }

        protected override void SetImpl(string key, DynamicValue value)
        {
            var prefixedKey = PrefixKey(key);
            switch (value.Type)
            {
                case DynamicValueType.String:
                    SetResourceKvp(prefixedKey, value.AsString());
                    break;
                case DynamicValueType.Float:
                    SetResourceKvpFloat(prefixedKey, value.AsFloat());
                    break;
                case DynamicValueType.Int:
                    SetResourceKvpInt(prefixedKey, value.AsInt());
                    break;
                case DynamicValueType.Bool:
                    SetResourceKvp(prefixedKey, value.AsBool().ToString());
                    break;
            }
        }

        protected override DynamicValue GetImpl(string key)
        {
            var prefixedKey = PrefixKey(key);
            foreach (var typedGetter in DataGetters)
            {
                try
                {
                    return typedGetter(prefixedKey);
                }
                catch
                {
                    // Ignore exceptions and try the next type
                }
            }

            throw new KeyNotFoundException($"Key '{key}' not found in the data store.");
        }

        protected override Dictionary<string, DynamicValue> GetAllImpl(string prefix = null, DynamicValueType? type = null)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "";
            }

            var keyValues = new Dictionary<string, DynamicValue>();

            var handle = StartFindKvp(PrefixKey(prefix));
            while (true)
            {
                var key = FindKvp(handle);
                if (key is "" or null or "NULL")
                {
                    break;
                }

                var value = GetImpl(key);
                if (type is null || value.Type == type)
                {
                    keyValues[UnprefixKey(key)] = value;
                }
            }
            EndFindKvp(handle);

            return keyValues;
        }

        protected override void DeleteImpl(string key)
        {
            DeleteResourceKvp(PrefixKey(key));
        }

        private string PrefixKey(string key) => $"{prefix}:{key}";
        private string UnprefixKey(string key) => key.Substring(prefix.Length);

        private static readonly List<Func<string, DynamicValue>> DataGetters = new List<Func<string, DynamicValue>>
        {
            // must come before string, as a bool is a special case of a string
            key => new DynamicValue(bool.Parse(GetResourceKvpString(key))),
            key => new DynamicValue(GetResourceKvpString(key)),
            key => new DynamicValue(GetResourceKvpFloat(key)),
            key => new DynamicValue(GetResourceKvpInt(key)),
        };

        private readonly string prefix;
    }

    public class TempDataStore : DataStore
    {
        public TempDataStore()
        {
            dataStore = new Dictionary<string, DynamicValue>();
        }

        public override Task Init()
        {
            return Task.FromResult(0);
        }

        protected override void SetImpl(string key, DynamicValue value)
        {
            dataStore[key] = value;
        }

        protected override DynamicValue GetImpl(string key)
        {
            if (dataStore.TryGetValue(key, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Key '{key}' not found in the data store.");
        }

        protected override Dictionary<string, DynamicValue> GetAllImpl(string prefix = null, DynamicValueType? type = null)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "";
            }

            return dataStore
                .Where(kv => kv.Key.StartsWith(prefix))
                .Where(kv => type is null || kv.Value.Type == type)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        protected override void DeleteImpl(string key)
        {
            dataStore.Remove(key);
        }

        private readonly Dictionary<string, DynamicValue> dataStore;
    }
}
