using Godot;

namespace FirstPerson.Helpers;

// Clamber/mantle for a CharacterBody3D.
//
// Detection sweeps the player's OWN collider up, forward, then down — the Godot stair-step idiom
// with a large step height. Because the sweep uses the real shape, a sweep that completes proves
// the landing spot is reachable, clear and standable, so there is no separate headroom, room-fit
// or ledge-thickness check to get wrong.
//
// Execution is parameterised by elapsed time, not by position, so the manoeuvre always terminates.
// Motion goes through the caller's MoveAndSlide as a velocity rather than a GlobalPosition lerp,
// so collision stays authoritative and the player can never end up inside geometry.
//
// Statechart-ready seam: TryStartClamber() is the transition guard, GetClamberVelocity() is
// StatePhysicsProcessing, !IsClambering is the transition out.
public partial class ClamberController : Node3D
{
    [Export] public CharacterBody3D Player { get; set; }

    [ExportGroup("Detection")]
    [Export] public float MaxClamberHeight { get; set; } = 1.6f;
    // Below this it is a step, not a clamber — leave short obstacles to stair-stepping.
    [Export] public float MinClamberHeight { get; set; } = 0.4f;
    [Export] public float ClamberReach { get; set; } = 0.75f;
    // Above ~0.01 the sweeps snag and report false positives.
    [Export] public float SafeMargin { get; set; } = 0.001f;

    [ExportGroup("Execution")]
    [Export] public float ClamberSpeed { get; set; } = 3.0f;
    [Export] public float MinDuration { get; set; } = 0.2f;
    [Export] public float MaxDuration { get; set; } = 0.9f;
    // How far over the lip the rise arcs before coming forward, so the capsule's rounded
    // bottom does not catch on the edge.
    [Export] public float Clearance { get; set; } = 0.1f;
    // Optional. Unset falls back to a lead-in/trail-out ease; see GetClamberVelocity.
    [Export] public Curve HeightCurve { get; set; }
    [Export] public Curve ForwardCurve { get; set; }
    // Blackout after a completed clamber only — never after a failed detection.
    [Export] public float CooldownSeconds { get; set; } = 0.25f;

    [Export] public bool DebugLog { get; set; }

    // Reach scales with body height: 1 at full height, 0.75 in a crouch three quarters as tall. Set
    // from the live capsule by whoever owns it (PlayerController), so a crouched player cannot pull
    // up onto a ledge that is only clamberable standing. MinClamberHeight deliberately does not
    // scale -- what counts as a step is a fact about the geometry, not about the body.
    public float HeightScale { get; set; } = 1f;

    public bool IsClambering { get; private set; }
    public Vector3 ClamberTarget => _landing;

    private Vector3 _start;
    private Vector3 _landing;
    private float _elapsed;
    private float _duration;
    private float _maxSpeed;
    private float _cooldown;

    public override void _Ready()
    {
        // Default to the body we hang off, so the usual setup needs no inspector wiring.
        Player ??= GetParent() as CharacterBody3D;
        if (Player == null)
            GD.PushError($"{Name}: Player is unset and the parent is not a CharacterBody3D.");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_cooldown > 0f) _cooldown -= (float)delta;
    }

    public bool TryStartClamber()
    {
        if (IsClambering || _cooldown > 0f) return false;
        if (!TryFindLanding(out var landing)) return false;

        _start = Player.GlobalPosition;
        _landing = landing;
        _elapsed = 0f;
        _duration = Mathf.Max(Mathf.Clamp((landing.Y - _start.Y) / ClamberSpeed, MinDuration, MaxDuration), 0.01f);
        _maxSpeed = 4f * _start.DistanceTo(_landing) / _duration;
        IsClambering = true;
        return true;
    }

    private bool TryFindLanding(out Vector3 landing)
    {
        landing = Vector3.Zero;
        var xform = Player.GlobalTransform;
        var hit = new KinematicCollision3D();

        // Up first. Sweeping forward before up tunnels through ceilings.
        // Rise past MaxClamberHeight by Clearance so a ledge of exactly that height still clears
        // its own lip on the forward sweep — the export means "tallest ledge", not "rise".
        var rise = MaxClamberHeight * HeightScale + Clearance;
        if (Player.TestMove(xform, Vector3.Up * rise, hit, SafeMargin))
            rise = hit.GetTravel().Length();
        if (rise < MinClamberHeight) return Reject("no headroom to rise");
        xform.Origin += Vector3.Up * rise;

        // Forward. Blocked means the wall is taller than MaxClamberHeight.
        var forward = -Player.GlobalBasis.Z * ClamberReach;
        if (Player.TestMove(xform, forward, null, SafeMargin)) return Reject("no room in front");
        xform.Origin += forward;

        // Down. Nothing to land on means a gap, not a ledge. Note a capsule *rests on* thin
        // geometry rather than passing over it, so railings stay clamberable — rejecting those
        // needs an explicit minimum-depth check, not this sweep.
        if (!Player.TestMove(xform, Vector3.Down * (rise + 0.05f), hit, SafeMargin))
            return Reject("nothing to stand on");
        if (hit.GetNormal().AngleTo(Vector3.Up) > Player.FloorMaxAngle) return Reject("surface too steep");

        landing = xform.Origin + Vector3.Down * hit.GetTravel().Length();
        if (landing.Y - Player.GlobalPosition.Y < MinClamberHeight) return Reject("too low, that is a step");
        if (DebugLog)
            GD.Print($"[Clamber] accepted: from {Player.GlobalPosition} rise {rise:F3} " +
                     $"downTravel {hit.GetTravel().Length():F3} landing {landing}");
        return true;
    }

    // Velocity the caller should assign before its own MoveAndSlide.
    public Vector3 GetClamberVelocity(double delta)
    {
        _elapsed += (float)delta;
        var t = Mathf.Min(_elapsed / _duration, 1f);

        // Rise first, forward second, no overlap. Starting flush against the ledge the capsule
        // only clears the lip at the very top of the rise, so any overlap drives it into the
        // face. Clearance arcs slightly over the lip and settles back down as we come forward.
        var h = HeightCurve?.Sample(t) ?? Mathf.SmoothStep(0f, 0.5f, t);
        var f = ForwardCurve?.Sample(t) ?? Mathf.SmoothStep(0.5f, 1f, t);
        // Clearance comes back off faster than the forward motion runs, finishing at t=0.85 rather
        // than trailing to the very end. Spent against the forward travel it reads as part of the
        // arc; spent over the whole phase it reads as a drop at the end of an otherwise finished
        // manoeuvre. Kept independent of ForwardCurve -- this is about unwinding the lip margin,
        // not about pacing, so a custom curve should not stretch it back out.
        //
        // 0.85 is the floor, not a taste value: it puts the feet back at ledge height ~78% through
        // the forward travel, which for the default ClamberReach is just past the capsule radius,
        // so the rounded bottom is clear of the lip corner when it touches down. Settling earlier
        // brings it down while still over the lip and it scrapes.
        var settle = Mathf.SmoothStep(0.5f, 0.85f, t);

        var target = new Vector3(
            Mathf.Lerp(_start.X, _landing.X, f),
            Mathf.Lerp(_start.Y, _landing.Y + Clearance, h) - Clearance * settle,
            Mathf.Lerp(_start.Z, _landing.Z, f));

        if (t >= 1f)
        {
            IsClambering = false;
            _cooldown = CooldownSeconds;
        }
        // Catch-up velocity so a transient scrape doesn't leave us behind schedule, but clamped:
        // a sustained block otherwise accumulates an impulse big enough to tunnel into geometry.
        return ((target - Player.GlobalPosition) / (float)delta).LimitLength(_maxSpeed);
    }

    private bool Reject(string why)
    {
        if (DebugLog) GD.Print($"[Clamber] rejected: {why}");
        return false;
    }
}
