using Godot;

namespace FirstPerson.PlayerStates;

public partial class CrouchingState : WalkingState
{
    protected override float SpeedMultiplier => 0.75f;

    // Sweeping up by the crouch drop covers the standing capsule's volume.
    // ponytail: one sweep per tick incl. _Process; cache per physics frame if it shows in a profile.
    private bool HasHeadroom =>
        !Player.TestMove(Player.GlobalTransform, Vector3.Up * Player.Camera.CrouchOffset);

    // A stand-up under a low ceiling is dropped, not queued.
    public override void StatePhysicsProcessing(double delta)
    {
        base.StatePhysicsProcessing(delta);
        if (!HasHeadroom) Player.CrouchToggled = true;
    }

    public override void StateEntered()
    {
        base.StateEntered();
        Player.Camera.Crouched = true;
    }

    public override void StateExited()
    {
        base.StateExited();
        Player.Camera.Crouched = false;
    }

    protected override void AddTransitions()
    {
        AddTransition("Sprinting", () => WantsSprint && HasHeadroom, () =>
        {
            Player.SprintArmed = false;
            Player.CrouchToggled = false;
        });
        AddTransition("Walking", () => !Player.CrouchToggled && HasHeadroom);
    }
}
