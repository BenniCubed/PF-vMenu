using System.Threading.Tasks;

using CitizenFX.Core;

using vMenuShared;

namespace vMenuClient
{
    public static class Request
    {
        public static async Task<Response<TResponseData>> Send<TResponseData, TRequestData>(
            string eventName,
            TRequestData data)
        {
            Response<TResponseData>? response = null;
            BaseScript.TriggerServerEvent(
                $"{eventName}]",
                data,
                (Response<TResponseData> response_) =>
                {
                    response = response_;
                });

            while (response == null)
            {
                await BaseScript.Delay(0);
            }
            return response.Value;
        }
    }
}
