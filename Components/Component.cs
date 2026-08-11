using System.Linq;
using Godot;

// Base for everything that hangs under a GameObject's `Components` node. See README.md.
//
// Node3D rather than Node: a component is allowed to have a place on the object it belongs to (a
// muzzle, a hitbox, a pickup socket). Ones that don't care, like Health, simply sit at the origin.
public abstract partial class Component : Node3D
{
	// The object this component describes: the parent of the `Components` container. Resolved once,
	// so a component can be reparented in the editor without anything caching a stale owner.
	public Node GameObject { get; private set; }

	public override void _Ready()
	{
		GameObject = GetParent()?.GetParent();
		if (GameObject is null || GetParent().Name != "Components")
			GD.PushError($"{Name}: components must be a direct child of a node named 'Components'.");
	}

	// Ask an object whether it has a capability. Null is a normal answer, not an error -- a wall has
	// no HealthComponent and that is exactly how "you cannot damage a wall" is expressed. Lookup is by
	// type and not by node name, so renaming a component node in the editor breaks nothing.
	public static T Get<T>(Node gameObject) where T : Component =>
		gameObject?.GetNodeOrNull("Components")?.GetChildren().OfType<T>().FirstOrDefault();
}
