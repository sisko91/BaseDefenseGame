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

        SetupWorldBarriers();

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

    private void SetupWorldBarriers() {
        CreateWorldBoundary(-RegionBounds.Y / 2, new Vector2(0, 1));
        CreateWorldBoundary(-RegionBounds.Y / 2, new Vector2(0, -1));
        CreateWorldBoundary(-RegionBounds.X / 2, new Vector2(1, 0));
        CreateWorldBoundary(-RegionBounds.X / 2, new Vector2(-1, 0));
    }

    private void SetupDynamicWall()
    {
        var wallScene = GD.Load<PackedScene>("res://World/wall.tscn");
        var wall = wallScene.Instantiate<Node2D>();
        navRegion.AddChild(wall);
        wall.Position = new Vector2(0, -150);
    }

    private void CreateWorldBoundary(float distance, Vector2 normal) {
        var boundShape = new WorldBoundaryShape2D();
        boundShape.Distance = distance;
        boundShape.Normal = normal;

        var bound = new StaticBody2D();
        bound.CollisionLayer = 8; //4th layer, reserved for world bounds. TODO: Make enum for collision layers
        bound.CollisionMask = 0b1; //Only collide with player/npc. Let anything else through like projectiles
        var collisionShape = new CollisionShape2D();
        collisionShape.Shape = boundShape;
        bound.AddChild(collisionShape);
        AddChild(bound);
    }
}
