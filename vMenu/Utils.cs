using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MenuAPI;

using Newtonsoft.Json;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;

namespace vMenuClient
{
    public class VehicleThumbnailDrawer
    {
        private readonly VehicleThumbnailTextureManager manager;

        private bool enabled;
        private readonly HashSet<Menu> menus = new HashSet<Menu>();

        public VehicleThumbnailDrawer(VehicleThumbnailTextureManager manager)
        {
            this.manager = manager;
        }

        public void AddMenu(Menu menu)
        {
            menus.Add(menu);
        }

        public void SetThumbnail(string vehicle)
        {
            enabled = true;
            manager.ChangeThumbnail(vehicle);
        }

        public void HideThumbnail()
        {
            enabled = false;
        }

        public async Task Setup()
        {
            await manager.Setup();
        }

        public async Task Draw()
        {
            if (!MenuController.IsAnyMenuOpen() || !enabled)
                return;

            var menu = MenuController.GetCurrentMenu();
            if (!menus.Contains(menu))
                return;

            var leftAligned = menu.LeftAligned;

            float size = 216f;
            float width = Menu.NormX(size);
            float height = Menu.NormY(size);

            var thumbnailX = Menu.DrawX(0f, width);
            var thumbnailY = Menu.DrawY(menu.DescriptionBottom + Menu.SectionYSep, height);

            var hAlign = (int)(leftAligned ? 'L' : 'R');
            var vAlign = (int)'T';
            SetScriptGfxAlign(hAlign, vAlign);
            SetScriptGfxAlignParams(0, 0, 0, 0);
            SetScriptGfxDrawOrder(3);
            DrawSprite(
                MainMenu.RUNTIME_TXD,
                VehicleThumbnailTextureManager.TEXTURE_NAME,
                thumbnailX,
                thumbnailY,
                width,
                height,
                0.0f,
                255,
                255,
                255,
                255);
            ResetScriptGfxAlign();
            SetScriptGfxDrawOrder(0);

            await Task.FromResult(0);
        }
    }

    public class VehicleThumbnailTextureManager
    {
        public const string TEXTURE_NAME = "vehicle_thumbnail";

        private readonly long runtimeTxdHandle;
        private readonly string runtimeTxdName;

        private long duiObj;

        private bool setup;

        public VehicleThumbnailTextureManager(
            long runtimeTxdHandle,
            string runtimeTxdName)
        {
            this.runtimeTxdHandle = runtimeTxdHandle;
            this.runtimeTxdName = runtimeTxdName;
        }

        public async Task Setup()
        {
            if (setup)
                throw new InvalidOperationException("VehicleThumbnailTextureManager already setup");

            var txdLoaded = () => HasStreamedTextureDictLoaded(runtimeTxdName);

            if (!txdLoaded())
            {
                RequestStreamedTextureDict(runtimeTxdName, false);
                while (!txdLoaded())
                {
                    await Delay(0);
                }
            }

            duiObj = CreateDui("https://cfx-nui-vMenu/vehiclethumbnail.html", 440, 440);
            while (!IsDuiAvailable(duiObj))
            {
                await Delay(0);
            }

            var duiHandle = GetDuiHandle(duiObj);
            CreateRuntimeTextureFromDuiHandle(runtimeTxdHandle, TEXTURE_NAME, duiHandle);

            setup = true;
        }

        public void ChangeThumbnail(string vehicle)
        {
            if (!setup)
                throw new InvalidOperationException("VehicleThumbnailTextureManager not setup");

            SendDuiMessage(duiObj, JsonConvert.SerializeObject(new { vehicle }));
        }
    }
}
