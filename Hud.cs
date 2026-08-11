using Godot;

// Two bars for the two numbers the player has to make decisions about. Polled every frame rather
// than driven by the components' signals: the shield's recharge is a continuous ramp with no signal
// to hang a bar off, so something here has to poll regardless, and two float reads a frame is not a
// budget worth managing. Signals are for things that happen once; a bar is a continuous readout.
//
// Lives under the player because it is a view of the player, and finds its subject with the same
// ancestor walk the state nodes use rather than a NodePath that has to be wired per scene.
public partial class Hud : CanvasLayer
{
	private HealthComponent _health;
	private ShieldComponent _shield;
	private InteractorComponent _interactor;
	private ProgressBar _healthBar;
	private ProgressBar _shieldBar;
	private Label _prompt;
	private string _key;

	public override void _Ready()
	{
		var player = PlayerController.Of(this);
		_health = Component.Get<HealthComponent>(player);
		_shield = Component.Get<ShieldComponent>(player);
		_interactor = Component.Get<InteractorComponent>(player);

		_healthBar = GetNode<ProgressBar>("Bars/Health");
		_shieldBar = GetNode<ProgressBar>("Bars/Shield");
		_prompt = GetNode<Label>("Prompt");

		// Read off the InputMap rather than written into the prompt as a literal, so the day there is
		// a rebinding screen this cannot quietly start lying. Resolved once: a binding cannot change
		// mid-frame, and there is nothing yet that can change one at all.
		_key = InputMap.ActionGetEvents("interact")[0] is InputEventKey k
			? OS.GetKeycodeString(k.PhysicalKeycode)
			: "the interact key";   // ponytail: a gamepad binding would land here; name it properly then

		// A missing component hides its bar outright. Nothing to report is not the same as a reading
		// of zero, and an object with no shield showing an empty shield bar would read as one hit
		// from death when it is in fact untouched.
		_healthBar.Visible = _health is not null;
		_shieldBar.Visible = _shield is not null;

		// Taken from the components, so retuning Max in the inspector needs no second edit here.
		if (_health is not null) _healthBar.MaxValue = _health.Max;
		if (_shield is not null) _shieldBar.MaxValue = _shield.Max;
	}

	public override void _Process(double delta)
	{
		if (_health is not null) _healthBar.Value = _health.Current;
		if (_shield is not null) _shieldBar.Value = _shield.Current;

		// The interactor decides what is under the crosshair; this only decides how to say it.
		var target = _interactor?.Target;
		_prompt.Visible = target is not null;
		if (target is not null) _prompt.Text = $"Press {_key} to {target.Verb}";
	}
}
