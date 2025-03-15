shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

//Tint based on time of day
uniform vec4 tint;

//Called for each pixel
void fragment() {
	vec4 tex = textureLod(screen_texture, SCREEN_UV, 0.0);
	COLOR = vec4(tex.r * tint.r, tex.g * tint.g, tex.b * tint.b, tint.a * tex.a);
}
