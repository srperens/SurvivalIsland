using Godot;

namespace SurvivalIsland;

public partial class PlayerInteraction : Node
{
    [Signal] public delegate void InteractionPromptChangedEventHandler(string prompt);
    [Signal] public delegate void InteractionProgressChangedEventHandler(float progress);

    [Export] public float InteractionDistance = 3.0f;
    [Export] public float HoldInteractionTime = 1.0f;

    private PlayerController _player = null!;
    private Camera3D _camera = null!;
    private IInteractable? _currentTarget;
    private float _holdProgress;
    private bool _isHolding;

    public override void _Ready()
    {
        _player = GetParent<PlayerController>();
        _camera = _player.GetNode<Camera3D>("Head/Camera3D");
    }

    public override void _Process(double delta)
    {
        UpdateInteractionTarget();
        HandleInteractionInput(delta);
    }

    private void UpdateInteractionTarget()
    {
        var spaceState = _player.GetWorld3D().DirectSpaceState;
        var from = _camera.GlobalPosition;
        var direction = -_camera.GlobalTransform.Basis.Z;
        var to = from + direction * InteractionDistance;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 0b1101; // Layers 1, 3, 4 (World, Interactables, Animals)
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            var hitPos = result["position"].AsVector3();

            // Check if the collider or its parent implements IInteractable
            IInteractable? interactable = null;

            if (collider is IInteractable directInteractable)
            {
                interactable = directInteractable;
            }
            else if (collider is Node node)
            {
                // Search up the tree for an IInteractable
                var current = node;
                while (current != null)
                {
                    if (current is IInteractable parentInteractable)
                    {
                        interactable = parentInteractable;
                        break;
                    }
                    current = current.GetParent();
                }
            }

            if (interactable != null)
            {
                bool canInteract = interactable.CanInteract();
                GD.Print($"Hit: {(interactable as Node)?.Name} at {hitPos}, CanInteract: {canInteract}");

                if (canInteract)
                {
                    if (_currentTarget != interactable)
                    {
                        _currentTarget = interactable;
                        _holdProgress = 0;
                        EmitSignal(SignalName.InteractionPromptChanged, interactable.GetInteractionPrompt());
                    }
                }
                else
                {
                    ClearTarget();
                }
            }
            else
            {
                ClearTarget();
            }
        }
        else
        {
            ClearTarget();
        }
    }

    private void ClearTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget = null;
            _holdProgress = 0;
            _isHolding = false;
            EmitSignal(SignalName.InteractionPromptChanged, "");
            EmitSignal(SignalName.InteractionProgressChanged, 0f);
        }
    }

    private void HandleInteractionInput(double delta)
    {
        if (Input.IsActionJustPressed("interact"))
        {
            GD.Print($"E pressed! Target: {(_currentTarget != null ? "yes" : "no")}");
        }

        if (_currentTarget == null) return;

        if (Input.IsActionJustPressed("interact"))
        {
            GD.Print($"Interacting with: {(_currentTarget as Node)?.Name}");
            if (!_currentTarget.RequiresHold())
            {
                _currentTarget.Interact(_player);
            }
            else
            {
                _isHolding = true;
            }
        }

        if (Input.IsActionJustReleased("interact"))
        {
            _isHolding = false;
            _holdProgress = 0;
            EmitSignal(SignalName.InteractionProgressChanged, 0f);
        }

        if (_isHolding && _currentTarget.RequiresHold())
        {
            _holdProgress += (float)delta / HoldInteractionTime;
            EmitSignal(SignalName.InteractionProgressChanged, _holdProgress);

            if (_holdProgress >= 1.0f)
            {
                _currentTarget.Interact(_player);
                _holdProgress = 0;
                _isHolding = false;
                EmitSignal(SignalName.InteractionProgressChanged, 0f);

                // Re-check if target is still valid
                if (!_currentTarget.CanInteract())
                {
                    ClearTarget();
                }
            }
        }
    }
}

public interface IInteractable
{
    string GetInteractionPrompt();
    bool CanInteract();
    bool RequiresHold();
    void Interact(PlayerController player);
}
