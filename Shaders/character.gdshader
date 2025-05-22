shader_type canvas_item;

//hit shader params
uniform bool is_hit = false;

//Day/night params
global uniform vec4 day_night_color : source_color;
uniform bool is_inside = false;
uniform float window_light_strength = 0.1;

//hit shader config
uniform float time_scale = 20; //Blink animation speed
uniform float gb_ceil = 0.75; //Max color green/blue can reach

varying vec4 modulate;
void vertex()
{
    modulate = COLOR;
}

vec4 hit(vec4 color) {
	float time_cycle = sin(TIME * time_scale);
	return vec4(1.0,
	            color.g * time_cycle * gb_ceil,
				color.b * time_cycle * gb_ceil,
				color.a);
}

void fragment() {
	vec4 color = texture(TEXTURE, UV);
	if (color.a == 0.0) {
		discard;
	}

	if (is_hit) {
		color = hit(color);
	}

	if (!is_inside) {
		color = color * day_night_color;
	} else {
		vec4 day_night = vec4(day_night_color.rgb * window_light_strength, 1.0);
		color = color * day_night;
	}

	COLOR = vec4(color.rgb, color.a * modulate.a);
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}