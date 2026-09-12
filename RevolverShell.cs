using Godot;

/// <summary>
/// An ejected revolver casing. Spawned by RevolverCylinderController during the reload and left
/// to Jolt; the tumble is emergent from the offset collider and the bounce material, so there is
/// no angular velocity applied anywhere.
/// </summary>
[GlobalClass]
public partial class RevolverShell : RigidBody3D
{
    [Export] public float LifetimeSeconds { get; set; } = 3.0f;

    public override void _Ready()
    {
        base._Ready();
        // processAlways: false so the despawn clock stops with the pause menu.
        GetTree().CreateTimer(LifetimeSeconds, processAlways: false).Timeout += QueueFree;
    }
}
