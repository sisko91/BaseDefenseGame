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

    // Cached reference to the background sprite defined by world.tscn.
    private Sprite2D backgroundSprite;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        backgroundSprite = GetNode<Sprite2D>("Background");
        // We enable Regions + Tiling and stretch the background to cover the world RegionBounds.
        backgroundSprite.RegionEnabled = true;
        backgroundSprite.TextureRepeat = TextureRepeatEnum.Enabled;
        var rect = backgroundSprite.RegionRect;
        rect.Size = RegionBounds;
        backgroundSprite.RegionRect = rect;
    }
}
