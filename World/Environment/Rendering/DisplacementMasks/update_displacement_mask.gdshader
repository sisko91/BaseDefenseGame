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

// Controls whether the last frame's data (TEXTURE) is preserved or overwritten
// by clear_color. 0 = clear, 1 = update with last frame.
uniform int update_mode : hint_enum("Clear", "Update") = 0;

// The color to render to the back of the screen if the previous frame is discarded.
uniform vec4 clear_color : source_color = vec4(0, 0, 0, 1);

// How long it takes for previous trail data to fade out completely 
// (requires update_mode == 1).
uniform float fade_time : hint_range(0, 360) = 1.0;

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
		float fade_per_second = 1.0 / fade_time;
		
		// Right now, RGB on the frame buffer all contains the same value.
		// If/when this changes we need to adjust here.
		float current = COLOR.r;

		// Apply fade
		float faded = max(current - fade_per_second * time_delta, 0.0);

		// Output the new value
		COLOR = vec4(faded, faded, faded, 1.0);
	}
}
