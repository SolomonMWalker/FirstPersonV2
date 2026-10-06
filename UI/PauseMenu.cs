using Godot;

namespace FirstPerson;

// Root uses PROCESS_MODE_ALWAYS so it can unpause.
public partial class PauseMenu : CanvasLayer
{
	public override void _Ready()
	{
		Visible = false;
		GetNode<Button>("Center/Buttons/Continue").Pressed += () => SetPaused(false);
		GetNode<Button>("Center/Buttons/Restart").Pressed += Restart;
		GetNode<Button>("Center/Buttons/Quit").Pressed += () => GetTree().Quit();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			SetPaused(!GetTree().Paused);
			GetViewport().SetInputAsHandled();
		}
	}

	private void SetPaused(bool paused)
	{
		GetTree().Paused = paused;
		Visible = paused;
		Input.MouseMode = paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}

	private void Restart()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}
}
