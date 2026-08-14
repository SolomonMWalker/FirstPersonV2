using Godot;

namespace FirstPerson;

// Lives inside a SubViewport, rendering only the viewmodel layer -- that separate render pass is
// the whole point (see player.tscn's cull_mask setup): it gives the gun its own near/far clip
// range and FOV, so it can never clip into world geometry the way a shared depth buffer would let
// it.
//
// A SubViewport is its own 3D transform root; nothing outside it, including the real camera's
// bob/roll/mouse-look/punch, reaches in by ordinary Node3D inheritance. So this copies the real
// camera's GlobalTransform every rendered frame instead. The viewmodel mesh itself needs none of
// this -- it stays a real child of the world Camera3D and inherits that motion for free. This
// node exists only to give the viewmodel its own render pass, not to move it.
public partial class ViewmodelCamera : Camera3D
{
	private CameraController _worldCamera;

	// Resolved by a live GetNode, not PlayerController.Of(this).Camera: children's _Ready always
	// runs before their parent's, and this node sits several levels below Player, whose own _Ready
	// is what populates that property. Reading it here would always see it unset.
	public override void _Ready() =>
		_worldCamera = PlayerController.Of(this).GetNode<CameraController>("Camera3D");

	public override void _Process(double delta) => GlobalTransform = _worldCamera.GlobalTransform;
}
