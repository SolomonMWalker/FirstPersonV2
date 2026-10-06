using Godot;
using System;
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
    // Casings are skinned to this bone; it defines chamber positions and eject direction.
    [Export] public string CylinderBoneName { get; set; } = "revolverCylinderRotationBone";

    // The model rests one chamber off. Relies on _Ready seeding the rotation from ShootPosition.
    private const float PhaseDegrees = -60f;
    private const float DegreesPerChamber = 60f;
    private const float AnimationFrame = 1f / 24f;

    // Cylinder angle with `bulletsLeft` rounds loaded.
    private static float ShootPosition(int bulletsLeft) =>
        (RevolverController.CylinderCapacity - bulletsLeft) * DegreesPerChamber + PhaseDegrees;

    public override void _Ready()
    {
        base._Ready();
        TintChambersForDebug();
        // Invariant: the cylinder always rests at ShootPosition(ammoInCylinder).
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder), 0f);
    }

    // ponytail: debug tint for live vs spent rounds; delete the _Ready call to turn it off.
    // Must stay a BaseMaterial3D and run before ViewmodelRenderer, which reads it back.
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

    public void RotateCylinderForPushHammerDown()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder - 1), 3 * AnimationFrame);
    }

    public void RotateCylinderForReload()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder + 1), 6 * AnimationFrame);
    }

    public void RotateCylinderReloadTurnCylinder()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder + 1), 3 * AnimationFrame);
    }

    // Normally a no-op; snaps back if anything drifted.
    public void RotateCylinderAfterReload()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder), 6 * AnimationFrame);
    }

    private void RotateCylinderTo(float targetRotationInDegrees, float durationInSeconds)
    {
        RevolverCylinderRotationModifier.RotateTo(targetRotationInDegrees, durationInSeconds);
    }

    public void FireBullet()
    {
        if (RevolverController.ammoInCylinder <= 0) return;
        // Called before the count drops.
        var indexOfBullet = RevolverController.ammoInCylinder - 1;
        Bullets[indexOfBullet].Visible = false;
        BulletShells[indexOfBullet].Visible = true;
    }

    public void ReloadBullet()
    {
        if (RevolverController.ammoInCylinder >= RevolverController.CylinderCapacity) return;
        // Called before the count rises. Live rounds stay one contiguous block from index 0.
        var indexOfBullet = RevolverController.ammoInCylinder;
        Bullets[indexOfBullet].Visible = true;
        BulletShells[indexOfBullet].Visible = false;
    }

    public void EmptyBulletShellsFromCylinder()
    {
        if (RevolverController.ammoInCylinder >= RevolverController.CylinderCapacity) return;
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

        // Bone +Y points toward the muzzle, so -Y ejects out the back toward the player.
        var boneWorld = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(boneIndex);
        var ejectDirection = -boneWorld.Basis.Y.Normalized();
        var player = GetTree().GetFirstNodeInGroup("player") as PhysicsBody3D;
        var worldCamera = GetViewport()?.GetCamera3D();
        var viewmodel = GetTree().GetFirstNodeInGroup(FirstPerson.ViewmodelRenderer.GroupName)
            as FirstPerson.ViewmodelRenderer;

        foreach (var bulletShell in BulletShells.Where(bs => bs is { Visible: true }).ToList())
        {
            var spawn = ToWorldPass(ChamberPosition(skeleton, boneWorld, boneIndex, bulletShell),
                worldCamera, viewmodel?.Fov ?? 0f);
            bulletShell.Visible = false;

            var shell = ShellScene.Instantiate<RigidBody3D>();
            GetTree().CurrentScene.AddChild(shell);
            // Spawned clear of the cylinder so the solver doesn't push it out.
            shell.GlobalTransform = new Transform3D(boneWorld.Basis.Orthonormalized(),
                spawn + ejectDirection * 0.02f);

            // Spread only across the ejection axis so speed and direction stay consistent.
            var spread = RandomSpread();
            spread -= ejectDirection * spread.Dot(ejectDirection);
            shell.LinearVelocity = (ejectDirection + spread).Normalized() * EjectSpeed;
            if (player is not null) shell.AddCollisionExceptionWith(player);
        }
    }

    // Moves a viewmodel-FOV point so the world camera draws it in the same screen spot.
    private static Vector3 ToWorldPass(Vector3 point, Camera3D worldCamera, float viewmodelFov)
    {
        if (worldCamera is null || viewmodelFov <= 0f) return point;
        var fovScale = Mathf.Tan(Mathf.DegToRad(worldCamera.Fov * 0.5f))
                       / Mathf.Tan(Mathf.DegToRad(viewmodelFov * 0.5f));
        var cameraTransform = worldCamera.GlobalTransform;
        var viewSpacePoint = cameraTransform.AffineInverse() * point;
        return cameraTransform * new Vector3(viewSpacePoint.X * fovScale, viewSpacePoint.Y * fovScale, viewSpacePoint.Z);
    }

    // Skinned meshes report the skeleton's transform, so resolve through the skin bind pose.
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

        return boneWorld.Origin;
    }

    private Vector3 RandomSpread() => new(
        (float)GD.RandRange(-EjectSpread, EjectSpread),
        (float)GD.RandRange(-EjectSpread, EjectSpread),
        (float)GD.RandRange(-EjectSpread, EjectSpread));
}
