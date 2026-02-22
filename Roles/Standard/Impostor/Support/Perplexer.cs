using AmongUs.GameOptions;
using EHR.Modules;
using System.Linq;

namespace EHR.Roles;

public class Perplexer : RoleBase
{
    public static bool On;
    private const int Id = 698000;

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;
    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    private byte MarkedId;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10f, new FloatValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 0.05f), OptionFormat.None)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        MarkedId = byte.MaxValue;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
        opt.SetVision(true);
    }
    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        bool animated = !Options.DisableShapeshiftAnimations.GetBool();

        if (MarkedId == byte.MaxValue && target.IsAlive())
        {
            MarkedId = target.PlayerId;
            Main.AllPlayerSpeed[target.PlayerId] *= -1;
            Utils.MarkEveryoneDirtySettings();
            LateTask.New(() => shapeshifter.RpcShapeshift(shapeshifter, animated), animated ? 1.2f : 0.5f);
            LateTask.New(() =>
            {
                if (target != null && target.IsAlive())
                {
                    Main.AllPlayerSpeed[target.PlayerId] *= -1;
                    MarkedId = byte.MaxValue;
                    Utils.MarkEveryoneDirtySettings();
                }
            }, AbilityDuration.GetFloat(), "Perplexer Revert Control Invert");
        }
        return false;
    }
}