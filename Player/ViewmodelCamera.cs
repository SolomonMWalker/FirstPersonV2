using Godot;

namespace FirstPerson;

/// <summary>
/// Renders the viewmodel at its own FOV, so the world can be wide without the gun looking
/// stretched. Lives in a SubViewport that shares the main World3D (own_world_3d off), so it only
/// has to follow the world camera and draw the one visual layer the world camera skips.
/// Assigning that layer happens here rather than on each imported mesh, so meshes added to the
/// rig later are covered without touching the scene.
/// </summary>
[GlobalClass]
public partial class ViewmodelCamera : Camera3D
{
	[Export] public Camera3D WorldCamera { get; set; }
	[Export] public Node3D Viewmodel { get; set; }

	// 1-based, matching the Layers checkboxes in the inspector.
	[Export(PropertyHint.Range, "1,20,1")] public int ViewmodelLayer { get; set; } = 2;

	public override void _Ready()
	{
		if (WorldCamera is null || Viewmodel is null)
		{
			GD.PushError($"{Name}: WorldCamera and Viewmodel must both be wired.");
			SetProcess(false);
			return;
		}

		var bit = 1u << (ViewmodelLayer - 1);
		CullMask = bit;
		WorldCamera.CullMask &= ~bit;
		MoveToLayer(Viewmodel, bit);
	}

	private static void MoveToLayer(Node node, uint bit)
	{
		if (node is VisualInstance3D visual) visual.Layers = bit;
		foreach (var child in node.GetChildren()) MoveToLayer(child, bit);
	}

	// _Process, not _PhysicsProcess: CameraController writes the eye transform on the physics tick,
	// and process runs after physics in the frame, so this always copies the settled value.
	public override void _Process(double delta)
	{
		GlobalTransform = WorldCamera.GlobalTransform;
	}
}
