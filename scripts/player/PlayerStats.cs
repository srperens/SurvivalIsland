using Godot;

namespace SurvivalIsland;

public partial class PlayerStats : Node
{
    [Signal] public delegate void HealthChangedEventHandler(float newValue, float maxValue);
    [Signal] public delegate void HungerChangedEventHandler(float newValue, float maxValue);
    [Signal] public delegate void ThirstChangedEventHandler(float newValue, float maxValue);
    [Signal] public delegate void WarmthChangedEventHandler(float newValue, float maxValue);
    [Signal] public delegate void PlayerDiedEventHandler();

    [Export] public float MaxHealth = 100f;
    [Export] public float MaxHunger = 100f;
    [Export] public float MaxThirst = 100f;
    [Export] public float MaxWarmth = 100f;

    [Export] public float HungerDecreaseRate = 1f / 30f; // Per second
    [Export] public float ThirstDecreaseRate = 1f / 20f; // Per second
    [Export] public float WarmthDecreaseRate = 0f; // Set by weather/environment

    [Export] public float DamageFromNoHunger = 2f; // Per second
    [Export] public float DamageFromNoThirst = 3f; // Per second
    [Export] public float DamageFromCold = 1.5f; // Per second

    private float _health;
    private float _hunger;
    private float _thirst;
    private float _warmth;

    public float Health
    {
        get => _health;
        set
        {
            _health = Mathf.Clamp(value, 0, MaxHealth);
            EmitSignal(SignalName.HealthChanged, _health, MaxHealth);
            if (_health <= 0)
                EmitSignal(SignalName.PlayerDied);
        }
    }

    public float Hunger
    {
        get => _hunger;
        set
        {
            _hunger = Mathf.Clamp(value, 0, MaxHunger);
            EmitSignal(SignalName.HungerChanged, _hunger, MaxHunger);
        }
    }

    public float Thirst
    {
        get => _thirst;
        set
        {
            _thirst = Mathf.Clamp(value, 0, MaxThirst);
            EmitSignal(SignalName.ThirstChanged, _thirst, MaxThirst);
        }
    }

    public float Warmth
    {
        get => _warmth;
        set
        {
            _warmth = Mathf.Clamp(value, 0, MaxWarmth);
            EmitSignal(SignalName.WarmthChanged, _warmth, MaxWarmth);
        }
    }

    public bool IsAlive => Health > 0;
    public bool IsNearWarmthSource { get; set; }
    public float AmbientTemperature { get; set; } = 20f; // Celsius

    public override void _Ready()
    {
        _health = MaxHealth;
        _hunger = MaxHunger;
        _thirst = MaxThirst;
        _warmth = MaxWarmth;

        EmitSignal(SignalName.HealthChanged, _health, MaxHealth);
        EmitSignal(SignalName.HungerChanged, _hunger, MaxHunger);
        EmitSignal(SignalName.ThirstChanged, _thirst, MaxThirst);
        EmitSignal(SignalName.WarmthChanged, _warmth, MaxWarmth);
    }

    public override void _Process(double delta)
    {
        if (!IsAlive) return;

        float dt = (float)delta;

        // Decrease hunger and thirst over time
        Hunger -= HungerDecreaseRate * dt;
        Thirst -= ThirstDecreaseRate * dt;

        // Handle warmth based on environment
        UpdateWarmth(dt);

        // Apply damage from low stats
        if (Hunger <= 0)
            Health -= DamageFromNoHunger * dt;

        if (Thirst <= 0)
            Health -= DamageFromNoThirst * dt;

        if (Warmth <= 20) // Cold threshold
            Health -= DamageFromCold * dt * (1 - Warmth / 20);
    }

    private void UpdateWarmth(float delta)
    {
        float targetWarmth;

        if (IsNearWarmthSource)
        {
            targetWarmth = MaxWarmth;
        }
        else
        {
            // Warmth based on ambient temperature
            // 20°C = comfortable (100 warmth)
            // 0°C = very cold (0 warmth)
            targetWarmth = Mathf.Clamp((AmbientTemperature / 20f) * MaxWarmth, 0, MaxWarmth);
        }

        // Gradually move towards target warmth
        Warmth = Mathf.MoveToward(Warmth, targetWarmth, 5f * delta);
    }

    public void Eat(float hungerRestore, float healthRestore = 0)
    {
        Hunger += hungerRestore;
        if (healthRestore > 0)
            Health += healthRestore;
    }

    public void Drink(float thirstRestore)
    {
        Thirst += thirstRestore;
    }

    public void TakeDamage(float damage)
    {
        Health -= damage;
    }

    public void Heal(float amount)
    {
        Health += amount;
    }
}
