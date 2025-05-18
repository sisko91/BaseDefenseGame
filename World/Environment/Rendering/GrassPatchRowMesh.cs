using Godot;

// GrassPatchRowMesh renders grass in a horizontal row across the screen.
//
// This node is not designed to be rotated or scaled. It is designed to be layered vertically with other instances of itself
// so that other elements (such as buildings and characters) can be positioned in between the layers and have Y-sorting naturally
// layer those entities in between the rows of grass to create depth.
//
// This node may be used on its own, but is typically created dynamically as part of a GrassPatch.
public partial class GrassPatchRowMesh : Node2D
{
    // The Shader that actually renders the blades of grass.
    [Export] public ShaderMaterial BladeMaterial;
    // The width of the row, in global coordinates. This extends to the right in +X direction from the row's origin.
    [Export] public float RowWidth = 100.0f;
    // The number of individual blades of grass to include in the row.
    [Export] public int BladeCount = 30;
    // How wide each blade of grass should be. This has no bearing on density of blades created or each blade's position relative to the row's origin (though it will affect visual density).s
    [Export] public float BladeWidth = 4.0f;
    // How tall each blade of grass should be. This has no bearing on where each blade of grass is positioned relative to the row's origin.
    [Export] public float BladeHeight = 16.0f;
    // How "tall" in Y units the area is where the base of a blade of grass can be placed. Each blade will be randomly offset by some amount less than or equal to this when placed in a row.
    [Export] public float BladeOriginRegionHeight = 10.0f;
    
    private Vector2[] Vertices;
    private Vector2[] UVs;
    private Color[] Colors;
    private int[] Indices;

    public float BladeCellWidth => RowWidth / BladeCount;

    public override void _Ready()
    {
        GenerateGrassMesh();
        QueueRedraw();
    }

    public override void _Draw()
    {
        base._Draw();
        var canvasRid = GetCanvasItem();
        RenderingServer.CanvasItemClear(canvasRid);
        RenderingServer.CanvasItemSetMaterial(canvasRid, BladeMaterial.GetRid());
        RenderingServer.CanvasItemAddTriangleArray(
            item: canvasRid,
            indices: Indices,
            points: Vertices,
            colors: Colors,
            uvs: UVs,
            bones: null,
            weights: null,
            texture: default(Rid) // TODO:
        );
    }
    
    // Generates a single mesh of vertices for all blades of grass within this row. Each blade is its own quad made up of two triangles.
    // The UV coordinates are always defined in blade-space i.e. V = 0 indicates the bottom of the blade and V = 1 indicates the top.
    // Color channels are used to pass information about each blade of grass such as random phase or height (used to vary each blade in shader logic).
    private void GenerateGrassMesh()
    {
        Vertices = new Vector2[BladeCount * 4];
        UVs = new Vector2[BladeCount * 4];
        Colors = new Color[BladeCount * 4];
        Indices = new int[BladeCount * 6];

        int vi = 0;
        int ii = 0;
        
        for (int col = 0; col < BladeCount; col++)
        {
            // Jitter within each cell
            float jitterX = (float)GD.RandRange(-BladeCellWidth * 0.4f, BladeCellWidth * 0.4f);
            float jitterY = (float)GD.RandRange(0, BladeOriginRegionHeight * 0.4f);
            float x = (col + 0.5f) * BladeCellWidth + jitterX;
            float y = jitterY;
            Vector2 basePos = new Vector2(x, y);

            Vector2 bl = basePos + new Vector2(-BladeWidth / 2f, 0);
            Vector2 br = basePos + new Vector2(BladeWidth / 2f, 0);
            Vector2 tl = basePos + new Vector2(-BladeWidth / 2f, -BladeHeight);
            Vector2 tr = basePos + new Vector2(BladeWidth / 2f, -BladeHeight);

            Vertices[vi + 0] = bl;
            Vertices[vi + 1] = br;
            Vertices[vi + 2] = tl;
            Vertices[vi + 3] = tr;

            UVs[vi + 0] = new Vector2(0, 0);
            UVs[vi + 1] = new Vector2(1, 0);
            UVs[vi + 2] = new Vector2(0, 1);
            UVs[vi + 3] = new Vector2(1, 1);

            // We pack multiple values into the vertex Color channel, for interpretation within the shader:
            // Red - "Phase" which is a random seed value computed per-blade.
            // Green - "CenterX" - this is the centerpoint X value of the blade at its base (local coordinates) so that
            //         Vertex (or Fragment) shaders can use that as a constant.
            // Blue - Unused / future TBD.
            float vertexPhase = GD.Randf();
            float vertexCenterX = (bl.X + br.X) / 2f;
            Color vertexColor = new Color(vertexPhase, vertexCenterX, 0);

            Colors[vi + 0] = vertexColor;
            Colors[vi + 1] = vertexColor;
            Colors[vi + 2] = vertexColor;
            Colors[vi + 3] = vertexColor;

            // Two triangles
            Indices[ii + 0] = vi + 0;
            Indices[ii + 1] = vi + 1;
            Indices[ii + 2] = vi + 2;
            Indices[ii + 3] = vi + 2;
            Indices[ii + 4] = vi + 1;
            Indices[ii + 5] = vi + 3;

            vi += 4;
            ii += 6;
        }
    }
}
