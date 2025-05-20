shader_type canvas_item;
const vec4 inside_color = vec4(0.25, 0.25, 0.25, 1);

//Just dims/shades everything so we can light it up (with windows, indoor lights, etc)
void fragment()
{
    vec4 color = texture(TEXTURE, UV);
	COLOR = color * inside_color;
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}