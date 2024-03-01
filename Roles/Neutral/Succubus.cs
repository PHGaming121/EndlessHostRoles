﻿using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Succubus : RoleBase
{
    private const int Id = 11200;
    private static List<byte> playerIdList = [];

    public static OptionItem CharmCooldown;
    public static OptionItem CharmCooldownIncrese;
    public static OptionItem CharmMax;
    public static OptionItem KnowTargetRole;
    public static OptionItem TargetKnowOtherTarget;
    public static OptionItem CanCharmNeutral;
    public static OptionItem CharmedCountMode;
    public static OptionItem CharmedDiesOnSuccubusDeath;

    public static readonly string[] CharmedCountModeStrings =
    [
        "CharmedCountMode.None",
        "CharmedCountMode.Succubus",
        "CharmedCountMode.Original",
    ];

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Succubus, 1, zeroOne: false);
        CharmCooldown = FloatOptionItem.Create(Id + 10, "SuccubusCharmCooldown", new(0f, 60f, 2.5f), 30f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus])
            .SetValueFormat(OptionFormat.Seconds);
        CharmCooldownIncrese = FloatOptionItem.Create(Id + 11, "SuccubusCharmCooldownIncrese", new(0f, 180f, 2.5f), 10f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus])
            .SetValueFormat(OptionFormat.Seconds);
        CharmMax = IntegerOptionItem.Create(Id + 12, "SuccubusCharmMax", new(1, 15, 1), 15, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus])
            .SetValueFormat(OptionFormat.Times);
        KnowTargetRole = BooleanOptionItem.Create(Id + 13, "SuccubusKnowTargetRole", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus]);
        TargetKnowOtherTarget = BooleanOptionItem.Create(Id + 14, "SuccubusTargetKnowOtherTarget", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus]);
        CharmedCountMode = StringOptionItem.Create(Id + 15, "CharmedCountMode", CharmedCountModeStrings, 0, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus]);
        CanCharmNeutral = BooleanOptionItem.Create(Id + 16, "SuccubusCanCharmNeutral", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus]);
        CharmedDiesOnSuccubusDeath = BooleanOptionItem.Create(Id + 17, "CharmedDiesOnSuccubusDeath", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Succubus]);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(CharmMax.GetInt());

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = id.GetAbilityUseLimit() >= 1 ? CharmCooldown.GetFloat() + (CharmMax.GetInt() - id.GetAbilityUseLimit()) * CharmCooldownIncrese.GetFloat() : 300f;
    public override bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && player.GetAbilityUseLimit() >= 1;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return false;
        if (CanBeCharmed(target))
        {
            killer.RpcRemoveAbilityUse();

            target.RpcSetCustomRole(CustomRoles.Charmed);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), GetString("SuccubusCharmedPlayer")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), GetString("CharmedBySuccubus")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Charmed, "Assign " + CustomRoles.Charmed);
            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), GetString("SuccubusInvalidTarget")));

        return false;
    }

    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (player.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Succubus)) return true;
        if (KnowTargetRole.GetBool() && player.Is(CustomRoles.Succubus) && target.Is(CustomRoles.Charmed)) return true;
        return TargetKnowOtherTarget.GetBool() && player.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed);
    }

    public static string GetCharmLimit(byte id) => Utils.ColorString(id.GetAbilityUseLimit() >= 1 ? Utils.GetRoleColor(CustomRoles.Succubus).ShadeColor(0.25f) : Color.gray, $"({id.GetAbilityUseLimit()})");

    public static bool CanBeCharmed(PlayerControl pc)
    {
        return pc != null && (pc.GetCustomRole().IsCrewmate() || pc.GetCustomRole().IsImpostor() ||
                              (CanCharmNeutral.GetBool() && (pc.GetCustomRole().IsNeutral() || pc.GetCustomRole().IsNeutralKilling()))) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Loyal);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!CharmedDiesOnSuccubusDeath.GetBool() || !GameStates.IsInTask || !IsEnable) return;
        if (pc == null || pc.IsAlive()) return;

        foreach (var charmed in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Charmed)))
        {
            charmed.Suicide(realKiller: pc);
        }
    }
}