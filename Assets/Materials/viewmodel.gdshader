// Draws the viewmodel at its own field of view without a second camera.
//
// The mesh stays in the world camera's render pass, so the sun, the environment and shadows
// reach it exactly like any other geometry -- which a SubViewport on its own visual layer
// cannot do. Only the projection is faked, in vertex(), and the depth is squeezed in
// fragment() so the gun does not clip into walls it is drawn in front of.
//
// Based on Riordan/Kastor's "First Person View Model Shader", updated for Godot 4.3+.
// Applied from Player/ViewmodelRenderer.cs, one instance per surface.

shader_type spatial;
render_mode depth_draw_opaque, cull_back;

// Degrees, the same meaning as Camera3D.fov on the world camera -- which is set to Keep Width,
// so this is the HORIZONTAL angle. ViewmodelRenderer pushes it in, and RevolverCylinderController
// reads the same number off it to place ejected shells.
uniform float fov : hint_range(20.0, 120.0) = 75.0;

// How far toward the near plane the model is pushed. Lower squeezes harder; 1.0 disables it.
uniform float depth_squeeze : hint_range(0.1, 1.0) = 0.7;

uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, hint_default_white;
uniform float roughness : hint_range(0.0, 1.0) = 1.0;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

const float DEG2RAD = 3.14159265359 / 180.0;

void vertex() {
	// The published shader assigns the two scale terms outright, in the vertical-FOV form. That
	// is wrong here: the world camera is Keep Width, so its fov is horizontal, and assigning the
	// vertical form at the same number would render far wider than the camera it replaced AND
	// leave ToWorldPass() short by a factor of the aspect ratio.
	//
	// Scaling what the camera already computed avoids the whole question -- Godot's sign and
	// aspect conventions come along for free, whatever they are, and the factor is exactly the
	// k that ToWorldPass() applies to put an ejected shell where the gun is drawn.
	float aspect = VIEWPORT_SIZE.x / VIEWPORT_SIZE.y;
	float k = (1.0 / tan(fov * 0.5 * DEG2RAD)) / (abs(PROJECTION_MATRIX[1][1]) / aspect);
	PROJECTION_MATRIX[0][0] *= k;
	PROJECTION_MATRIX[1][1] *= k;
}

void fragment() {
	ALBEDO = albedo.rgb * texture(albedo_texture, UV).rgb;
	ROUGHNESS = roughness;
	METALLIC = metallic;
	// Reverse-Z (Godot 4.3+): 1.0 is the near plane, so this pulls the fragment forward.
	// Writing DEPTH costs early-Z on these surfaces, which is fine for a handful of meshes.
	DEPTH = 1.0 - (1.0 - FRAGCOORD.z) * depth_squeeze;
}
