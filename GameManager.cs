using Godot;

public partial class GameManager : Node
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
			GetTree().Quit();
	}
}
