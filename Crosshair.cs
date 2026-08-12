using Godot;

// A dot plus four separated tick marks -- the minimal FPS crosshair. Drawn rather than built from
// a handful of Control nodes so the day this grows spread/recoil dynamism, it's a couple of
// numbers recalculated here each frame, not several nodes to reposition.
public partial class Crosshair : Control
{
	[Export] public float DotRadius = 1.5f;
	[Export] public float LineLength = 6f;
	[Export] public float Gap = 4f;   // distance from centre to the start of each line
	[Export] public float LineWidth = 2f;
	[Export] public Color CrosshairColor = Colors.White;

	// Hitmarker: four diagonal lines (NE/SE/SW/NW), the classic X, slotted between the resting
	// cross's own up/down/left/right arms rather than replacing or resizing them -- the resting
	// dot and lines never change. Health and shield are both live today. Weakspot has no way to
	// fire yet -- there is no hit-location system -- but the color is ready for whenever one
	// exists, same as the roadmap asked for.
	[Export] public float FlashDuration = 0.15f;
	[Export] public Color HealthFlashColor = Colors.White;
	[Export] public Color ShieldFlashColor = new(0.2f, 0.55f, 1f);
	[Export] public Color WeakspotFlashColor = new(1f, 0.2f, 0.2f);

	private static readonly Vector2[] DiagonalDirections =
	[
		new Vector2(1, -1).Normalized(),   // NE
		new Vector2(1, 1).Normalized(),    // SE
		new Vector2(-1, 1).Normalized(),   // SW
		new Vector2(-1, -1).Normalized(),  // NW
	];

	private float _flashTimer;
	private Color _flashColor;

	public override void _Ready()
	{
		// Component.Get, not PlayerController.Weapon: that field is only populated during Player's
		// own _Ready, which always runs after this node's (Crosshair sits several levels below
		// Player) -- reading it here would always see it unset. See ViewmodelCamera for the same
		// trap.
		var gun = Component.Get<HitscanComponent>(PlayerController.Of(this));
		if (gun is not null) gun.ShotLanded += OnShotLanded;
	}

	private void OnShotLanded(DamageResult result)
	{
		_flashColor = result switch
		{
			DamageResult.Shield => ShieldFlashColor,
			_ => HealthFlashColor,   // Health, and Weakspot until something can actually produce it
		};
		_flashTimer = FlashDuration;
	}

	public override void _Process(double delta)
	{
		if (_flashTimer <= 0f) return;
		_flashTimer -= (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		var center = Size / 2f;
		DrawCircle(center, DotRadius, CrosshairColor);
		DrawLine(center + Vector2.Up * Gap, center + Vector2.Up * (Gap + LineLength), CrosshairColor, LineWidth);
		DrawLine(center + Vector2.Down * Gap, center + Vector2.Down * (Gap + LineLength), CrosshairColor, LineWidth);
		DrawLine(center + Vector2.Left * Gap, center + Vector2.Left * (Gap + LineLength), CrosshairColor, LineWidth);
		DrawLine(center + Vector2.Right * Gap, center + Vector2.Right * (Gap + LineLength), CrosshairColor, LineWidth);

		if (_flashTimer <= 0f) return;
		// Same geometry as the resting lines, just rotated into the gaps between them -- only
		// position and color mark this as a hit, not a change to the crosshair itself.
		foreach (var dir in DiagonalDirections)
			DrawLine(center + dir * Gap, center + dir * (Gap + LineLength), _flashColor, LineWidth);
	}
}
