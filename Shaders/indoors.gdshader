shader_type canvas_item;

global uniform vec4 day_night_color : source_color;
uniform float window_light_strength = 0.5;

//Just dims/shades everything so we can light it up (with windows, indoor lights, etc)
void fragment()
{
    vec4 color = texture(TEXTURE, UV);
	vec4 day_night = vec4(day_night_color.rgb * window_light_strength, 1.0);
	COLOR = color * day_night;
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}