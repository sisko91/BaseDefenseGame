shader_type canvas_item;

global uniform vec4 day_night_color : source_color;

void fragment()
{
    vec4 color = texture(TEXTURE, UV);

	//Canvas modulate multiplies the entire canvas by this value before the light pass
	//If we divide by the same value before the light pass, we can negate the day/night effect inside
	COLOR = color / day_night_color;
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}