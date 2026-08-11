using Godot;

// "This object can interact with things." Dropping this one node on the player is the entire
// feature: it looks where the camera looks, publishes whatever InteractableComponent is under the
// crosshair, and presses it on the interact key.
//
// The ray is a manual query rather than a RayCast3D node because it has to follow the camera, and a
// component sitting under Components cannot inherit the camera's rotation without either living
// somewhere else in the tree or copying its transform every frame. Six lines beats both.
[GlobalClass]
public partial class InteractorComponent : Component
{
	[Export] public float Range = 3f;

	// What is under the crosshair right now, or null. Read by the HUD to decide what to prompt --
	// this component finds, the HUD displays, and neither needs to know how the other works.
	public InteractableComponent Target { get; private set; }

	private PlayerController _player;
	private HealthComponent _health;

	public override void _Ready()
	{
		base._Ready();
		_player = PlayerController.Of(this);
		_health = Get<HealthComponent>(GameObject);
	}

	public override void _PhysicsProcess(double delta)
	{
		// Corpses do not open doors. Same posture as the shield refusing to recharge on one.
		if (_health is { Alive: false })
		{
			Target = null;
			return;
		}

		var camera = _player.Camera;
		var from = camera.GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(from, from - camera.GlobalBasis.Z * Range);
		// The ray starts inside the player's own capsule, so without this it can hit it and nothing
		// is ever interactable.
		query.Exclude = [_player.GetRid()];

		// Get walks up from the collider to the GameObject that owns it, so aiming at a turret's body
		// finds the turret. A hit with no interactable -- a wall, the floor -- is the normal case.
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		Target = hit.Count > 0 ? Get<InteractableComponent>(hit["collider"].As<Node>()) : null;
		if (Target is { Enabled: false }) Target = null;

		// Read straight off Input, as GameManager already does for pause. PlayerController.SampleInput
		// exists so the movement STATES can consume a latched edge a frame later; this consumes the
		// press immediately and has nothing to latch for.
		if (Input.IsActionJustPressed("interact")) Target?.Interact();
	}
}
