using Godot;
using System;

namespace SurvivalIsland;

public partial class ForestGenerator : Node3D
{
    [Export] public PackedScene? TreeScene { get; set; }
    [Export] public PackedScene? PineTreeScene { get; set; }
    [Export] public PackedScene? BushScene { get; set; }
    [Export] public PackedScene? AppleTreeScene { get; set; }
    [Export] public PackedScene? BananaTreeScene { get; set; }
    [Export] public PackedScene? BerryBushScene { get; set; }
    [Export] public int TreeCount = 3600;
    [Export] public int BushCount = 3000;
    [Export] public int FruitTreeCount = 80;
    [Export] public int BerryBushCount = 120;
    [Export] public float SpawnRadius = 220f;
    [Export] public float MinDistanceBetweenTrees = 4f;
    [Export] public float MinDistanceBetweenBushes = 2.5f;
    [Export] public float MinDistanceFromCenter = 15f;
    [Export] public float PineTreeChance = 0.4f;
    [Export] public float TerrainHeightScale = 12.0f;
    [Export] public float TerrainNoiseScale = 0.012f;
    [Export] public NoiseTexture2D? NoiseTexture;

    private RandomNumberGenerator _rng = new();
    private Image? _noiseImage;

    private float SampleNoiseTexture(float u, float v)
    {
        if (_noiseImage == null) return 0f;

        // Wrap UV coordinates to 0-1 range (seamless tiling)
        u = u - Mathf.Floor(u);
        v = v - Mathf.Floor(v);

        int x = (int)(u * (_noiseImage.GetWidth() - 1));
        int y = (int)(v * (_noiseImage.GetHeight() - 1));

        return _noiseImage.GetPixel(x, y).R;
    }

    private float GetTerrainHeight(float worldX, float worldZ)
    {
        // Match the shader's get_height function exactly
        float u = worldX * TerrainNoiseScale;
        float v = worldZ * TerrainNoiseScale;

        float h = SampleNoiseTexture(u, v);
        h += SampleNoiseTexture(u * 2f, v * 2f) * 0.5f;
        h += SampleNoiseTexture(u * 4f, v * 4f) * 0.25f;
        h = h / 1.75f;
        h *= TerrainHeightScale;

        // Flatten near center (spawn area) - matches shader
        float distFromCenter = new Vector2(worldX, worldZ).Length();
        float flatten = Mathf.SmoothStep(0, 30, distFromCenter);
        h *= flatten;

        // Flatten near river - matches shader
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
                GenerateAll();
            }
            else
            {
                // Wait for texture to be ready
                NoiseTexture.Changed += OnNoiseTextureReady;
            }
        }
        else
        {
            GD.PrintErr("ForestGenerator: NoiseTexture not set!");
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
                GenerateAll();
            }
        }
    }

    private void GenerateAll()
    {
        GenerateForest();
        GenerateBushes();
        GenerateFruitTrees();
        GenerateBerryBushes();
    }

    private void GenerateForest()
    {
        if (TreeScene == null && PineTreeScene == null)
        {
            GD.PrintErr("ForestGenerator: No tree scenes assigned!");
            return;
        }

        var placedPositions = new System.Collections.Generic.List<Vector3>();
        int attempts = 0;
        int maxAttempts = TreeCount * 10;

        while (placedPositions.Count < TreeCount && attempts < maxAttempts)
        {
            attempts++;

            // Random position within spawn radius
            float x = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            var position = new Vector3(x, 0, z);

            // Skip if too close to center (spawn area)
            if (position.Length() < MinDistanceFromCenter)
                continue;

            // Skip if too close to another tree
            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if (position.DistanceTo(placed) < MinDistanceBetweenTrees)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            // Choose tree type
            PackedScene? sceneToUse;
            if (TreeScene != null && PineTreeScene != null)
            {
                sceneToUse = _rng.Randf() < PineTreeChance ? PineTreeScene : TreeScene;
            }
            else
            {
                sceneToUse = TreeScene ?? PineTreeScene;
            }

            if (sceneToUse == null)
                continue;

            // Instantiate tree
            var tree = sceneToUse.Instantiate<Node3D>();
            AddChild(tree);

            // Set position with terrain height
            float height = GetTerrainHeight(position.X, position.Z);
            tree.Position = new Vector3(position.X, height, position.Z);
            tree.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);
            float scaleVariation = _rng.RandfRange(0.8f, 1.2f);
            tree.Scale = new Vector3(scaleVariation, scaleVariation, scaleVariation);

            placedPositions.Add(position);
        }

        GD.Print($"ForestGenerator: Placed {placedPositions.Count} trees");
    }

    private void GenerateBushes()
    {
        if (BushScene == null)
            return;

        var placedPositions = new System.Collections.Generic.List<Vector3>();
        int attempts = 0;
        int maxAttempts = BushCount * 10;

        while (placedPositions.Count < BushCount && attempts < maxAttempts)
        {
            attempts++;

            float x = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            var position = new Vector3(x, 0, z);

            // Skip if too close to center
            if (position.Length() < MinDistanceFromCenter * 0.7f)
                continue;

            // Skip river area
            if (x > 75 && x < 165)
                continue;

            // Check distance from other bushes
            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if (position.DistanceTo(placed) < MinDistanceBetweenBushes)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            var bush = BushScene.Instantiate<Node3D>();
            AddChild(bush);

            // Set position with terrain height
            float height = GetTerrainHeight(position.X, position.Z);
            bush.Position = new Vector3(position.X, height, position.Z);
            bush.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);
            float scaleVariation = _rng.RandfRange(0.6f, 1.3f);
            bush.Scale = new Vector3(scaleVariation, scaleVariation, scaleVariation);

            placedPositions.Add(position);
        }

        GD.Print($"ForestGenerator: Placed {placedPositions.Count} bushes");
    }

    private void GenerateFruitTrees()
    {
        if (AppleTreeScene == null && BananaTreeScene == null)
            return;

        var placedPositions = new System.Collections.Generic.List<Vector3>();
        int attempts = 0;
        int maxAttempts = FruitTreeCount * 15;

        while (placedPositions.Count < FruitTreeCount && attempts < maxAttempts)
        {
            attempts++;

            float x = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            var position = new Vector3(x, 0, z);

            if (position.Length() < MinDistanceFromCenter * 1.5f)
                continue;

            // Skip river area
            if (x > 75 && x < 165)
                continue;

            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if (position.DistanceTo(placed) < MinDistanceBetweenTrees * 2)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            // Choose tree type
            PackedScene? sceneToUse;
            if (AppleTreeScene != null && BananaTreeScene != null)
            {
                sceneToUse = _rng.Randf() < 0.6f ? AppleTreeScene : BananaTreeScene;
            }
            else
            {
                sceneToUse = AppleTreeScene ?? BananaTreeScene;
            }

            if (sceneToUse == null)
                continue;

            var tree = sceneToUse.Instantiate<Node3D>();
            AddChild(tree);

            float height = GetTerrainHeight(position.X, position.Z);
            tree.Position = new Vector3(position.X, height, position.Z);
            tree.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);

            placedPositions.Add(position);
        }

        GD.Print($"ForestGenerator: Placed {placedPositions.Count} fruit trees");
    }

    private void GenerateBerryBushes()
    {
        if (BerryBushScene == null)
            return;

        var placedPositions = new System.Collections.Generic.List<Vector3>();
        int attempts = 0;
        int maxAttempts = BerryBushCount * 15;

        while (placedPositions.Count < BerryBushCount && attempts < maxAttempts)
        {
            attempts++;

            float x = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            float z = _rng.RandfRange(-SpawnRadius, SpawnRadius);
            var position = new Vector3(x, 0, z);

            if (position.Length() < MinDistanceFromCenter)
                continue;

            // Skip river area
            if (x > 75 && x < 165)
                continue;

            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if (position.DistanceTo(placed) < MinDistanceBetweenBushes * 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            var bush = BerryBushScene.Instantiate<Node3D>();
            AddChild(bush);

            float height = GetTerrainHeight(position.X, position.Z);
            bush.Position = new Vector3(position.X, height, position.Z);
            bush.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);
            float scaleVariation = _rng.RandfRange(0.8f, 1.2f);
            bush.Scale = new Vector3(scaleVariation, scaleVariation, scaleVariation);

            placedPositions.Add(position);
        }

        GD.Print($"ForestGenerator: Placed {placedPositions.Count} berry bushes");
    }
}
