using Godot;

namespace FirstPerson;

// Dev framerate readout. Autoload, so it is on in every scene with no per-scene wiring, and it
// builds its own Label rather than carrying a .tscn that would be one more file to keep in sync.
//
// Layer sits above PauseMenu's 10 and ProcessMode is Always, so the number keeps updating while
// paused -- a frozen readout on a paused game reads as a hang.
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
			// Outline rather than a panel: readable over both a bright skybox and a dark interior
			// without putting an opaque box over the corner of the screen.
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
