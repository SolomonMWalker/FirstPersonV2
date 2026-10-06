using Godot;

namespace FirstPerson;

// Renders the viewmodel at its own FOV via a shader projection override, keeping it in the world
// pass so scene lighting still reaches it. Assigned at runtime so it survives glb reimports.
[GlobalClass]
public partial class ViewmodelRenderer : Node
{
	public const string GroupName = "viewmodel";

	[Export] public Node3D Viewmodel { get; set; }
	[Export] public Shader Shader { get; set; }

	// Degrees, same axis as the world camera's fov (Keep Width: horizontal).
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
			// Shadows use the real projection and would land inside the player's head.
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
			// Carries over any existing MaterialOverride (e.g. the debug chamber tint).
			var source = mesh.GetActiveMaterial(surface) as BaseMaterial3D;
			var material = new ShaderMaterial { Shader = Shader };
			material.SetShaderParameter("fov", Fov);
			material.SetShaderParameter("albedo", source?.AlbedoColor ?? Colors.White);
			material.SetShaderParameter("albedo_texture", source?.AlbedoTexture);
			material.SetShaderParameter("roughness", source?.Roughness ?? 1.0f);
			material.SetShaderParameter("metallic", source?.Metallic ?? 0.0f);
			mesh.SetSurfaceOverrideMaterial(surface, material);
		}

		// Cleared last; it would outrank the surface overrides above.
		mesh.MaterialOverride = null;
	}
}
