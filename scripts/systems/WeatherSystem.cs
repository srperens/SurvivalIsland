using Godot;

namespace SurvivalIsland;

public partial class WeatherSystem : Node
{
    [Signal] public delegate void WeatherChangedEventHandler(WeatherType weather);

    public enum WeatherType { Clear, Cloudy, Rain, Storm }

    [Export] public float MinWeatherDuration = 120f; // Seconds
    [Export] public float MaxWeatherDuration = 600f;

    [Export] public GpuParticles3D? RainParticles { get; set; }
    [Export] public AudioStreamPlayer? RainAudio { get; set; }
    [Export] public AudioStreamPlayer? ThunderAudio { get; set; }
    [Export] public WorldEnvironment? Environment { get; set; }
    [Export] public DayNightCycle? DayNightCycle { get; set; }

    [Export] public float ClearTemperatureModifier = 0f;
    [Export] public float CloudyTemperatureModifier = -2f;
    [Export] public float RainTemperatureModifier = -5f;
    [Export] public float StormTemperatureModifier = -8f;

    private WeatherType _currentWeather = WeatherType.Clear;
    private float _weatherTimer;
    private float _nextWeatherChange;
    private float _thunderTimer;

    public WeatherType CurrentWeather => _currentWeather;
    public bool IsRaining => _currentWeather == WeatherType.Rain || _currentWeather == WeatherType.Storm;

    public float TemperatureModifier
    {
        get
        {
            return _currentWeather switch
            {
                WeatherType.Clear => ClearTemperatureModifier,
                WeatherType.Cloudy => CloudyTemperatureModifier,
                WeatherType.Rain => RainTemperatureModifier,
                WeatherType.Storm => StormTemperatureModifier,
                _ => 0f
            };
        }
    }

    public float EffectiveTemperature
    {
        get
        {
            float baseTemp = DayNightCycle?.AmbientTemperature ?? 20f;
            return baseTemp + TemperatureModifier;
        }
    }

    public override void _Ready()
    {
        _nextWeatherChange = GD.Randf() * (MaxWeatherDuration - MinWeatherDuration) + MinWeatherDuration;
        SetWeather(WeatherType.Clear);
    }

    public override void _Process(double delta)
    {
        _weatherTimer += (float)delta;

        if (_weatherTimer >= _nextWeatherChange)
        {
            ChangeWeatherRandomly();
            _weatherTimer = 0;
            _nextWeatherChange = GD.Randf() * (MaxWeatherDuration - MinWeatherDuration) + MinWeatherDuration;
        }

        // Handle storm thunder
        if (_currentWeather == WeatherType.Storm)
        {
            _thunderTimer -= (float)delta;
            if (_thunderTimer <= 0)
            {
                PlayThunder();
                _thunderTimer = GD.Randf() * 20 + 10; // 10-30 seconds between thunder
            }
        }

        UpdateWeatherEffects();
    }

    private void ChangeWeatherRandomly()
    {
        // Weather tends to progress: Clear -> Cloudy -> Rain -> Storm -> Cloudy -> Clear
        float rand = GD.Randf();

        var newWeather = _currentWeather switch
        {
            WeatherType.Clear => rand < 0.7f ? WeatherType.Clear : WeatherType.Cloudy,
            WeatherType.Cloudy => rand < 0.3f ? WeatherType.Clear : (rand < 0.7f ? WeatherType.Cloudy : WeatherType.Rain),
            WeatherType.Rain => rand < 0.2f ? WeatherType.Cloudy : (rand < 0.7f ? WeatherType.Rain : WeatherType.Storm),
            WeatherType.Storm => rand < 0.6f ? WeatherType.Rain : WeatherType.Cloudy,
            _ => WeatherType.Clear
        };

        SetWeather(newWeather);
    }

    public void SetWeather(WeatherType weather)
    {
        if (_currentWeather == weather) return;

        _currentWeather = weather;
        EmitSignal(SignalName.WeatherChanged, (int)weather);

        // Update rain particles
        if (RainParticles != null)
        {
            RainParticles.Emitting = IsRaining;

            // Heavier rain in storm
            if (RainParticles.ProcessMaterial is ParticleProcessMaterial material)
            {
                material.InitialVelocityMin = weather == WeatherType.Storm ? 30f : 20f;
                material.InitialVelocityMax = weather == WeatherType.Storm ? 40f : 25f;
            }
        }

        // Update rain audio
        if (RainAudio != null)
        {
            if (IsRaining)
            {
                if (!RainAudio.Playing)
                    RainAudio.Play();
                RainAudio.VolumeDb = weather == WeatherType.Storm ? 0 : -6;
            }
            else
            {
                RainAudio.Stop();
            }
        }

        // Update fog/sky
        UpdateEnvironment();

        GD.Print($"Weather changed to: {weather}");
    }

    private void UpdateEnvironment()
    {
        if (Environment?.Environment == null) return;

        var env = Environment.Environment;

        // Adjust fog based on weather
        env.FogEnabled = _currentWeather != WeatherType.Clear;

        switch (_currentWeather)
        {
            case WeatherType.Clear:
                env.FogDensity = 0;
                break;
            case WeatherType.Cloudy:
                env.FogDensity = 0.001f;
                env.FogLightColor = new Color(0.7f, 0.7f, 0.75f);
                break;
            case WeatherType.Rain:
                env.FogDensity = 0.003f;
                env.FogLightColor = new Color(0.5f, 0.5f, 0.55f);
                break;
            case WeatherType.Storm:
                env.FogDensity = 0.005f;
                env.FogLightColor = new Color(0.3f, 0.3f, 0.35f);
                break;
        }
    }

    private void UpdateWeatherEffects()
    {
        // Gradually transition fog, etc.
    }

    private void PlayThunder()
    {
        if (ThunderAudio != null && !ThunderAudio.Playing)
        {
            ThunderAudio.Play();
            // Could also flash the screen white briefly
        }
    }
}
