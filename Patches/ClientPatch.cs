using System;
using System.Linq;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
    internal static class MakePublicPatch
    {
        public static bool Prefix()
        {
            if (ModUpdater.IsBroken || (ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || !VersionChecker.IsSupported)
            {
                var message = string.Empty;
                if (!VersionChecker.IsSupported) message = GetString("UnsupportedVersion");

                if (ModUpdater.IsBroken) message = GetString("ModBrokenMessage");

                if (ModUpdater.HasUpdate) message = GetString("CanNotJoinPublicRoomNoLatest");

                Logger.Info(message, "MakePublicPatch");
                Logger.SendInGame(message);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(MMOnlineManager), nameof(MMOnlineManager.Start))]
// ReSharper disable once InconsistentNaming
    internal static class MMOnlineManagerStartPatch
    {
        public static void Postfix()
        {
            if (!((ModUpdater.HasUpdate && ModUpdater.ForceUpdate) || ModUpdater.IsBroken)) return;

            GameObject obj = GameObject.Find("FindGameButton");

            if (obj)
            {
                obj.SetActive(false);
                TextMeshPro textObj = Object.Instantiate(obj.transform.FindChild("Text_TMP").GetComponent<TextMeshPro>());
                textObj.transform.position = new(1f, -0.3f, 0);
                textObj.name = "CanNotJoinPublic";

                string message = ModUpdater.IsBroken
                    ? $"<size=2>{Utils.ColorString(Color.red, GetString("ModBrokenMessage"))}</size>"
                    : $"<size=2>{Utils.ColorString(Color.red, GetString("CanNotJoinPublicRoomNoLatest"))}</size>";

                LateTask.New(() => { textObj.text = message; }, 0.01f, "CanNotJoinPublic");
            }
        }
    }

    [HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
    internal static class SplashLogoAnimatorPatch
    {
        public static void Prefix(SplashManager __instance)
        {
            __instance.sceneChanger.AllowFinishLoadingScene();
            __instance.startedSceneLoad = true;
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
    internal static class RunLoginPatch
    {
        public const int ClickCount = 0;

        public static void Prefix(ref bool canOnline)
        {
            if (DebugModeManager.AmDebugger) canOnline = true;
        }
    }

    [HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
    internal static class BanMenuSetVisiblePatch
    {
        public static bool Prefix(BanMenu __instance, bool show)
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            show &= PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data != null;
            __instance.BanButton.gameObject.SetActive(AmongUsClient.Instance.CanBan());
            __instance.KickButton.gameObject.SetActive(AmongUsClient.Instance.CanKick());
            __instance.MenuButton.gameObject.SetActive(show);
            return false;
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CanBan))]
    internal static class InnerNetClientCanBanPatch
    {
        public static bool Prefix(InnerNetClient __instance, ref bool __result)
        {
            __result = __instance.AmHost;
            return false;
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
    internal static class KickPlayerPatch
    {
        public static bool Prefix( /*InnerNetClient __instance,*/ int clientId, bool ban)
        {
            if (!AmongUsClient.Instance.AmHost && !OnGameJoinedPatch.JoiningGame) return true;

            if (AmongUsClient.Instance.ClientId == clientId)
            {
                Logger.SendInGame($"Game Attempting to {(ban ? "Ban" : "Kick")} Host, Blocked the attempt.");
                Logger.Info("Game attempted to kick/ban host....", "KickPlayerPatch");
                return false;
            }

            if (ban) BanManager.AddBanPlayer(AmongUsClient.Instance.GetRecentClient(clientId));

            return true;
        }
    }

    [HarmonyPatch(typeof(ResolutionManager), nameof(ResolutionManager.SetResolution))]
    internal static class SetResolutionManager
    {
        public static void Postfix()
        {
            if (MainMenuManagerPatch.UpdateButton != null) MainMenuManagerPatch.UpdateButton.transform.localPosition = MainMenuManagerPatch.Template.transform.localPosition + new Vector3(0.25f, 0.75f);
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
    internal static class InnerNetObjectSerializePatch
    {
        public static void Prefix()
        {
            if (AmongUsClient.Instance.AmHost) Main.Instance.StartCoroutine(GameOptionsSender.SendAllGameOptionsAsync());
        }
    }

    internal static class InnerNetClientPatch
    {
        private static byte Timer;

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendInitialData))]
        [HarmonyPrefix]
        public static bool SendInitialDataPrefix(InnerNetClient __instance, int clientId)
        {
            if (!Constants.IsVersionModded() || GameStates.IsInGame || __instance.NetworkMode != NetworkModes.OnlineGame) return true;

            // We make sure other stuff like PlayerControl and LobbyBehavior is spawned properly
            // Then we spawn the networked data for new clients
            MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
            messageWriter.StartMessage(6);
            messageWriter.Write(__instance.GameId);
            messageWriter.WritePacked(clientId);
            List<InnerNetObject> obj = __instance.allObjects;

            lock (obj)
            {
                System.Collections.Generic.HashSet<GameObject> hashSet = [];

                for (var i = 0; i < __instance.allObjects.Count; i++)
                {
                    InnerNetObject innerNetObject = __instance.allObjects[i]; // False error

                    if (innerNetObject && (innerNetObject.OwnerId != -4 || __instance.AmModdedHost) && hashSet.Add(innerNetObject.gameObject))
                    {
                        var gameManager = innerNetObject as GameManager;

                        if (gameManager != null)
                            __instance.SendGameManager(clientId, gameManager);
                        else
                        {
                            if (innerNetObject is not NetworkedPlayerInfo) __instance.WriteSpawnMessage(innerNetObject, innerNetObject.OwnerId, innerNetObject.SpawnFlags, messageWriter);
                        }
                    }
                }

                messageWriter.EndMessage();
                __instance.SendOrDisconnect(messageWriter);
                messageWriter.Recycle();
            }

            DelaySpawnPlayerInfo(__instance, clientId);
            return false;
        }

        private static void DelaySpawnPlayerInfo(InnerNetClient __instance, int clientId)
        {
            Il2CppArrayBase<NetworkedPlayerInfo> players = GameData.Instance.AllPlayers.ToArray();

            // We send 5 players at a time to prevent too large packets
            foreach (NetworkedPlayerInfo[] batch in players.Chunk(5))
            {
                MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
                messageWriter.StartMessage(6);
                messageWriter.Write(__instance.GameId);
                messageWriter.WritePacked(clientId);

                batch.DoIf(p => p != null && p.ClientId != clientId && !p.Disconnected, p => __instance.WriteSpawnMessage(p, p.OwnerId, p.SpawnFlags, messageWriter));

                messageWriter.EndMessage();
                __instance.SendOrDisconnect(messageWriter);
                messageWriter.Recycle();
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
        [HarmonyPrefix]
        public static bool SendAllStreamedObjectsPrefix(InnerNetClient __instance, ref bool __result)
        {
            if (!Constants.IsVersionModded() || __instance.NetworkMode != NetworkModes.OnlineGame) return true;

            // Bypass all NetworkedData here.
            __result = false;
            List<InnerNetObject> obj = __instance.allObjects;

            lock (obj)
            {
                for (var i = 0; i < __instance.allObjects.Count; i++)
                {
                    InnerNetObject innerNetObject = __instance.allObjects[i]; // False error

                    if (innerNetObject && innerNetObject is not NetworkedPlayerInfo && innerNetObject.IsDirty && (innerNetObject.AmOwner || (innerNetObject.OwnerId == -2 && __instance.AmHost)))
                    {
                        MessageWriter messageWriter = __instance.Streams[(int)innerNetObject.sendMode];
                        messageWriter.StartMessage(1);
                        messageWriter.WritePacked(innerNetObject.NetId);

                        try
                        {
                            if (innerNetObject.Serialize(messageWriter, false))
                                messageWriter.EndMessage();
                            else
                                messageWriter.CancelMessage();

                            if (innerNetObject.Chunked && innerNetObject.IsDirty) __result = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.Exception(ex, "SendAllStreamedObjectsPrefix");
                            messageWriter.CancelMessage();
                        }
                    }
                }
            }

            for (var j = 0; j < __instance.Streams.Length; j++)
            {
                MessageWriter messageWriter2 = __instance.Streams[j];

                if (messageWriter2.HasBytes(7))
                {
                    messageWriter2.EndMessage();
                    __instance.SendOrDisconnect(messageWriter2);
                    messageWriter2.Clear((SendOption)j);
                    messageWriter2.StartMessage(5);
                    messageWriter2.Write(__instance.GameId);
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
        [HarmonyPostfix]
        public static void FixedUpdatePostfix(InnerNetClient __instance)
        {
            if (!Constants.IsVersionModded() || GameStates.IsInGame || !__instance.AmHost || __instance.Streams == null || __instance.NetworkMode != NetworkModes.OnlineGame) return;

            if (Timer == 0)
            {
                Timer = 1;
                return;
            }

            NetworkedPlayerInfo player = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(x => x.IsDirty);

            if (player != null)
            {
                Timer = 0;
                MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
                messageWriter.StartMessage(5);
                messageWriter.Write(__instance.GameId);
                messageWriter.StartMessage(1);
                messageWriter.WritePacked(player.NetId);

                try
                {
                    if (player.Serialize(messageWriter, false))
                        messageWriter.EndMessage();
                    else
                    {
                        messageWriter.CancelMessage();
                        player.ClearDirtyBits();
                        return;
                    }

                    messageWriter.EndMessage();
                    __instance.SendOrDisconnect(messageWriter);
                    messageWriter.Recycle();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "FixedUpdatePostfix");
                    messageWriter.CancelMessage();
                    player.ClearDirtyBits();
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class AuthTimeoutPatch
    {
        [HarmonyPatch(typeof(AuthManager._CoConnect_d__4), nameof(AuthManager._CoConnect_d__4.MoveNext))]
        [HarmonyPatch(typeof(AuthManager._CoWaitForNonce_d__6), nameof(AuthManager._CoWaitForNonce_d__6.MoveNext))]
        [HarmonyPrefix]
        // From Reactor.gg
        // https://github.com/NuclearPowered/Reactor/blob/master/Reactor/Patches/Miscellaneous/CustomServersPatch.cs
        public static bool CoWaitforNoncePrefix(ref bool __result)
        {
            if (GameStates.IsVanillaServer) return true;

            __result = false;
            return false;
        }

        // If you don't patch this, you still need to wait for 5 s.
        // I have no idea why this is happening
        [HarmonyPatch(typeof(AmongUsClient._CoJoinOnlinePublicGame_d__1), nameof(AmongUsClient._CoJoinOnlinePublicGame_d__1.MoveNext))]
        [HarmonyPrefix]
        public static void EnableUdpMatchmakingPrefix(AmongUsClient._CoJoinOnlinePublicGame_d__1 __instance)
        {
            // Skip to state 1, which just calls CoJoinOnlineGameDirect
            if (__instance.__1__state == 0 && !ServerManager.Instance.IsHttp)
            {
                __instance.__1__state = 1;

                __instance.__8__1 = new()
                {
                    matchmakerToken = string.Empty
                };
            }
        }
    }
}