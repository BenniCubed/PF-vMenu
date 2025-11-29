using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;

namespace vMenuClient
{
    public class NoClip : BaseScript
    {
        private static List<Tuple<string, float>> speeds = new List<Tuple<string, float>>
        {
            new Tuple<string, float>("Super Slow", 20.0f),
            new Tuple<string, float>("Very Slow", 50.0f),
            new Tuple<string, float>("Slow", 100.0f),
            new Tuple<string, float>("Normal", 250.0f),
            new Tuple<string, float>("Fast", 1000.0f),
            new Tuple<string, float>("Very Fast", 2000.0f),
            new Tuple<string, float>("Super Fast", 4000.0f),
            new Tuple<string, float>("Lightning Fast", 10000.0f),
        };
        private static Dictionary<string, int> speedsMap = speeds
            .Select((t, i) => new Tuple<string, int>(t.Item1, i))
            .ToDictionary(t => t.Item1, t => t.Item2);

        private static bool NoclipActive { get; set; } = false;
        private static int MovingSpeed { get; set; } = speedsMap["Normal"];
        private static int Scale { get; set; } = -1;
        private static bool FollowCamMode { get; set; } = true;
        private static bool FlyCamMode { get; set; } = true;

        public NoClip()
        {
            Tick += NoClipHandler;

            Vector3 prevPos = Player.Local.Character.Position;
        }

        internal static void SetNoclipActive(bool active)
        {
            NoclipActive = active;
        }

        internal static bool IsNoclipActive()
        {
            return NoclipActive;
        }
        static string HashString(string command)
        {
            uint hash = 0;
            string str = command.ToLower();

            for (int i = 0; i < str.Length; i++)
            {
                uint letter = (uint)str[i];
                hash = hash + letter;
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }

            hash += (hash << 3);
            if (hash < 0)
            {
                hash = (uint)((int)hash);
            }

            hash ^= (hash >> 11);
            hash += (hash << 15);

            if (hash < 0)
            {
                hash = (uint)((int)hash);
            }

            return hash.ToString("X");
        }
        private async Task NoClipHandler()
        {
            if (NoclipActive)
            {
                if (vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_notification_type).ToLower() == "mosh")
                {
                    TriggerEvent("mosh_notify:notify", "SUCCESS", "<span class=\"text-white\">You are now in NoClip! Press F2 to turn off NoClip.</span>", "success", "success", 5000);
                }
                Scale = RequestScaleformMovie("INSTRUCTIONAL_BUTTONS");
                while (!HasScaleformMovieLoaded(Scale))
                {
                    await Delay(0);
                }

                DrawScaleformMovieFullscreen(Scale, 255, 255, 255, 0, 0);
            }
            while (NoclipActive)
            {
                if (!IsHudHidden())
                {
                    BeginScaleformMovieMethod(Scale, "CLEAR_ALL");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(0);
                    PushScaleformMovieMethodParameterString($"{speeds[MovingSpeed].Item1}");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(1);
                    PushScaleformMovieMethodParameterString("~INPUT_SPRINT~");
                    PushScaleformMovieMethodParameterString($"Faster");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(2);
                    PushScaleformMovieMethodParameterString("~INPUT_JUMP~");
                    PushScaleformMovieMethodParameterString($"Slower");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(3);
                    PushScaleformMovieMethodParameterString("~INPUT_MOVE_LR~");
                    PushScaleformMovieMethodParameterString($"Left/Right");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(4);
                    PushScaleformMovieMethodParameterString("~INPUT_MOVE_UD~");
                    PushScaleformMovieMethodParameterString($"Move");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(5);
                    PushScaleformMovieMethodParameterString("~INPUT_MULTIPLAYER_INFO~");
                    PushScaleformMovieMethodParameterString($"Down");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(6);
                    PushScaleformMovieMethodParameterString("~INPUT_COVER~");
                    PushScaleformMovieMethodParameterString($"Up");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(7);
                    PushScaleformMovieMethodParameterString("~INPUT_VEH_HEADLIGHT~");
                    PushScaleformMovieMethodParameterString($"Cam Mode");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "SET_DATA_SLOT");
                    ScaleformMovieMethodAddParamInt(8);
                    PushScaleformMovieMethodParameterString($"~INPUT_{HashString($"{vMenuShared.ConfigManager.GetSettingsString(vMenuShared.ConfigManager.Setting.vmenu_individual_server_id)}vMenu:NoClip")}~");
                    PushScaleformMovieMethodParameterString($"Toggle NoClip");
                    EndScaleformMovieMethod();

                    BeginScaleformMovieMethod(Scale, "DRAW_INSTRUCTIONAL_BUTTONS");
                    ScaleformMovieMethodAddParamInt(0);
                    EndScaleformMovieMethod();

                    DrawScaleformMovieFullscreen(Scale, 255, 255, 255, 255, 0);
                }

                var noclipEntity = Game.PlayerPed.IsInVehicle() ? Game.PlayerPed.CurrentVehicle.Handle : Game.PlayerPed.Handle;

                FreezeEntityPosition(noclipEntity, true);
                SetEntityInvincible(noclipEntity, true);

                Vector3 newPos;
                Game.DisableControlThisFrame(0, Control.AccurateAim);
                Game.DisableControlThisFrame(0, Control.Attack);
                Game.DisableControlThisFrame(0, Control.Attack2);
                Game.DisableControlThisFrame(0, Control.Aim);
                Game.DisableControlThisFrame(0, Control.Cover);
                Game.DisableControlThisFrame(0, Control.Detonate);
                Game.DisableControlThisFrame(0, Control.MeleeAttack1);
                Game.DisableControlThisFrame(0, Control.MeleeAttack2);
                Game.DisableControlThisFrame(0, Control.MeleeAttackAlternate);
                Game.DisableControlThisFrame(0, Control.MeleeAttackLight);
                Game.DisableControlThisFrame(0, Control.MeleeAttackHeavy);
                Game.DisableControlThisFrame(0, Control.MeleeBlock);
                Game.DisableControlThisFrame(0, Control.MoveUpOnly);
                Game.DisableControlThisFrame(0, Control.MoveUp);
                Game.DisableControlThisFrame(0, Control.MoveUpDown);
                Game.DisableControlThisFrame(0, Control.MoveDown);
                Game.DisableControlThisFrame(0, Control.MoveDownOnly);
                Game.DisableControlThisFrame(0, Control.MoveLeft);
                Game.DisableControlThisFrame(0, Control.MoveLeftOnly);
                Game.DisableControlThisFrame(0, Control.MoveLeftRight);
                Game.DisableControlThisFrame(0, Control.MoveRight);
                Game.DisableControlThisFrame(0, Control.MoveRightOnly);
                Game.DisableControlThisFrame(0, Control.MultiplayerInfo);
                Game.DisableControlThisFrame(0, Control.NextWeapon);
                Game.DisableControlThisFrame(0, Control.PrevWeapon);
                Game.DisableControlThisFrame(0, Control.SelectNextWeapon);
                Game.DisableControlThisFrame(0, Control.SelectPrevWeapon);
                Game.DisableControlThisFrame(0, Control.SelectWeapon);
                Game.DisableControlThisFrame(0, Control.SelectWeaponAutoRifle);
                Game.DisableControlThisFrame(0, Control.SelectWeaponHandgun);
                Game.DisableControlThisFrame(0, Control.SelectWeaponHeavy);
                Game.DisableControlThisFrame(0, Control.SelectWeaponMelee);
                Game.DisableControlThisFrame(0, Control.SelectWeaponSmg);
                Game.DisableControlThisFrame(0, Control.SelectWeaponSniper);
                Game.DisableControlThisFrame(0, Control.SelectWeaponSpecial);
                Game.DisableControlThisFrame(0, Control.SelectWeaponUnarmed);
                Game.DisableControlThisFrame(0, Control.ThrowGrenade);
                Game.DisableControlThisFrame(0, Control.VehicleAim);
                Game.DisableControlThisFrame(0, Control.VehicleAttack);
                Game.DisableControlThisFrame(0, Control.VehicleAttack2);
                Game.DisableControlThisFrame(0, Control.VehicleFlyAttack);
                Game.DisableControlThisFrame(0, Control.VehicleFlyAttack2);
                Game.DisableControlThisFrame(0, Control.VehicleFlyAttackCamera);
                Game.DisableControlThisFrame(0, Control.VehicleFlySelectNextWeapon);
                Game.DisableControlThisFrame(0, Control.VehicleFlySelectPrevWeapon);
                Game.DisableControlThisFrame(0, Control.VehicleGunDown);
                Game.DisableControlThisFrame(0, Control.VehicleGunLeft);
                Game.DisableControlThisFrame(0, Control.VehicleGunLeftRight);
                Game.DisableControlThisFrame(0, Control.VehicleGunRight);
                Game.DisableControlThisFrame(0, Control.VehicleGunUp);
                Game.DisableControlThisFrame(0, Control.VehicleGunUpDown);
                Game.DisableControlThisFrame(0, Control.VehicleHeadlight);
                Game.DisableControlThisFrame(0, Control.VehicleSelectNextWeapon);
                Game.DisableControlThisFrame(0, Control.VehicleSelectPrevWeapon);
                Game.DisableControlThisFrame(0, Control.VehiclePassengerAim);
                Game.DisableControlThisFrame(0, Control.VehiclePassengerAttack);
                Game.DisableControlThisFrame(0, Control.WeaponSpecial);
                Game.DisableControlThisFrame(0, Control.WeaponWheelLeftRight);
                Game.DisableControlThisFrame(0, Control.WeaponWheelNext);
                Game.DisableControlThisFrame(0, Control.WeaponWheelPrev);
                Game.DisableControlThisFrame(0, Control.WeaponWheelUpDown);
                if (Game.PlayerPed.IsInVehicle())
                    Game.DisableControlThisFrame(0, Control.VehicleRadioWheel);

                var xoff = 0.0f;
                var yoff = 0.0f;
                var zoff = 0.0f;

                if (UpdateOnscreenKeyboard() != 0 && !Game.IsPaused)
                {
                    if (Game.IsControlJustPressed(0, Control.Sprint))
                    {
                        MovingSpeed = Math.Min(MovingSpeed + 1, speeds.Count - 1);
                    }
                    else if (Game.IsControlJustPressed(0, Control.Jump))
                    {
                        MovingSpeed = Math.Max(0, MovingSpeed - 1);
                    }

                    if (Game.IsDisabledControlPressed(0, Control.MoveUpOnly))
                    {
                        yoff = 1f;
                    }
                    if (Game.IsDisabledControlPressed(0, Control.MoveDownOnly))
                    {
                        yoff = -1f;
                    }
                    if (Game.IsDisabledControlPressed(0, Control.MoveLeftOnly))
                    {
                        if (FlyCamMode && FollowCamMode)
                        {
                            xoff = -1f;
                        }
                        else if (!FollowCamMode)
                        {
                            SetEntityHeading(Game.PlayerPed.Handle, GetEntityHeading(Game.PlayerPed.Handle) + 3f);
                        }
                    }
                    if (Game.IsDisabledControlPressed(0, Control.MoveRightOnly))
                    {
                        if (FlyCamMode && FollowCamMode)
                        {
                            xoff = 1f;
                        }
                        else if (!FollowCamMode)
                        {
                            SetEntityHeading(Game.PlayerPed.Handle, GetEntityHeading(Game.PlayerPed.Handle) - 3f);
                        }
                    }
                    if (Game.IsDisabledControlPressed(0, Control.Cover))
                    {
                        zoff = 0.5f;
                    }
                    if (Game.IsDisabledControlPressed(0, Control.MultiplayerInfo))
                    {
                        zoff = -0.5f;
                    }
                    if (Game.IsDisabledControlJustPressed(0, Control.VehicleHeadlight))
                    {
                        if (FlyCamMode)
                        {
                            FlyCamMode = false;
                        }
                        else if (FollowCamMode)
                        {
                            FollowCamMode = false;
                        }
                        else
                        {
                            FollowCamMode = true;
                            FlyCamMode = true;
                        }
                    }
                }

                float relPitch = FlyCamMode ? MathUtil.DegreesToRadians(GetGameplayCamRelativePitch()) : 0f;
                float factorxy = (float)Math.Cos((double)relPitch);
                float factorz = (float)Math.Sin((double)relPitch);

                Vector3 off = new Vector3(factorxy * xoff, factorxy * yoff, zoff + factorz * yoff);
                off.Normalize();
                off *= GetFrameTime() * speeds[MovingSpeed].Item2 / 3.6f;

                newPos = GetOffsetFromEntityInWorldCoords(noclipEntity, off.X, off.Y, off.Z);
                var heading = GetEntityHeading(noclipEntity);

                SetEntityVelocity(noclipEntity, 0f, 0f, 0f);
                SetEntityRotation(noclipEntity, 0f, 0f, 0f, 0, false);
                SetEntityHeading(noclipEntity, FollowCamMode ? GetGameplayCamRelativeHeading() : heading);
                SetEntityCollision(noclipEntity, false, false);
                SetEntityCoordsNoOffset(noclipEntity, newPos.X, newPos.Y, newPos.Z, true, true, true);

                SetEntityVisible(noclipEntity, false, false);
                SetLocalPlayerVisibleLocally(true);
                SetEntityAlpha(noclipEntity, (int)(255 * 0.2), 0);

                SetEveryoneIgnorePlayer(Game.PlayerPed.Handle, true);
                SetPoliceIgnorePlayer(Game.PlayerPed.Handle, true);

                // After the next game tick, reset the entity properties.
                await Delay(0);
                FreezeEntityPosition(noclipEntity, false);
                SetEntityInvincible(noclipEntity, false);
                SetEntityCollision(noclipEntity, true, true);

                // If the player is not set as invisible by PlayerOptions or if the noclip entity is not the player ped, reset the visibility
                if (MainMenu.PlayerOptionsMenu == null || (!MainMenu.PlayerOptionsMenu.PlayerInvisible || (MainMenu.PlayerOptionsMenu.PlayerInvisible && noclipEntity == Game.PlayerPed.Handle)))
                {
                    SetEntityVisible(noclipEntity, true, false);
                    SetLocalPlayerVisibleLocally(true);
                }

                // Always reset the alpha.
                ResetEntityAlpha(noclipEntity);

                SetEveryoneIgnorePlayer(Game.PlayerPed.Handle, false);
                SetPoliceIgnorePlayer(Game.PlayerPed.Handle, false);
            }

            await Task.FromResult(0);
        }
    }
}
