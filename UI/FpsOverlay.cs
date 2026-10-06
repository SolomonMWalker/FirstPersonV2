using Godot;

namespace FirstPerson;

// Autoloaded dev FPS readout. Above PauseMenu and keeps updating while paused.
public partial class FpsOverlay : CanvasLayer
{
	private Label _label;

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always;
		AddChild(_label = new Label
		{
			Position = new Vector2(8, 4),
			LabelSettings = new LabelSettings
			{
				FontSize = 16,
				OutlineSize = 4,
				OutlineColor = Colors.Black,
			},
		});
	}

	public override void _Process(double delta) =>
		_label.Text = $"{Engine.GetFramesPerSecond()} FPS";
}
