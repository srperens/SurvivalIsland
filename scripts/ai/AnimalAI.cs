using Godot;

namespace SurvivalIsland;

public enum AnimalState { Idle, Wander, Alert, Chase, Flee, Attack }
public enum AnimalBehavior { Passive, Neutral, Aggressive }

public partial class AnimalAI : CharacterBody3D
{
    [Signal] public delegate void StateChangedEventHandler(AnimalState newState);
    [Signal] public delegate void DiedEventHandler();

    [Export] public AnimalBehavior Behavior { get; set; } = AnimalBehavior.Passive;
    [Export] public float WalkSpeed = 2.0f;
    [Export] public float RunSpeed = 6.0f;
    [Export] public float DetectionRange = 15.0f;
    [Export] public float AttackRange = 2.0f;
    [Export] public float AttackDamage = 10f;
    [Export] public float AttackCooldown = 1.5f;
    [Export] public float MaxHealth = 50f;
    [Export] public float WanderRadius = 20f;

    [Export] public ItemData? DropItem { get; set; }
    [Export] public int MinDropAmount = 1;
    [Export] public int MaxDropAmount = 3;

    private NavigationAgent3D? _navAgent;
    private Area3D? _detectionArea;

    private AnimalState _currentState = AnimalState.Idle;
    private float _health;
    private float _gravity;
    private float _stateTimer;
    private float _attackTimer;
    private PlayerController? _targetPlayer;
    private Vector3 _wanderTarget;
    private Vector3 _homePosition;

    public AnimalState CurrentState => _currentState;
    public float Health => _health;
    public bool IsAlive => _health > 0;

    public override void _Ready()
    {
        _health = MaxHealth;
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _homePosition = GlobalPosition;

        // Find child nodes
        _navAgent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        _detectionArea = GetNodeOrNull<Area3D>("DetectionArea");

        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered += OnBodyEnteredDetection;
            _detectionArea.BodyExited += OnBodyExitedDetection;
        }

        if (_navAgent != null)
        {
            _navAgent.VelocityComputed += OnVelocityComputed;
            _navAgent.PathDesiredDistance = 1.0f;
            _navAgent.TargetDesiredDistance = 1.0f;
        }

        SetState(AnimalState.Idle);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive) return;

        // Apply gravity
        var velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;

        _stateTimer += (float)delta;
        _attackTimer -= (float)delta;

        switch (_currentState)
        {
            case AnimalState.Idle:
                ProcessIdle(delta);
                break;
            case AnimalState.Wander:
                ProcessWander(delta);
                break;
            case AnimalState.Alert:
                ProcessAlert(delta);
                break;
            case AnimalState.Chase:
                ProcessChase(delta);
                break;
            case AnimalState.Flee:
                ProcessFlee(delta);
                break;
            case AnimalState.Attack:
                ProcessAttack(delta);
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    private void ProcessIdle(double delta)
    {
        // Randomly start wandering
        if (_stateTimer > GD.Randf() * 5 + 2)
        {
            SetState(AnimalState.Wander);
        }
    }

    private void ProcessWander(double delta)
    {
        if (_navAgent == null) return;

        if (_navAgent.IsNavigationFinished())
        {
            SetState(AnimalState.Idle);
            return;
        }

        MoveTowardsTarget(WalkSpeed);

        // Check if wandered long enough
        if (_stateTimer > 10)
        {
            SetState(AnimalState.Idle);
        }
    }

    private void ProcessAlert(double delta)
    {
        if (_targetPlayer == null)
        {
            SetState(AnimalState.Idle);
            return;
        }

        // Face the player
        LookAt(new Vector3(_targetPlayer.GlobalPosition.X, GlobalPosition.Y, _targetPlayer.GlobalPosition.Z));

        // Decide action after alert period
        if (_stateTimer > 1.5f)
        {
            switch (Behavior)
            {
                case AnimalBehavior.Passive:
                    SetState(AnimalState.Flee);
                    break;
                case AnimalBehavior.Aggressive:
                    SetState(AnimalState.Chase);
                    break;
                case AnimalBehavior.Neutral:
                    // Neutral animals only attack if provoked
                    SetState(AnimalState.Idle);
                    break;
            }
        }
    }

    private void ProcessChase(double delta)
    {
        if (_targetPlayer == null || _navAgent == null)
        {
            SetState(AnimalState.Idle);
            return;
        }

        float distanceToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);

        if (distanceToPlayer > DetectionRange * 1.5f)
        {
            // Lost target
            SetState(AnimalState.Idle);
            return;
        }

        if (distanceToPlayer <= AttackRange)
        {
            SetState(AnimalState.Attack);
            return;
        }

        // Update navigation target
        _navAgent.TargetPosition = _targetPlayer.GlobalPosition;
        MoveTowardsTarget(RunSpeed);
    }

    private void ProcessFlee(double delta)
    {
        if (_targetPlayer == null || _navAgent == null)
        {
            SetState(AnimalState.Idle);
            return;
        }

        float distanceToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);

        if (distanceToPlayer > DetectionRange * 2)
        {
            // Safe distance
            SetState(AnimalState.Idle);
            return;
        }

        // Flee away from player
        Vector3 fleeDirection = (GlobalPosition - _targetPlayer.GlobalPosition).Normalized();
        Vector3 fleeTarget = GlobalPosition + fleeDirection * 20;
        _navAgent.TargetPosition = fleeTarget;

        MoveTowardsTarget(RunSpeed);
    }

    private void ProcessAttack(double delta)
    {
        if (_targetPlayer == null)
        {
            SetState(AnimalState.Idle);
            return;
        }

        float distanceToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);

        if (distanceToPlayer > AttackRange * 1.5f)
        {
            SetState(AnimalState.Chase);
            return;
        }

        // Face player
        LookAt(new Vector3(_targetPlayer.GlobalPosition.X, GlobalPosition.Y, _targetPlayer.GlobalPosition.Z));

        // Attack if cooldown is ready
        if (_attackTimer <= 0)
        {
            PerformAttack();
            _attackTimer = AttackCooldown;
        }
    }

    private void MoveTowardsTarget(float speed)
    {
        if (_navAgent == null || _navAgent.IsNavigationFinished()) return;

        Vector3 nextPosition = _navAgent.GetNextPathPosition();
        Vector3 direction = (nextPosition - GlobalPosition).Normalized();
        direction.Y = 0;

        var velocity = Velocity;
        velocity.X = direction.X * speed;
        velocity.Z = direction.Z * speed;
        Velocity = velocity;

        // Face movement direction
        if (direction.LengthSquared() > 0.01f)
        {
            LookAt(GlobalPosition + direction);
        }

        _navAgent.Velocity = velocity;
    }

    private void OnVelocityComputed(Vector3 safeVelocity)
    {
        Velocity = new Vector3(safeVelocity.X, Velocity.Y, safeVelocity.Z);
    }

    private void PerformAttack()
    {
        if (_targetPlayer == null) return;

        var stats = _targetPlayer.GetNode<PlayerStats>("PlayerStats");
        if (stats != null)
        {
            stats.TakeDamage(AttackDamage);
            GD.Print($"Animal attacked player for {AttackDamage} damage!");
        }
    }

    private void SetState(AnimalState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        _stateTimer = 0;

        // Setup new state
        switch (newState)
        {
            case AnimalState.Wander:
                SetWanderTarget();
                break;
        }

        EmitSignal(SignalName.StateChanged, (int)newState);
    }

    private void SetWanderTarget()
    {
        if (_navAgent == null) return;

        // Random point within wander radius of home
        Vector2 randomOffset = new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f).Normalized() * GD.Randf() * WanderRadius;
        _wanderTarget = _homePosition + new Vector3(randomOffset.X, 0, randomOffset.Y);

        _navAgent.TargetPosition = _wanderTarget;
    }

    private void OnBodyEnteredDetection(Node3D body)
    {
        if (body is PlayerController player)
        {
            _targetPlayer = player;

            if (_currentState == AnimalState.Idle || _currentState == AnimalState.Wander)
            {
                SetState(AnimalState.Alert);
            }
        }
    }

    private void OnBodyExitedDetection(Node3D body)
    {
        if (body == _targetPlayer)
        {
            if (_currentState == AnimalState.Alert)
            {
                _targetPlayer = null;
                SetState(AnimalState.Idle);
            }
        }
    }

    public void TakeDamage(float damage, PlayerController? attacker = null)
    {
        _health -= damage;

        // Track the attacker for loot drops
        if (attacker != null)
            _targetPlayer = attacker;

        if (_health <= 0)
        {
            Die();
        }
        else
        {
            // Neutral animals become aggressive when attacked
            if (Behavior == AnimalBehavior.Neutral && _targetPlayer != null)
            {
                Behavior = AnimalBehavior.Aggressive;
                SetState(AnimalState.Chase);
            }
        }
    }

    private void Die()
    {
        EmitSignal(SignalName.Died);

        // Drop loot to player who killed the animal
        if (DropItem != null && _targetPlayer != null)
        {
            var inventory = _targetPlayer.GetNode<InventorySystem>("InventorySystem");
            if (inventory != null)
            {
                int amount = GD.RandRange(MinDropAmount, MaxDropAmount);
                inventory.AddItem(DropItem, amount);
                GD.Print($"Got {amount}x {DropItem.DisplayName} from hunting!");
            }
        }

        // Play death animation then remove
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.Zero, 0.5f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
