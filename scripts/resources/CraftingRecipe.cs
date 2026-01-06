using Godot;
using Godot.Collections;

namespace SurvivalIsland;

[GlobalClass]
public partial class CraftingRecipe : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public ItemData? Result { get; set; }
    [Export] public int ResultAmount { get; set; } = 1;
    [Export] public Array<CraftingIngredient> Ingredients { get; set; } = new();
    [Export] public bool RequiresCampfire { get; set; }
    [Export] public bool RequiresWorkbench { get; set; }
    [Export] public float CraftingTime { get; set; } = 1.0f;
}

[GlobalClass]
public partial class CraftingIngredient : Resource
{
    [Export] public ItemData? Item { get; set; }
    [Export] public int Amount { get; set; } = 1;
}
