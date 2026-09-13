using System.Collections.Generic;
using Godot;

public partial class RigController : Node3D
{
    [Export] public AnimationTree AnimationTree { get; set; }
    [Export] public RevolverController Revolver { get; set; }

    // Which branch of the AnimationTree the rig is currently showing: "Hip" or "Aim".
    // Latched by HipState/AimState, and deliberately NOT the same thing as
    // RevolverController.aiming -- the player can release aim mid-fire, but the rig only
    // changes branch once the Action region is back in Idle.
    public string Stance { get; set; } = "Hip";

    // Resolved on first use, not in _Ready: the StateMachine child readies before this node and
    // enters its initial state, which travels immediately.
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

    private AnimationNodeStateMachinePlayback RootPlayback => Playback("parameters/playback");

    // Hip and Aim are nested state machines, each with its own playback. The root playback
    // only says which branch is on screen; the branch playback owns the clip, so anything
    // that inspects position or length has to go through it.
    private AnimationNodeStateMachinePlayback TargetPlayback
    {
        get
        {
            var slash = _target.IndexOf('/');
            return slash < 0 ? RootPlayback : Playback($"parameters/{_target[..slash]}/playback");
        }
    }

    // "Hip" for "Hip/RevolverRigHipHammerUpIdle"; the whole string when there is no branch.
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

    public void GetInput(bool pressFire, bool aim, bool pressReload)
    {
        Revolver.GetInput(pressFire, aim, pressReload);
    }

    // Takes a path into a nested state machine, e.g. "Hip/RevolverRigHipHammerUpIdle", and
    // drives both levels: the root moves between branches, the branch moves between clips.
    // IdleState re-travels every physics frame, so a repeat has to be free -- travelling to the
    // node we are already heading for would restart it.
    public void Travel(string animationPath)
    {
        if (animationPath == _target) return;

        var previousBranch = TargetBranch;
        _target = animationPath;

        RootPlayback.Travel(TargetBranch);
        if (TargetNode == TargetBranch) return;

        if (TargetBranch == previousBranch)
        {
            // Same branch: Travel so the transition between clips, and its crossfade, is used.
            TargetPlayback.Travel(TargetNode);
        }
        else
        {
            // Crossing branches. The destination branch is idle right now, so a Travel would
            // spend the first couple of frames of the root crossfade routing Start -> target
            // while contributing no pose at all, and then snap in partway through the blend.
            // Start puts it straight on the target clip instead.
            TargetPlayback.Start(TargetNode);
        }
    }

    // True once the requested clip is the one on screen: routing finished, blend finished.
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

    // What the tree is ACTUALLY showing: the root's current node, plus the clip inside it when
    // that node is a nested state machine. Compare it against the state the chart thinks it is in
    // -- they disagree exactly when a transition auto-advanced out from under C#, which is the
    // failure mode that deadlocks IsCurrentAnimationFinished().
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
