shader_type canvas_item;

render_mode blend_add;

global uniform float time_delta;

// Velocity on the scale of 0 - 1. This can be the velocity direction/normal but the size of the
// vector is expected to be <= 1.0 and is interpreted as the relative speed/intensity (out of 1.0).
uniform vec2 unit_velocity = vec2(0.0, 0.0);

// This shader calculates a Red/Green/Blue color value representing the direction of displacement at
// every point on the marker sprite. The intensity of the R/G vector along the sprite indicates
// the relative power of the displacement force. Since values output by this shader include negative
// numbers it is necessary for the displacement mask to be a 16-bit float buffer (Viewport must have
// UseHdr2D = true).

// This shader assumes that there is a TEXTURE providing the base displacement intensity of this
// marker at all UV locations using the same R/G format. Red/Green are assumed opaque/directionless
// values and should (usually) have the same magnitude solely indicating the displacement strength.

// The blue channel is ignored by this shader, as it communicates relative age of the displacement
// and will either default to 1.0 or otherwise will be set by the marker base texture.

void fragment() {
	// compute the initial displacement direction as a deviation from center
	vec2 direction = UV - vec2(0.5); 
	// At the exact center the intensity should be high but direction should be "none".
	if(length(direction) < 0.001) {
		// We choose "up" rather than "none".
		direction = vec2(0.0, -1.0);
	}
	
	// We also want to include the velocity.
	direction = normalize(direction + unit_velocity);
	if(length(unit_velocity) < 0.1) {
		// dampen the intensity even further when the velocity is zero to compensate, and so 
		// stationary displacements aren't super powerful.
		direction *= time_delta * 2.0;
	}
	
	// R/G already carries the intensity vector, so just combine with direction.
	COLOR.rg *= direction;
}