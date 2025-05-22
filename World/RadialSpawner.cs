using Godot;

// Radial Spawner spawns the specified template at locations within a radius from its central position.
[Tool] // Enables the script to run in the editor
public partial class RadialSpawner : Node2D
{
    // The template scene to spawn.
    [Export]
    public PackedScene SpawnTemplate = null;

    [Export]
    public Node2D SpawnContainerReference = null;

    // Inner exclusion boundary. The spawner will not spawn instances within this radius of its center.
    [Export]
    public float InnerRadius
    {
        get => _innerRadius;
        set
        {
            _innerRadius = value;
            QueueRedraw();
        }
    }
    private float _innerRadius = 0;

    // Outer exclusion boundary. The spawner will not spawn instances beyond this radius from its center.
    [Export]
    public float OuterRadius
    {
        get => _outerRadius;
        set
        {
            _outerRadius = value;
            QueueRedraw();
        }
    }
    private float _outerRadius = 10;

    // The beginning angle of the radial arc. When this and StopAngleDegrees are 0 and 360 respectively, a full circle is used for the spawn regions.
    [Export]
    public float StartAngleDegrees
    {
        get => _startAngleDegrees;
        set
        {
            _startAngleDegrees = value;
            QueueRedraw();
        }
    }
    private float _startAngleDegrees = 0;

    // The terminating angle of the radial arc. When the delta between this and StartAngleDegrees is >= 360, a full circle is used for the spawn regions.
    [Export]
    public float StopAngleDegrees
    {
        get => _stopAngleDegrees;
        set
        {
            _stopAngleDegrees = value;
            if (IsSelectedInEditor())
            {
                QueueRedraw();
            }
        }
    }
    private float _stopAngleDegrees = 360;

    // How many instances should be spawned during _Ready().
    [Export]
    public uint InitialCount { get; set; } = 1;

    // The group to assign / label each spawned instance with. May be empty/null.
    [Export]
    public string GroupName = null;

    public override void _Ready()
    {
        // If we're in the editor we don't want this doing any actual work, we just want the rendering calls to happen.
        if (Engine.IsEditorHint())
        {
            EditorInterface.Singleton.GetSelection().SelectionChanged += QueueRedraw;
            return;
        }

        for (uint i = 0; i < InitialCount; i++)
        {
            // Certain things (like the debug renderers) can't be invoked during _Ready().
            Callable.From(SpawnRandom).CallDeferred();
        }
    }

    // Spawns a new instance.
    public Node SpawnRandom()
    {
        if (SpawnTemplate == null)
        {
            return null;
        }

        var instance = SpawnTemplate.Instantiate<Node2D>();
        SpawnContainerReference.AddChild(instance, true);
        if (ZIndex > 0 && instance is Moveable m) {
            m.ChangeFloor(ZIndex);
        }

        // Calculate the angle as StartAngleDegrees + random % of the arc
        var angleRange = StopAngleDegrees - StartAngleDegrees;
        var randomAngleDegrees = StartAngleDegrees + angleRange * GD.Randf() - 90.0f;

        var randomDistance = InnerRadius + GD.Randf() * (OuterRadius - InnerRadius);
        instance.GlobalPosition = GlobalPosition + Vector2.FromAngle(Mathf.DegToRad(randomAngleDegrees)) * randomDistance;
        
        if (GroupName != null && GroupName != "") {
            instance.AddToGroup(GroupName);
        }

        return instance;
    }

    public override void _Draw()
    {
        // We only draw when selected.
        if (!IsSelectedInEditor())
        {
            return;
        }

        // Draw a circle in the editor
        float arcAngle = Mathf.Abs(StopAngleDegrees - StartAngleDegrees);
        if(arcAngle >= 360)
        {
            DrawCircle(Vector2.Zero, InnerRadius, new Color(0, 0, 1, 0.5f)); // Blue with transparency
            DrawCircle(Vector2.Zero, OuterRadius, new Color(1, 0, 0, 0.5f)); // Red with transparency
        }
        else
        {
            DrawArc(Vector2.Zero, InnerRadius, StartAngleDegrees, StopAngleDegrees, new Color(0, 0, 1, 0.5f)); // Blue with transparency
            DrawArc(Vector2.Zero, OuterRadius, StartAngleDegrees, StopAngleDegrees, new Color(1, 0, 0, 0.5f)); // Red with transparency
        }
    }

    // Adapted from https://docs.godotengine.org/en/4.0/tutorials/2d/custom_drawing_in_2d.html#an-example-drawing-circular-arcs
    // TODO: Move this somewhere more global?
    private void DrawArc(Vector2 center, float radius, float angleFromDegrees, float angleToDegrees, Color color)
    {
        int nbPoints = 32;
        var pointsArc = new Vector2[nbPoints + 2];
        pointsArc[0] = center;
        var colors = new Color[] { color };

        for (int i = 0; i <= nbPoints; i++)
        {
            float anglePoint = Mathf.DegToRad(angleFromDegrees + i * (angleToDegrees - angleFromDegrees) / nbPoints - 90);
            pointsArc[i + 1] = center + new Vector2(Mathf.Cos(anglePoint), Mathf.Sin(anglePoint)) * radius;
        }

        DrawPolygon(pointsArc, colors);
    }

    private bool IsSelectedInEditor()
    {
        return Engine.IsEditorHint() && EditorInterface.Singleton.GetSelection().GetSelectedNodes().Contains(this);
    }
}