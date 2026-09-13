using Godot;

namespace FirstPerson;

/// <summary>
/// Renders the viewmodel at its own FOV, so the world can be wide without the gun looking
/// stretched, while leaving it in the world camera's render pass -- which is what lets the sun,
/// the environment and shadows reach it. The FOV comes from a projection override in
/// Assets/Materials/viewmodel.gdshader rather than from a second camera in a SubViewport, which
/// could not be lit by the scene at all.
///
/// The material is assigned here rather than on each imported mesh for the same reason the old
/// layer assignment was: meshes added to the rig later are covered without touching the scene,
/// and the glb's own materials are regenerated on every reimport, so hand-edits there would not
/// survive.
/// </summary>
[GlobalClass]
public partial class ViewmodelRenderer : Node
{
	/// Lets code elsewhere find the viewmodel FOV without a NodePath across the scene boundary.
	public const string GroupName = "viewmodel";

	[Export] public Node3D Viewmodel { get; set; }
	[Export] public Shader Shader { get; set; }

	/// Vertical degrees, as Camera3D.fov. Read back by RevolverCylinderController.
	[Export(PropertyHint.Range, "20,120,1")] public float Fov { get; set; } = 75.0f;

	public override void _Ready()
	{
		AddToGroup(GroupName);
		if (Viewmodel is null || Shader is null)
		{
			GD.PushError($"{Name}: Viewmodel and Shader must both be wired.");
			return;
		}

		Apply(Viewmodel);
	}

	private void Apply(Node node)
	{
		if (node is GeometryInstance3D geometry)
		{
			// The shadow pass uses the real projection, so a cast shadow would land where the gun
			// physically is -- inside the player's head -- rather than where it is drawn.
			geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			if (node is MeshInstance3D mesh) Rematerialise(mesh);
		}

		foreach (var child in node.GetChildren()) Apply(child);
	}

	private void Rematerialise(MeshInstance3D mesh)
	{
		if (mesh.Mesh is null) return;

		for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
		{
			// GetActiveMaterial resolves override -> surface override -> mesh material, so a
			// MaterialOverride set earlier in _Ready -- RevolverCylinderController's debug chamber
			// tint -- is read and carried across here instead of fighting this sweep. Most of the
			// rig has no material at all, hence the fallbacks.
			var source = mesh.GetActiveMaterial(surface) as BaseMaterial3D;
			var material = new ShaderMaterial { Shader = Shader };
			material.SetShaderParameter("fov", Fov);
			material.SetShaderParameter("albedo", source?.AlbedoColor ?? Colors.White);
			material.SetShaderParameter("albedo_texture", source?.AlbedoTexture);
			material.SetShaderParameter("roughness", source?.Roughness ?? 1.0f);
			material.SetShaderParameter("metallic", source?.Metallic ?? 0.0f);
			mesh.SetSurfaceOverrideMaterial(surface, material);
		}

		// Last, and only after GetActiveMaterial has read it: an override outranks the surface
		// overrides just installed, so leaving it would undo all of the above.
		mesh.MaterialOverride = null;
	}
}
