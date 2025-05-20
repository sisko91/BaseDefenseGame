shader_type canvas_item;

//Day/night params
global uniform vec4 day_night_color : source_color;
uniform bool is_inside = false;
uniform float window_light_strength = 0.1;

void fragment()
{
    vec4 color = texture(TEXTURE, UV);
	if (color.a == 0.0) {
		discard;
	}

	if (!is_inside) {
		COLOR = color * day_night_color;
	} else {
		vec4 day_night = vec4(day_night_color.rgb * window_light_strength, 1.0);
		color = color * day_night;
	}
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}