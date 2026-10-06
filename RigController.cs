using Godot;

public partial class RigController : AnimationTreeDriver
{
    [Export] public RevolverController Revolver { get; set; }

    // "Hip" or "Aim". Lags RevolverController.aiming until the Action region is back in Idle.
    public string Stance { get; set; } = "Hip";

    public void GetInput(bool pressFire, bool aim, bool pressReload)
    {
        Revolver.GetInput(pressFire, aim, pressReload);
    }
}
