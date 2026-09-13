using System.Collections.Generic;
using Godot;

// Composes the grunt's one animation path out of the four facets its statechart regions latch.
//
// Awareness, Locomotion and Action are orthogonal regions and Falling/Staggered override all
// three, so the path is a function of every region at once and no single state can own the
// Travel call. Each region writes only its own facet; this recomposes from all of them.
public partial class GruntAnimator : AnimationTreeDriver
{
    // The clip names are not a clean concatenation -- "idleWithGunDown" but "walkGunDown" -- so
    // the irregularity lives in one table instead of being spread across four states.
    private static readonly Dictionary<(string Posture, string Motion), string> Clips = new()
    {
        [("NotInCombat", "idle")] = "idleWithGunDown",
        [("NotInCombat", "walk")] = "walkGunDown",
        [("InCombat", "idle")] = "idleWithGunReady",
        [("InCombat", "walk")] = "walkGunReady",
    };

    [ExportGroup("Test drivers")]
    // Scaffolding so the binding can be exercised before perception, movement and damage exist.
    // Delete these, and the guards that read them, once real behaviour writes the facets.
    [Export] public bool TestInCombat { get; set; }
    [Export] public bool TestWalking { get; set; }
    [Export] public bool TestFire { get; set; }
    [Export] public bool TestFalling { get; set; }
    [Export] public bool TestStagger { get; set; }

    private string _posture = "NotInCombat";
    private string _motion = "idle";
    private string _action; //attack or aim
    private string _override; //stagger and falling override, or interrupt
    private bool _dirty = true;

    // Write-only on purpose. States push their facet in and never read one back: a state that
    // read another region's facet would be reaching across an orthogonal boundary, which is the
    // coupling this whole arrangement exists to avoid. Compose() is the only reader.
    public string Posture { set => Latch(ref _posture, value); }
    public string Motion { set => Latch(ref _motion, value); }
    public string Action { set => Latch(ref _action, value); }
    public string Override { set => Latch(ref _override, value); }

    // "Latch" as in a latching switch: the value stays put until somebody sets a different one.
    // A state writes its facet once in StateEntered and it holds for as long as that state is
    // active -- nothing has to keep re-asserting it each frame.
    //
    // `ref` is how one method serves all four facets: the caller hands over the backing field
    // itself, so `Latch(ref _posture, "InCombat")` assigns straight into _posture. Without it
    // this would be four near-identical setters.
    //
    // Marking dirty rather than travelling here is the important half. Entering a state does not
    // move the animation -- it only records what the animation SHOULD be. _PhysicsProcess reads
    // all four facets afterwards and issues at most one Travel per frame, which is what keeps a
    // transition that changes two facets at once (dropping out of combat while walking, say)
    // from briefly travelling somewhere neither facet asked for.
    //
    // The early return matters too: re-latching the value already held leaves _dirty alone, so a
    // state re-entered on a frame where nothing actually changed costs nothing.
    private void Latch(ref string facet, string value)
    {
        if (facet == value) return;
        facet = value;
        _dirty = true;
    }

    public override void _Ready()
    {
        base._Ready();
        // Tick after the StateMachine so a facet written this frame travels this frame, whatever
        // order the two nodes sit in.
        ProcessPhysicsPriority = 1;
    }

    // Deferred rather than travelling from the setters: several facets change during a single
    // transition drain, and the intermediate paths would make Travel mis-pick its
    // Start-vs-Travel branch when the branch appears to change and then change back.
    public override void _PhysicsProcess(double delta)
    {
        if (!_dirty) return;
        _dirty = false;
        Travel(Compose());
    }

    // The four facets collapse into one "Branch/Node" path, highest precedence first:
    // an override wins outright, then an action, then plain posture + motion.
    // Falling -> "falling"; (InCombat, walk, aimGun) -> "InCombat/aimGun";
    // (NotInCombat, walk, none) -> "NotInCombat/walkGunDown".
    private string Compose()
    {
        if (_override is not null) return _override;
        // aimGun and fireGun exist only inside the InCombat branch, so an action surviving a
        // drop out of combat would otherwise compose a path that is not in the tree.
        if (_action is not null && _posture == "InCombat") return $"{_posture}/{_action}";
        return $"{_posture}/{Clips[(_posture, _motion)]}";
    }
}
