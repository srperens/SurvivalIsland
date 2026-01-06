using Godot;

namespace SurvivalIsland;

public enum ItemType
{
    Resource,
    Tool,
    Food,
    Drink,
    Buildable
}

[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public Texture2D? Icon { get; set; }
    [Export] public ItemType Type { get; set; }
    [Export] public int MaxStack { get; set; } = 64;

    // For food/drink
    [Export] public float HungerRestore { get; set; }
    [Export] public float ThirstRestore { get; set; }
    [Export] public float HealthRestore { get; set; }
    [Export] public bool RequiresCooking { get; set; }

    // For tools
    [Export] public float ToolPower { get; set; } = 1.0f;
    [Export] public float ToolDurability { get; set; } = 100f;

    // For buildables
    [Export] public PackedScene? BuildableScene { get; set; }
}
