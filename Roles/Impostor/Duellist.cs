﻿using System.Collections.Generic;
using AmongUs.GameOptions;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public class Duellist : RoleBase
    {
        private const int Id = 642850;
        private static List<byte> playerIdList = [];
        private static Dictionary<byte, byte> DuelPair = [];
        private static OptionItem SSCD;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Duellist);
            SSCD = FloatOptionItem.Create(Id + 5, "ShapeshiftCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Duellist])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            DuelPair = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public override bool IsEnable => playerIdList.Count > 0 || Randomizer.Exists;

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
        }

        public override bool OnShapeshift(PlayerControl duellist, PlayerControl target, bool shapeshifting)
        {
            if (!IsEnable) return false;
            if (duellist == null || target == null) return false;
            if (target.inMovingPlat || target.onLadder || target.MyPhysics.Animations.IsPlayingEnterVentAnimation() || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || !target.IsAlive())
            {
                duellist.Notify(GetString("TargetCannotBeTeleported"));
                return false;
            }

            var pos = Pelican.GetBlackRoomPS();
            duellist.TP(pos);
            target.TP(pos);
            DuelPair[duellist.PlayerId] = target.PlayerId;

            return false;
        }

        public static void OnFixedUpdate()
        {
            if (DuelPair.Count == 0) return;

            foreach (var pair in DuelPair)
            {
                var duellist = GetPlayerById(pair.Key);
                var target = GetPlayerById(pair.Value);
                var DAlive = duellist.IsAlive();
                var TAlive = target.IsAlive();

                switch (DAlive)
                {
                    case false when !TAlive:
                        DuelPair.Remove(pair.Key);
                        break;
                    case true when !TAlive:
                        DuelPair.Remove(pair.Key);
                        _ = new LateTask(() => { duellist.TPtoRndVent(); }, 0.5f, log: false);
                        break;
                    case false:
                        DuelPair.Remove(pair.Key);
                        _ = new LateTask(() => { target.TPtoRndVent(); }, 0.5f, log: false);
                        break;
                }
            }
        }
    }
}