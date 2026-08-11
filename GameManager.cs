using System;
using Godot;

public partial class GameManager : Node
{
	private CanvasLayer _pauseMenu;
	private CanvasLayer _deathScreen;
	private Control _mainButtons;
	private Control _settingsPanel;
	private bool _dead;
	private PlayerController _player;
	private CameraController _camera;
	private GameSettings _settings;

	public override void _Ready()
	{
		_pauseMenu = GetNode<CanvasLayer>("../../PauseMenu");
		_mainButtons = GetNode<Control>("../../PauseMenu/Center/Buttons");
		_settingsPanel = GetNode<Control>("../../PauseMenu/Center/SettingsPanel");

		GetNode<Button>("../../PauseMenu/Center/Buttons/Continue").Pressed += () => SetPaused(false);
		GetNode<Button>("../../PauseMenu/Center/Buttons/Settings").Pressed += OpenSettings;
		GetNode<Button>("../../PauseMenu/Center/Buttons/Restart").Pressed += Restart;
		GetNode<Button>("../../PauseMenu/Center/Buttons/Quit").Pressed += () => GetTree().Quit();
		GetNode<Button>("../../PauseMenu/Center/SettingsPanel/Back").Pressed += CloseSettings;

		// Looked up directly rather than through PlayerController.Camera: Managers is earlier in the
		// scene tree than CharacterBody3D, so this runs before PlayerController._Ready populates it.
		_player = GetNode<PlayerController>("../../CharacterBody3D");
		_camera = GetNode<CameraController>("../../CharacterBody3D/Camera3D");

		// Reuses Restart and Quit as-is: dying and giving up mid-level want exactly the same two
		// things to happen, and a second copy of either would be a second thing to keep in step.
		_deathScreen = GetNode<CanvasLayer>("../../DeathScreen");
		GetNode<Button>("../../DeathScreen/Center/Buttons/Restart").Pressed += Restart;
		GetNode<Button>("../../DeathScreen/Center/Buttons/Quit").Pressed += () => GetTree().Quit();

		// Subscribing before HealthComponent._Ready has run is fine -- the node exists from the
		// moment the scene is instantiated, and Died cannot fire until something deals damage.
		var health = Component.Get<HealthComponent>(_player);
		if (health is not null) health.Died += OnPlayerDied;

		_settings = GameSettings.Load();
		_settings.ApplyTo(_player, _camera);

		WireSlider("Sensitivity", _settings.MouseSensitivity, v => _settings.MouseSensitivity = v);
		WireSlider("Fov", _settings.Fov, v => _settings.Fov = v);
		WireSlider("Bob", _settings.BobAmount, v => _settings.BobAmount = v);
		WireSlider("Roll", _settings.RollAngle, v => _settings.RollAngle = v);
	}

	// Range and step live on the HSlider node in the scene; this only seeds the starting value and
	// applies + saves on every change, so a value takes effect and survives a restart the moment
	// the slider moves -- no separate "Apply" button to forget to press.
	private void WireSlider(string row, float initial, Action<float> assign)
	{
		var slider = GetNode<HSlider>($"../../PauseMenu/Center/SettingsPanel/{row}/Slider");
		var value = GetNode<Label>($"../../PauseMenu/Center/SettingsPanel/{row}/Value");
		slider.Value = initial;
		value.Text = initial.ToString("0.###");
		slider.ValueChanged += v =>
		{
			assign((float)v);
			value.Text = v.ToString("0.###");
			_settings.ApplyTo(_player, _camera);
			_settings.Save();
		};
	}

	// Polled, matching PlayerController's SampleInput, rather than read off the input event in
	// _UnhandledInput.
	public override void _PhysicsProcess(double delta)
	{
		// Death takes the pause key away. Otherwise Escape would stack the pause menu on top of the
		// death screen and hand the mouse back and forth between them, and unpausing out of that
		// leaves an unclickable death screen with the cursor captured.
		if (_dead) return;

		if (Input.IsActionJustPressed("pause"))
			SetPaused(!GetTree().Paused);
	}

	// Deliberately does not touch GetTree().Paused: the world keeps running behind the screen, which
	// is the whole difference between this and the pause menu. Only the cursor comes back, because
	// the buttons need something to click them with.
	private void OnPlayerDied()
	{
		_dead = true;
		_deathScreen.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void SetPaused(bool paused)
	{
		GetTree().Paused = paused;
		_pauseMenu.Visible = paused;
		Input.MouseMode = paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		// Always reopen on the main list, never mid-settings, however it was last left.
		if (paused) CloseSettings();
	}

	private void OpenSettings()
	{
		_mainButtons.Visible = false;
		_settingsPanel.Visible = true;
	}

	private void CloseSettings()
	{
		_mainButtons.Visible = true;
		_settingsPanel.Visible = false;
	}

	private void Restart()
	{
		SetPaused(false);
		GetTree().ReloadCurrentScene();
	}
}
