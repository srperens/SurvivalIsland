using Godot;

namespace SurvivalIsland;

public partial class Shelter : StaticBody3D, IInteractable
{
    [Signal] public delegate void PlayerEnteredEventHandler();
    [Signal] public delegate void PlayerExitedEventHandler();

    [Export] public float RainProtection = 1.0f; // 0-1, how much rain is blocked
    [Export] public float WindProtection = 0.5f;
    [Export] public float WarmthBonus = 5f; // Temperature degrees added

    [Export] public Area3D? ShelterArea { get; set; }
    [Export] public Node3D? SpawnPoint { get; set; }

    private bool _playerInside;

    public bool IsPlayerInside => _playerInside;
    public Vector3 RespawnPosition => SpawnPoint?.GlobalPosition ?? GlobalPosition + Vector3.Up;

    public override void _Ready()
    {
        CollisionLayer = 4; // Interactables

        if (ShelterArea != null)
        {
            ShelterArea.BodyEntered += OnBodyEntered;
            ShelterArea.BodyExited += OnBodyExited;
        }
    }

    public string GetInteractionPrompt()
    {
        return "[E] Rest in shelter";
    }

    public bool CanInteract() => true;
    public bool RequiresHold() => true;

    public void Interact(PlayerController player)
    {
        // Could trigger a rest/save mechanic
        GD.Print("Resting in shelter... (Save game would happen here)");

        // Slight health/stat recovery
        var stats = player.GetNode<PlayerStats>("PlayerStats");
        if (stats != null)
        {
            stats.Heal(5);
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is PlayerController player)
        {
            _playerInside = true;

            var stats = player.GetNode<PlayerStats>("PlayerStats");
            if (stats != null)
            {
                // Shelter provides warmth bonus
                stats.AmbientTemperature += WarmthBonus;
            }

            EmitSignal(SignalName.PlayerEntered);
            GD.Print("Entered shelter");
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is PlayerController player)
        {
            _playerInside = false;

            var stats = player.GetNode<PlayerStats>("PlayerStats");
            if (stats != null)
            {
                stats.AmbientTemperature -= WarmthBonus;
            }

            EmitSignal(SignalName.PlayerExited);
            GD.Print("Left shelter");
        }
    }
}
