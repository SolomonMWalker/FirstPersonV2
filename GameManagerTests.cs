using Godot;

namespace FirstPerson;

// Run headless:  godot --headless --path . res://test_game_manager.tscn
// Exits 0 on pass, 1 on failure. Restart and Quit aren't exercised here -- ReloadCurrentScene and
// Quit would end this test scene itself, since it's the one running as the tree's current scene.
// Both are one-line calls into engine APIs; the branching logic worth covering is the pause toggle.
public partial class GameManagerTests : Node
{
    private System.Collections.Generic.List<string> _failures = [];
    private int _frame;
    private CanvasLayer _menu;
    private CanvasLayer _death;

    public override void _Ready()
    {
        // Pinned rate, same reasoning as PlayerStateTests: the schedule below is really a wall-clock
        // duration, and must mean the same thing regardless of the project's physics tick rate.
        Engine.PhysicsTicksPerSecond = 60;
        // Must keep ticking through the paused frames below, or the driver stalls with the tree.
        ProcessMode = ProcessModeEnum.Always;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Escape(bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = down });

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 10) return;

        if (_frame == 10)
        {
            _menu = GetNode<CanvasLayer>("LevelSkeleton/PauseMenu");
            True(!GetTree().Paused, "starts unpaused");
            True(!_menu.Visible, "menu starts hidden");
            Escape(true);
            return;
        }
        if (_frame == 12) { Escape(false); return; }
        if (_frame == 20)
        {
            True(GetTree().Paused, "Escape pauses");
            True(_menu.Visible, "pausing shows the menu");
            Escape(true);
            return;
        }
        if (_frame == 22) { Escape(false); return; }
        if (_frame == 30)
        {
            True(!GetTree().Paused, "a second Escape unpauses");
            True(!_menu.Visible, "unpausing hides the menu");
            // Mouse capture isn't checked here: --headless has no display server, so
            // Input.MouseMode can't actually be captured and always reads back as Visible.
            Escape(true);
            return;
        }
        if (_frame == 32) { Escape(false); return; }
        if (_frame == 40)
        {
            var buttons = GetNode<Control>("LevelSkeleton/PauseMenu/Center/Buttons");
            var settings = GetNode<Control>("LevelSkeleton/PauseMenu/Center/SettingsPanel");
            True(buttons.Visible, "pausing opens on the main list");
            True(!settings.Visible, "settings panel starts closed");

            GetNode<Button>("LevelSkeleton/PauseMenu/Center/Buttons/Settings").EmitSignal(BaseButton.SignalName.Pressed);
            True(!buttons.Visible, "Settings hides the main list");
            True(settings.Visible, "Settings opens the settings panel");

            GetNode<Button>("LevelSkeleton/PauseMenu/Center/SettingsPanel/Back").EmitSignal(BaseButton.SignalName.Pressed);
            True(buttons.Visible, "Back restores the main list");
            True(!settings.Visible, "Back closes the settings panel");

            // Re-open the settings panel, then pause/unpause/pause again: reopening must always land
            // back on the main list rather than wherever it was left, or a player who tabs out of
            // settings without pressing Back gets stuck there next time they pause.
            GetNode<Button>("LevelSkeleton/PauseMenu/Center/Buttons/Settings").EmitSignal(BaseButton.SignalName.Pressed);
            Escape(true);
            return;
        }
        if (_frame == 41) { Escape(false); return; }
        if (_frame == 49) { Escape(true); return; }
        if (_frame == 50) { Escape(false); return; }
        if (_frame == 58)
        {
            var buttons = GetNode<Control>("LevelSkeleton/PauseMenu/Center/Buttons");
            var settings = GetNode<Control>("LevelSkeleton/PauseMenu/Center/SettingsPanel");
            True(buttons.Visible, "re-pausing resets to the main list");
            True(!settings.Visible, "re-pausing leaves the settings panel closed");

            // Back to normal play before the death scenario, or "death did not pause" would be
            // asserted against a tree that was already paused for another reason.
            Escape(true);
            return;
        }
        if (_frame == 59) { Escape(false); return; }

        // --- death: the screen appears, and the world deliberately keeps running behind it ---
        if (_frame == 66)
        {
            _death = GetNode<CanvasLayer>("LevelSkeleton/DeathScreen");
            True(!GetTree().Paused, "unpaused again before dying");
            True(!_death.Visible, "death screen starts hidden");

            // Two hits, not one: the shield absorbs the first whole however big it is, so the
            // killing blow has to land on a shield that is already down.
            var health = Component.Get<HealthComponent>(GetNode<Node>("LevelSkeleton/CharacterBody3D"));
            health.TakeDamage(9999f);
            health.TakeDamage(9999f);
            True(!health.Alive, "player survived two 9999 hits");
            return;
        }
        if (_frame == 70)
        {
            True(_death.Visible, "dying does not show the death screen");
            True(!GetTree().Paused, "the death screen paused the game; it is supposed to leave it running");
            True(!_menu.Visible, "the pause menu is showing over the death screen");
            // Escape must be inert now -- otherwise it stacks the pause menu on top and the two
            // fight over the mouse.
            Escape(true);
            return;
        }
        if (_frame == 71) { Escape(false); return; }
        if (_frame == 78)
        {
            True(!GetTree().Paused, "Escape still pauses after death");
            True(_death.Visible, "the death screen went away on its own");

            if (_failures.Count == 0) GD.Print("game manager tests: all passed");
            else foreach (var f in _failures) GD.PrintErr(f);
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}
