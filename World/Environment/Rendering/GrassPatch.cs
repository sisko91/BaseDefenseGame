using Godot;

// GrassPatch renders grass within the defined bounds.
public partial class GrassPatch : Node2D
{
    // The shader to use to render the grass.
    [Export] public ShaderMaterial BladeMaterial;
    // The bounds within which Grass should be rendered.
    [Export] public Rect2 Bounds;
    // How many rows of grass to spawn. These are evenly distributed across the vertical bounds of the GrassPatch.
    // Increase BladeRows and/or BladeHeight to introduce more natural layering (at slight increased cost to render).
    [Export] public int BladeRows = 5;
    // How many blades of grass should be included in each row. Increase this to increase overall grass density.
    [Export] public int BladesPerRow = 100;
    // The horizontal size of the base of each blade of grass.
    [Export] public float BladeWidth = 4.0f;
    // How tall each blade of grass should be.
    [Export] public float BladeHeight = 16.0f;

    public float BladeRowHeight => Mathf.Min(Bounds.Size.Y / BladeRows, BladeHeight * BladeRows);
    
    public override void _Ready()
    {
        GenerateGrassRows();
    }
    
    private void GenerateGrassRows()
    {
        for (int row = 0; row < BladeRows; row++)
        {
            var grassRow = new GrassPatchRowMesh();
            grassRow.Position = new Vector2(Bounds.Position.X, Bounds.Position.Y + row * BladeRowHeight);
            grassRow.RowWidth = Bounds.Size.X;
            // CRITICAL: CanvasItem shaders (2d) do not support per-instance uniforms so we have to duplicate the
            // material to configure things per-row of grass.
            grassRow.BladeMaterial = BladeMaterial.Duplicate() as ShaderMaterial;
            grassRow.BladeHeight = BladeHeight;
            grassRow.BladeWidth = BladeWidth;
            grassRow.BladeCount = BladesPerRow;
            grassRow.BladeOriginRegionHeight = BladeRowHeight;
            AddChild(grassRow);
        }
    }
}
