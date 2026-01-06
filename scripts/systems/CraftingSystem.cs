using Godot;
using Godot.Collections;
using System.Linq;

namespace SurvivalIsland;

public partial class CraftingSystem : Node
{
    [Signal] public delegate void CraftingStartedEventHandler(string recipeId);
    [Signal] public delegate void CraftingProgressEventHandler(float progress);
    [Signal] public delegate void CraftingCompletedEventHandler(string recipeId);
    [Signal] public delegate void CraftingFailedEventHandler(string reason);

    [Export] public Array<CraftingRecipe> Recipes { get; set; } = new();

    private InventorySystem _inventory = null!;
    private bool _isCrafting;
    private CraftingRecipe? _currentRecipe;
    private float _craftingProgress;

    public bool IsNearCampfire { get; set; }
    public bool IsNearWorkbench { get; set; }

    public override void _Ready()
    {
        // Inventory will be set when player is ready
    }

    public void Initialize(InventorySystem inventory)
    {
        _inventory = inventory;
    }

    public override void _Process(double delta)
    {
        if (_isCrafting && _currentRecipe != null)
        {
            _craftingProgress += (float)delta / _currentRecipe.CraftingTime;
            EmitSignal(SignalName.CraftingProgress, _craftingProgress);

            if (_craftingProgress >= 1.0f)
            {
                CompleteCrafting();
            }
        }
    }

    public Array<CraftingRecipe> GetAvailableRecipes()
    {
        var available = new Array<CraftingRecipe>();

        foreach (var recipe in Recipes)
        {
            if (CanCraft(recipe))
            {
                available.Add(recipe);
            }
        }

        return available;
    }

    public bool CanCraft(CraftingRecipe recipe)
    {
        if (recipe.RequiresCampfire && !IsNearCampfire)
            return false;

        if (recipe.RequiresWorkbench && !IsNearWorkbench)
            return false;

        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.Item == null) continue;
            if (!_inventory.HasItem(ingredient.Item.Id, ingredient.Amount))
                return false;
        }

        return true;
    }

    public bool StartCrafting(CraftingRecipe recipe)
    {
        if (_isCrafting)
        {
            EmitSignal(SignalName.CraftingFailed, "Already crafting");
            return false;
        }

        if (!CanCraft(recipe))
        {
            EmitSignal(SignalName.CraftingFailed, "Missing ingredients or station");
            return false;
        }

        // Consume ingredients
        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.Item != null)
            {
                _inventory.RemoveItem(ingredient.Item.Id, ingredient.Amount);
            }
        }

        _currentRecipe = recipe;
        _craftingProgress = 0;
        _isCrafting = true;

        EmitSignal(SignalName.CraftingStarted, recipe.Id);
        return true;
    }

    public void CancelCrafting()
    {
        if (!_isCrafting || _currentRecipe == null) return;

        // Refund ingredients
        foreach (var ingredient in _currentRecipe.Ingredients)
        {
            if (ingredient.Item != null)
            {
                _inventory.AddItem(ingredient.Item, ingredient.Amount);
            }
        }

        _isCrafting = false;
        _currentRecipe = null;
        _craftingProgress = 0;

        EmitSignal(SignalName.CraftingProgress, 0f);
    }

    private void CompleteCrafting()
    {
        if (_currentRecipe?.Result == null) return;

        if (_inventory.AddItem(_currentRecipe.Result, _currentRecipe.ResultAmount))
        {
            EmitSignal(SignalName.CraftingCompleted, _currentRecipe.Id);
        }
        else
        {
            EmitSignal(SignalName.CraftingFailed, "Inventory full");
            // Could spawn item on ground instead
        }

        _isCrafting = false;
        var completedRecipe = _currentRecipe;
        _currentRecipe = null;
        _craftingProgress = 0;
    }

    public CraftingRecipe? GetRecipeById(string id)
    {
        return Recipes.FirstOrDefault(r => r.Id == id);
    }
}
