using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace vMenuShared
{
    public abstract class SyncDataStoreSyncHandlerBase
    {
        public SyncDataStoreSyncHandlerBase()
        {
        }

        public abstract Task<Dictionary<string, DynamicValue>> GetAll();
        public abstract Task Set(string key, DynamicValue value);
        public abstract Task SetMany(Dictionary<string, DynamicValue> keyValues);
        public abstract Task Delete(string key);
    }

    public class SyncDataStore : DataStore
    {
        public SyncDataStore(SyncDataStoreSyncHandlerBase sync, DataStore cache = null)
        {
            this.sync = sync ?? throw new ArgumentNullException(nameof(sync));
            this.cache = cache;
        }

        public override async Task Init()
        {
            await cache?.Init();
            var cachedKvs = cache.GetAll();
            var syncedKvs = await sync.GetAll();

            // Sync local values if they are not in the sync store or if they differ, cache sync values if they are not
            // in the local cache.

            var cacheKvs = syncedKvs
                .Where(kv => !cachedKvs.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var kv in cacheKvs)
            {
                cache.Set(kv.Key, kv.Value);
            }

            var syncKvs = cachedKvs
                .Where(kv => !syncedKvs.ContainsKey(kv.Key) || syncedKvs[kv.Key] != kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            if (syncKvs.Count > 0)
            {
                await sync.SetMany(syncKvs);
            }
        }

        protected override void SetImpl(string key, DynamicValue value)
        {
            sync.Set(key, value);
            cache?.Set(key, value);
        }

        protected override DynamicValue GetImpl(string key)
        {
            return cache?.Get(key);
        }

        protected override Dictionary<string, DynamicValue> GetAllImpl(string prefix = null, DynamicValueType? type = null)
        {
            return cache?.GetAll(prefix, type);
        }

        protected override void DeleteImpl(string key)
        {
            sync.Delete(key);
            cache?.Delete(key);
        }

        private readonly SyncDataStoreSyncHandlerBase sync;
        private readonly DataStore cache;
    }

    public static class SyncDataStoreClientServerSync
    {
        public struct RequestGetAllData
        {
            public string Name { get; set; }
        }

        public struct RequestSetData
        {
            public string Name { get; set; }
            public string Key { get; set; }
            public DynamicValue Value { get; set; }
        }

        public struct RequestSetManyData
        {
            public string Name { get; set; }
            public Dictionary<string, DynamicValue> KeyValues { get; set; }
        }

        public struct RequestDeleteData
        {
            public string Name { get; set; }
            public string Key { get; set; }
        }

        public struct ResponseGetAllData
        {
            public Dictionary<string, DynamicValue> KeyValues { get; set; }
        }

        public struct ResponseSetData
        {
        }

        public struct ResponseSetManyData
        {
        }

        public struct ResponseDeleteData
        {
        }

        public const string GetAllRequestEventName = "vMenu:DataStore:GetAll";
        public const string SetRequestEventName = "vMenu:DataStore:Set";
        public const string SetManyRequestEventName = "vMenu:DataStore:SetMany";
        public const string DeleteRequestEventName = "vMenu:DataStore:Delete";
    }
}
