shader_type canvas_item;

/// Globals
// The location and size of the world.
global uniform vec4 world_rect; // x, y = topleft; z, w = size
// The location and size of the screen in the world right now. No local offsets.
global uniform vec4 screen_world_rect; // x, y = topleft; z, w = size
// The displacement mask textures, which are configured and set globally.
global uniform sampler2D screen_displacement_mask_tex;
global uniform sampler2D global_displacement_mask_tex;

/// Shader-specific uniforms.
uniform float blade_width = 4.0;
uniform float blade_height = 24.0;
uniform vec4 blade_color = vec4(0.51, 0.97, 0.0, 1.0);
// 0 - 1 degree of how strongly the blade comes to a point near the top.
uniform float blade_pointiness = 1.0;
uniform float ambient_sway_amount = 3.0;
uniform float ambient_sway_speed = 0.5;
uniform float displacement_bend_intensity = 10.0;
// Controls which displacement texture is sampled as well as how the UV sampling coordinates are calculated to do so.
// 0 = screen-space, 1 = world-space (world-space expects the shader-local uniform world_rect to be populated).
uniform int sample_mode : hint_enum("Screen-Space", "World-Space") = 1;

/// Shader-specific state.
// The uv coordinates to sample on the displacement mask for the base of this
// grass blade (The original X,Y position of each vertex).
// Updated in the Vertex shader below for use in the Fragment shader.
varying flat vec2 base_displacement_uv;

varying float displacement_strength;
varying vec2 bend_direction;

const vec2 right_vector = vec2(1.0, 0);
const vec2 up_vector = vec2(0.0, -1.0);

void vertex() {
	// Unpack Vertex Color values. (See GrassPatchRowMesh.cs)
	// Phase is our random seed per grass blade.
	float phase = COLOR.r;

	// Sample from the original location of the grass blade before it has been grown.
	vec2 blade_base_pos = VERTEX.xy;
	// We always sample the displacement texture from the base of the blade of
	// grass, rather from whatever vertex we're at. This way, all vertices of
	// the blade bend in the same direction with the same characteristics.
	// Vertex global position in world space.
	vec2 blade_base_world_pos = (MODEL_MATRIX * vec4(blade_base_pos, 0.0, 1.0)).xy;
	vec4 uvRect = sample_mode == 0 ? screen_world_rect : world_rect;

	// Compute relative UV coordinates of this vertex on the screen (as the
	// displacement texture is a full-screen buffer).
	base_displacement_uv = (blade_base_world_pos - uvRect.xy) / uvRect.zw;

	// Extend the grass vertex according to its UV coordinate, but coming to a point near the top.
	VERTEX.x += (UV.x - 0.5) * blade_width * (1.0 - UV.y * blade_pointiness);
	VERTEX.y -= UV.y * blade_height;

	// Compute ambient sway (not caused by any direct influence).
	float ambient_sway = sin(TIME * ambient_sway_speed + phase * TAU) * ambient_sway_amount;

	// Make bending effects stronger toward the tip of the blade (UV.y = 1)
	// and disabled at the base (UV.y = 0)
	float vertical_weight = UV.y;

	// Sample the Displacement color - what direction and how strongly the displacement occurs at
	// this position.
	//
	// The RG channels hold the displacement direction. The intensity of the displacement is the
	// size/length of this vector.
	// The B channel holds a relative age, which is defined on the displacement viewport that
	// produces the displacement_mask_tex being sampled below.
	// I hate that this if branch exists here, but you're not allowed to declare local sampler2D 
	// variables so we have to...
	vec3 raw_displacement;
	if(sample_mode == 0) {
		raw_displacement = texture(screen_displacement_mask_tex, base_displacement_uv).rgb;
	}
	else {
		raw_displacement = texture(global_displacement_mask_tex, base_displacement_uv).rgb;
	}
	
	// The blue channel on the displacement mask contains the relative (0 -> 1.0) age of the displacement
	// as determined by the displacement viewport's configuration. We use this as a strength multiplier
	// that controls how long the grass stays displaced.
	float age_weight = raw_displacement.b;

	displacement_strength = length(raw_displacement.rg);
	bend_direction = displacement_strength > 0.001 ? normalize(raw_displacement.rg) : vec2(0.0, 0.0);

	// The strength of displacement should be modulated by the age.
	displacement_strength *= age_weight;
	
	// Displace based on ambient sway, but not when the displacement value is high.
	VERTEX.x += ambient_sway * vertical_weight * max(1.0 - displacement_strength, 0.0);

	// Bend away from the center.
	float bend_amount = displacement_strength * vertical_weight * displacement_bend_intensity;
	VERTEX.x += bend_amount * dot(bend_direction, right_vector);
	VERTEX.x = clamp(VERTEX.x, blade_base_pos.x - blade_height*1.1, blade_base_pos.x + blade_height*1.1);
	
	// For Y bending, we want to make sure that the grass stays approximately the same length so we
	// clamp how far up or down the grass tip can be based on the defined grass height.
	VERTEX.y += -bend_amount * dot(bend_direction, up_vector);
	VERTEX.y = clamp(VERTEX.y, blade_base_pos.y - blade_height*1.1, blade_base_pos.y + blade_height*1.1);

	// Uncomment to debug displacement direction.
	//COLOR.rg = bend_direction;
	//COLOR.b = 0.0;

	// Uncomment to debug screen uvs.
	//COLOR.rg = base_displacement_uv;
	//COLOR.b = 0.0;
}

void fragment()
{
	// Unpack Vertex Color values. (See GrassPatchRowMesh.cs)
	// Phase is our random seed per grass blade.
   	float phase = COLOR.r;

	// 1. Base color.
	COLOR = blade_color;

	vec3 displacement_amount;
	if(sample_mode == 0) {
		displacement_amount = texture(screen_displacement_mask_tex, base_displacement_uv).rgb;
	}
	else {
		displacement_amount = texture(global_displacement_mask_tex, base_displacement_uv).rgb;
	}
	
	float age = displacement_amount.b;

	// Color blades based on phase.
	COLOR.rgb *= max(1.0 - phase * (1.0 - UV.y), 0.3);

	// 3. Flicker if desired
	float flicker = sin(TIME * 10.0 + phase * 6.2831) * 0.5 + 0.5;
	float flicker_strength = age * UV.y * flicker;
	vec3 glow_color = vec3(0.8, 1.0, 0.7);
	COLOR.rgb = mix(COLOR.rgb, glow_color, flicker_strength * 0.1);

	// Near the top, we want the tips to be slightly transparent.
	float tip_transparency = 0.2;
	float tip_fade = clamp((UV.y - 0.8) / 0.2, 0.0, 1.0); // 0 → 1 as UV.y goes from tip_transparency to 1.0
	COLOR.a = mix(1.0, tip_transparency, tip_fade);

	// Uncomment to debug displacement.
	//COLOR.b = displacement_amount;
	
	//COLOR.rgb = vec3(0.0, bend_direction);
	//COLOR.r = -bend_direction.x;
}
