using System;
using System.Collections.Generic;

using CitizenFX.Core;

using Newtonsoft.Json;

namespace vMenuClient
{
    public class CallbackFactory : BaseScript
    {
        public CallbackFactory()
        {
            if (initialized)
            {
                throw new InvalidOperationException("CallbackFactory has already been initialized!");
            }

            initialized = true;
        }

        public static object Create<T>(Action<T> callback)
        {
            ulong id = currentId++;

            callbacksExecutors.Add(id, data => callback(JsonConvert.DeserializeObject<T>(data)));

            return JsonConvert.SerializeObject(id);
        }

        [EventHandler("vMenu:CallbackInvoked")]
        public void OnCallbackInvoked(ulong id, string data)
        {
            callbacksExecutors[id](data);
            callbacksExecutors.Remove(id);
        }

        private static bool initialized = false;

        private static uint currentId = 0;
        private static Dictionary<ulong, Action<string>> callbacksExecutors = new Dictionary<ulong, Action<string>>();
    }
}
