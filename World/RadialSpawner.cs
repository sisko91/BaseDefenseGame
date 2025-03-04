using Godot;
using System;

// Radial Spawner spawns the specified template at locations within a radius from its central position.
[Tool] // Enables the script to run in the editor
public partial class RadialSpawner : Node2D
{
    // The template scene to spawn.
    [Export]
    public PackedScene SpawnTemplate = null;

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

    // How many instances should be spawned during _Ready().
    [Export]
    public uint InitialCount { get; set; } = 1;

    // The group to assign / label each spawned instance with. May be empty/null.
    [Export]
    public string GroupName = null;

    public override void _Ready()
    {
        for(uint i = 0; i < InitialCount; i++)
        {
            SpawnRandom();
        }
    }

    // Spawns a new instance.
    public Node SpawnRandom()
    {
        if(SpawnTemplate == null)
        {
            return null;
        }

        var instance = SpawnTemplate.Instantiate<Node2D>();

        var randomAngle = GD.Randf() * Mathf.Pi * 2;
        var randomDistance = InnerRadius + GD.Randf() * (OuterRadius - InnerRadius);
        instance.Position = Vector2.FromAngle(randomAngle) * randomDistance;

        if (GroupName != null && GroupName != "") {
            instance.AddToGroup(GroupName);
        }

        AddChild(instance);

        return instance;
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint()) return; // Only draw in the editor

        // Draw a circle in the editor
        DrawCircle(Vector2.Zero, InnerRadius, new Color(0, 0, 1, 0.5f)); // Blue with transparency
        DrawCircle(Vector2.Zero, OuterRadius, new Color(1, 0, 0, 0.5f)); // Red with transparency
    }
}
