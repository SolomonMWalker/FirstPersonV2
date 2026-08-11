using FirstPerson.CustomTypes.StateMachine;
using Godot;

// Debug overlay: the player machine's active configuration plus horizontal speed, which is the only
// thing that actually differs between Walking, Sprinting and Crouching.
//
// Found via the player rather than by searching the tree for a node called "StateMachine". That
// search worked exactly as long as there was one machine in the level; the first enemy brought a
// second and it started reporting the enemy's brain, then crashing when the enemy turned out to have
// no PlayerController above it.
public partial class StateLabel : Label
{
	private StateMachine _machine;
	private PlayerController _player;

	public override void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		_machine = _player?.GetNodeOrNull<StateMachine>("StateMachine");
	}

	public override void _Process(double delta)
	{
		if (_machine is null) { Text = "no player StateMachine in tree"; return; }

		var speed = new Vector2(_player.Velocity.X, _player.Velocity.Z).Length();
		Text = $"{_machine.GetStateMachineString()}\nspeed {speed:F2}";
	}
}
