using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_interact.tscn
// Exits 0 on pass, 1 on failure.
//
// Drives the real test_level, because the ray is the part that can silently miss and no synchronous
// harness would exercise it. Aiming is done by teleporting the player in front of the second turret
// and calling LookAt on the body: the camera inherits body yaw and its pitch stays 0, so this is a
// level ray at eye height and nothing needs write access to PlayerController.LookPitch.
//
// The second turret's body is 2m tall for exactly this reason -- a 1.2m box like the first turret's
// sits below the 1.5m eye line, and you would have to look at your feet to interact with it.
public partial class InteractableComponentTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private PlayerController _player;
    private Node3D _turretObject;
    private InteractorComponent _interactor;
    private InteractableComponent _switch;
    private GunComponent _turret;
    private Label _prompt;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void PressE(bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.E, Pressed = down });

    // Stands the player `distance` out along the turret's own facing and turns them around to look
    // back at it.
    private void StandInFrontOfTurret(float distance)
    {
        var facing = -_turretObject.GlobalBasis.Z;
        _player.GlobalPosition = (_turretObject.GlobalPosition + facing * distance) with { Y = 1f };
        _player.Velocity = Vector3.Zero;
        _player.LookAt(_turretObject.GlobalPosition with { Y = _player.GlobalPosition.Y });
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        if (_frame == 10)
        {
            _player = GetNode<PlayerController>("LevelSkeleton/Player");
            _turretObject = GetNode<Node3D>("LevelSkeleton/Enemy2");
            _interactor = Component.Get<InteractorComponent>(_player);
            _switch = Component.Get<InteractableComponent>(_turretObject);
            _turret = Component.Get<GunComponent>(_turretObject);
            _prompt = GetNode<Label>("LevelSkeleton/Player/Hud/Prompt");

            True(_interactor is not null, "the player has no InteractorComponent");
            True(_switch is not null, "the second turret has no InteractableComponent");
            True(_turret is not null, "the second turret has no GunComponent");
            True(!_turret.Firing, "the second turret is supposed to start switched off");
            True(_switch.Verb == "turn the turret on", $"initial verb reads '{_switch.Verb}'");
            True(!_prompt.Visible, "the prompt starts visible with nothing targeted");

            StandInFrontOfTurret(2.5f);
            return;
        }

        // Looking at it: the ray hits the child Body collider, and Get has to walk up from there to
        // Enemy2's Components. That walk is the whole reason this scene needed no restructuring.
        if (_frame == 20)
        {
            True(_interactor.Target == _switch,
                $"looking at the turret did not target its interactable (got {_interactor.Target?.Name ?? "null"})");
            True(_prompt.Visible, "the prompt stayed hidden while looking at an interactable");
            True(_prompt.Text == "Press E to turn the turret on", $"prompt reads '{_prompt.Text}'");
            PressE(true);
            return;
        }
        if (_frame == 21) { PressE(false); return; }

        if (_frame == 25)
        {
            True(_turret.Firing, "pressing E did not switch the turret on");
            True(_switch.Verb == "turn the turret off", $"verb after switching on reads '{_switch.Verb}'");
            True(_prompt.Text == "Press E to turn the turret off", $"prompt after switching on reads '{_prompt.Text}'");
            PressE(true);
            return;
        }
        if (_frame == 26) { PressE(false); return; }

        if (_frame == 30)
        {
            True(!_turret.Firing, "a second press did not switch the turret back off");
            True(_switch.Verb == "turn the turret on", $"verb after switching off reads '{_switch.Verb}'");

            // Turn to face the opposite way -- +Z is behind, since forward is -Z. Whatever is out
            // there (open floor, a distant wall, nothing) is not interactable, the ordinary case.
            var behind = _player.GlobalPosition + _player.GlobalBasis.Z * 5f;
            _player.LookAt(behind with { Y = _player.GlobalPosition.Y });
            return;
        }

        if (_frame == 40)
        {
            True(_interactor.Target is null,
                $"looking away still targeted something ({_interactor.Target?.Name})");
            True(!_prompt.Visible, "the prompt stayed up after looking away");

            // Out of range: standing well back, the same aim must find nothing. Range is 3m.
            StandInFrontOfTurret(8f);
            return;
        }

        if (_frame == 50)
        {
            True(_interactor.Target is null, "an interactable 8m away was targeted with a 3m range");
            True(!_prompt.Visible, "the prompt showed for an out-of-range interactable");

            // ...and stepping back into range picks it up again.
            StandInFrontOfTurret(2.5f);
            return;
        }

        if (_frame < 60) return;

        True(_interactor.Target == _switch, "stepping back into range did not re-target the turret");

        if (_failures.Count == 0) GD.Print("interactable component tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}
