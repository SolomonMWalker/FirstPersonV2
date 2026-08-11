using System.Collections.Generic;
using Godot;

namespace FirstPerson;

// Run headless:  godot --headless --path . res://test_game_settings.tscn
// Exits 0 on pass, 1 on failure.
public partial class GameSettingsTests : Node
{
    private const string TestPath = "user://test_settings.tres";

    public override void _Ready()
    {
        var failures = new List<string>();
        var osPath = ProjectSettings.GlobalizePath(TestPath);
        if (FileAccess.FileExists(TestPath)) DirAccess.RemoveAbsolute(osPath);

        // No file on disk yet: Load() must fall back to the defaults, not throw.
        var defaults = GameSettings.Load(TestPath);
        if (!Mathf.IsEqualApprox(defaults.Fov, 75f))
            failures.Add($"defaults: expected Fov=75, got {defaults.Fov}");

        // Round trip: what gets saved, including the motion-sickness-off case of Bob/Roll at 0,
        // must be exactly what comes back.
        var saved = new GameSettings { MouseSensitivity = 0.0099f, Fov = 90f, BobAmount = 0f, RollAngle = 0f };
        saved.Save(TestPath);
        var loaded = GameSettings.Load(TestPath);
        Near(saved.MouseSensitivity, loaded.MouseSensitivity, "MouseSensitivity", failures);
        Near(saved.Fov, loaded.Fov, "Fov", failures);
        Near(saved.BobAmount, loaded.BobAmount, "BobAmount", failures);
        Near(saved.RollAngle, loaded.RollAngle, "RollAngle", failures);

        DirAccess.RemoveAbsolute(osPath);

        if (failures.Count == 0) GD.Print("game settings tests: all passed");
        else foreach (var f in failures) GD.PrintErr(f);
        GetTree().Quit(failures.Count == 0 ? 0 : 1);
    }

    private static void Near(float expected, float actual, string what, List<string> failures)
    {
        if (!Mathf.IsEqualApprox(expected, actual))
            failures.Add($"{what} did not round-trip: expected {expected}, got {actual}");
    }
}
