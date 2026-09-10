using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class RevolverCylinderController : Node
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RevolverCylinderRotationModifier RevolverCylinderRotationModifier { get; set; }
    [Export] public Godot.Collections.Array<MeshInstance3D> Bullets { get; set; }
    [Export] public Godot.Collections.Array<MeshInstance3D> BulletShells { get; set; }
    
    private Dictionary<int, int> _bulletsLeftInCylinderToShootPosition = new Dictionary<int, int>()
    {
        { 6, 0 },
        { 5, 60 },
        { 4, 120 },
        { 3, 180 },
        { 2, 240 },
        { 1, 300 },
        { 0, 360 }
    };

    private int _reloadSlotOffsetInDegrees = -120;
    private int _hammerDownTurnAmountInDegrees = 60;

    public void RotateCylinderForReload()
    {
        var angleToRotateTo = _bulletsLeftInCylinderToShootPosition[RevolverController.ammoInCylinder] +
                              _reloadSlotOffsetInDegrees;
        RotateCylinderTo(angleToRotateTo, (6.0f/24.0f));
    }

    public void RotateCylinderAfterReload()
    {
        var angleToRotateTo = _bulletsLeftInCylinderToShootPosition[RevolverController.ammoInCylinder];
        RotateCylinderTo(angleToRotateTo, (6.0f/24.0f));
    }
    
    private void RotateCylinderTo(float targetRotationInDegrees, float durationInSeconds)
    {
        RevolverCylinderRotationModifier.RotateTo(targetRotationInDegrees, durationInSeconds);
    }

    public void FireBullet()
    {
        if (RevolverController.ammoInCylinder <= 0) return;
        var indexOfBullet = RevolverController.ammoInCylinder - 1;
        Bullets[indexOfBullet].Visible = false;
        BulletShells[indexOfBullet].Visible = true;
    }

    public void ReloadBullet()
    {
        if (RevolverController.ammoInCylinder >= 6) return;
        var indexOfBullet = RevolverController.ammoInCylinder;
        Bullets[indexOfBullet].Visible = true;
        BulletShells[indexOfBullet].Visible = false;
    }

    public void EmptyBulletShellsFromCylinder()
    {
        if (RevolverController.ammoInCylinder >= 6) return;
        foreach (var bulletShell in BulletShells.Where(bs => bs.Visible == true))
        {
            var bulletShellGlobalLocation = bulletShell.GlobalPosition;
            bulletShell.Visible = false;
            //instance a physics bullet shell to drop out of cylinder
        }
    }
}
