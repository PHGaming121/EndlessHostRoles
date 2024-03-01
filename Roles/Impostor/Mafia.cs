﻿using System;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Translator;
using Object = UnityEngine.Object;

namespace TOHE.Roles.Impostor
{
    internal class Mafia : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return Utils.CanMafiaKill();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = Options.MafiaShapeshiftCD.GetFloat();
            AURoleOptions.ShapeshifterDuration = Options.MafiaShapeshiftDur.GetFloat();
        }

        public static bool MafiaMsgCheck(PlayerControl pc, string msg, bool isUI = false)
        {
            if (!AmongUsClient.Instance.AmHost) return false;
            if (!GameStates.IsInGame || pc == null) return false;
            if (!pc.Is(CustomRoles.Mafia)) return false;
            msg = msg.Trim().ToLower();
            if (msg.Length < 3 || msg[..3] != "/rv") return false;
            if (Options.MafiaCanKillNum.GetInt() < 1)
            {
                if (!isUI) Utils.SendMessage(GetString("MafiaKillDisable"), pc.PlayerId);
                else pc.ShowPopUp(GetString("MafiaKillDisable"));
                return true;
            }

            if (!pc.Data.IsDead)
            {
                Utils.SendMessage(GetString("MafiaAliveKill"), pc.PlayerId);
                return true;
            }

            if (msg == "/rv")
            {
                string text = GetString("PlayerIdList");
                text = Main.AllAlivePlayerControls.Aggregate(text, (current, npc) => current + ("\n" + npc.PlayerId + " → (" + npc.GetDisplayRoleName() + ") " + npc.GetRealName()));

                Utils.SendMessage(text, pc.PlayerId);
                return true;
            }

            if (!Main.MafiaRevenged.TryAdd(pc.PlayerId, 0))
            {
                if (Main.MafiaRevenged[pc.PlayerId] >= Options.MafiaCanKillNum.GetInt())
                {
                    if (!isUI) Utils.SendMessage(GetString("MafiaKillMax"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("MafiaKillMax"));
                    return true;
                }
            }

            PlayerControl target;
            try
            {
                int targetId = int.Parse(msg.Replace("/rv", string.Empty));
                target = Utils.GetPlayerById(targetId);
            }
            catch
            {
                if (!isUI) Utils.SendMessage(GetString("MafiaKillDead"), pc.PlayerId);
                else pc.ShowPopUp(GetString("MafiaKillDead"));
                return true;
            }

            if (target == null || target.Data.IsDead)
            {
                if (!isUI) Utils.SendMessage(GetString("MafiaKillDead"), pc.PlayerId);
                else pc.ShowPopUp(GetString("MafiaKillDead"));
                return true;
            }

            if (target.Is(CustomRoles.Pestilence))
            {
                if (!isUI) Utils.SendMessage(GetString("PestilenceImmune"), pc.PlayerId);
                else pc.ShowPopUp(GetString("PestilenceImmune"));
                return true;
            }

            Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} 复仇了 {target.GetNameWithRole().RemoveHtmlTags()}", "Mafia");

            string Name = target.GetRealName();

            Main.MafiaRevenged[pc.PlayerId]++;

            CustomSoundsManager.RPCPlayCustomSoundAll("AWP");

            _ = new LateTask(() =>
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
                target.SetRealKiller(pc);

                if (GameStates.IsMeeting)
                {
                    target.RpcGuesserMurderPlayer();

                    //死者检查
                    Utils.AfterPlayerDeathTasks(target, true);

                    Utils.NotifyRoles(isForMeeting: GameStates.IsMeeting, NoCache: true);
                }
                else
                {
                    target.Kill(target);
                    Main.PlayerStates[target.PlayerId].SetDead();
                }

                _ = new LateTask(() => { Utils.SendMessage(string.Format(GetString("MafiaKillSucceed"), Name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mafia), GetString("MafiaRevengeTitle"))); }, 0.6f, "Mafia Kill");
            }, 0.2f, "Mafia Kill");
            return true;
        }

        private static void SendRPC(byte playerId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.MafiaRevenge, SendOption.Reliable);
            writer.Write(playerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
        {
            int PlayerId = reader.ReadByte();
            MafiaMsgCheck(pc, $"/rv {PlayerId}", true);
        }

        private static void MafiaOnClick(byte playerId /*, MeetingHud __instance*/)
        {
            Logger.Msg($"Click: ID {playerId}", "Mafia UI");
            var pc = Utils.GetPlayerById(playerId);
            if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;
            if (AmongUsClient.Instance.AmHost) MafiaMsgCheck(PlayerControl.LocalPlayer, $"/rv {playerId}", true);
            else SendRPC(playerId);
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        class StartMeetingPatch
        {
            public static void Postfix(MeetingHud __instance)
            {
                if (PlayerControl.LocalPlayer.Is(CustomRoles.Mafia) && !PlayerControl.LocalPlayer.IsAlive())
                    CreateJudgeButton(__instance);
            }
        }

        public static void CreateJudgeButton(MeetingHud __instance)
        {
            foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
            {
                var pc = Utils.GetPlayerById(pva.TargetPlayerId);
                if (pc == null || !pc.IsAlive()) continue;
                GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
                GameObject targetBox = Object.Instantiate(template, pva.transform);
                targetBox.name = "ShootButton";
                targetBox.transform.localPosition = new(-0.95f, 0.03f, -1.31f);
                SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
                renderer.sprite = CustomButton.Get("TargetIcon");
                PassiveButton button = targetBox.GetComponent<PassiveButton>();
                button.OnClick.RemoveAllListeners();
                button.OnClick.AddListener((Action)(() => MafiaOnClick(pva.TargetPlayerId /*, __instance*/)));
            }
        }
    }
}
