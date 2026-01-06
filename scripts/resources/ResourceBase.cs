using Godot;

namespace SurvivalIsland;

public partial class ResourceBase : StaticBody3D, IInteractable
{
    [Export] public ItemData? DropItem { get; set; }
    [Export] public int MinDropAmount = 1;
    [Export] public int MaxDropAmount = 3;
    [Export] public bool RequiresTool { get; set; }
    [Export] public string RequiredToolType { get; set; } = "";
    [Export] public float HarvestTime = 1.0f;
    [Export] public bool DestroyOnHarvest { get; set; } = true;
    [Export] public int MaxHarvests { get; set; } = 1;
    [Export] public string InteractionVerb { get; set; } = "Pick up";

    private int _harvestsRemaining;
    private bool _canHarvest = true;

    public override void _Ready()
    {
        _harvestsRemaining = MaxHarvests;
        CollisionLayer = 4; // Interactables layer
    }

    public virtual string GetInteractionPrompt()
    {
        if (!_canHarvest) return "";

        if (RequiresTool && !string.IsNullOrEmpty(RequiredToolType))
            return $"[E] {InteractionVerb} (Requires {RequiredToolType})";

        return $"[E] {InteractionVerb}";
    }

    public virtual bool CanInteract()
    {
        return _canHarvest && _harvestsRemaining > 0;
    }

    public virtual bool RequiresHold()
    {
        return HarvestTime > 0.5f;
    }

    public virtual void Interact(PlayerController player)
    {
        if (!CanInteract()) return;

        // Check for required tool
        if (RequiresTool)
        {
            var inventory = player.GetNode<InventorySystem>("InventorySystem");
            var selectedItem = inventory?.SelectedItem;

            if (selectedItem == null || selectedItem.Type != ItemType.Tool)
            {
                GD.Print("Need a tool to harvest this");
                return;
            }

            // Check tool type matches
            if (!string.IsNullOrEmpty(RequiredToolType) &&
                !selectedItem.Id.Contains(RequiredToolType.ToLower()))
            {
                GD.Print($"Need a {RequiredToolType} to harvest this");
                return;
            }
        }

        Harvest(player);
    }

    protected virtual void Harvest(PlayerController player)
    {
        if (DropItem == null) return;

        var inventory = player.GetNode<InventorySystem>("InventorySystem");
        if (inventory == null) return;

        int amount = GD.RandRange(MinDropAmount, MaxDropAmount);

        if (inventory.AddItem(DropItem, amount))
        {
            GD.Print($"Collected {amount}x {DropItem.DisplayName}");
        }
        else
        {
            GD.Print("Inventory full!");
            return;
        }

        _harvestsRemaining--;

        if (_harvestsRemaining <= 0 || DestroyOnHarvest)
        {
            OnDepleted();
        }
    }

    protected virtual void OnDepleted()
    {
        _canHarvest = false;

        if (DestroyOnHarvest)
        {
            // Play particle effect, then queue free
            var tween = CreateTween();
            tween.TweenProperty(this, "scale", Vector3.Zero, 0.3f);
            tween.TweenCallback(Callable.From(QueueFree));
        }
    }
}
