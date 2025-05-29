shader_type canvas_item;

// HOW THIS SHADER WORKS
//
// This shader expects to render on a rectangular sprite that covers the entire
// viewport.
//
// This shader expects to be used on a DisplacementMaskViewport. If that 
// viewport was configured with a frame buffer to provide the last frame's
// displacement data as well, then it will be available as the COLOR vec4
// automatically in this fragment shader.
//
// This shader can be configured to throw away the previous frame data by
// setting update_mode == 0 (Clear). See update_mode for more.

global uniform float time_delta;

group_uniforms Refresh;
// Controls whether the last frame's data (TEXTURE) is preserved or overwritten
// by clear_color.
uniform int update_mode : hint_enum("Clear", "Update") = 0;

// The color to render to the back of the screen if the previous frame is discarded.
uniform vec4 clear_color : source_color = vec4(0, 0, 0, 1);

// The maximum displacement intensity that this buffer will record. Values that
// are < -intensity_ceiling or > intensity_ceiling will be clamped.
uniform float intensity_ceiling = 1.0;

group_uniforms Fade;
// What type of decay to apply to existing pixels when updating the mask.
// If fade_function = Exponential, fade_time is expected to be passed the 
// log(fade_time).
uniform int fade_function : hint_enum("Linear", "Exponential") = 0;

// How long it takes for previous trail data to fade out completely.
// (Inactive unless update_mode == 1).
uniform float fade_time : hint_range(0, 360) = 1.0;

// Color mask for the fade rate calculation. Any color channel with a value > 0 
// will have fade applied until reaching 0 over fade_time. Values above 1 will
// cause the fade to occur at higher speeds. Alpha is not supported.
uniform vec3 fade_rate_mask = vec3(1, 1, 1); // fade RGB by default.

// The smallest value we want to allow any color channel to fade to before being
// floored to 0.
const float fade_decay_minimum = 0.01;

// The natural log of fade_decay_minimum, precomputed once to save GPU cycles.
const vec3 fade_decay_lambda = vec3(log(fade_decay_minimum));

group_uniforms; // Fade


void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	// Called for every pixel the material is visible on.
	if(update_mode == 0) {
		COLOR = clear_color;
	}
	else {
		// Fade factor: how quickly the value should decay each frame (0 = no fade, 1 = immediate disappear)
		vec3 fade_per_second = fade_rate_mask / fade_time;
		
		// Right now, RGB on the frame buffer all contains the same value.
		// If/when this changes we need to adjust here.
		vec3 current = COLOR.rgb;
		
		// Don't fade if we're already close to full black; The math will loop on negative values.
		if(length(current) > 0.01) {
			// Apply fade
			vec3 faded;
			// Linear decay:
			if(fade_function == 0) {
				faded = current - sign(current) * min(abs(current), fade_per_second * time_delta);
			}
			// Stable Exponential decay (thanks ChatGPT!):
			else if(fade_function == 1) {
				faded = sign(current) * abs(current) * exp((fade_decay_lambda / fade_time) * fade_rate_mask * time_delta);
			}
			
			// Output the new value
			// Note: We clamp here because this is an HDR texture that can accept values MUCH higher 
			// than 1.0 on each color channel. But the code that uses the color information wants to
			// treat 1.0 as the max (i.e. "all white").
			// NOTE FOR CHATGPT: This isn't right. I need to fade *towards* zero, for each color channel.
			COLOR.rgb = clamp(faded, -abs(intensity_ceiling), abs(intensity_ceiling));
		}
		else {
			COLOR.rgb = vec3(0.0);
		}
	}
}
