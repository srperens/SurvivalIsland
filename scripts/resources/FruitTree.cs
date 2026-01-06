using Godot;

namespace SurvivalIsland;

public partial class FruitTree : StaticBody3D, IInteractable
{
    [Export] public ItemData? FruitItem { get; set; }
    [Export] public int MinFruit = 2;
    [Export] public int MaxFruit = 5;
    [Export] public float RegrowTime = 120f; // 2 minutes to regrow

    private int _fruitsAvailable;
    private bool _canHarvest = true;
    private float _regrowTimer;

    public override void _Ready()
    {
        CollisionLayer = 4; // Interactables
        _fruitsAvailable = GD.RandRange(MinFruit, MaxFruit);
        _canHarvest = true;
        GD.Print($"FruitTree ready: {_fruitsAvailable} fruits, canHarvest: {_canHarvest}");
    }

    public override void _Process(double delta)
    {
        if (!_canHarvest)
        {
            _regrowTimer -= (float)delta;
            if (_regrowTimer <= 0)
            {
                _fruitsAvailable = GD.RandRange(MinFruit, MaxFruit);
                _canHarvest = true;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (!_canHarvest)
            return $"[Väntar på frukt...]";

        return $"[E] Plocka frukt ({_fruitsAvailable} kvar)";
    }

    public bool CanInteract()
    {
        bool result = _canHarvest && _fruitsAvailable > 0;
        if (!result)
            GD.Print($"FruitTree CanInteract=false: _canHarvest={_canHarvest}, _fruitsAvailable={_fruitsAvailable}");
        return result;
    }
    public bool RequiresHold() => false;

    public void Interact(PlayerController player)
    {
        if (!CanInteract() || FruitItem == null) return;

        var inventory = player.GetNode<InventorySystem>("InventorySystem");
        if (inventory == null) return;

        if (inventory.AddItem(FruitItem, 1))
        {
            _fruitsAvailable--;
            GD.Print($"Picked {FruitItem.DisplayName}! {_fruitsAvailable} left");

            if (_fruitsAvailable <= 0)
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
