using System.Collections.Generic;
using Godot;

// Drives an AnimationTree of nested state machines via "Branch/Node" paths.
public partial class AnimationTreeDriver : Node3D
{
    [Export] public AnimationTree AnimationTree { get; set; }

    // Lazy: the StateMachine child travels before this node's _Ready.
    private readonly Dictionary<string, AnimationNodeStateMachinePlayback> _playbacks = [];
    private string _target = "";

    private AnimationNodeStateMachinePlayback Playback(string parameterPath)
    {
        if (!_playbacks.TryGetValue(parameterPath, out var playback))
        {
            playback = (AnimationNodeStateMachinePlayback)AnimationTree.Get(parameterPath);
            _playbacks[parameterPath] = playback;
        }

        return playback;
    }

    protected AnimationNodeStateMachinePlayback RootPlayback => Playback("parameters/playback");

    private AnimationNodeStateMachinePlayback TargetPlayback
    {
        get
        {
            var slash = _target.IndexOf('/');
            return slash < 0 ? RootPlayback : Playback($"parameters/{_target[..slash]}/playback");
        }
    }

    private string TargetBranch
    {
        get
        {
            var slash = _target.IndexOf('/');
            return slash < 0 ? _target : _target[..slash];
        }
    }

    private string TargetNode
    {
        get
        {
            var slash = _target.IndexOf('/');
            return slash < 0 ? _target : _target[(slash + 1)..];
        }
    }

    // e.g. "Hip/RevolverRigHipHammerUpIdle". Repeat calls are no-ops.
    public void Travel(string animationPath)
    {
        if (animationPath == _target) return;

        var previousBranch = TargetBranch;
        _target = animationPath;

        RootPlayback.Travel(TargetBranch);
        if (TargetNode == TargetBranch) return;

        if (TargetBranch == previousBranch)
        {
            TargetPlayback.Travel(TargetNode);
        }
        else
        {
            // Start, not Travel: routing from an idle branch snaps in mid-blend.
            TargetPlayback.Start(TargetNode);
        }
    }

    public bool IsTravelComplete()
    {
        if (_target == "") return false;
        if (RootPlayback.GetTravelPath().Count > 0) return false;
        if (RootPlayback.GetCurrentNode() != TargetBranch) return false;
        if (RootPlayback.GetFadingFromNode() != "") return false;
        if (TargetNode == TargetBranch) return true;

        var playback = TargetPlayback;
        if (playback.GetTravelPath().Count > 0) return false;
        if (playback.GetCurrentNode() != TargetNode) return false;
        return playback.GetFadingFromNode() == "";
    }

    // What the tree is actually showing; differs from _target if the tree auto-advanced.
    public string CurrentNode
    {
        get
        {
            var branch = RootPlayback.GetCurrentNode().ToString();
            var nested = AnimationTree.Get($"parameters/{branch}/playback")
                .As<AnimationNodeStateMachinePlayback>();
            return nested is null ? branch : $"{branch}/{nested.GetCurrentNode()}";
        }
    }

    public bool IsCurrentAnimationFinished()
    {
        if (!IsTravelComplete()) return false;

        var playback = TargetPlayback;
        return playback.GetCurrentPlayPosition() >= playback.GetCurrentLength();
    }
}
