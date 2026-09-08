using Godot;

namespace FirstPerson;

// Pause screen lifted from first-person-v-2: resume / restart / quit, without that project's
// settings panel and death screen. Root is a PROCESS_MODE_ALWAYS CanvasLayer so it keeps ticking
// while GetTree().Paused is true -- that is what lets ui_cancel unpause and keeps the buttons live.
public partial class PauseMenu : CanvasLayer
{
	public override void _Ready()
	{
		Visible = false;
		GetNode<Button>("Center/Buttons/Continue").Pressed += () => SetPaused(false);
		GetNode<Button>("Center/Buttons/Restart").Pressed += Restart;
		GetNode<Button>("Center/Buttons/Quit").Pressed += () => GetTree().Quit();
	}

	// ui_cancel is Escape by default -- no input-map entry needed. Marked handled so a captured
	// PlayerController never also sees the same press.
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
