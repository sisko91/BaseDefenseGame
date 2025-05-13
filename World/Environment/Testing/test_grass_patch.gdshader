shader_type canvas_item;

uniform float sway_amount = 5.0;
uniform float sway_speed = 2.0;

void vertex() {
    float phase = COLOR.r;
    float sway = sin(TIME * sway_speed + phase * 6.2831) * sway_amount;
    
    // Make sway stronger toward the tip of the blade (UV.y = 1)
    float vertical_weight = UV.y;
    VERTEX.x += sway * vertical_weight;
}

void fragment()
{
    //vec4 color = texture(TEXTURE, UV);
	//COLOR = vec4(0, 1, 0, 1);
	COLOR.g = COLOR.r * 0.5f;
	COLOR.r = 0.0;
}