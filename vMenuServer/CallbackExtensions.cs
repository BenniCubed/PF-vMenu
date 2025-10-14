using CitizenFX.Core;

using Newtonsoft.Json;

namespace vMenuServer
{
    public static class CallbackExtensions
    {
        public static void InvokeAsCallback<T>(this object callback, Player player, T data)
        {
            player.TriggerEvent("vMenu:CallbackInvoked", JsonConvert.DeserializeObject<ulong>((string)callback), JsonConvert.SerializeObject(data));
        }
    }
}
