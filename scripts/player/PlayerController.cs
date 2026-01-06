using Godot;

namespace SurvivalIsland;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float WalkSpeed = 6.0f;
    [Export] public float SprintSpeed = 12.0f;
    [Export] public float CrouchSpeed = 3.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float MouseSensitivity = 0.002f;
    [Export] public float StandingHeight = 1.8f;
    [Export] public float CrouchingHeight = 1.0f;
    [Export] public float CrouchTransitionSpeed = 10.0f;

    [Export] public float HeadBobFrequency = 2.0f;
    [Export] public float HeadBobAmplitude = 0.05f;

    [Export] public float AttackDamage = 15f;
    [Export] public float AttackRange = 3.0f;
    [Export] public float AttackCooldown = 0.5f;

    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private CollisionShape3D _collisionShape = null!;
    private RayCast3D _interactionRay = null!;

    private float _gravity;
    private float _currentSpeed;
    private bool _isCrouching;
    private float _headBobTime;
    private Vector3 _originalCameraPosition;
    private float _targetHeight;
    private float _attackTimer;

    public bool IsSprinting { get; private set; }
    public bool IsCrouching => _isCrouching;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        _interactionRay = GetNode<RayCast3D>("Head/Camera3D/InteractionRay");

        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _currentSpeed = WalkSpeed;
        _targetHeight = StandingHeight;
        _originalCameraPosition = _camera.Position;

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _head.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);

            var headRotation = _head.Rotation;
            headRotation.X = Mathf.Clamp(headRotation.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));
            _head.Rotation = headRotation;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.MouseMode = Input.MouseModeEnum.Visible;
            else
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        // Click anywhere to recapture mouse
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                // Attack with left click
                TryAttack();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Fallback: click to recapture mouse (catches clicks that _Input might miss)
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;

        // Gravity
        if (!IsOnFloor())
        {
            velocity.Y -= _gravity * (float)delta;
        }

        // Jump
        if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_isCrouching)
        {
            velocity.Y = JumpVelocity;
        }

        // Crouch
        HandleCrouch(delta);

        // Sprint
        IsSprinting = Input.IsActionPressed("sprint") && !_isCrouching && IsOnFloor();

        // Determine speed
        if (_isCrouching)
            _currentSpeed = CrouchSpeed;
        else if (IsSprinting)
            _currentSpeed = SprintSpeed;
        else
            _currentSpeed = WalkSpeed;

        // Movement input
        var inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        var direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * _currentSpeed;
            velocity.Z = direction.Z * _currentSpeed;

            // Head bob when moving on ground
            if (IsOnFloor())
            {
                _headBobTime += (float)delta * _currentSpeed;
                ApplyHeadBob();
            }
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, _currentSpeed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, _currentSpeed);

            // Reset head bob
            _camera.Position = _camera.Position.Lerp(_originalCameraPosition, (float)delta * 10);
        }

        Velocity = velocity;
        MoveAndSlide();

        // Keep player within map bounds
        const float MapBound = 240f;
        var pos = GlobalPosition;
        bool outOfBounds = false;

        if (pos.X < -MapBound) { pos.X = -MapBound; outOfBounds = true; }
        if (pos.X > MapBound) { pos.X = MapBound; outOfBounds = true; }
        if (pos.Z < -MapBound) { pos.Z = -MapBound; outOfBounds = true; }
        if (pos.Z > MapBound) { pos.Z = MapBound; outOfBounds = true; }

        // Teleport back up if fallen below terrain
        if (pos.Y < -10f)
        {
            pos.Y = 20f;
            pos.X = Mathf.Clamp(pos.X, -50f, 50f);
            pos.Z = Mathf.Clamp(pos.Z, -50f, 50f);
        }

        if (outOfBounds || pos != GlobalPosition)
        {
            GlobalPosition = pos;
            Velocity = new Vector3(0, Velocity.Y, 0);
        }
    }

    private void HandleCrouch(double delta)
    {
        if (Input.IsActionPressed("crouch"))
        {
            _isCrouching = true;
            _targetHeight = CrouchingHeight;
        }
        else if (_isCrouching)
        {
            // Check if we can stand up
            if (CanStandUp())
            {
                _isCrouching = false;
                _targetHeight = StandingHeight;
            }
        }

        // Smoothly transition collision shape and head position
        if (_collisionShape.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = Mathf.Lerp(capsule.Height, _targetHeight, (float)delta * CrouchTransitionSpeed);
            _collisionShape.Position = new Vector3(0, capsule.Height / 2, 0);
            _head.Position = new Vector3(0, capsule.Height - 0.2f, 0);
        }
    }

    private bool CanStandUp()
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D();

        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.4f;
        capsule.Height = StandingHeight;

        query.Shape = capsule;
        query.Transform = new Transform3D(Basis.Identity, GlobalPosition + new Vector3(0, StandingHeight / 2, 0));
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectShape(query, 1);
        return result.Count == 0;
    }

    private void ApplyHeadBob()
    {
        var bobOffset = new Vector3(
            Mathf.Cos(_headBobTime * HeadBobFrequency / 2) * HeadBobAmplitude,
            Mathf.Sin(_headBobTime * HeadBobFrequency) * HeadBobAmplitude,
            0
        );

        _camera.Position = _originalCameraPosition + bobOffset;
    }

    public Node3D? GetInteractionTarget()
    {
        if (_interactionRay.IsColliding())
        {
            return _interactionRay.GetCollider() as Node3D;
        }
        return null;
    }

    public override void _Process(double delta)
    {
        _attackTimer -= (float)delta;
    }

    private void TryAttack()
    {
        if (_attackTimer > 0) return;
        _attackTimer = AttackCooldown;

        // Check if we hit something
        if (_interactionRay.IsColliding())
        {
            var collider = _interactionRay.GetCollider();

            // Attack animals
            if (collider is AnimalAI animal)
            {
                animal.TakeDamage(AttackDamage, this);
                GD.Print($"Hit animal for {AttackDamage} damage!");
            }
        }
    }
}
