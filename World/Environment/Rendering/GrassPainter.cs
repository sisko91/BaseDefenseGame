using Godot;

// GrassPainter follows a Node2D and continuously repositions grass rendering nodes to maintain coverage over the bounds
// specified. GrassPainter can optionally track the main viewport instead.
public partial class GrassPainter : Node2D
{
    // How wide a standard row of grass is in unscaled screen pixels. This is the rectangle that will be instantiated
    // in a matrix around the painter and filled with grass. If the grass height is configured greater than the Y size
    // then grass from one layer will overlap grass in the next.
    [Export] public Vector2I RowSize = new Vector2I(256, 16);
    
    [ExportCategory("Population")]

    [Export] public float GrassHeight = 24f;
    [Export] public float BladeWidth = 2f;
    
    // Blade Count is normally a function of RowSide / BladeWidth; But adjusting this multiplier influences the total.
    [Export] public float BladeCountMultiplier = 1f;
    
    // The shader to use to render the grass.
    [Export] public ShaderMaterial BladeMaterial;
    // The base color of all blades. This is also the color of the backing texture if EnableBackingTexture==true.
    [Export] public Color BladeColor = Godot.Colors.Green;
    
    [ExportCategory("Proximity")] 
    // If not null, this node's GlobalPosition will be mirrored by the GrassPainter. The bounds will still be derived 
    // from the main viewport.
    [Export] public Node2D OriginTrackingNode = null;

    private GrassPatchRowMesh[,] GrassRows;
    
    // The logical index / delineation in the GrassRows grid where the center is.
    private Vector2I GrassRowsCenter => new (GrassRows.GetLength(0) / 2, GrassRows.GetLength(1) / 2);
    
    // Where the painter was when the grass around it was first instantiated.
    private Vector2 TrackedStartPosition;
    public override void _Ready()
    {
        Callable.From(DeferredReady).CallDeferred();
    }

    private void DeferredReady()
    {
        var bounds = ReconstructBounds();
        TrackedStartPosition = bounds.GetCenter();
        GlobalPosition = TrackedStartPosition;
        
        // Initialize our grid of grass rows. We want the grid to be 1 column of grass wider on each end, and at least
        // 1 row of grass taller on each end, so that we can shuffle which grass is in which position as the origin
        // point moves.
        
        // First figure out how many columns we need to fill the screen, based on the defined row size.
        var viewport = Main.Instance.GetViewport();
        var screenSize = viewport.GetVisibleRect().Size;
        // TODO: DECIDE WHAT TO DO ABOUT ZOOM
        //var camera = viewport.GetCamera2D();
        //var worldSize = screenSize * camera.Zoom;

        Vector2 gridDimensions = screenSize / RowSize;
        // Round up to the nearest even whole number + 2 on each dimension so we have over-hang on all sides
        // (if this introduces shimmering on the edges, make it +4)
        var gridSize = new Vector2I(
            Mathf.CeilToInt(gridDimensions.X / 2f) * 2 + 2,
            Mathf.CeilToInt(gridDimensions.Y / 2f) * 2 + 2
        );
        GrassRows = new GrassPatchRowMesh[gridSize.X, gridSize.Y];
        
        // Build each grass row.
        GenerateGrassRows();
    }

    private Rect2 ReconstructBounds()
    {
        var viewport = Main.Instance.GetViewport();
        var camera = viewport.GetCamera2D();
        var screenSize = viewport.GetVisibleRect().Size;
        var worldSize = screenSize * camera.Zoom;
        if (OriginTrackingNode == null)
        {
            return new Rect2(camera.GetScreenCenterPosition() - worldSize / 2f, worldSize);
        }
        else
        {
            return new Rect2(OriginTrackingNode.GlobalPosition - worldSize / 2f, worldSize);
        }
    }

    private void GenerateGrassRows()
    {
        var center = GrassRowsCenter;
        var rows = GrassRows.GetLength(1);
        var cols = GrassRows.GetLength(0);
        var bladeCountPerRow = Mathf.FloorToInt((RowSize.X * BladeCountMultiplier) / Mathf.CeilToInt(BladeWidth));
        GD.Print($"Blade Rows: {rows}\n" +
                 $"Blade Columns: {cols}\n" +
                 $"Blades Per Row: {bladeCountPerRow}\n" +
                 $"Blades Total: {bladeCountPerRow * rows * cols}");
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var grassRow = new GrassPatchRowMesh
                {
                    RowWidth = RowSize.X,
                    BladeOriginRegionHeight = RowSize.Y,
                    BladeHeight = GrassHeight,
                    BladeCount = bladeCountPerRow,
                    BladeWidth = BladeWidth,
                    BladeColor = BladeColor,
                    BladeMaterial = BladeMaterial?.Duplicate() as ShaderMaterial
                };
                
                // Calculate the local position relative to the center cell
                var offset = new Vector2(
                    (x - center.X) * RowSize.X,
                    (y - center.Y) * RowSize.Y
                );
                grassRow.Position = offset;
                
                GrassRows[x, y] = grassRow;
                AddChild(grassRow);
            }
        }
    }

    public override void _Process(double delta)
    {
        var bounds = ReconstructBounds();
        
        var currentCenter = bounds.GetCenter();

        Vector2 deltaFromStart = currentCenter - TrackedStartPosition;
        Vector2I shiftAmount = new(
            Mathf.FloorToInt(deltaFromStart.X / RowSize.X),
            Mathf.FloorToInt(deltaFromStart.Y / RowSize.Y)
        );

        if (shiftAmount != Vector2I.Zero)
        {
            ShiftGridBy(shiftAmount);
            TrackedStartPosition += new Vector2(shiftAmount.X * RowSize.X, shiftAmount.Y * RowSize.Y);
            GlobalPosition += new Vector2(shiftAmount.X * RowSize.X, shiftAmount.Y * RowSize.Y);
        }
    }
    
    private void ShiftGridBy(Vector2I shiftAmount)
    {
        int cols = GrassRows.GetLength(0);
        int rows = GrassRows.GetLength(1);

        // Temporary array to hold new positions
        var newGrid = new GrassPatchRowMesh[cols, rows];

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                // Wrap around using mod
                int newX = Mod(x - shiftAmount.X, cols);
                int newY = Mod(y - shiftAmount.Y, rows);

                var patch = GrassRows[x, y];
                newGrid[newX, newY] = patch;

                // Reposition the mesh in local space
                Vector2 offsetFromCenter = new Vector2(
                    (newX - GrassRowsCenter.X) * RowSize.X,
                    (newY - GrassRowsCenter.Y) * RowSize.Y
                );
                patch.Position = offsetFromCenter;
            }
        }

        GrassRows = newGrid;
    }

    private int Mod(int x, int m)
    {
        return (x % m + m) % m; // ensures result is always positive
    }
}
