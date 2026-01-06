using Godot;

namespace SurvivalIsland;

public partial class GameManager : Node
{
    [Signal] public delegate void GameStartedEventHandler();
    [Signal] public delegate void GamePausedEventHandler();
    [Signal] public delegate void GameResumedEventHandler();
    [Signal] public delegate void GameOverEventHandler();

    public static GameManager? Instance { get; private set; }

    [Export] public PackedScene? PlayerScene { get; set; }
    [Export] public Node3D? PlayerSpawnPoint { get; set; }

    private PlayerController? _player;
    private HUD? _hud;
    private DayNightCycle? _dayNight;
    private WeatherSystem? _weather;
    private bool _isPaused;

    public PlayerController? Player => _player;
    public bool IsPaused => _isPaused;

    public override void _Ready()
    {
        Instance = this;

        // Get references
        _dayNight = GetNodeOrNull<DayNightCycle>("Environment/DayNightCycle");
        _weather = GetNodeOrNull<WeatherSystem>("Environment/WeatherSystem");
        _hud = GetNodeOrNull<HUD>("HUD");

        // Find or spawn player
        _player = GetNodeOrNull<PlayerController>("Player");

        if (_player == null && PlayerScene != null)
        {
            _player = PlayerScene.Instantiate<PlayerController>();
            AddChild(_player);

            if (PlayerSpawnPoint != null)
            {
                _player.GlobalPosition = PlayerSpawnPoint.GlobalPosition;
            }
        }

        // Setup systems on player (only if not already present)
        if (_player != null)
        {
            var inventory = _player.GetNodeOrNull<InventorySystem>("InventorySystem");
            if (inventory == null)
            {
                inventory = new InventorySystem();
                inventory.Name = "InventorySystem";
                _player.AddChild(inventory);
            }

            var crafting = _player.GetNodeOrNull<CraftingSystem>("CraftingSystem");
            if (crafting == null)
            {
                crafting = new CraftingSystem();
                crafting.Name = "CraftingSystem";
                _player.AddChild(crafting);
            }
            crafting.Initialize(inventory);

            // Initialize HUD
            _hud?.Initialize(_player, _dayNight, _weather);

            // Connect player death
            var stats = _player.GetNode<PlayerStats>("PlayerStats");
            if (stats != null)
            {
                stats.PlayerDied += OnPlayerDied;
            }

            // Connect weather to player stats
            if (_weather != null && stats != null)
            {
                _weather.WeatherChanged += (weather) =>
                {
                    stats.AmbientTemperature = _weather.EffectiveTemperature;
                };
            }
        }

        EmitSignal(SignalName.GameStarted);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        GetTree().Paused = _isPaused;

        if (_isPaused)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            EmitSignal(SignalName.GamePaused);
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            EmitSignal(SignalName.GameResumed);
        }
    }

    private void OnPlayerDied()
    {
        GD.Print("Game Over - Player died!");
        EmitSignal(SignalName.GameOver);

        // Could show game over screen, restart options, etc.
    }

    public void RestartGame()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
