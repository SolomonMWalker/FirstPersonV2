using System.Collections.Generic;
using System.Linq;
using FirstPersonV3.Components;
using Godot;

namespace FirstPerson;

// Root of grunt_enemy.tscn. Owns the body's physics and the AI's tuning; behaviour lives in the StateMachine.
public partial class GruntEnemy : CharacterBody3D
{
    [ExportGroup("References")]
    [Export] public PhysicalBoneSimulator3D PhysicalBoneSimulator3D { get; set; }
    [Export] public HealthComponent HealthComponent { get; set; }
    [Export] public FlickerRedHitReactionComponent FlickerRedHitReactionComponent { get; set; }

    [ExportGroup("Physics")]
    // Air time before Falling; absorbs IsOnFloor flicker on stairs, slope crests and small drops.
    [Export] public float FallGraceTime { get; set; } = 0.15f;

    [ExportGroup("AI")]
    // Set by CombatStartZone when the player enters it; can also be assigned by hand.
    [Export] public Node3D Target { get; set; }
    [Export] public Area3D CombatStartZone { get; set; }
    [Export] public NavigationAgent3D NavAgent { get; set; }
    [Export] public float MoveSpeed { get; set; } = 3f;
    [Export(PropertyHint.None, "suffix:rad/s")] public float TurnSpeed { get; set; } = 8f;
    // Chase stops inside StopDistance and resumes beyond ResumeDistance; the gap stops it flickering at the edge.
    [Export] public float StopDistance { get; set; } = 3f;
    [Export] public float ResumeDistance { get; set; } = 4.5f;

    [ExportGroup("Animation")]
    // Horizontal speed above which Locomotion plays the walk animation.
    [Export] public float WalkAnimationSpeed { get; set; } = 0.1f;

    private List<PhysicalBone3DHitbox> _pBHitboxes = [];

    private float _airTime;

    public bool IsFalling => _airTime > FallGraceTime;

    // Latches on: once the player has entered the zone, the grunt stays in combat.
    public bool IsInCombat { get; private set; }

    public bool IsMoving => HorizontalVelocity.LengthSquared() > WalkAnimationSpeed * WalkAnimationSpeed;

    public bool IsDead { get; private set; } = false;

    public bool HasTarget => IsInstanceValid(Target);

    // Flat distance, so the target's height doesn't affect stopping.
    public float DistanceToTarget => HasTarget ? Flatten(Target.GlobalPosition - GlobalPosition).Length() : float.PositiveInfinity;

    public Vector3 HorizontalVelocity
    {
        get => Flatten(Velocity);
        set => Velocity = new Vector3(value.X, Velocity.Y, value.Z);
    }

    public override void _Ready()
    {
        // After the StateMachine, so velocity set by AI states this frame moves the body this frame.
        ProcessPhysicsPriority = 1;

        if (CombatStartZone is not null)
            CombatStartZone.BodyEntered += OnCombatStartZoneBodyEntered;

        _pBHitboxes = [.. PhysicalBoneSimulator3D.GetChildren().OfType<PhysicalBone3DHitbox>()];

        WireUpSignals();
    }

    private void WireUpSignals()
    {
        foreach (var hitbox in _pBHitboxes)
            hitbox.Hit += OnHitboxHit;
        HealthComponent.HealthAtZero += OnHealthAtZero;
    }

    private void OnHitboxHit(PhysicalBone3DHitbox source, int damageAmount, Vector3 damageSourceGlobalPosition) =>
        OnHit(damageAmount, damageSourceGlobalPosition);

    private void OnCombatStartZoneBodyEntered(Node3D body)
    {
        if (IsInCombat || !body.IsInGroup("player")) return;
        Target = body;
        IsInCombat = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsOnFloor())
            Velocity += GetGravity() * (float)delta;

        MoveAndSlide();

        _airTime = IsOnFloor() ? 0f : _airTime + (float)delta;
    }

    public void FaceTowards(Vector3 point, double delta) => FaceDirection(point - GlobalPosition, delta);

    // Yaw only; the model faces -Z (Godot forward). No-op while Y rotation is frozen.
    public void FaceDirection(Vector3 direction, double delta)
    {
        if (AxisLockAngularY) return;

        direction = Flatten(direction);
        if (direction.IsZeroApprox()) return;

        var yaw = Mathf.Atan2(-direction.X, -direction.Z);
        Rotation = Rotation with { Y = Mathf.RotateToward(Rotation.Y, yaw, TurnSpeed * (float)delta) };
    }

    private static Vector3 Flatten(Vector3 v) => new(v.X, 0f, v.Z);

    private void OnHit(int damageAmount, Vector3 damageSourceGlobalPosition)
    {
        if(damageAmount > 0)
        {
            HealthComponent.ReduceHealth(damageAmount);
            FlickerRedHitReactionComponent.FlickerRed();
        }
    }

    private void OnHealthAtZero()
    {
        IsDead = true;
    }
}