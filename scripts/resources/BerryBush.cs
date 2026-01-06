using Godot;

namespace SurvivalIsland;

public partial class BerryBush : StaticBody3D, IInteractable
{
    [Export] public ItemData? BerryItem { get; set; }
    [Export] public int MinBerries = 3;
    [Export] public int MaxBerries = 8;
    [Export] public float RegrowTime = 90f; // 1.5 minutes to regrow

    private int _berriesAvailable;
    private bool _canHarvest = true;
    private float _regrowTimer;

    public override void _Ready()
    {
        CollisionLayer = 4; // Interactables
        _berriesAvailable = GD.RandRange(MinBerries, MaxBerries);
        _canHarvest = true;
        GD.Print($"BerryBush ready: {_berriesAvailable} berries, canHarvest: {_canHarvest}");
    }

    public override void _Process(double delta)
    {
        if (!_canHarvest)
        {
            _regrowTimer -= (float)delta;
            if (_regrowTimer <= 0)
            {
                _berriesAvailable = GD.RandRange(MinBerries, MaxBerries);
                _canHarvest = true;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (!_canHarvest)
            return "[Väntar på bär...]";

        return $"[E] Plocka bär ({_berriesAvailable} kvar)";
    }

    public bool CanInteract()
    {
        bool result = _canHarvest && _berriesAvailable > 0;
        if (!result)
            GD.Print($"BerryBush CanInteract=false: _canHarvest={_canHarvest}, _berriesAvailable={_berriesAvailable}");
        return result;
    }
    public bool RequiresHold() => false;

    public void Interact(PlayerController player)
    {
        if (!CanInteract() || BerryItem == null) return;

        var inventory = player.GetNode<InventorySystem>("InventorySystem");
        if (inventory == null) return;

        // Pick multiple berries at once
        int toPick = Mathf.Min(3, _berriesAvailable);

        if (inventory.AddItem(BerryItem, toPick))
        {
            _berriesAvailable -= toPick;
            GD.Print($"Picked {toPick} berries! {_berriesAvailable} left");

            if (_berriesAvailable <= 0)
            {
                _canHarvest = false;
                _regrowTimer = RegrowTime;
            }
        }
        else
        {
            GD.Print("Inventory full!");
        }
    }
}
