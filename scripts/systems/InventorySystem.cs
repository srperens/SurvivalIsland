using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SurvivalIsland;

public partial class InventorySystem : Node
{
    [Signal] public delegate void InventoryChangedEventHandler();
    [Signal] public delegate void HotbarSelectionChangedEventHandler(int slot);
    [Signal] public delegate void ItemAddedEventHandler(string itemId, int amount);
    [Signal] public delegate void ItemRemovedEventHandler(string itemId, int amount);

    [Export] public int InventoryWidth = 10;
    [Export] public int InventoryHeight = 4;
    [Export] public int HotbarSize = 5;
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> StartingItems { get; set; } = new();

    private InventorySlot[,] _inventory = null!;
    private InventorySlot[] _hotbar = null!;
    private int _selectedHotbarSlot;

    public int SelectedHotbarSlot
    {
        get => _selectedHotbarSlot;
        set
        {
            _selectedHotbarSlot = Mathf.Clamp(value, 0, HotbarSize - 1);
            EmitSignal(SignalName.HotbarSelectionChanged, _selectedHotbarSlot);
        }
    }

    public ItemData? SelectedItem => _hotbar[_selectedHotbarSlot].Item;

    public override void _Ready()
    {
        _inventory = new InventorySlot[InventoryWidth, InventoryHeight];
        _hotbar = new InventorySlot[HotbarSize];

        for (int x = 0; x < InventoryWidth; x++)
            for (int y = 0; y < InventoryHeight; y++)
                _inventory[x, y] = new InventorySlot();

        for (int i = 0; i < HotbarSize; i++)
            _hotbar[i] = new InventorySlot();

        // Add starting items
        GD.Print($"StartingItems count: {StartingItems.Count}");
        foreach (var entry in StartingItems)
        {
            GD.Print($"Entry keys: {string.Join(", ", entry.Keys)}");
            if (entry.TryGetValue("item", out var itemVar) && entry.TryGetValue("amount", out var amountVar))
            {
                GD.Print($"itemVar type: {itemVar.VariantType}, amountVar: {amountVar}");
                var item = itemVar.As<ItemData>();
                if (item != null)
                {
                    int amount = amountVar.AsInt32();
                    AddItem(item, amount);
                    GD.Print($"Added starting item: {item.DisplayName} x{amount}");
                }
                else
                {
                    GD.Print("Failed to cast item to ItemData");
                }
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Hotbar selection with number keys
        for (int i = 0; i < HotbarSize; i++)
        {
            if (@event.IsActionPressed($"hotbar_{i + 1}"))
            {
                SelectedHotbarSlot = i;
                break;
            }
        }

        // Mouse wheel for hotbar
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                SelectedHotbarSlot = (SelectedHotbarSlot - 1 + HotbarSize) % HotbarSize;
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                SelectedHotbarSlot = (SelectedHotbarSlot + 1) % HotbarSize;
        }
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        int remaining = amount;

        // First try to stack with existing items
        remaining = TryStackItem(item, remaining);

        // Then try to add to empty slots
        if (remaining > 0)
            remaining = TryAddToEmptySlots(item, remaining);

        if (remaining < amount)
        {
            EmitSignal(SignalName.ItemAdded, item.Id, amount - remaining);
            EmitSignal(SignalName.InventoryChanged);
        }

        return remaining == 0;
    }

    private int TryStackItem(ItemData item, int amount)
    {
        // Try hotbar first
        foreach (var slot in _hotbar)
        {
            if (slot.Item?.Id == item.Id && slot.Amount < item.MaxStack)
            {
                int canAdd = item.MaxStack - slot.Amount;
                int toAdd = Mathf.Min(canAdd, amount);
                slot.Amount += toAdd;
                amount -= toAdd;
                if (amount == 0) return 0;
            }
        }

        // Then inventory
        for (int y = 0; y < InventoryHeight; y++)
        {
            for (int x = 0; x < InventoryWidth; x++)
            {
                var slot = _inventory[x, y];
                if (slot.Item?.Id == item.Id && slot.Amount < item.MaxStack)
                {
                    int canAdd = item.MaxStack - slot.Amount;
                    int toAdd = Mathf.Min(canAdd, amount);
                    slot.Amount += toAdd;
                    amount -= toAdd;
                    if (amount == 0) return 0;
                }
            }
        }

        return amount;
    }

    private int TryAddToEmptySlots(ItemData item, int amount)
    {
        // Try hotbar first
        foreach (var slot in _hotbar)
        {
            if (slot.Item == null)
            {
                int toAdd = Mathf.Min(item.MaxStack, amount);
                slot.Item = item;
                slot.Amount = toAdd;
                amount -= toAdd;
                if (amount == 0) return 0;
            }
        }

        // Then inventory
        for (int y = 0; y < InventoryHeight; y++)
        {
            for (int x = 0; x < InventoryWidth; x++)
            {
                var slot = _inventory[x, y];
                if (slot.Item == null)
                {
                    int toAdd = Mathf.Min(item.MaxStack, amount);
                    slot.Item = item;
                    slot.Amount = toAdd;
                    amount -= toAdd;
                    if (amount == 0) return 0;
                }
            }
        }

        return amount;
    }

    public bool RemoveItem(string itemId, int amount = 1)
    {
        int remaining = amount;
        var slotsToUpdate = new List<InventorySlot>();

        // Search all slots
        var allSlots = _hotbar.Concat(_inventory.Cast<InventorySlot>());

        foreach (var slot in allSlots)
        {
            if (slot.Item?.Id == itemId)
            {
                int toRemove = Mathf.Min(slot.Amount, remaining);
                slot.Amount -= toRemove;
                remaining -= toRemove;

                if (slot.Amount == 0)
                    slot.Item = null;

                if (remaining == 0) break;
            }
        }

        if (remaining < amount)
        {
            EmitSignal(SignalName.ItemRemoved, itemId, amount - remaining);
            EmitSignal(SignalName.InventoryChanged);
        }

        return remaining == 0;
    }

    public int GetItemCount(string itemId)
    {
        int count = 0;

        foreach (var slot in _hotbar)
            if (slot.Item?.Id == itemId)
                count += slot.Amount;

        for (int x = 0; x < InventoryWidth; x++)
            for (int y = 0; y < InventoryHeight; y++)
                if (_inventory[x, y].Item?.Id == itemId)
                    count += _inventory[x, y].Amount;

        return count;
    }

    public bool HasItem(string itemId, int amount = 1)
    {
        return GetItemCount(itemId) >= amount;
    }

    public InventorySlot GetHotbarSlot(int index) => _hotbar[index];
    public InventorySlot GetInventorySlot(int x, int y) => _inventory[x, y];

    public void SwapSlots(InventorySlot slot1, InventorySlot slot2)
    {
        (slot1.Item, slot2.Item) = (slot2.Item, slot1.Item);
        (slot1.Amount, slot2.Amount) = (slot2.Amount, slot1.Amount);
        EmitSignal(SignalName.InventoryChanged);
    }
}

public class InventorySlot
{
    public ItemData? Item { get; set; }
    public int Amount { get; set; }

    public bool IsEmpty => Item == null || Amount == 0;
}
