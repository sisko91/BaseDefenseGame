shader_type canvas_item;

global uniform sampler2D player_texture : repeat_disable, filter_nearest;

//Would prefer to use an array here, but not yet supported for global uniforms:
//https://github.com/godotengine/godot-proposals/discussions/9553
global uniform vec2 player_screen_uv_start;
global uniform vec2 player_screen_uv_end;

void fragment() {
	//Check if this pixel is inside the player's sprite rect
	if (SCREEN_UV.x >= player_screen_uv_start.x && SCREEN_UV.y >= player_screen_uv_start.y &&
	    SCREEN_UV.x <= player_screen_uv_end.x && SCREEN_UV.y <= player_screen_uv_end.y) {

		//TODO: Check if the overlapping player pixel is not transparent
		//For now, just color the whole rect
		vec4 color = texture(TEXTURE, UV);
		COLOR = vec4(color.rgb * 0.5, color.a);
	}
}
