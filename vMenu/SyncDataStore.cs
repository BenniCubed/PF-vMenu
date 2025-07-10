using System.Collections.Generic;
using System.Threading.Tasks;

using vMenuShared;

using CitizenFX.Core;

using static vMenuShared.SyncDataStoreClientServerSync;

namespace vMenuClient
{
    public class SyncDataStoreSyncHandler : SyncDataStoreSyncHandlerBase
    {
        public string Name { get; private set; }

        public SyncDataStoreSyncHandler(string name)
        {
            Name = name;
        }

        public override async Task<Dictionary<string, DynamicValue>> GetAll()
        {
            var response = await Request.Send<ResponseGetAllData, RequestGetAllData>(
                $"{GetAllRequestEventName}",
                new RequestGetAllData
                {
                    Name = Name,
                });

            if (response.Success)
            {
                return response.Data.KeyValues;
            }
            else
            {
                Debug.WriteLine($"Failed to get data for datastore {Name}: {response.Error}");
                return new Dictionary<string, DynamicValue>();
            }
        }

        public override async Task Set(string key, DynamicValue value)
        {
            var response = await Request.Send<ResponseSetData, RequestSetData>(
                $"{SetRequestEventName}",
                new RequestSetData
                {
                    Name = Name,
                    Key = key,
                    Value = value,
                });

            if (!response.Success)
            {
                Debug.WriteLine($"Failed to set data for datastore {Name} with key {key}: {response.Error}");
            }
        }

        public override async Task SetMany(Dictionary<string, DynamicValue> keyValues)
        {
            var response = await Request.Send<ResponseSetManyData, RequestSetManyData>(
                $"{SetManyRequestEventName}",
                new RequestSetManyData
                {
                    Name = Name,
                    KeyValues = keyValues,
                });

            if (!response.Success)
            {
                Debug.WriteLine($"Failed to set multiple data for datastore {Name}: {response.Error}");
            }
        }

        public override async Task Delete(string key)
        {
            var response = await Request.Send<ResponseDeleteData, RequestDeleteData>(
                $"{DeleteRequestEventName}",
                new RequestDeleteData
                {
                    Name = Name,
                    Key = key,
                });

            if (!response.Success)
            {
                Debug.WriteLine($"Failed to delete data for datastore {Name} with key {key}: {response.Error}");
            }
        }
    }
}
