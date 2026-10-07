using System.Collections.Generic;
using Godot;

public enum GruntCombatState { NotInCombat, InCombat }
public enum GruntMotion { Idle, Walk }
public enum GruntAction { None, Firing }
public enum GruntOverride { None, Falling, Stagger, Dead }

// Each statechart region sets one AnimParam; this composes them into a single animation path.
public partial class GruntAnimator : AnimationTreeDriver
{
    // Enum -> AnimationTree node name, so either can be renamed independently.
    private static readonly Dictionary<GruntCombatState, string> CombatStateBranches = new()
    {
        [GruntCombatState.NotInCombat] = "NotInCombat",
        [GruntCombatState.InCombat] = "InCombat",
    };

    private static readonly Dictionary<(GruntCombatState, GruntMotion), string> Clips = new()
    {
        [(GruntCombatState.NotInCombat, GruntMotion.Idle)] = "idleWithGunDown",
        [(GruntCombatState.NotInCombat, GruntMotion.Walk)] = "walkGunDown",
        [(GruntCombatState.InCombat, GruntMotion.Idle)] = "idleWithGunReady",
        [(GruntCombatState.InCombat, GruntMotion.Walk)] = "walkGunReady",
    };

    // Firing enters at aimGun; the tree auto-advances to fireGun.
    private static readonly Dictionary<GruntAction, string> Actions = new()
    {
        [GruntAction.Firing] = "aimGun",
    };

    // TODO: Dead has no tree node yet.
    private static readonly Dictionary<GruntOverride, string> Overrides = new()
    {
        [GruntOverride.Falling] = "falling",
        [GruntOverride.Stagger] = "stagger",
    };

    [ExportGroup("Test drivers")]
    // Temporary until perception, movement and damage exist.
    [Export] public bool TestFire { get; set; }
    [Export] public bool TestStagger { get; set; }

    // AnimParam values
    private GruntCombatState _combatState = GruntCombatState.NotInCombat;
    private GruntMotion _motion = GruntMotion.Idle;
    private GruntAction _action = GruntAction.None;
    private GruntOverride _override = GruntOverride.None;

    // Set when an AnimParam field changes, so the next physics frame travels to the new animation.
    private bool _animParamChangeSinceLastFrame = true;

    // Write-only so regions can't read each other's AnimParams.
    public GruntCombatState CombatState { set => AnimParamSet(ref _combatState, value); }
    public GruntMotion Motion { set => AnimParamSet(ref _motion, value); }
    public GruntAction Action { set => AnimParamSet(ref _action, value); }
    public GruntOverride Override { set => AnimParamSet(ref _override, value); }

    private void AnimParamSet<T>(ref T animParam, T value)
    {
        if (EqualityComparer<T>.Default.Equals(animParam, value)) return;
        animParam = value;
        _animParamChangeSinceLastFrame = true;
    }

    public override void _Ready()
    {
        base._Ready();
        // After the StateMachine, so AnimParams set this frame travel this frame.
        ProcessPhysicsPriority = 1;
    }

    // One Travel per frame; several AnimParams can change in a single transition.
    public override void _PhysicsProcess(double delta)
    {
        if (!_animParamChangeSinceLastFrame) return;
        _animParamChangeSinceLastFrame = false;
        Travel(ComposeAnimationPath());
    }

    // Precedence: override, then action, then combat state + motion.
    private string ComposeAnimationPath()
    {
        if (_override != GruntOverride.None) return Overrides[_override];
        var branch = CombatStateBranches[_combatState];
        // Actions only exist in the InCombat branch.
        if (_action != GruntAction.None && _combatState == GruntCombatState.InCombat)
            return $"{branch}/{Actions[_action]}";
        return $"{branch}/{Clips[(_combatState, _motion)]}";
    }
}
