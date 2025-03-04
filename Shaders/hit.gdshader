shader_type canvas_item;

uniform float time_scale = 20; //Blink animation speed
uniform float gb_ceil = 0.75; //Max color green/blue can reach

//Called for each pixel
void fragment() {
	float time_cycle = sin(TIME * time_scale);
	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = vec4(1.0,
	             tex_color.g * time_cycle * gb_ceil,
				 tex_color.b * time_cycle * gb_ceil,
				 tex_color.a);
}