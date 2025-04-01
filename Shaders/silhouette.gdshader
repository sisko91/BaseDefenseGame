shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

void fragment() {
	vec4 sprite_color = texture(TEXTURE, UV);
	vec4 background_color = texture(screen_texture, SCREEN_UV);

	if (all(lessThan(abs(sprite_color - background_color), vec4(0.1)))) {
		COLOR = sprite_color;
	} else {
		COLOR = vec4(0, 0, 0, 0.5 * sprite_color.a);
	}
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}