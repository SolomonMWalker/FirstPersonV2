using Godot;

namespace FirstPerson.PlayerStates;

// Releasing shift alone doesn't end a sprint.
public partial class SprintingState : WalkingState
{
    protected override float SpeedMultiplier => 1.4f;

    protected override void AddTransitions()
    {
        AddTransition("Crouching", () => Player.CrouchToggled);
        AddTransition("Walking", () => Player.MoveInput == Vector2.Zero);
    }
}
