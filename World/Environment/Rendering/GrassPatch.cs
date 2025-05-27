using Godot;

// GrassPatch renders grass within the defined bounds.
[Tool]
public partial class GrassPatch : Sprite2D
{
    // The shader to use to render the grass.
    [Export] public ShaderMaterial BladeMaterial;
    // The base color of all blades. This is also the color of the backing texture if EnableBackingTexture==true.
    [Export] public Color BladeColor = Godot.Colors.Green;
    // If true, a flat texture will be rendered behind the grass blades over this row of grass up to the BladeOriginRegionHeight. The color of the texture is controlled by BladeColor.
    [Export] public bool EnableBackingTexture = false;
    // The bounds within which Grass should be rendered.
    [Export]
    public Vector2 Size
    {
        get => _size;
        set
        {
            _size = value;
            QueueRedraw();
        }
    }
    private Vector2 _size;
    
    // How many rows of grass to spawn. These are evenly distributed across the vertical bounds of the GrassPatch.
    // Increase BladeRows and/or BladeHeight to introduce more natural layering (at slight increased cost to render).
    [Export] public int BladeRows = 5;
    // How many blades of grass should be included in each row. Increase this to increase overall grass density.
    [Export] public int BladesPerRow = 100;
    // The horizontal size of the base of each blade of grass.
    [Export] public float BladeWidth = 4.0f;
    // How tall each blade of grass should be.
    [Export] public float BladeHeight = 16.0f;
    // If true, the global displacement mask is used for determining local displacements to this grass patch. Otherwise, the screen-space displacement mask is assumed.
    [Export] public bool UseGlobalDisplacementMask = false;

    public float BladeRowHeight => Mathf.Min(Size.Y / BladeRows, BladeHeight * BladeRows);

    public override void _Draw()
    {
        if (IsSelectedInEditor())
        {
            DrawRect(new Rect2(Vector2.Zero, Size), BladeColor, true);
        }
    }
    
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return; // Don't generate anything in the editor.
        }
        
        if (EnableBackingTexture)
        {
            GenerateBackingTexture();
            Offset = Size / 2f;
        }

        if (Texture != null)
        {
            // Stretch the texture to fit the bounds of the grass patch.
            RegionEnabled = true;
            RegionRect = new Rect2(Vector2.Zero, Size);
        }
        GenerateGrassRows();
    }
    
    private void GenerateBackingTexture()
    {
        var backingTexture = new GradientTexture2D();
        backingTexture.Fill = GradientTexture2D.FillEnum.Linear;
        // We generate a small texture because it's all a flat color and doesn't matter.
        backingTexture.Width = 32;
        backingTexture.Height = 32;
        backingTexture.Gradient = new Gradient();
        backingTexture.Gradient.SetColor(0, BladeColor);
        backingTexture.Gradient.SetColor(1, BladeColor);
        Texture = backingTexture;
    }
    
    private void GenerateGrassRows()
    {
        for (int row = 0; row <= BladeRows; row++)
        {
            var grassRow = new GrassPatchRowMesh();
            grassRow.Position = new Vector2(0, row * BladeRowHeight);
            grassRow.RowWidth = Size.X;
            // CRITICAL: CanvasItem shaders (2d) do not support per-instance uniforms so we have to duplicate the
            // material to configure things per-row of grass.
            grassRow.BladeMaterial = BladeMaterial.Duplicate() as ShaderMaterial;
            // Make sure all grass has the same (base) blade width and color.
            grassRow.BladeMaterial?.SetShaderParameter("blade_width", BladeWidth);
            grassRow.BladeMaterial?.SetShaderParameter("blade_height", BladeHeight);
            grassRow.BladeMaterial?.SetShaderParameter("blade_color", BladeColor);
            // Tell the shader it should use the global displacement mask.
            // 0 = ScreenSpace, 1 = Global.
            grassRow.BladeMaterial?.SetShaderParameter("sample_mode", UseGlobalDisplacementMask ? 1 : 0);
            grassRow.BladeHeight = BladeHeight;
            grassRow.BladeWidth = BladeWidth;
            grassRow.BladeCount = BladesPerRow;
            grassRow.BladeOriginRegionHeight = BladeRowHeight;
            
            AddChild(grassRow);
        }
    }
    
    private bool IsSelectedInEditor()
    {
        return Engine.IsEditorHint() && EditorInterface.Singleton.GetSelection().GetSelectedNodes().Contains(this);
    }
}
