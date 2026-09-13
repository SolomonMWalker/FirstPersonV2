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
    // The casings are skinned entirely to this bone, so it defines both where each chamber is
    // and which way is "out of the cylinder".
    [Export] public string CylinderBoneName { get; set; } = "revolverCylinderRotationBone";
    
    // Where the cylinder rests with `bulletsLeft` rounds in it -- 0, 60, 120 ... 360 -- the one
    // table every rotation below looks into. Ported from the FirstPerson project's
    // CylinderController._bulletInTopLeftBasis, and checked against the chamber angles measured
    // out of Arms.blend: all six positions agree.
    //
    // Positive, because the chambers are numbered so that bullet 6 comes under the hammer first.
    // The fire order and this sign are the same fact, not two.
    //
    // The model's rest does not put the right chamber under the hammer -- it sits one round
    // clockwise. PhaseDegrees rotates the whole cycle rigidly to line up: every position and the
    // startup seed in _Ready move together, so no delta changes and nothing can desynchronise.
    //
    // This is only safe BECAUSE _Ready seeds SpinDegrees from ShootPosition. Without that seed
    // SpinDegrees starts at a hard 0 and a non-zero phase makes just the first rotation wrong,
    // which is what an earlier 29.3 here did -- it turned cock #1 into an 89.3-degree turn.
    //
    // One chamber, so a whole 60. If it lands one chamber the other way, flip the sign.
    private const float PhaseDegrees = -60f;

    private static float ShootPosition(int bulletsLeft) => (6 - bulletsLeft) * 60f + PhaseDegrees;

    public override void _Ready()
    {
        base._Ready();
        TintChambersForDebug();
        // The invariant the whole scheme rests on: the cylinder sits at ShootPosition of whatever
        // is loaded. Seed it, so starting part-loaded from the inspector is not a phase error.
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder), 0f);
    }

    // ponytail: debug aid so live rounds vs spent cases are readable at a glance.
    // MaterialOverride, so the meshes' real materials are left alone. ViewmodelRenderer reads
    // these back through GetActiveMaterial() and folds the colour into the viewmodel shader, so
    // this has to stay a plain BaseMaterial3D and has to run first -- it does, the rig readies
    // before the renderer node. Delete the _Ready call to turn it off.
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

    // Cocking advances to the chamber this shot will fire -- which is exactly where the cylinder
    // rests once ammoInCylinder drops by one.
    // Cocking advances to where the cylinder will rest once this shot is spent: +60.
    public void RotateCylinderForPushHammerDown()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder - 1), (3.0f/24.0f));
    }

    // Reloading runs the table the other way: -60, bringing the next empty chamber up to the same
    // position that fires. That opposition is the whole scheme -- it needs no second constant, and
    // adding one is what previously made the open step one way and every turn after it step back.
    public void RotateCylinderForReload()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder + 1), (6.0f/24.0f));
    }

    public void RotateCylinderReloadTurnCylinder()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder + 1), (3.0f/24.0f));
    }

    // A no-op when the invariant holds -- each insert already raised the count to match where the
    // cylinder is. It is here to snap the canonical position back if anything drifted.
    public void RotateCylinderAfterReload()
    {
        RotateCylinderTo(ShootPosition(RevolverController.ammoInCylinder), (6.0f/24.0f));
    }
    
    private void RotateCylinderTo(float targetRotationInDegrees, float durationInSeconds)
    {
        RevolverCylinderRotationModifier.RotateTo(targetRotationInDegrees, durationInSeconds);
    }

    public void FireBullet()
    {
        if (RevolverController.ammoInCylinder <= 0) return;
        // Called before the count drops, so the round under the hammer is bullet number
        // ammoInCylinder -- the highest one still loaded. Arrays are 0-based, hence the -1.
        var indexOfBullet = RevolverController.ammoInCylinder - 1;
        Bullets[indexOfBullet].Visible = false;
        BulletShells[indexOfBullet].Visible = true;
    }

    public void ReloadBullet()
    {
        if (RevolverController.ammoInCylinder >= 6) return;
        // Called before the count rises, so the round being seated is bullet number
        // ammoInCylinder + 1 -- the next one up. Firing takes the highest loaded and reloading
        // adds the next, so bullets 1..ammoInCylinder are live and the rest spent: always one
        // contiguous block, which is what makes an interrupted reload leave a sane cylinder.
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
        // Bone local Y is the cylinder's axis -- that is what the rotation modifier spins about --
        // and -Y is out of the BACK of the cylinder, toward the player. Measured off the chamber
        // meshes: a live round and a spent case share a rear face at local Y -0.016, but the live
        // round reaches +0.0446 where the case stops at +0.0326. That extra 12mm is the
        // projectile, so +Y is the muzzle.
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
            // Started clear of the cylinder it is leaving, so the solver has nothing to resolve.
            shell.GlobalTransform = new Transform3D(boneWorld.Basis.Orthonormalized(),
                spawn + ejectDirection * 0.02f);

            // Spread ACROSS the ejection axis, not added in world axes. Added raw it fought the
            // axis directly -- up to 41 degrees off backward, with the speed swinging 1.2 to
            // 3.0 m/s. Projected, every shell leaves backward at EjectSpeed.
            var spread = RandomSpread();
            spread -= ejectDirection * spread.Dot(ejectDirection);
            shell.LinearVelocity = (ejectDirection + spread).Normalized() * EjectSpeed;
            if (player is not null) shell.AddCollisionExceptionWith(player);
        }
    }

    // The gun is drawn with an overridden projection at its own FOV (viewmodel.gdshader); a shell
    // parented to the level is drawn with the world camera's. Same world point, different
    // projection, so an uncorrected shell appears about 100px away from the cylinder -- it reads
    // as ejecting from the muzzle.
    // A point projects to roughly (x/z, y/z) / tan(fov/2) in view space, so scaling the spawn's
    // view-space X and Y by the ratio of the two half-angle tangents makes the world camera draw
    // it exactly where the gun is. Measured residual: 0.00 px.
    // The trade is ~0.2 m of lateral world-space offset, which only matters once the shell has
    // fallen away from the gun.
    private static Vector3 ToWorldPass(Vector3 point, Camera3D worldCamera, float viewmodelFov)
    {
        if (worldCamera is null || viewmodelFov <= 0f) return point;
        var k = Mathf.Tan(Mathf.DegToRad(worldCamera.Fov * 0.5f))
                / Mathf.Tan(Mathf.DegToRad(viewmodelFov * 0.5f));
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

    // Componentwise in world axes; the caller projects out the part along the ejection axis.
    private Vector3 RandomSpread() => new(
        (float)GD.RandRange(-EjectSpread, EjectSpread),
        (float)GD.RandRange(-EjectSpread, EjectSpread),
        (float)GD.RandRange(-EjectSpread, EjectSpread));
}
