// Viewmodel at its own FOV in the world pass, depth-squeezed so it doesn't clip into walls.
// Based on Riordan/Kastor's "First Person View Model Shader".

shader_type spatial;
render_mode depth_draw_opaque, cull_back;

// Horizontal degrees (world camera is Keep Width).
uniform float fov : hint_range(20.0, 120.0) = 75.0;

// Lower squeezes harder; 1.0 disables.
uniform float depth_squeeze : hint_range(0.1, 1.0) = 0.7;

uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, hint_default_white;
uniform float roughness : hint_range(0.0, 1.0) = 1.0;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

const float DEG2RAD = 3.14159265359 / 180.0;

void vertex() {
	// Scales the camera's projection rather than replacing it; matches ToWorldPass() in C#.
	float aspect = VIEWPORT_SIZE.x / VIEWPORT_SIZE.y;
	float fov_scale = (1.0 / tan(fov * 0.5 * DEG2RAD)) / (abs(PROJECTION_MATRIX[1][1]) / aspect);
	PROJECTION_MATRIX[0][0] *= fov_scale;
	PROJECTION_MATRIX[1][1] *= fov_scale;
}

void fragment() {
	ALBEDO = albedo.rgb * texture(albedo_texture, UV).rgb;
	ROUGHNESS = roughness;
	METALLIC = metallic;
	// Reverse-Z: 1.0 is the near plane.
	DEPTH = 1.0 - (1.0 - FRAGCOORD.z) * depth_squeeze;
}
