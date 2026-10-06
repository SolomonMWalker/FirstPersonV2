using Godot;

// Tumble comes from the offset collider, not applied spin.
[GlobalClass]
public partial class RevolverShell : RigidBody3D
{
    [Export] public float LifetimeSeconds { get; set; } = 3.0f;

    public override void _Ready()
    {
        base._Ready();
        GetTree().CreateTimer(LifetimeSeconds, processAlways: false).Timeout += QueueFree;
    }
}
