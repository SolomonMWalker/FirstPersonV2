using FirstPerson.CustomTypes.StateMachine;
using Godot;

// Debug overlay: the machine's active configuration plus horizontal speed, which is the only thing
// that actually differs between Walking, Sprinting and Crouching.
// ponytail: grabs the first StateMachine in the tree. Add an [Export] if a scene ever has two.
public partial class StateLabel : Label
{
	private StateMachine _machine;
	private PlayerController _player;

	public override void _Ready()
	{
		_machine = GetTree().Root.FindChild("StateMachine", true, false) as StateMachine;
		if (_machine is not null) _player = PlayerController.Of(_machine);
	}

	public override void _Process(double delta)
	{
		if (_machine is null) { Text = "no StateMachine in tree"; return; }

		var speed = new Vector2(_player.Velocity.X, _player.Velocity.Z).Length();
		Text = $"{_machine.GetStateMachineString()}\nspeed {speed:F2}";
	}
}
