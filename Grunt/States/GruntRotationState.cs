using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Rotation/*
// The region picks its state from the other regions; the rules live here so every child shares them.
public abstract partial class GruntRotationState : GruntState
{
    [Export] public State FiringState { get; set; }
    [Export] public State InCombatState { get; set; }

    private static readonly string[] RotationStates = ["NoRotation", "RotateToTarget", "RotateToMovementDirection"];

    // Firing locks aim, so it outranks combat facing.
    private string DesiredStateName()
    {
        if (FiringState.Enabled) return "NoRotation";
        if (InCombatState.Enabled && Grunt.HasTarget) return "RotateToTarget";
        return "RotateToMovementDirection";
    }

    protected override void AddTransitions()
    {
        foreach (var stateName in RotationStates)
        {
            // A self-transition would exit and re-enter every frame.
            if (stateName == Name) continue;
            AddTransition(stateName, () => DesiredStateName() == stateName);
        }
    }
}
