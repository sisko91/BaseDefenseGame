shader_type canvas_item;

render_mode blend_add;

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
	// compute deviation from center
	vec2 direction = UV - vec2(0.5); 
	// Practically the center, where intensity should be high but direction should be "none".
	if(length(direction) < 0.001) {
		// We choose "up" rather than "none".
		direction = vec2(0.0, -1.0);
	}
	else {
		direction = normalize(direction); // TODO: Is normalize the right thing here?
	}
	
	// R/G already carries the intensity vector, so just combine with direction.
	COLOR.rg *= direction;
}