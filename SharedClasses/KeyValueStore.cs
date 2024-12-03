using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using static CitizenFX.Core.Native.API;

#if CLIENT
namespace vMenuClient
#else
namespace vMenuServer
#endif
{
    public static partial class KeyValueStore
    {
        public enum ValueType
        {
            String = 0,
            Float = 1,
            Int = 2,
        }

        public struct ValueInfo
        {
            public ValueInfo(string value, ValueType type)
            {
                Value = value;
                Type = type;
            }
            public ValueInfo(string value) : this(value, ValueType.String)
            {
            }
            public ValueInfo(int value) : this(value.ToString(), ValueType.Int)
            {
            }
            public ValueInfo(float value) : this(value.ToString(), ValueType.Float)
            {
            }

            public string Value { get; set; }
            public ValueType Type { get; set; }

            public string AsString()
            {
                if (Type != ValueType.String)
                    throw new InvalidOperationException();

                return Value;
            }

            public int AsInt()
            {
                if (Type != ValueType.Int)
                    throw new InvalidOperationException();

                return int.TryParse(Value, out var result)
                    ? result
                    : 0;
            }

            public float AsFloat()
            {
                if (Type != ValueType.Float)
                    throw new InvalidOperationException();

                return float.TryParse(Value, out var result)
                    ? result
                    : 0;
            }
        }

        private static void SetLocal(string key, ValueInfo vi)
        {
            switch(vi.Type)
            {
                case ValueType.String:
                    SetLocal(key, vi.AsString());
                    break;
                case ValueType.Int:
                    SetLocal(key, vi.AsInt());
                    break;
                case ValueType.Float:
                    SetLocal(key, vi.AsFloat());
                    break;
            }
        }
        public static void SetLocal(string key, string value) => SetResourceKvp(key, value);
        public static void SetLocal(string key, int value) => SetResourceKvpInt(key, value);
        public static void SetLocal(string key, float value) => SetResourceKvpFloat(key, value);

        private static ValueInfo Get(string key)
        {
            try
            {
                return new ValueInfo(GetResourceKvpString(key));
            }
            catch
            {
                try
                {
                    return new ValueInfo(GetResourceKvpInt(key));
                }
                catch
                {
                    return new ValueInfo(GetResourceKvpFloat(key));
                }
            }
        }

        public static string GetString(string key)
        {
            var vi = Get(key);
            try
            {
                return vi.AsString();
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine($"^1[ERROR]^7 Trying to load key-value store key {key} as string, but is {vi.Type}.");
                return "";
            }
        }

        public static int GetInt(string key)
        {
            var vi = Get(key);
            try
            {
                return vi.AsInt();
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine($"^1[ERROR]^7 Trying to load key-value store key {key} as int, but is {vi.Type}.");
                return 0;
            }
        }

        public static float GetFloat(string key)
        {
            var vi = Get(key);
            try
            {
                return vi.AsFloat();
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine($"^1[ERROR]^7 Trying to load key-value store key {key} as float, but is {vi.Type}.");
                return 0;
            }
        }


        public static Dictionary<string, ValueInfo> GetAll(ValueType? type = null) => GetAllWithPrefix("", type);
        public static Dictionary<string, string> GetAllString() => GetAllWithPrefixString("");
        public static Dictionary<string, int> GetAllInt() => GetAllWithPrefixInt("");
        public static Dictionary<string, float> GetAllFloat() => GetAllWithPrefixFloat("");

        public static Dictionary<string, ValueInfo> GetAllWithPrefix(string keyPrefix, ValueType? type = null)
        {
            var keyValues = new Dictionary<string, ValueInfo>();

            var handle = StartFindKvp(keyPrefix);
            while (true)
            {
                var key = FindKvp(handle);
                if (key is "" or null or "NULL")
                    break;

                var vi = Get(key);
                if (type is null || vi.Type == type)
                    keyValues[key] = vi;
            }
            EndFindKvp(handle);

            return keyValues;
        }
        public static Dictionary<string, string> GetAllWithPrefixString(string keyPrefix) =>
            GetAllWithPrefix(keyPrefix, ValueType.String).ToDictionary(kv => kv.Key, kv => kv.Value.AsString());
        public static Dictionary<string, int> GetAllWithPrefixInt(string keyPrefix) =>
            GetAllWithPrefix(keyPrefix, ValueType.Int).ToDictionary(kv => kv.Key, kv => kv.Value.AsInt());
        public static Dictionary<string, float> GetAllWithPrefixFloat(string keyPrefix) =>
            GetAllWithPrefix(keyPrefix, ValueType.Float).ToDictionary(kv => kv.Key, kv => kv.Value.AsFloat());
    }
}

namespace vMenuShared
{
#if CLIENT
using vMenuClient;
#else
using vMenuServer;
#endif

    public static class KeyValueStoreSync
    {
        public struct Request
        {
            public enum RequestType
            {
                GetAll,
                Remove,
                Set,
                SetAll,
            }


            public struct RequestDataGetAll
            {
            }

            public struct RequestDataRemove
            {
                public string Key { get; set; }
            }

            public struct RequestDataSet
            {
                public string Key { get; set; }
                public KeyValueStore.ValueInfo ValueInfo { get; set; }
            }
            public struct RequestDataSetAll
            {
                public Dictionary<string, KeyValueStore.ValueInfo> KeyValues;
            }


            public ulong Id { get; set; }

            public RequestType Type { get; set; }

            public RequestDataGetAll? DataGetAll { get; set; }
            public RequestDataRemove? DataRemove { get; set; }
            public RequestDataSet? DataSet { get; set; }
            public RequestDataSetAll? DataSetAll { get; set; }
        }

        public struct Response
        {
            public enum ResponseType
            {
                Ok,
                Error,
                NoServerStore
            }


            public struct ResponseDataGetAll
            {
                public Dictionary<string, KeyValueStore.ValueInfo> KeyValues { get; set; }
            }

            public struct ResponseDataRemove
            {
            }

            public struct ResponseDataSet
            {
            }
            public struct ResponseDataSetAll
            {
            }


            public ulong Id { get; set; }
            public string Error { get; set; }

            public ResponseType Type { get; set; }

            public ResponseDataGetAll? DataGetAll { get; set; }
            public ResponseDataRemove? DataRemove { get; set; }
            public ResponseDataSet? DataSet { get; set; }
            public ResponseDataSetAll? DataSetAll { get; set; }
        }
    }
}
