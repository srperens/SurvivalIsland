using Godot;

namespace SurvivalIsland;

public partial class TreeResource : ResourceBase
{
    [Export] public ItemData? WoodItem { get; set; }
    [Export] public ItemData? BranchItem { get; set; }
    [Export] public int WoodAmount = 5;
    [Export] public int BranchAmount = 3;
    [Export] public float FallDuration = 2.0f;

    private bool _isFalling;
    private MeshInstance3D? _treeMesh;

    public override void _Ready()
    {
        base._Ready();
        RequiresTool = true;
        RequiredToolType = "axe";
        HarvestTime = 3.0f;
        DestroyOnHarvest = false;
        MaxHarvests = 1;
        InteractionVerb = "Chop";

        _treeMesh = GetNodeOrNull<MeshInstance3D>("TreeMesh");
    }

    public override string GetInteractionPrompt()
    {
        if (_isFalling) return "";
        return base.GetInteractionPrompt();
    }

    public override bool CanInteract()
    {
        return !_isFalling && base.CanInteract();
    }

    protected override void Harvest(PlayerController player)
    {
        if (_isFalling) return;

        var inventory = player.GetNode<InventorySystem>("InventorySystem");
        if (inventory == null) return;

        // Start falling animation
        _isFalling = true;
        StartFallingAnimation(player, inventory);
    }

    private async void StartFallingAnimation(PlayerController player, InventorySystem inventory)
    {
        // Determine fall direction (away from player)
        Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0;
        Vector3 fallDirection = -toPlayer.Normalized();

        // Create tween for falling
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.In);

        // Calculate rotation axis (perpendicular to fall direction)
        Vector3 rotationAxis = fallDirection.Cross(Vector3.Up).Normalized();

        // Animate rotation
        var targetRotation = Rotation + rotationAxis * Mathf.DegToRad(90);
        tween.TweenProperty(this, "rotation", targetRotation, FallDuration);

        await ToSignal(tween, Tween.SignalName.Finished);

        // Give items
        if (WoodItem != null)
            inventory.AddItem(WoodItem, WoodAmount);

        if (BranchItem != null)
            inventory.AddItem(BranchItem, BranchAmount);

        GD.Print($"Tree felled! Got {WoodAmount} wood and {BranchAmount} branches");

        // Wait a moment then disappear
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

        var fadeTween = CreateTween();
        fadeTween.TweenProperty(this, "scale", Vector3.Zero, 0.5f);
        fadeTween.TweenCallback(Callable.From(QueueFree));
    }
}
