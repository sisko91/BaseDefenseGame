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
uniform float ambient_sway_amount = 3.0;
uniform float ambient_sway_speed = 0.5;
uniform float displacement_bend_intensity = 15.0;
// Controls which displacement texture is sampled as well as how the UV sampling coordinates are calculated to do so.
// 0 = screen-space, 1 = world-space (world-space expects the shader-local uniform world_rect to be populated).
uniform int sample_mode : hint_enum("Screen-Space", "World-Space") = 0;

/// Shader-specific state.
// The uv coordinates to sample on the displacement mask for the base of this
// grass blade (The original X,Y position of each vertex).
// Updated in the Vertex shader below for use in the Fragment shader.
varying flat vec2 base_displacement_uv;

varying float displacement_gradient_strength;
varying vec2 bend_direction;

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

	// Extend the grass vertex according to its UV coordinate.
	VERTEX.x += (UV.x - 0.5) * blade_width;
	VERTEX.y -= UV.y * blade_height;

	// Compute ambient sway (not caused by any direct influence).
	float ambient_sway = sin(TIME * ambient_sway_speed + phase * TAU) * ambient_sway_amount;

	// Make bending effects stronger toward the tip of the blade (UV.y = 1)
	// and disabled at the base (UV.y = 0)
	float vertical_weight = UV.y;

	// Compute a Displacement Gradient — how much displacement is changing across screen space.
	// The RG channels hold the displacement intensity (no difference today) so we use R.
	// The B channel holds a relative age, which is defined on the displacement viewport that
	// produces the displacement_mask_tex being sampled below.
	// TODO: This is inefficient - we're sampling the displacement texture FIVE TIMES per pixel
	//       on the screen. Right now we draw a white circle with gradient fill for the displacement
	//		 markers but what we COULD do instead is use that texture's color channels to encode
	//       not only displacement intensity in R, but also direction in GB. Then we would only need
	//		 to sample the point directly beneath the grass blade here.
	// I also hate that this if branch exists here, but you're not allowed to declare local
	// sampler2D variables so we have to...
	vec3 center;
	float right, left, up, down;
	if(sample_mode == 0) {
		vec2 texel_size = vec2(1.0) / vec2(textureSize(screen_displacement_mask_tex, 0));
		center = texture(screen_displacement_mask_tex, base_displacement_uv).rgb;
		right = texture(screen_displacement_mask_tex, base_displacement_uv + vec2(texel_size.x, 0.0)).r;
		left  = texture(screen_displacement_mask_tex, base_displacement_uv - vec2(texel_size.x, 0.0)).r;
		up    = texture(screen_displacement_mask_tex, base_displacement_uv + vec2(0.0, texel_size.y)).r;
		down  = texture(screen_displacement_mask_tex, base_displacement_uv - vec2(0.0, texel_size.y)).r;
	}
	else {
		vec2 texel_size = vec2(1.0) / vec2(textureSize(global_displacement_mask_tex, 0));
		center = texture(global_displacement_mask_tex, base_displacement_uv).rgb;
		right = texture(global_displacement_mask_tex, base_displacement_uv + vec2(texel_size.x, 0.0)).r;
		left  = texture(global_displacement_mask_tex, base_displacement_uv - vec2(texel_size.x, 0.0)).r;
		up    = texture(global_displacement_mask_tex, base_displacement_uv + vec2(0.0, texel_size.y)).r;
		down  = texture(global_displacement_mask_tex, base_displacement_uv - vec2(0.0, texel_size.y)).r;
	}

	vec2 gradient = -vec2(right - left, up - down);
	//vec2 bend_direction = normalize(gradient); // <-- creates NaN errors if the gradient is 0
	displacement_gradient_strength = length(gradient);
	bend_direction = displacement_gradient_strength > 0.001 ? normalize(gradient) : vec2(0.0, 0.0); // default no bend

	// The blue channel on the displacement mask contains the relative (0 -> 1.0) age of the displacement
	// as determined by the displacement viewport's configuration. We use this as a strength multiplier
	// that controls how long the grass stays displaced.
	float age_weight = center.b;

	float aged_displacement = center.r * age_weight;

	// Displace based on ambient sway, but not when the displacement value is high.
	VERTEX.x += ambient_sway * vertical_weight * (1.0 - aged_displacement);

	// Bend away from the center.
	vec2 right_vector = vec2(1.0, 0);
	float bend_amount = dot(bend_direction, right_vector) * aged_displacement * vertical_weight * displacement_bend_intensity;
	VERTEX.x += bend_amount;
	VERTEX.y += aged_displacement * vertical_weight * displacement_bend_intensity / 2.0;

	// Alternative bending logic:
	//vec2 bend_offset = bend_direction * center * vertical_weight * sway_amount * 3.0;
    //VERTEX.xy += bend_offset;

	// Uncomment to debug displacement amount.
	//COLOR.r = displacementAmount;
	//COLOR.gb = vec2(0,0);

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

	float aged_displacement = displacement_amount.r * displacement_amount.b;

	// Falloff: reduce effect near the base and near low displacement
	// The higher the exponent, the sharper the falloff. Lower for smoother fade.
	float fade_strength = 1.0 - pow(aged_displacement * UV.y, 0.25);

	// Color blades based on phase.
	COLOR.rgb *= max(1.0 - phase * (1.0 - UV.y), 0.3);

	// 2. Gradient strength shimmer
	//float gradient_highlight = clamp(displacement_gradient_strength * 2.0, 0.0, 1.0);
	//vec3 highlight_color = vec3(0.6, 0.9, 0.3);
	//COLOR.rgb = mix(COLOR.rgb, highlight_color, gradient_highlight * fade_strength);

	// 3. Flicker if desired
	float flicker = sin(TIME * 10.0 + phase * 6.2831) * 0.5 + 0.5;
	float flicker_strength = aged_displacement * UV.y * flicker;
	vec3 glow_color = vec3(0.8, 1.0, 0.7);
	COLOR.rgb = mix(COLOR.rgb, glow_color, flicker_strength * 0.1);

	// Near the top, we want the tips to be slightly transparent.
	float tip_transparency = 0.2;
	float tip_fade = clamp((UV.y - 0.8) / 0.2, 0.0, 1.0); // 0 → 1 as UV.y goes from tip_transparency to 1.0
	COLOR.a = mix(1.0, tip_transparency, tip_fade);

	// Uncomment to debug displacement.
	//COLOR.b = displacement_amount;
}