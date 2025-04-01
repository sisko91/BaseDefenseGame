shader_type canvas_item;

//Day/night params
global uniform vec4 day_night_color : source_color;
uniform bool is_inside = false;

void fragment()
{
    vec4 color = texture(TEXTURE, UV);

	if (!is_inside) {
		COLOR = color * day_night_color;
	}
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}