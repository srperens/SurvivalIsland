using Godot;

namespace SurvivalIsland;

[Tool]
public partial class TerrainCollision : StaticBody3D
{
    [Export] public float TerrainSize = 500f;
    [Export] public int Resolution = 100;
    [Export] public float HeightScale = 12.0f;
    [Export] public float NoiseScale = 0.012f;
    [Export] public int NoiseSeed = 42;
    [Export] public NoiseTexture2D? NoiseTexture;

    private Image? _noiseImage;

    public override void _Ready()
    {
        // Wait for noise texture to be ready if it exists
        if (NoiseTexture != null)
        {
            if (NoiseTexture.GetImage() != null)
            {
                GenerateCollision();
            }
            else
            {
                NoiseTexture.Changed += OnNoiseTextureReady;
            }
        }
        else
        {
            GenerateCollision();
        }
    }

    private void OnNoiseTextureReady()
    {
        if (NoiseTexture != null)
        {
            NoiseTexture.Changed -= OnNoiseTextureReady;
        }
        GenerateCollision();
    }

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

    private float GetHeight(float worldX, float worldZ)
    {
        // Match the shader's get_height function exactly
        float u = worldX * NoiseScale;
        float v = worldZ * NoiseScale;

        float h = SampleNoiseTexture(u, v);
        h += SampleNoiseTexture(u * 2f, v * 2f) * 0.5f;
        h += SampleNoiseTexture(u * 4f, v * 4f) * 0.25f;
        h = h / 1.75f;
        h *= HeightScale;

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

    private void GenerateCollision()
    {
        // Get the noise image for sampling
        if (NoiseTexture != null)
        {
            _noiseImage = NoiseTexture.GetImage();
        }

        if (_noiseImage == null)
        {
            GD.PrintErr("TerrainCollision: NoiseTexture not set or not ready!");
            return;
        }

        // Create heightmap data
        var image = Image.CreateEmpty(Resolution + 1, Resolution + 1, false, Image.Format.Rf);

        float halfSize = TerrainSize / 2;
        float step = TerrainSize / Resolution;

        float minH = float.MaxValue;
        float maxH = float.MinValue;

        // First pass - find min/max
        for (int z = 0; z <= Resolution; z++)
        {
            for (int x = 0; x <= Resolution; x++)
            {
                float worldX = -halfSize + x * step;
                float worldZ = -halfSize + z * step;
                float h = GetHeight(worldX, worldZ);
                minH = Mathf.Min(minH, h);
                maxH = Mathf.Max(maxH, h);
            }
        }

        // Second pass - write normalized heights
        for (int z = 0; z <= Resolution; z++)
        {
            for (int x = 0; x <= Resolution; x++)
            {
                float worldX = -halfSize + x * step;
                float worldZ = -halfSize + z * step;
                float h = GetHeight(worldX, worldZ);
                // Normalize to 0-1 range
                float normalized = (h - minH) / (maxH - minH + 0.001f);
                image.SetPixel(x, z, new Color(normalized, 0, 0, 1));
            }
        }

        // Create HeightMapShape3D
        var heightMapShape = new HeightMapShape3D();
        heightMapShape.MapWidth = Resolution + 1;
        heightMapShape.MapDepth = Resolution + 1;

        // Convert image to float array
        var mapData = new float[(Resolution + 1) * (Resolution + 1)];
        for (int z = 0; z <= Resolution; z++)
        {
            for (int x = 0; x <= Resolution; x++)
            {
                float worldX = -halfSize + x * step;
                float worldZ = -halfSize + z * step;
                mapData[z * (Resolution + 1) + x] = GetHeight(worldX, worldZ);
            }
        }

        heightMapShape.MapData = mapData;

        // Create collision shape
        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = heightMapShape;
        collisionShape.Scale = new Vector3(step, 1, step);

        AddChild(collisionShape);

        CollisionLayer = 1;

        GD.Print($"TerrainCollision: Generated heightmap {Resolution + 1}x{Resolution + 1}");
    }
}
