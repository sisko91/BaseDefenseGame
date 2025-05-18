shader_type canvas_item;

global uniform vec4 day_night_color : source_color;
varying float modulate_alpha;

void vertex() {
	modulate_alpha = COLOR.a;
}

void fragment()
{
    vec4 color = texture(TEXTURE, UV);
	COLOR = color * day_night_color * vec4(1.0, 1.0, 1.0, modulate_alpha);
}

void light()
{
    vec4 color = texture(TEXTURE, UV);
    LIGHT = vec4(color.rgb * LIGHT_COLOR.rgb * LIGHT_ENERGY, LIGHT_COLOR.a);
}