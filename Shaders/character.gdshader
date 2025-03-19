shader_type canvas_item;

//hit shader params
uniform bool is_hit = false;

//Day/night params
global uniform vec4 day_night_color : source_color;
uniform bool is_inside = false;

//hit shader config
uniform float time_scale = 20; //Blink animation speed
uniform float gb_ceil = 0.75; //Max color green/blue can reach

vec4 hit(vec4 color) {
	float time_cycle = sin(TIME * time_scale);
	return vec4(1.0,
	            color.g * time_cycle * gb_ceil,
				color.b * time_cycle * gb_ceil,
				color.a);
}

void fragment() {
	vec4 color = texture(TEXTURE, UV);
	if (is_hit) {
		color = hit(color);
	}
	
	if (is_inside) {
		color = color / day_night_color;
	}
	
	COLOR = color;
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}