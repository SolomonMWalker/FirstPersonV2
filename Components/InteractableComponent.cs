using Godot;

namespace FirstPerson;

// "The player can do something to this." That is the whole component -- it knows nothing about what
// it is attached to or what interacting with it means. A sibling component subscribes to Interacted
// and supplies the meaning, so a door, a button and a light switch are all this plus one collaborator.
//
// There is no interact volume to author. The player's InteractorComponent raycasts at whatever is
// under the crosshair and asks it for one of these, so an object becomes interactable by carrying
// this component and having a collider -- which anything solid enough to walk up to already does.
[GlobalClass]
public partial class InteractableComponent : Component
{
	[Signal] public delegate void InteractedEventHandler();

	// Completes "Press E to ___". Mutable rather than a fixed authored string: a switch has to read
	// "turn the turret on" or "turn the turret off" depending on which way it is currently thrown,
	// and the sibling that owns the behaviour is the only thing that knows which.
	[Export] public string Verb = "interact";

	// A door welded shut still exists; it just cannot be used. Also hides the prompt, so there is no
	// state where the HUD offers something that will not happen.
	[Export] public bool Enabled = true;

	public void Interact()
	{
		if (Enabled) EmitSignal(SignalName.Interacted);
	}
}
