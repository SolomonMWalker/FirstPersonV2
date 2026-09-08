using Godot;
using System;

public partial class RigController : Node3D
{
    [Export] public AnimationTree AnimationTree { get; set; }
    [Export] public RevolverController Revolver { get; set; }

    private AnimationNodeStateMachinePlayback _stateMachinePlayback;

    public override void _Ready()
    {
        base._Ready();
        _stateMachinePlayback = (AnimationNodeStateMachinePlayback)AnimationTree.Get("parameters/playback");
    }

    public void GetInput(bool pressFire, bool holdFire, bool aim)
    {
        Revolver.GetInput(pressFire, aim);
    }

    public void Travel(string animationName)
    {
        _stateMachinePlayback.Travel(animationName);
    }
    
    public bool IsCurrentAnimationFinished()
    {
        if (_stateMachinePlayback.GetFadingFromNode() != "") return false;
        return _stateMachinePlayback.GetCurrentPlayPosition() >= _stateMachinePlayback.GetCurrentLength();
    }
}
