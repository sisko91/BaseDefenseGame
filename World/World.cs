using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class World : Node2D
{
    // The extents / bounds of the world, in world-space units. X is the width, Y is the height. The world extends half of this distance in each direction from the World's location.
    [Export]
    public Vector2 RegionBounds {
        get => _regionBounds;
        set
        {
            _regionBounds = value;
            // Update the world rect global uniform if it changes.
            UpdateGlobalUniforms();
        }
    }

    private Vector2 _regionBounds;

    // All Player nodes currently in the scene.
    public List<Player> Players
    {
        get
        {
            return GetTree().GetNodesInGroup("Player").Cast<Player>().ToList();
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

    public Godot.Collections.Array<Node> Crystals
    {
        get
        {
            return GetTree().GetNodesInGroup("Crystal");
        }
    }

    public DayNight DayNight
    {
        get
        {
            return GetNode<DayNight>("DayNight");
        }
    }

    //Y-sorted background layer
    public Node2D Background {
        get {
            return GetNode<Node2D>("Background");
        }
    }

    //Y-sorted middle layer. Most things (player, enemies) go here
    public Node2D Middleground
    {
        get
        {
            return GetNode<Node2D>("Middleground");
        }
    }

    //Y-sorted foreground layer. Most projectiles go here. This affords some
    //additional flexibility over just using ZIndex, like relative ordering
    //For example, bullets inside a house should draw above things in the house, but behind things outside the house
    public Node2D Foreground {
        get {
            return GetNode<Node2D>("Foreground");
        }
    }

    // Cached reference to the background sprite defined by the .tscn.
    private Sprite2D backgroundSprite;

    // A map/region for each floor
    public List<Rid> NavMaps;
    private List<NavigationRegion2D> NavRegions;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        SetupBackground();
        SetupNavMesh();
        SetupWorldBarriers();
        
        UpdateGlobalUniforms();
    }

    //TODO: Only rebake floors we need to
    public void RebakeNavMesh() {
        if (NavRegions == null) {
            return;
        }
        foreach (NavigationRegion2D navRegion in NavRegions) {
            navRegion.BakeNavigationPolygon();
        }
    }

    private void SetupBackground()
    {
        backgroundSprite = GetNodeOrNull<Sprite2D>("Background");
        if (backgroundSprite == null) {
            return;
        }
        // We enable Regions + Tiling and stretch the background to cover the world RegionBounds.
        backgroundSprite.RegionEnabled = true;
        backgroundSprite.TextureRepeat = TextureRepeatEnum.Enabled;
        var rect = backgroundSprite.RegionRect;
        rect.Size = RegionBounds;
        backgroundSprite.RegionRect = rect;
    }

    private void SetupNavMesh()
    {
        NavMaps = new List<Rid>();
        NavRegions = new List<NavigationRegion2D>();
        var boundingOutline = new Vector2[] {
            new Vector2(-RegionBounds.X / 2, -RegionBounds.Y / 2),
            new Vector2(-RegionBounds.X / 2, RegionBounds.Y / 2),
            new Vector2(RegionBounds.X / 2, RegionBounds.Y / 2),
            new Vector2(RegionBounds.X / 2, -RegionBounds.Y / 2),
        };

        for (int i = 0; i < GetMaxFloorsInScene(); i++) {
            var navRegion = new NavigationRegion2D();
            NavigationPolygon navPolygon = new NavigationPolygon();
            navPolygon.SourceGeometryMode = NavigationPolygon.SourceGeometryModeEnum.GroupsWithChildren;
            navPolygon.SourceGeometryGroupName = NavigationConfig.FLOOR_GROUP_PREFIX + i;
            navPolygon.AgentRadius = NavigationConfig.NAV_POLYGON_AGENT_RADIUS;
            navPolygon.ParsedCollisionMask = (uint)Math.Pow(2, i * CollisionConfig.LAYERS_PER_FLOOR + CollisionConfig.ENVIRONMENT_LAYER - 1) | (uint)Math.Pow(2, CollisionConfig.WORLD_BOUNDS_LAYER - 1);
            navPolygon.AddOutline(boundingOutline);

            navRegion.NavigationPolygon = navPolygon;

            Rid map = NavigationServer2D.MapCreate();
            NavigationServer2D.MapSetCellSize(map, 1);
            NavigationServer2D.MapSetActive(map, true);
            NavMaps.Add(map);

            navRegion.SetNavigationMap(map);
            NavRegions.Add(navRegion);
            AddChild(navRegion);
        }

        RebakeNavMesh();
    }

    private void SetupWorldBarriers() {
        CreateWorldBoundary(-RegionBounds.Y / 2, new Vector2(0, 1));
        CreateWorldBoundary(-RegionBounds.Y / 2, new Vector2(0, -1));
        CreateWorldBoundary(-RegionBounds.X / 2, new Vector2(1, 0));
        CreateWorldBoundary(-RegionBounds.X / 2, new Vector2(-1, 0));
    }

    private void CreateWorldBoundary(float distance, Vector2 normal) {
        var boundShape = new WorldBoundaryShape2D();
        boundShape.Distance = distance;
        boundShape.Normal = normal;

        var bound = new StaticBody2D();
        bound.CollisionLayer = (uint) Mathf.Pow(2, CollisionConfig.WORLD_BOUNDS_LAYER - 1);
        bound.CollisionMask = 0b1; //Only collide with player/npc. Let anything else through like projectiles
        var collisionShape = new CollisionShape2D();
        collisionShape.Shape = boundShape;
        bound.AddChild(collisionShape);
        AddChild(bound);
    }

    private int GetMaxFloorsInScene() {
        //TODO: Implement. Increment below number in the meantime if you want to test > 3 floors
        return 3;
    }

    private void UpdateGlobalUniforms()
    {
        RenderingServer.GlobalShaderParameterSet("world_rect", new Rect2(GlobalPosition - _regionBounds/2f, _regionBounds));
    }
}
