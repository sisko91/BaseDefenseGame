# Rendering Notes for Gurdy.

## Displacement Masks
**Root:** /World/Environment/

**Components**
* DisplacementMaskViewport (.tscn && .cs)
* DisplacementMarker.cs
	
Displacement Masks are screen-sized virtual textures that a subviewport renders information to so that we can sample that rendered data from shaders rendering the main screen of the game. The data rendered to this virtual texture is generally simplified i.e. they provide color in areas that are either "in" or "out" by some qualifying criteria. For example white circles may be drawn to this texture anywhere on screen that a character is currently standing.

**How it works:** 

There is a custom `DisplacementMaskViewport` introduced to our Main scene and lives as a sibling to our main PlayerCamera. This viewport has its own `DisplacementCamera` which mirrors the position and zoom factors of PlayerCamera exactly each frame; that camera renders to a texture that never gets presented to the screen.
	
The `DisplacementMaskViewport` has a `MarkerRoot` child node, which serves as a container for ALL things that should get rendered to the "displacement mask" texture owned by `DisplacementCamera`. Entities in the game which know about the `DisplacementMaskViewport` can access it and add their own e.g. Sprite2D nodes as children to the viewport's `MarkerRoot`. Those nodes will then get rendered *exclusively* to the "displacement mask" and not render to the real scene. 

During rendering, this viewport's texture can be accessed and provided to shaders, which can then sample this displacement texture to do interesting things.
	
**Example Use: Grass**

All characters in the game include a `GrassDisplacementMarker` which is continuously updated to have the same position as the character, but is a child of the `DisplacementMaskViewport`'s container. `GrassDisplacementMarker`s render white circles to the displacement mask texture wherever a character is on screen.

Before grass is rendered, the displacement mask texture is set as a `uniform` parameter for the grass material shader. The shader associated with the grass's material (on an instance of GrassPatch.cs or GrassPatchRowMesh.cs) is written to sample the displacement texture, and anywhere that displacement color is found the grass is faded/bent/removed.

### Grass
**Root:** /World/Environment/
**Components**
* GrassPatch.cs 
* GrassPatchRowMesh.cs
* grass_patch.gdshader

Grass is rendered to the screen as many individual quads, but using a single draw pass for performance. Think of it as one large square mesh made up of hundreds/thousands of tiny rectangles - because that's exactly what it is.

**How it works:**

GrassPatchRowMesh.cs renders a single horizontal line of grass rectangles as a single mesh, talking directly to the `RenderingServer2D` to push vertex information for all grass in the row. Each grass blade is 4 vertices / 2 triangles / 1 quad.

The vertices of each blade of grass are colored with packed values that are later interpreted by the shader:
	1. Red with a value between [0, 1.0] to indicate what "phase" the blade is in. The phase is a random seed used for things like to ensure each blade of grass has a unique time offset inside its vertex shader for how it sways in the wind, so that all blades don't all sway at the same time and in the same way.
	2. Green with an unbound float value to capture the X-position of the middle of each blade of grass at its base. This is difficult to recover from a vertex or fragment shader and is a useful constant.
	3. Blue - TBD. No current use and is set to 0.0.
	
The shader for the grass is written to extract these values from each vertex color and use them to dynamically-offset the vertex position, simulating wind-sway and other physical interactions. The amount that the grass is offset depends on the Y-height of each vertex, so the tops of the blades move most, and the bottom of the blade doesn't move at all. 

GrassPatch.cs dynamically creates and vertically-stacks rows of GrassPatchRowMesh instances in order to provide width to the grass instead of it being a single line.
>The critical reason that GrassPatch is independent of GrassPatchRowMesh is because it works well with Y-Sorting. If the grass was one rectangular patch it would all share one origin point, and we wouldn't be able to Y-sort the grass well. By using multiple rows of meshes, each mesh gets its own Y-origin and sorts more naturally.
