using Godot;

namespace FirstPerson;

// Clamber (timed mantle) and step-up (instant lift). Both sweep the player's own collider up,
// forward, then down, so a completed sweep proves the landing is reachable and standable.
public partial class ClamberController : Node3D
{
    // Defaults to the parent.
    [Export] public CharacterBody3D Player { get; set; }

    [ExportGroup("Detection")]
    [Export] public float MaxClamberHeight { get; set; } = 1.6f;
    // Boundary between step-up and clamber.
    [Export] public float MinClamberHeight { get; set; } = 0.4f;
    [Export] public float ClamberReach { get; set; } = 0.75f;
    // Sweep skin width. Above ~0.01 sweeps snag and give false positives.
    [Export] public float SafeMargin { get; set; } = 0.001f;
    // 0 = any surface.
    [Export(PropertyHint.Layers3DPhysics)] public uint ClamberableLayers { get; set; }

    [ExportGroup("Step-up")]
    // Ignores seams and hairline gaps.
    [Export] public float MinStepHeight { get; set; } = 0.03f;
    // Just past the capsule radius.
    [Export] public float StepReach { get; set; } = 0.6f;

    [ExportGroup("Execution")]
    [Export] public float ClamberSpeed { get; set; } = 3.0f;  // metres/second
    // Seconds. MaxDuration also ends a stalled mantle.
    [Export] public float MinDuration { get; set; } = 0.2f;
    [Export] public float MaxDuration { get; set; } = 0.9f;
    // Extra rise over the lip so the capsule's rounded bottom doesn't catch.
    [Export] public float Clearance { get; set; } = 0.1f;
    // Optional, sampled 0..1 over the mantle.
    [Export] public Curve HeightCurve { get; set; }
    [Export] public Curve ForwardCurve { get; set; }
    // Applies after a completed clamber only, not a failed detection.
    [Export] public float CooldownSeconds { get; set; } = 0.25f;

    [Export] public bool DebugLog { get; set; }

    // Current capsule height / standing height; shrinks max clamber height while crouched.
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
                ClamberableLayers, out var landing)) return false;

        _start = Player.GlobalPosition;
        _landing = landing;
        _elapsed = 0f;
        _duration = Mathf.Max(Mathf.Clamp((landing.Y - _start.Y) / ClamberSpeed, MinDuration, MaxDuration), 0.01f);
        _maxSpeed = 4f * _start.DistanceTo(_landing) / _duration;
        IsClambering = true;
        return true;
    }

    // Call before MoveAndSlide. Lifts the player onto a step blocking this tick's velocity.
    public bool TryStepUp(double delta)
    {
        if (IsClambering || !Player.IsOnFloor()) return false;

        var horizontal = Player.Velocity with { Y = 0 };
        if (horizontal.LengthSquared() < 0.0001f) return false;
        var direction = horizontal.Normalized();

        // Cheap probe so open-ground walking skips the full sweep.
        if (!Player.TestMove(Player.GlobalTransform, direction * 0.05f, null, SafeMargin)) return false;

        if (!TrySweep(direction, StepReach, MinStepHeight, MinClamberHeight, 0, out var landing))
            return false;

        // Lift only; the caller's MoveAndSlide supplies the forward motion.
        Player.GlobalPosition = Player.GlobalPosition with { Y = landing.Y };
        return true;
    }

    private bool TrySweep(Vector3 direction, float reach, float minRise, float maxRise, uint requireLayers,
        out Vector3 landing)
    {
        landing = Vector3.Zero;
        var sweepTransform = Player.GlobalTransform;
        var hit = new KinematicCollision3D();

        // Up before forward, or the sweep tunnels through ceilings.
        var rise = maxRise + Clearance;
        if (Player.TestMove(sweepTransform, Vector3.Up * rise, hit, SafeMargin))
            rise = hit.GetTravel().Length();
        if (rise < minRise) return Reject("no headroom to rise");
        sweepTransform.Origin += Vector3.Up * rise;

        var forward = direction * reach;
        if (Player.TestMove(sweepTransform, forward, null, SafeMargin)) return Reject("no room in front");
        sweepTransform.Origin += forward;

        if (!Player.TestMove(sweepTransform, Vector3.Down * (rise + 0.05f), hit, SafeMargin))
            return Reject("nothing to stand on");
        var ledge = hit.GetCollider() as CollisionObject3D;
        if (requireLayers != 0 && (ledge is null || (ledge.CollisionLayer & requireLayers) == 0))
            return Reject("ledge not on a clamberable layer");
        if (hit.GetNormal().AngleTo(Vector3.Up) > Player.FloorMaxAngle) return Reject("surface too steep");

        landing = sweepTransform.Origin + Vector3.Down * hit.GetTravel().Length();
        var actualRise = landing.Y - Player.GlobalPosition.Y;
        if (actualRise < minRise) return Reject("too low, that is a step");
        if (actualRise > maxRise + 0.001f) return Reject("too high");
        if (DebugLog)
            GD.Print($"[Clamber] accepted: from {Player.GlobalPosition} rise {rise:F3} " +
                     $"landing {landing}");
        return true;
    }

    // Assign to Velocity before MoveAndSlide.
    public Vector3 GetClamberVelocity(double delta)
    {
        _elapsed += (float)delta;
        var t = Mathf.Min(_elapsed / _duration, 1f);

        // Rise fully before moving forward, or the capsule drives into the ledge face.
        var heightProgress = HeightCurve?.Sample(t) ?? Mathf.SmoothStep(0f, 0.5f, t);
        var forwardProgress = ForwardCurve?.Sample(t) ?? Mathf.SmoothStep(0.5f, 1f, t);
        // Settling before 0.85 brings the capsule down over the lip and it scrapes.
        var clearanceSettle = Mathf.SmoothStep(0.5f, 0.85f, t);

        var target = new Vector3(
            Mathf.Lerp(_start.X, _landing.X, forwardProgress),
            Mathf.Lerp(_start.Y, _landing.Y + Clearance, heightProgress) - Clearance * clearanceSettle,
            Mathf.Lerp(_start.Z, _landing.Z, forwardProgress));

        if (t >= 1f)
        {
            IsClambering = false;
            _cooldown = CooldownSeconds;
        }
        // Clamped so a sustained block can't build up enough speed to tunnel.
        return ((target - Player.GlobalPosition) / (float)delta).LimitLength(_maxSpeed);
    }

    private bool Reject(string why)
    {
        if (DebugLog) GD.Print($"[Clamber] rejected: {why}");
        return false;
    }
}
