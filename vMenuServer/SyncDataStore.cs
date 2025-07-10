using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using vMenuShared;

namespace vMenuServer
{
    public class SyncDataStoreSyncHandler : SyncDataStoreSyncHandlerBase
    {
        public SyncDataStoreSyncHandler(DatabaseKeyValueStore databaseTable, bool prefixServerLicense = false)
        {
            this.databaseTable = databaseTable ?? throw new ArgumentNullException(nameof(databaseTable));
            serverLicenseStr = prefixServerLicense
                ? ServerLicense.Prefixed
                : ServerLicense.Unprefixed;
        }

        public override async Task<Dictionary<string, DynamicValue>> GetAll()
        {
            return await databaseTable.GetAll(serverLicenseStr);
        }

        public override async Task Set(string key, DynamicValue value)
        {
            await databaseTable.Set(serverLicenseStr, key, value);
        }

        public override async Task SetMany(Dictionary<string, DynamicValue> keyValues)
        {
            await databaseTable.SetMany(serverLicenseStr, keyValues);
        }

        public override async Task Delete(string key)
        {
            await databaseTable.Delete(serverLicenseStr, key);
        }

        private readonly DatabaseKeyValueStore databaseTable;

        private readonly string serverLicenseStr;

        private readonly static License ServerLicense = new License("license:0000000000000000000000000000000000000000");
    }
}
