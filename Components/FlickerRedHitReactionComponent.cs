using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPersonV3.Components;

// Tints every mesh under MeshRoot red and fades it out.
public partial class FlickerRedHitReactionComponent : Node
{
    // Every MeshInstance3D under this node flashes. Defaults to the scene root this component is saved in.
    [Export] public Node MeshRoot { get; set; }
    // Alpha is the flash's starting strength; it fades to zero over Duration.
    [Export] public Color Color { get; set; } = new(1f, 0f, 0f, 0.6f);
    [Export(PropertyHint.None, "suffix:s")] public float Duration { get; set; } = 0.15f;

    private List<MeshInstance3D> _meshes = [];
    private StandardMaterial3D _material;
    private Tween _tween;

    public override void _Ready()
    {
        MeshRoot ??= Owner;

        // owned: false, because the meshes belong to an instanced .glb scene, not this one.
        _meshes = [.. MeshRoot.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()];
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
    }

    // A new call restarts the flash at full strength.
    public void FlickerRed()
    {
        _tween?.Kill();
        _material.AlbedoColor = Color;
        SetMeshOverlay(_material);

        _tween = CreateTween();
        _tween.TweenProperty(_material, "albedo_color:a", 0f, Duration);
        // Remove the overlay afterwards so the meshes aren't drawing an invisible extra pass.
        _tween.TweenCallback(Callable.From(() => SetMeshOverlay(null)));
    }

    private void SetMeshOverlay(Material material)
    {
        foreach (var mesh in _meshes)
            mesh.MaterialOverlay = material;
    }
}
