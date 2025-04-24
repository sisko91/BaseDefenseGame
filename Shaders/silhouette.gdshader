shader_type canvas_item;

global uniform sampler2D player_texture;

//Would prefer to use an array here, but not yet supported for global uniforms:
//https://github.com/godotengine/godot-proposals/discussions/9553
global uniform vec2 player_screen_uv_start;
global uniform vec2 player_screen_uv_end;

void fragment() {
	//Check if this pixel is inside the player's sprite rect
	if (SCREEN_UV.x >= player_screen_uv_start.x && SCREEN_UV.y >= player_screen_uv_start.y &&
	    SCREEN_UV.x <= player_screen_uv_end.x && SCREEN_UV.y <= player_screen_uv_end.y) {

		//Get the color of the player sprite at this position
		vec2 player_sprite_uv = vec2(
			(SCREEN_UV.x - player_screen_uv_start.x) / (player_screen_uv_end.x - player_screen_uv_start.x),
			(SCREEN_UV.y - player_screen_uv_start.y) / (player_screen_uv_end.y - player_screen_uv_start.y));
		vec4 player_color = texture(player_texture, player_sprite_uv);

		//Filtering out transparent and partially-transparent pixels.
		//The latter provides more detail on pixel-y art. 0.25 looks good on this sprite,
		//might be different for other sprites
		if (player_color.a > 0.25) {
			vec4 color = texture(TEXTURE, UV);
			COLOR = vec4(color.rgb * 0.5, color.a);
		}
	}
}
