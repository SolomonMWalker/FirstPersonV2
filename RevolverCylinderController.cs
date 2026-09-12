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

    [ExportGroup("Shell ejection")]
    [Export] public PackedScene ShellScene { get; set; }
    [Export] public float EjectSpeed { get; set; } = 2.0f;
    [Export] public float EjectSpread { get; set; } = 0.4f;
    // The casings are skinned entirely to this bone, so it defines both where each chamber is
    // and which way is "out of the cylinder".
    [Export] public string CylinderBoneName { get; set; } = "revolverCylinderRotationBone";
    
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

    public override void _Ready()
    {
        base._Ready();
        TintChambersForDebug();
    }

    // ponytail: debug aid so live rounds vs spent cases are readable at a glance.
    // MaterialOverride, so the meshes' real materials are left alone. Delete the _Ready
    // call to turn it off.
    private void TintChambersForDebug()
    {
        Tint(Bullets, Colors.Blue);
        Tint(BulletShells, Colors.Red);
    }

    private static void Tint(Godot.Collections.Array<MeshInstance3D> meshes, Color color)
    {
        if (meshes is null) return;
        var material = new StandardMaterial3D { AlbedoColor = color };
        foreach (var mesh in meshes)
        {
            if (mesh is not null) mesh.MaterialOverride = material;
        }
    }

    // ---------------------------------------------------------------------------------
    // TEMPORARY test hook -- press R to dump all six shells, to check direction and force.
    // Delete this whole block (and the _Process override) when done.
    public override void _Process(double delta)
    {
        if (!Input.IsActionJustPressed("reload")) return;
        foreach (var shell in BulletShells) shell.Visible = true;
        RevolverController.ammoInCylinder = 0;
        GD.Print("[TEMP] ejecting 6 shells");
        EmptyBulletShellsFromCylinder();
    }
    // ---------------------------------------------------------------------------------

    public void RotateCylinderForPushHammerDown()
    {
        var angleToRotateTo = _bulletsLeftInCylinderToShootPosition[RevolverController.ammoInCylinder] + _hammerDownTurnAmountInDegrees;
        RotateCylinderTo(angleToRotateTo, (3.0f/24.0f));
    }
    
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

    public void RotateCylinderReloadTurnCylinder()
    {
        var angleToRotateTo = _bulletsLeftInCylinderToShootPosition[RevolverController.ammoInCylinder] + 
                              _reloadSlotOffsetInDegrees;
        RotateCylinderTo(angleToRotateTo, (3.0f/24.0f));
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
        if (ShellScene is null)
        {
            GD.PushError($"{Name}: ShellScene is not assigned; no casings will be ejected.");
            return;
        }

        var skeleton = RevolverCylinderRotationModifier?.GetSkeleton();
        if (skeleton is null) return;
        var boneIndex = skeleton.FindBone(CylinderBoneName);
        if (boneIndex < 0)
        {
            GD.PushError($"{Name}: no bone named '{CylinderBoneName}'.");
            return;
        }

        // The cylinder bone's live world transform, which the rotation modifier is spinning.
        var boneWorld = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(boneIndex);
        var axis = boneWorld.Basis.Y.Normalized();
        var player = GetTree().GetFirstNodeInGroup("player") as PhysicsBody3D;
        var worldCamera = GetViewport()?.GetCamera3D();
        var viewmodelCamera = GetTree().GetFirstNodeInGroup(FirstPerson.ViewmodelCamera.GroupName)
            as Camera3D;

        foreach (var bulletShell in BulletShells.Where(bs => bs is { Visible: true }).ToList())
        {
            var spawn = ToWorldPass(ChamberPosition(skeleton, boneWorld, boneIndex, bulletShell),
                worldCamera, viewmodelCamera);
            bulletShell.Visible = false;

            var shell = ShellScene.Instantiate<RigidBody3D>();
            GetTree().CurrentScene.AddChild(shell);
            shell.GlobalTransform = new Transform3D(boneWorld.Basis.Orthonormalized(), spawn);
            // -axis is out of the back of the cylinder, matching where the ejector pushes.
            shell.LinearVelocity = (-axis + RandomSpread()) * EjectSpeed;
            if (player is not null) shell.AddCollisionExceptionWith(player);
        }
    }

    // The gun is drawn by the viewmodel camera at its own FOV; a shell parented to the level is
    // drawn by the world camera. Same world point, different projection, so an uncorrected shell
    // appears about 100px away from the cylinder -- it reads as ejecting from the muzzle.
    // A point projects to roughly (x/z, y/z) / tan(fov/2) in view space, so scaling the spawn's
    // view-space X and Y by the ratio of the two half-angle tangents makes the world camera draw
    // it exactly where the gun is. Measured residual: 0.00 px.
    // The trade is ~0.2 m of lateral world-space offset, which only matters once the shell has
    // fallen away from the gun.
    private static Vector3 ToWorldPass(Vector3 point, Camera3D worldCamera, Camera3D viewmodelCamera)
    {
        if (worldCamera is null || viewmodelCamera is null) return point;
        var k = Mathf.Tan(Mathf.DegToRad(worldCamera.Fov * 0.5f))
                / Mathf.Tan(Mathf.DegToRad(viewmodelCamera.Fov * 0.5f));
        var eye = worldCamera.GlobalTransform;
        var local = eye.AffineInverse() * point;
        return eye * new Vector3(local.X * k, local.Y * k, local.Z);
    }

    // These meshes are skinned, so their node transform is the skeleton's, not the chamber's.
    // The chamber's real position has to come back through the skinning chain.
    private Vector3 ChamberPosition(Skeleton3D skeleton, Transform3D boneWorld, int boneIndex,
        MeshInstance3D shellMesh)
    {
        var skin = shellMesh.Skin;
        if (skin is not null)
        {
            for (var bind = 0; bind < skin.GetBindCount(); bind++)
            {
                var bound = skin.GetBindBone(bind);
                if (bound < 0) bound = skeleton.FindBone(skin.GetBindName(bind));
                if (bound != boneIndex) continue;
                return boneWorld * skin.GetBindPose(bind) * shellMesh.GetAabb().GetCenter();
            }
        }

        // Not skinned to the cylinder bone: fall back to the bone itself rather than the
        // skeleton origin, which is where the mesh's own transform would wrongly point.
        return boneWorld.Origin;
    }

    private Vector3 RandomSpread() => new(
        (float)GD.RandRange(-EjectSpread, EjectSpread),
        (float)GD.RandRange(-EjectSpread, EjectSpread),
        (float)GD.RandRange(-EjectSpread, EjectSpread));
}
