using Godot;
using System;

public partial class RigController : Node3D
{
    [Export] public AnimationTree AnimationTree { get; set; }
    [Export] public RevolverController Revolver { get; set; }

    // Resolved on first use, not in _Ready: the StateMachine child readies before this node and
    // enters its initial state, which travels immediately.
    private AnimationNodeStateMachinePlayback _stateMachinePlayback;
    private AnimationNodeStateMachinePlayback StateMachinePlayback =>
        _stateMachinePlayback ??= (AnimationNodeStateMachinePlayback)AnimationTree.Get("parameters/playback");

    public void GetInput(bool pressFire, bool holdFire, bool aim)
    {
        Revolver.GetInput(pressFire, aim);
    }

    public void Travel(string animationName)
    {
        StateMachinePlayback.Travel(animationName);
    }
    
    public bool IsCurrentAnimationFinished()
    {
        if (StateMachinePlayback.GetFadingFromNode() != "") return false;
        return StateMachinePlayback.GetCurrentPlayPosition() >= StateMachinePlayback.GetCurrentLength();
    }
}
