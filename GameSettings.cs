using Godot;

// Everything a future options menu needs to read and write, and nothing a config framework would
// add on top: no schema, no change events, just fields that round-trip through Godot's own
// Resource (de)serializer.
[GlobalClass]
public partial class GameSettings : Resource
{
	private const string DefaultPath = "user://settings.tres";

	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float Fov = 75f;

	// Motion sickness is the most common complaint about camera juice, so both stay switchable at
	// 0 -- same posture as the knobs they mirror on CameraController.
	[Export] public float BobAmount = 0.045f;
	[Export] public float RollAngle = 2.0f;

	// Path is a parameter, not baked into the calls below, so tests can round-trip through a
	// throwaway file instead of the player's real save.
	public static GameSettings Load(string path = DefaultPath) =>
		ResourceLoader.Exists(path) ? GD.Load<GameSettings>(path) : new GameSettings();

	public void Save(string path = DefaultPath) => ResourceSaver.Save(this, path);

	public void ApplyTo(PlayerController player, CameraController camera)
	{
		player.MouseSensitivity = MouseSensitivity;
		camera.Fov = Fov;
		camera.BobAmount = BobAmount;
		camera.RollAngle = RollAngle;
	}
}
