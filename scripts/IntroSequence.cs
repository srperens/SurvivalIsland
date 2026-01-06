using Godot;

namespace SurvivalIsland;

public partial class IntroSequence : Node3D
{
    [Signal] public delegate void IntroFinishedEventHandler();

    [Export] public float IntroDuration = 8.0f;
    [Export] public PackedScene? MainScene { get; set; }

    [Export] public Camera3D? IntroCamera { get; set; }
    [Export] public Node3D? Plane { get; set; }
    [Export] public ColorRect? FadeOverlay { get; set; }
    [Export] public AudioStreamPlayer? CrashSound { get; set; }
    [Export] public AudioStreamPlayer? AlarmSound { get; set; }

    private float _timer;
    private bool _isPlaying;
    private Vector3 _initialPlanePosition;
    private float _shakeIntensity;

    public override void _Ready()
    {
        if (Plane != null)
            _initialPlanePosition = Plane.Position;

        if (FadeOverlay != null)
        {
            FadeOverlay.Color = new Color(0, 0, 0, 0);
            FadeOverlay.Visible = true;
        }

        StartIntro();
    }

    public void StartIntro()
    {
        _isPlaying = true;
        _timer = 0;

        AlarmSound?.Play();
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying) return;

        _timer += (float)delta;

        // Phase 1: Flying with turbulence (0-3 sec)
        if (_timer < 3.0f)
        {
            float turbulence = Mathf.Sin(_timer * 10) * 0.1f + Mathf.Sin(_timer * 7) * 0.05f;
            _shakeIntensity = Mathf.Lerp(0.02f, 0.15f, _timer / 3.0f);

            if (IntroCamera != null)
            {
                IntroCamera.Rotation = new Vector3(
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                    turbulence
                );
            }

            if (Plane != null)
            {
                Plane.Position = _initialPlanePosition + new Vector3(0, -_timer * 2, -_timer * 10);
                Plane.Rotation = new Vector3(
                    Mathf.DegToRad(-10 - _timer * 5),
                    0,
                    turbulence * 2
                );
            }
        }
        // Phase 2: Crash impact (3-4 sec)
        else if (_timer < 4.0f)
        {
            float impactProgress = (_timer - 3.0f);
            _shakeIntensity = 0.3f * (1 - impactProgress);

            if (IntroCamera != null)
            {
                IntroCamera.Rotation = new Vector3(
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity)
                );
            }

            if (impactProgress < 0.1f && CrashSound != null && !CrashSound.Playing)
            {
                CrashSound.Play();
                AlarmSound?.Stop();
            }
        }
        // Phase 3: Fade to black (4-5 sec)
        else if (_timer < 5.0f)
        {
            float fadeProgress = (_timer - 4.0f);
            if (FadeOverlay != null)
            {
                FadeOverlay.Color = new Color(0, 0, 0, fadeProgress);
            }

            _shakeIntensity = 0.1f * (1 - fadeProgress);
            if (IntroCamera != null)
            {
                IntroCamera.Rotation = new Vector3(
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                    (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                    0
                );
            }
        }
        // Phase 4: Black screen - "time passes" (5-7 sec)
        else if (_timer < 7.0f)
        {
            if (FadeOverlay != null)
            {
                FadeOverlay.Color = new Color(0, 0, 0, 1);
            }
        }
        // Phase 5: Transition to game (7-8 sec)
        else if (_timer < IntroDuration)
        {
            float fadeProgress = (_timer - 7.0f);
            if (FadeOverlay != null)
            {
                FadeOverlay.Color = new Color(0, 0, 0, 1 - fadeProgress);
            }
        }
        // End intro
        else
        {
            EndIntro();
        }
    }

    private void EndIntro()
    {
        _isPlaying = false;
        EmitSignal(SignalName.IntroFinished);

        // Load main scene
        if (MainScene != null)
        {
            GetTree().ChangeSceneToPacked(MainScene);
        }
        else
        {
            GetTree().ChangeSceneToFile("res://scenes/main.tscn");
        }
    }

    public void SkipIntro()
    {
        _timer = IntroDuration;
    }

    public override void _Input(InputEvent @event)
    {
        // Allow skipping intro
        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_cancel"))
        {
            SkipIntro();
        }
    }
}
