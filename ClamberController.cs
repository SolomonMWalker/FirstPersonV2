using Godot;

namespace FirstPerson.Helpers;

// Clamber/mantle, and the smaller step-up that fills the dead band under it, for a
// CharacterBody3D.
//
// Detection sweeps the player's OWN collider up, forward, then down — the Godot stair-step idiom
// with a large step height. Because the sweep uses the real shape, a sweep that completes proves
// the landing spot is reachable, clear and standable, so there is no separate headroom, room-fit
// or ledge-thickness check to get wrong. Clamber and step-up share this exact sweep, differing
// only in height range, forward reach and direction -- see TrySweep.
//
// Execution differs between the two, though. Clamber is parameterised by elapsed time, not by
// position, so the manoeuvre always terminates, and moves the player through the caller's
// MoveAndSlide as a velocity rather than a GlobalPosition lerp, so collision stays authoritative.
// Step-up has no animation to run: it is a single instant lift applied before that tick's
// MoveAndSlide, small enough that CameraController.StepSmooth absorbs the pop the same way it
// already does for a stair tread.
//
// Statechart-ready seam: TryStartClamber() is the transition guard, GetClamberVelocity() is
// StatePhysicsProcessing, !IsClambering is the transition out. TryStepUp() has no seam to offer --
// it never leaves Locomoting, which is the point of it.
public partial class ClamberController : Node3D
{
    [Export] public CharacterBody3D Player { get; set; }

    [ExportGroup("Detection")]
    [Export] public float MaxClamberHeight { get; set; } = 1.6f;
    // Below this it is a step, not a clamber -- TryStepUp owns everything under it instead.
    [Export] public float MinClamberHeight { get; set; } = 0.4f;
    [Export] public float ClamberReach { get; set; } = 0.75f;
    // Above ~0.01 the sweeps snag and report false positives.
    [Export] public float SafeMargin { get; set; } = 0.001f;

    [ExportGroup("Step-up")]
    // Floor for what counts as a step at all, not a tuned match for wherever Godot's own capsule
    // stops rolling over things unaided -- that point moves with speed and geometry, and TryStepUp
    // only ever runs on a tick the capsule is already blocked (see the gate in TryStepUp), so a
    // height MoveAndSlide would have climbed on its own just gets rejected here as "too low" and
    // costs three cheap sweeps for nothing. This is purely a floor against registering a texture
    // seam or a hairline CSG gap as a step.
    [Export] public float MinStepHeight { get; set; } = 0.03f;
    // Just past the capsule radius: this only has to reach the obstacle already touching the
    // player, never out to something a stride away.
    [Export] public float StepReach { get; set; } = 0.6f;

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
        if (!TrySweep(-Player.GlobalBasis.Z, ClamberReach, MinClamberHeight, MaxClamberHeight * HeightScale,
                out var landing)) return false;

        _start = Player.GlobalPosition;
        _landing = landing;
        _elapsed = 0f;
        _duration = Mathf.Max(Mathf.Clamp((landing.Y - _start.Y) / ClamberSpeed, MinDuration, MaxDuration), 0.01f);
        _maxSpeed = 4f * _start.DistanceTo(_landing) / _duration;
        IsClambering = true;
        return true;
    }

    // Called once per physics tick, before the caller's MoveAndSlide. Lifts the player straight
    // onto a step in one move if their own velocity is already blocked by one this tick; otherwise
    // does nothing and costs one cheap probe. Not gated by cooldown -- unlike clamber this has no
    // animation to protect from re-triggering, it just stops firing the instant nothing is ahead.
    public bool TryStepUp(double delta)
    {
        if (IsClambering || !Player.IsOnFloor()) return false;

        var horizontal = Player.Velocity with { Y = 0 };
        if (horizontal.LengthSquared() < 0.0001f) return false;
        var direction = horizontal.Normalized();

        // The gate the whole performance case rests on: a plain forward probe, no vertical offset,
        // costing one TestMove. Only when THIS is blocked do the three real sweeps below run, so
        // open-ground walking -- the overwhelming majority of ticks -- never pays for them.
        if (!Player.TestMove(Player.GlobalTransform, direction * 0.05f, null, SafeMargin)) return false;

        if (!TrySweep(direction, StepReach, MinStepHeight, MinClamberHeight, out var landing)) return false;

        // Only the lift is applied here -- horizontal motion is left to the MoveAndSlide the caller
        // is about to run with the velocity already sampled this tick. Applying the sweep's forward
        // offset too would double up with that and overshoot.
        Player.GlobalPosition = Player.GlobalPosition with { Y = landing.Y };
        return true;
    }

    // Shared by clamber and step-up. Sweeps the player's own capsule up, then along `direction` by
    // `reach`, then down, and reports where it lands if the whole path is clear and standable.
    // `minRise`/`maxRise` bound what counts as a valid landing -- clamber and step-up pass
    // complementary ranges that meet exactly at MinClamberHeight, so neither has a gap or overlap
    // with the other.
    private bool TrySweep(Vector3 direction, float reach, float minRise, float maxRise, out Vector3 landing)
    {
        landing = Vector3.Zero;
        var xform = Player.GlobalTransform;
        var hit = new KinematicCollision3D();

        // Up first. Sweeping forward before up tunnels through ceilings.
        // Rise past maxRise by Clearance so an obstacle of exactly that height still clears its own
        // lip on the forward sweep -- maxRise means "tallest obstacle", not "how far to rise".
        var rise = maxRise + Clearance;
        if (Player.TestMove(xform, Vector3.Up * rise, hit, SafeMargin))
            rise = hit.GetTravel().Length();
        if (rise < minRise) return Reject("no headroom to rise");
        xform.Origin += Vector3.Up * rise;

        // Forward. Blocked means the obstacle is taller than maxRise.
        var forward = direction * reach;
        if (Player.TestMove(xform, forward, null, SafeMargin)) return Reject("no room in front");
        xform.Origin += forward;

        // Down. Nothing to land on means a gap, not a ledge. Note a capsule *rests on* thin
        // geometry rather than passing over it, so railings and single steps stay steppable;
        // rejecting those needs an explicit minimum-depth check, not this sweep.
        if (!Player.TestMove(xform, Vector3.Down * (rise + 0.05f), hit, SafeMargin))
            return Reject("nothing to stand on");
        if (hit.GetNormal().AngleTo(Vector3.Up) > Player.FloorMaxAngle) return Reject("surface too steep");

        landing = xform.Origin + Vector3.Down * hit.GetTravel().Length();
        var actualRise = landing.Y - Player.GlobalPosition.Y;
        if (actualRise < minRise) return Reject("too low, that is a step");
        // Guards the boundary from the other side too: without this, a landing a hair over
        // MinClamberHeight could still get taken as a step because the up-sweep's own Clearance
        // margin let it rise that far unblocked.
        if (actualRise > maxRise + 0.001f) return Reject("too high");
        if (DebugLog)
            GD.Print($"[Clamber] accepted: from {Player.GlobalPosition} rise {rise:F3} " +
                     $"landing {landing}");
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
