using Godot;

namespace SurvivalIsland;

public partial class Campfire : StaticBody3D, IInteractable
{
    [Signal] public delegate void FireLitEventHandler();
    [Signal] public delegate void FireExtinguishedEventHandler();

    [Export] public float BurnDuration = 300f; // 5 minutes per fuel
    [Export] public float WarmthRadius = 5f;
    [Export] public float CookingTime = 10f;

    [Export] public GpuParticles3D? FireParticles { get; set; }
    [Export] public OmniLight3D? FireLight { get; set; }
    [Export] public AudioStreamPlayer3D? FireSound { get; set; }
    [Export] public Area3D? WarmthArea { get; set; }
    [Export] public ItemData? CookedMeatItem { get; set; }

    private bool _isLit;
    private float _burnTimer;
    private bool _isCooking;
    private float _cookTimer;
    private PlayerController? _cookingPlayer;

    public bool IsLit => _isLit;

    public override void _Ready()
    {
        CollisionLayer = 4; // Interactables

        // Setup warmth area
        if (WarmthArea != null)
        {
            WarmthArea.BodyEntered += OnBodyEnteredWarmth;
            WarmthArea.BodyExited += OnBodyExitedWarmth;
        }

        SetFireState(false);
    }

    public override void _Process(double delta)
    {
        if (_isLit)
        {
            _burnTimer -= (float)delta;

            // Flicker light
            if (FireLight != null)
            {
                FireLight.LightEnergy = 2.0f + Mathf.Sin((float)Time.GetTicksMsec() * 0.01f) * 0.3f;
            }

            if (_burnTimer <= 0)
            {
                Extinguish();
            }
        }

        if (_isCooking)
        {
            _cookTimer -= (float)delta;
            if (_cookTimer <= 0)
            {
                FinishCooking();
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (!_isLit)
            return "[E] Light fire (needs wood)";

        return "[E] Add fuel / Cook";
    }

    public bool CanInteract() => true;
    public bool RequiresHold() => !_isLit;

    public void Interact(PlayerController player)
    {
        var inventory = player.GetNode<InventorySystem>("InventorySystem");
        if (inventory == null) return;

        if (!_isLit)
        {
            // Try to light with wood
            if (inventory.HasItem("wood", 1))
            {
                inventory.RemoveItem("wood", 1);
                Light();
            }
            else
            {
                GD.Print("Need wood to light fire");
            }
        }
        else
        {
            // Add fuel or cook
            if (inventory.HasItem("wood", 1))
            {
                inventory.RemoveItem("wood", 1);
                _burnTimer += BurnDuration;
                GD.Print("Added fuel to fire");
            }
            else if (inventory.HasItem("raw_meat", 1))
            {
                inventory.RemoveItem("raw_meat", 1);
                StartCooking(player);
            }
        }
    }

    public void Light()
    {
        if (_isLit) return;

        _isLit = true;
        _burnTimer = BurnDuration;
        SetFireState(true);
        EmitSignal(SignalName.FireLit);

        GD.Print("Fire lit!");
    }

    public void Extinguish()
    {
        if (!_isLit) return;

        _isLit = false;
        _burnTimer = 0;
        SetFireState(false);
        EmitSignal(SignalName.FireExtinguished);

        GD.Print("Fire went out");
    }

    private void SetFireState(bool lit)
    {
        if (FireParticles != null)
            FireParticles.Emitting = lit;

        if (FireLight != null)
            FireLight.Visible = lit;

        if (FireSound != null)
        {
            if (lit && !FireSound.Playing)
                FireSound.Play();
            else if (!lit)
                FireSound.Stop();
        }
    }

    private void StartCooking(PlayerController player)
    {
        _isCooking = true;
        _cookTimer = CookingTime;
        _cookingPlayer = player;
        GD.Print("Cooking meat...");
    }

    private void FinishCooking()
    {
        _isCooking = false;

        if (_cookingPlayer != null && CookedMeatItem != null)
        {
            var inventory = _cookingPlayer.GetNode<InventorySystem>("InventorySystem");
            if (inventory != null)
            {
                inventory.AddItem(CookedMeatItem, 1);
                GD.Print("Meat is cooked! Added to inventory.");
            }
        }
        else
        {
            GD.Print("Meat is cooked! (No player or item configured)");
        }

        _cookingPlayer = null;
    }

    private void OnBodyEnteredWarmth(Node3D body)
    {
        if (body is PlayerController player && _isLit)
        {
            var stats = player.GetNode<PlayerStats>("PlayerStats");
            if (stats != null)
                stats.IsNearWarmthSource = true;
        }
    }

    private void OnBodyExitedWarmth(Node3D body)
    {
        if (body is PlayerController player)
        {
            var stats = player.GetNode<PlayerStats>("PlayerStats");
            if (stats != null)
                stats.IsNearWarmthSource = false;
        }
    }
}
