using System.Collections.Generic;
using Godot;

// Each statechart region sets one facet; this composes them into a single animation path.
public partial class GruntAnimator : AnimationTreeDriver
{
    private static readonly Dictionary<(string Posture, string Motion), string> Clips = new()
    {
        [("NotInCombat", "idle")] = "idleWithGunDown",
        [("NotInCombat", "walk")] = "walkGunDown",
        [("InCombat", "idle")] = "idleWithGunReady",
        [("InCombat", "walk")] = "walkGunReady",
    };

    [ExportGroup("Test drivers")]
    // Temporary until perception, movement and damage exist.
    [Export] public bool TestInCombat { get; set; }
    [Export] public bool TestWalking { get; set; }
    [Export] public bool TestFire { get; set; }
    [Export] public bool TestFalling { get; set; }
    [Export] public bool TestStagger { get; set; }

    private string _posture = "NotInCombat";
    private string _motion = "idle";
    private string _action;
    private string _override;
    private bool _dirty = true;

    // Write-only so regions can't read each other's facets.
    public string Posture { set => Latch(ref _posture, value); }
    public string Motion { set => Latch(ref _motion, value); }
    public string Action { set => Latch(ref _action, value); }
    public string Override { set => Latch(ref _override, value); }

    private void Latch(ref string facet, string value)
    {
        if (facet == value) return;
        facet = value;
        _dirty = true;
    }

    public override void _Ready()
    {
        base._Ready();
        // After the StateMachine, so facets set this frame travel this frame.
        ProcessPhysicsPriority = 1;
    }

    // One Travel per frame; several facets can change in a single transition.
    public override void _PhysicsProcess(double delta)
    {
        if (!_dirty) return;
        _dirty = false;
        Travel(Compose());
    }

    // Precedence: override, then action, then posture + motion.
    private string Compose()
    {
        if (_override is not null) return _override;
        // Actions only exist in the InCombat branch.
        if (_action is not null && _posture == "InCombat") return $"{_posture}/{_action}";
        return $"{_posture}/{Clips[(_posture, _motion)]}";
    }
}
