shader_type spatial;

uniform sampler2D noise_texture;
uniform float height_scale : hint_range(0.0, 30.0) = 8.0;
uniform float noise_scale : hint_range(0.001, 0.1) = 0.015;
uniform vec3 grass_color_light : source_color = vec3(0.28, 0.52, 0.18);
uniform vec3 grass_color_dark : source_color = vec3(0.12, 0.32, 0.08);
uniform vec3 dirt_color : source_color = vec3(0.35, 0.25, 0.15);
uniform vec3 rock_color : source_color = vec3(0.4, 0.38, 0.35);

varying float height;
varying vec3 world_pos;
varying vec3 world_normal;

float get_height(vec2 pos) {
    vec2 uv = pos * noise_scale;
    float h = texture(noise_texture, uv).r;
    h += texture(noise_texture, uv * 2.0).r * 0.5;
    h += texture(noise_texture, uv * 4.0).r * 0.25;
    h = h / 1.75;
    return h * height_scale;
}

// Hash function for procedural patterns
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Simplex-like noise for grass detail
float grass_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    height = get_height(world_pos.xz);

    float dist_from_center = length(world_pos.xz);
    float flatten = smoothstep(0.0, 30.0, dist_from_center);
    height *= flatten;

    float river_dist = abs(world_pos.x - 120.0);
    float river_flatten = smoothstep(20.0, 50.0, river_dist);
    height *= river_flatten;

    VERTEX.y += height;

    float h_left = get_height(world_pos.xz + vec2(-1.0, 0.0)) * flatten * river_flatten;
    float h_right = get_height(world_pos.xz + vec2(1.0, 0.0)) * flatten * river_flatten;
    float h_up = get_height(world_pos.xz + vec2(0.0, -1.0)) * flatten * river_flatten;
    float h_down = get_height(world_pos.xz + vec2(0.0, 1.0)) * flatten * river_flatten;

    world_normal = normalize(vec3(h_left - h_right, 2.0, h_up - h_down));
    NORMAL = world_normal;
}

void fragment() {
    // Use world_normal for slope calculation (NORMAL is in view space)
    float slope = 1.0 - dot(world_normal, vec3(0.0, 1.0, 0.0));

    // Multi-scale grass texture
    float grass_large = grass_noise(world_pos.xz * 0.5);
    float grass_medium = grass_noise(world_pos.xz * 2.0);
    float grass_fine = grass_noise(world_pos.xz * 8.0);
    float grass_detail = grass_noise(world_pos.xz * 20.0);

    // Combine scales for natural look
    float grass_pattern = grass_large * 0.4 + grass_medium * 0.3 + grass_fine * 0.2 + grass_detail * 0.1;

    // Grass blade-like streaks
    float blade_pattern = grass_noise(world_pos.xz * vec2(15.0, 3.0));
    blade_pattern = smoothstep(0.3, 0.7, blade_pattern);

    // Mix light and dark grass
    vec3 grass_color = mix(grass_color_dark, grass_color_light, grass_pattern * 0.7 + blade_pattern * 0.3);

    // Add subtle yellow/brown patches
    float dry_patches = grass_noise(world_pos.xz * 0.3);
    dry_patches = smoothstep(0.6, 0.8, dry_patches);
    vec3 dry_grass = vec3(0.4, 0.38, 0.2);
    grass_color = mix(grass_color, dry_grass, dry_patches * 0.3);

    vec3 color = grass_color;

    // Dirt on slopes
    color = mix(color, dirt_color, smoothstep(0.15, 0.4, slope));

    // Rock on steep slopes
    color = mix(color, rock_color, smoothstep(0.4, 0.7, slope));

    // Height variation
    color = mix(color, color * 1.15, smoothstep(3.0, 8.0, height));

    ALBEDO = color;
    ROUGHNESS = 0.85;
}
