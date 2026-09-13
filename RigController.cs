using Godot;

public partial class RigController : AnimationTreeDriver
{
    [Export] public RevolverController Revolver { get; set; }

    // Which branch of the AnimationTree the rig is currently showing: "Hip" or "Aim".
    // Latched by HipState/AimState, and deliberately NOT the same thing as
    // RevolverController.aiming -- the player can release aim mid-fire, but the rig only
    // changes branch once the Action region is back in Idle.
    public string Stance { get; set; } = "Hip";

    public void GetInput(bool pressFire, bool aim, bool pressReload)
    {
        Revolver.GetInput(pressFire, aim, pressReload);
    }
}
