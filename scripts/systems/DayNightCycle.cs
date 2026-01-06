using Godot;

namespace SurvivalIsland;

public partial class DayNightCycle : Node
{
    [Signal] public delegate void TimeChangedEventHandler(float hour);
    [Signal] public delegate void DayChangedEventHandler(int day);
    [Signal] public delegate void PeriodChangedEventHandler(DayPeriod period);

    public enum DayPeriod { Dawn, Day, Dusk, Night }

    [Export] public float DayDurationMinutes = 0.5f; // Real-time minutes per game day (30 sec)
    [Export] public float StartHour = 8f; // Start at 8 AM

    [Export] public NodePath? SunPath { get; set; }
    [Export] public NodePath? EnvironmentPath { get; set; }

    private DirectionalLight3D? _sun;
    private WorldEnvironment? _environment;

    [Export] public Color DawnColor = new Color(1.0f, 0.4f, 0.2f);   // Deep orange
    [Export] public Color DayColor = new Color(1.0f, 0.98f, 0.95f);  // Warm white
    [Export] public Color DuskColor = new Color(1.0f, 0.3f, 0.1f);   // Red-orange
    [Export] public Color NightColor = new Color(0.1f, 0.1f, 0.2f);  // Very dark blue
    [Export] public Color MoonColor = new Color(0.6f, 0.7f, 0.9f);  // Pale blue moonlight

    [Export] public float DawnIntensity = 0.5f;
    [Export] public float DayIntensity = 1.0f;
    [Export] public float DuskIntensity = 0.4f;
    [Export] public float NightIntensity = 0.05f;
    [Export] public float MoonIntensity = 0.15f;  // Moonlight intensity

    private float _currentTime; // 0-24 hours
    private int _currentDay = 1;
    private DayPeriod _currentPeriod;
    private float _secondsPerHour;

    public float CurrentHour => _currentTime;
    public int CurrentDay => _currentDay;
    public DayPeriod CurrentPeriod => _currentPeriod;
    public bool IsNight => _currentTime < 6 || _currentTime >= 20;

    // Ambient temperature based on time
    public float AmbientTemperature
    {
        get
        {
            // Coldest at 4 AM (10°C), warmest at 2 PM (25°C)
            float tempCurve = Mathf.Sin((_currentTime - 4) * Mathf.Pi / 12);
            return 17.5f + tempCurve * 7.5f;
        }
    }

    public override void _Ready()
    {
        _currentTime = StartHour;
        _secondsPerHour = (DayDurationMinutes * 60) / 24f;

        // Get nodes from paths
        if (SunPath != null)
            _sun = GetNodeOrNull<DirectionalLight3D>(SunPath);
        if (EnvironmentPath != null)
            _environment = GetNodeOrNull<WorldEnvironment>(EnvironmentPath);

        GD.Print($"DayNightCycle: Started at {StartHour}:00, {DayDurationMinutes} min/day");
        GD.Print($"DayNightCycle: Sun={_sun != null}, Environment={_environment != null}");
        UpdatePeriod();
        UpdateLighting();
    }

    public override void _Process(double delta)
    {
        // Advance time
        float hoursToAdd = (float)delta / _secondsPerHour;
        _currentTime += hoursToAdd;

        // Handle day rollover
        if (_currentTime >= 24f)
        {
            _currentTime -= 24f;
            _currentDay++;
            EmitSignal(SignalName.DayChanged, _currentDay);
        }

        UpdatePeriod();
        UpdateLighting();

        EmitSignal(SignalName.TimeChanged, _currentTime);
    }

    private void UpdatePeriod()
    {
        DayPeriod newPeriod;

        if (_currentTime >= 5 && _currentTime < 7)
            newPeriod = DayPeriod.Dawn;
        else if (_currentTime >= 7 && _currentTime < 18)
            newPeriod = DayPeriod.Day;
        else if (_currentTime >= 18 && _currentTime < 20)
            newPeriod = DayPeriod.Dusk;
        else
            newPeriod = DayPeriod.Night;

        if (newPeriod != _currentPeriod)
        {
            _currentPeriod = newPeriod;
            EmitSignal(SignalName.PeriodChanged, (int)_currentPeriod);
        }
    }

    private void UpdateLighting()
    {
        if (_sun == null) return;

        // Sun is visible from 6 AM to 18 PM
        bool isSunUp = _currentTime >= 6 && _currentTime <= 18;
        bool isNightTime = _currentTime < 6 || _currentTime > 18;

        if (isSunUp)
        {
            // Sun arc: rises at 6, peaks at 12, sets at 18
            float dayProgress = Mathf.Clamp((_currentTime - 6) / 12f, 0f, 1f);
            float sunAltitude = Mathf.Sin(dayProgress * Mathf.Pi) * 80f; // Max 80 degrees at noon

            // Get interpolated color and intensity
            var (color, intensity) = GetLightingForTime();

            _sun.Visible = sunAltitude > 0;
            float sunAzimuth = -90 + (dayProgress * 180); // East to West
            _sun.RotationDegrees = new Vector3(-sunAltitude, sunAzimuth, 0);
            _sun.LightColor = color;
            _sun.LightEnergy = intensity;

            // Soft sun shadows - softer at dawn/dusk
            float shadowSoftness = Mathf.Lerp(4.0f, 2.0f, Mathf.Sin(dayProgress * Mathf.Pi));
            _sun.ShadowBlur = shadowSoftness;
            _sun.LightAngularDistance = 1.5f; // Sun angular size
        }
        else
        {
            // Moon at night - rises at 20, peaks at midnight, sets at 6
            // Calculate moon position
            float nightProgress;
            if (_currentTime >= 20)
                nightProgress = (_currentTime - 20) / 10f; // 20:00 to 06:00 = 10 hours
            else
                nightProgress = (_currentTime + 4) / 10f; // 00:00 to 06:00

            float moonAltitude = Mathf.Sin(nightProgress * Mathf.Pi) * 60f; // Max 60 degrees at midnight

            _sun.Visible = true; // Use sun as moon
            float moonAzimuth = 90 - (nightProgress * 180); // West to East (opposite of sun)
            _sun.RotationDegrees = new Vector3(-Mathf.Max(moonAltitude, 15f), moonAzimuth, 0);
            _sun.LightColor = MoonColor;
            _sun.LightEnergy = MoonIntensity;

            // Very soft moon shadows
            _sun.ShadowBlur = 6.0f;
            _sun.LightAngularDistance = 1.0f; // Diffuse moonlight
        }

        // Update environment
        if (_environment?.Environment != null)
        {
            var env = _environment.Environment;

            if (isNightTime)
            {
                // Night: dim blue moonlight ambient
                env.AmbientLightColor = new Color(0.05f, 0.05f, 0.1f);
                env.AmbientLightEnergy = 0.1f;
                env.BackgroundEnergyMultiplier = 0.02f;
                env.FogEnabled = false; // Disable fog at night
            }
            else
            {
                var (color, intensity) = GetLightingForTime();
                env.AmbientLightColor = color * 0.3f;
                env.AmbientLightEnergy = intensity * 0.5f;
                env.BackgroundEnergyMultiplier = 1.0f;
                env.FogEnabled = true;
                env.FogLightColor = color;
                env.FogDensity = 0.0005f;
            }
        }

        // Update sky colors for night
        UpdateSkyColors(isNightTime);
    }

    private void UpdateSkyColors(bool isNight)
    {
        if (_environment?.Environment?.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            // Define color palettes
            var nightTop = new Color(0.0f, 0.0f, 0.01f);       // Almost black
            var nightHorizon = new Color(0.0f, 0.0f, 0.02f);   // Very dark

            var dawnTop = new Color(0.2f, 0.1f, 0.3f);         // Deep purple
            var dawnHorizon = new Color(1.0f, 0.35f, 0.1f);    // Vivid orange-red

            var dayTop = new Color(0.3f, 0.5f, 0.9f);          // Blue
            var dayHorizon = new Color(0.6f, 0.75f, 0.95f);    // Light blue

            var duskTop = new Color(0.3f, 0.1f, 0.2f);         // Deep purple-red
            var duskHorizon = new Color(1.0f, 0.25f, 0.05f);   // Intense red-orange

            if (isNight)
            {
                sky.SkyTopColor = nightTop;
                sky.SkyHorizonColor = nightHorizon;
                sky.GroundHorizonColor = nightHorizon;
            }
            else if (_currentTime >= 5 && _currentTime < 7) // Dawn
            {
                float t = (_currentTime - 5) / 2f;
                sky.SkyTopColor = nightTop.Lerp(dawnTop, t).Lerp(dayTop, t);
                sky.SkyHorizonColor = nightHorizon.Lerp(dawnHorizon, Mathf.Sin(t * Mathf.Pi));
                sky.GroundHorizonColor = sky.SkyHorizonColor * 0.8f;
            }
            else if (_currentTime >= 17 && _currentTime < 19) // Dusk
            {
                float t = (_currentTime - 17) / 2f;
                sky.SkyTopColor = dayTop.Lerp(duskTop, t);
                sky.SkyHorizonColor = dayHorizon.Lerp(duskHorizon, Mathf.Sin(t * Mathf.Pi));
                sky.GroundHorizonColor = sky.SkyHorizonColor * 0.8f;
            }
            else // Day
            {
                sky.SkyTopColor = dayTop;
                sky.SkyHorizonColor = dayHorizon;
                sky.GroundHorizonColor = dayHorizon * 0.9f;
            }
        }
    }

    private (Color color, float intensity) GetLightingForTime()
    {
        // Dawn: 5-7
        // Day: 7-18
        // Dusk: 18-20
        // Night: 20-5

        if (_currentTime >= 5 && _currentTime < 7)
        {
            float t = (_currentTime - 5) / 2;
            return (NightColor.Lerp(DawnColor, t).Lerp(DayColor, t),
                    Mathf.Lerp(NightIntensity, DawnIntensity, t));
        }
        else if (_currentTime >= 7 && _currentTime < 18)
        {
            float t = (_currentTime - 7) / 11;
            float midT = 1 - Mathf.Abs(t - 0.5f) * 2; // Peak at noon
            return (DayColor,
                    Mathf.Lerp(DawnIntensity, DayIntensity, midT));
        }
        else if (_currentTime >= 18 && _currentTime < 20)
        {
            float t = (_currentTime - 18) / 2;
            return (DayColor.Lerp(DuskColor, t).Lerp(NightColor, t),
                    Mathf.Lerp(DayIntensity, DuskIntensity, t));
        }
        else
        {
            return (NightColor, NightIntensity);
        }
    }

    public string GetTimeString()
    {
        int hours = (int)_currentTime;
        int minutes = (int)((_currentTime - hours) * 60);
        return $"{hours:D2}:{minutes:D2}";
    }

    public void SetTime(float hour)
    {
        _currentTime = Mathf.Clamp(hour, 0, 24);
        UpdatePeriod();
        UpdateLighting();
    }
}
