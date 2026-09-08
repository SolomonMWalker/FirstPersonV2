using Godot;

namespace FirstPerson.PlayerStates;

// Walking, slower. Left by sprinting (which clears the latch as the edge is taken) or by pressing
// C again, which clears it directly -- but only when there is room to stand up.
public partial class CrouchingState : WalkingState
{
    protected override float SpeedMultiplier => 0.75f;

    // Sweeping the crouched capsule up by exactly the amount it shrank covers the same volume the
    // standing capsule would occupy -- a vertical sweep of a capsule is just a taller capsule of the
    // same radius. So a clear sweep proves standing fits, with no second shape to build or keep in
    // sync. Same trick as ClamberController's rise, and read-only, so it is legal in a guard.
    //
    // ponytail: one sweep per state-machine tick while crouched, and guards are polled on _Process
    // too. If that ever shows up in a profile, cache it per physics frame.
    private bool HasHeadroom =>
        !Player.TestMove(Player.GlobalTransform, Vector3.Up * Player.Camera.CrouchOffset);

    // A stand-up asked for under a low ceiling is dropped, not queued: re-arm the latch that the
    // press cleared, so standing back up is always a deliberate press made when there is room. The
    // guards still test headroom themselves -- transitions are evaluated on _Process too, ahead of
    // this, so the guard is the safety and this is only what stops the request from surviving.
    public override void StatePhysicsProcessing(double delta)
    {
        base.StatePhysicsProcessing(delta);
        if (!HasHeadroom) Player.CrouchToggled = true;
    }

    // The camera eases down and the capsule follows it; both undo themselves on exit.
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

    // Sprint is the one exit that does resume on its own -- shift is a held key, not a toggle, so
    // walking out from under a ledge with it down should sprint without a re-press.
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
