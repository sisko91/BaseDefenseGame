using Godot;
using System;
using System.Collections.Generic;

public partial class World : Node2D
{
    // The extents / bounds of the world, in world-space units. X is the width, Y is the height. The world extends half of this distance in each direction from the World's location.
    [Export]
    public Vector2 RegionBounds { get; set; }

    // All Player nodes currently in the scene.
    public Godot.Collections.Array<Node> Players
    {
        get
        {
            return GetTree().GetNodesInGroup("Player");
        }
    }

    // All Hostile NPC nodes currently in the scene.
    public Godot.Collections.Array<Node> Hostiles
    {
        get
        {
            return GetTree().GetNodesInGroup("Hostile");
        }
    }

    // Cached reference to the background sprite defined by the .tscn.
    private Sprite2D backgroundSprite;

    // Cached reference to the navigation region defined by the .tscn
    private NavigationRegion2D navRegion;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        SetupBackground();

        SetupNavMesh();

        SetupDynamicWall();

        navRegion.BakeNavigationPolygon();
    }

    private void SetupBackground()
    {
        backgroundSprite = GetNode<Sprite2D>("Background");
        // We enable Regions + Tiling and stretch the background to cover the world RegionBounds.
        backgroundSprite.RegionEnabled = true;
        backgroundSprite.TextureRepeat = TextureRepeatEnum.Enabled;
        var rect = backgroundSprite.RegionRect;
        rect.Size = RegionBounds;
        backgroundSprite.RegionRect = rect;
    }

    private void SetupNavMesh()
    {
        navRegion = GetNode<NavigationRegion2D>("NavRegion");
        var polygon = new NavigationPolygon();
        var boundingOutline = new Vector2[] {
            new Vector2(-RegionBounds.X/2, -RegionBounds.Y/2),
            new Vector2(-RegionBounds.X/2, RegionBounds.Y/2),
            new Vector2(RegionBounds.X/2, RegionBounds.Y/2),
            new Vector2(RegionBounds.X/2, -RegionBounds.Y/2),
        };
        polygon.AddOutline(boundingOutline);
        navRegion.NavigationPolygon = polygon;
    }

    private void SetupDynamicWall()
    {
        var wallScene = GD.Load<PackedScene>("res://World/wall.tscn");
        var wall = wallScene.Instantiate<Node2D>();
        navRegion.AddChild(wall);
        wall.Position = new Vector2(0, -150);
    }
}
