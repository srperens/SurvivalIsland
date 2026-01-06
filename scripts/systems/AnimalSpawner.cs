using Godot;
using System;

namespace SurvivalIsland;

public partial class AnimalSpawner : Node3D
{
    [Export] public PackedScene? DeerScene { get; set; }
    [Export] public PackedScene? WolfScene { get; set; }
    [Export] public PackedScene? RabbitScene { get; set; }
    [Export] public PackedScene? ElephantScene { get; set; }

    [Export] public int DeerCount = 8;
    [Export] public int WolfCount = 3;
    [Export] public int RabbitCount = 12;
    [Export] public int ElephantCount = 4;

    [Export] public float SpawnRadius = 150f;
    [Export] public float MinDistanceFromCenter = 30f;
    [Export] public float MinDistanceBetweenAnimals = 15f;
    [Export] public float TerrainHeightScale = 12.0f;
    [Export] public float TerrainNoiseScale = 0.012f;
    [Export] public NoiseTexture2D? NoiseTexture;

    private RandomNumberGenerator _rng = new();
    private Image? _noiseImage;

    private float SampleNoiseTexture(float u, float v)
    {
        if (_noiseImage == null) return 0f;
        u = u - Mathf.Floor(u);
        v = v - Mathf.Floor(v);
        int x = (int)(u * (_noiseImage.GetWidth() - 1));
        int y = (int)(v * (_noiseImage.GetHeight() - 1));
        return _noiseImage.GetPixel(x, y).R;
    }

    private float GetTerrainHeight(float worldX, float worldZ)
    {
        float u = worldX * TerrainNoiseScale;
        float v = worldZ * TerrainNoiseScale;

        float h = SampleNoiseTexture(u, v);
        h += SampleNoiseTexture(u * 2f, v * 2f) * 0.5f;
        h += SampleNoiseTexture(u * 4f, v * 4f) * 0.25f;
        h = h / 1.75f;
        h *= TerrainHeightScale;

        float distFromCenter = new Vector2(worldX, worldZ).Length();
        float flatten = Mathf.SmoothStep(0, 30, distFromCenter);
        h *= flatten;

        float riverDist = Mathf.Abs(worldX - 120);
        float riverFlatten = Mathf.SmoothStep(20, 50, riverDist);
        h *= riverFlatten;

        return h;
    }

    public override void _Ready()
    {
        _rng.Randomize();

        if (NoiseTexture != null)
        {
            var img = NoiseTexture.GetImage();
            if (img != null)
            {
                _noiseImage = img;
                CallDeferred(nameof(SpawnAnimals));
            }
            else
            {
                // Wait for texture to be ready
                NoiseTexture.Changed += OnNoiseTextureReady;
            }
        }
        else
        {
            GD.PrintErr("AnimalSpawner: NoiseTexture not set!");
        }
    }

    private void OnNoiseTextureReady()
    {
        if (NoiseTexture != null)
        {
            NoiseTexture.Changed -= OnNoiseTextureReady;
            _noiseImage = NoiseTexture.GetImage();
            if (_noiseImage != null)
            {
                CallDeferred(nameof(SpawnAnimals));
            }
        }
    }

    private void SpawnAnimals()
    {
        var placedPositions = new System.Collections.Generic.List<Vector3>();

        // Spawn deer
        SpawnAnimalType(DeerScene, DeerCount, placedPositions, "Deer");

        // Spawn wolves (further from center)
        SpawnAnimalType(WolfScene, WolfCount, placedPositions, "Wolf", minFromCenter: 60f);

        // Spawn rabbits
        SpawnAnimalType(RabbitScene, RabbitCount, placedPositions, "Rabbit", minDistance: 8f);

        // Spawn elephants (large, need more space)
        SpawnAnimalType(ElephantScene, ElephantCount, placedPositions, "Elephant", minDistance: 25f, minFromCenter: 50f);

        GD.Print($"AnimalSpawner: Spawned animals - {DeerCount} deer, {WolfCount} wolves, {RabbitCount} rabbits, {ElephantCount} elephants");
    }

    private void SpawnAnimalType(PackedScene? scene, int count,
        System.Collections.Generic.List<Vector3> placedPositions,
        string name, float minDistance = -1, float minFromCenter = -1)
    {
        if (scene == null) return;

        if (minDistance < 0) minDistance = MinDistanceBetweenAnimals;
        if (minFromCenter < 0) minFromCenter = MinDistanceFromCenter;

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = count * 20;

        while (spawned < count && attempts < maxAttempts)
        {
            attempts++;

            float x = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            var position = new Vector3(x, 0.5f, z);

            // Check distance from center
            if (new Vector2(x, z).Length() < minFromCenter)
                continue;

            // Check distance from other animals
            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if (position.DistanceTo(placed) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            // Avoid spawning in water (river area)
            if (x > 80 && x < 160)
                continue;

            float height = GetTerrainHeight(position.X, position.Z);

            var animal = scene.Instantiate<Node3D>();
            AddChild(animal);

            animal.Position = new Vector3(position.X, height + 0.5f, position.Z);
            animal.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);

            placedPositions.Add(position);
            spawned++;
        }
    }
}
