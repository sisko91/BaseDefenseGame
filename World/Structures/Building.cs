using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Gurdy.ProcGen;

// A building has at least one interior region.
public partial class Building : Placeable
{
    private static int WEATHER_Z_LAYER = 10;

    private HashSet<Node2D> EntitiesInside = new HashSet<Node2D>();

    [Export]
    public int BuildingHeight;
    public List<Door> Exits;

    private Godot.Collections.Array<BuildingRegion> _allRegions = null;
    public Godot.Collections.Array<BuildingRegion> AllRegions 
    { 
        get
        {
            if(_allRegions == null)
            {
                _allRegions = new Godot.Collections.Array<BuildingRegion>();
                foreach(var child in GetChildren())
                {
                    if(child is BuildingRegion region)
                    {
                        region.OwningBuilding = this;
                        region.AddToGroup($"{NavigationConfig.FLOOR_GROUP_PREFIX}{region.ElevationLevel}");
                        _allRegions.Add(region);
                        // Record the highest elevation in the building.
                        if(region.ElevationLevel > HighestElevation)
                        {
                            HighestElevation = region.ElevationLevel;
                        }
                    }
                }
            }
            return _allRegions;
        } 
    }

    // Returns the (calculated) highest elevation of any interior region in this building.
    public int HighestElevation { get; private set; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();
        Exits = new List<Door>();

        // Subscribe to events whenever a player enters a floor of this building.
        foreach (var region in AllRegions)
        {
            // Subscribe to each region's BodyEntered event, but also supply the region as well since the callback drops it.
            region.BodyEntered += (Node2D body) => OnBodyEnteredRegion(body, region);
            region.BodyExited += (Node2D body) => OnBodyExitedRegion(body, region);

            region.AreaEntered += (Area2D area) => OnAreaEnteredRegion(area, region);

            Exits.AddRange(region.GetExits());
        }

        //Light RangeZMin/Max is not relative, so we need to set it to the absolute value our building will be at when the player is inside
        foreach (Node child in this.GetAllChildren()) {
            if (child is Light2D light) {
                light.RangeZMin = WEATHER_Z_LAYER;
                light.RangeZMax = WEATHER_Z_LAYER + 1;
            }
        }
    }

    private void OnBodyEnteredRegion(Node2D body, BuildingRegion region)
    {
        if (region.ShouldIgnoreNode(body)) {
            return;
        }

        //Reparent has to be deferred since this is called from a physics process
        Callable.From(() => {
            Moveable m = body as Moveable;
            if (m == null) {
                return;
            }

            HashSet<uint> ignoredLayers = new HashSet<uint>() { 0, (uint)Math.Pow(2, m.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR + CollisionConfig.INTERACTIONS_LAYER - 1) };
            if (!IsInstanceValid(m) || ignoredLayers.Contains(m.CollisionLayer)) {
                return;
            }

            // We only want to look at events for the body's current elevation, and only bodies that have not yet been reparented to this region
            if (m.CurrentElevationLevel != region.ElevationLevel || m.CurrentRegion == region) {
                return;
            }

            //TODO: Just use YSort node after updating old test building    
            var ySort = region.GetNodeOrNull("YSort");
            region.AddMonitoringException(body);
            m.Reparent(ySort != null ? ySort : region);
            region.RemoveMonitoringException(body);

            EntitiesInside.Add(body);
            
            m.CurrentRegion = region;
            m.SetInside(region != null && region.InteriorRegion);

            if (body is Player player)
            {
                // Set all regions on this elevation to be visible, and all above this elevation to be invisible.
                foreach (var other in AllRegions)
                {
                    other.Visible = other.ElevationLevel == region.ElevationLevel || !region.InteriorRegion;
                    if (other.Visible)
                    {
                        //Render above weather layer so we dont show clouds and stuff inside
                        other.ZIndex = region.InteriorRegion ? WEATHER_Z_LAYER + 1 : 0;

                        //Hide parts of this region that should be hidden when the player is inside
                        var facade = other.GetNodeOrNull<Node2D>("YSort/Facade");
                        if (facade != null) {
                            facade.Modulate = new Color(1, 1, 1, 0.5f);
                        }
                    }
                }

                UpdateAllNonPlayerBodies();
            }
            else
            {
                UpdateNonPlayerBody(m);
            }
        }).CallDeferred();
    }

    private void OnBodyExitedRegion(Node2D body, BuildingRegion region)
    {
        if (region.ShouldIgnoreNode(body)) {
            return;
        }

        Callable.From(() => {
            Moveable m = body as Moveable;
            if (m == null) {
                return;
            }

            //Some projectiles disable collisions as part of their functionality (e.g. grenades, barbs)
            //This triggers an exit event, but we don't want to remove these from the region. Same for expiring projectiles
            HashSet<uint> ignoredLayers = new HashSet<uint>() { 0, (uint)Math.Pow(2, m.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR + CollisionConfig.INTERACTIONS_LAYER - 1) };
            if (!IsInstanceValid(m) || ignoredLayers.Contains(m.CollisionLayer)) {
                return;
            }

            // We only want to look at events for the body's current elevation, and only bodies that have been reparented to this region
            if (m.CurrentElevationLevel != region.ElevationLevel || m.CurrentRegion != region) {
                return;
            }

            region.AddMonitoringException(body);
            m.Reparent(this.GetGameWorld().YSortNode);
            region.RemoveMonitoringException(body);

            EntitiesInside.Remove(body);

            //Fell off the roof
            if (!region.OverlapsBody(m))
            {
                m.CurrentRegion = null;
                m.ChangeFloor(0);
                //TODO: Should model per floor height, not building height
                m.GlobalPosition = m.GlobalPosition + new Vector2(0, BuildingHeight);
            }

            m.SetInside(m.CurrentRegion != null && m.CurrentRegion.InteriorRegion);

            if (body is Player player)
            {
                ResetVisibility();
                UpdateAllNonPlayerBodies();
            }
            else
            {
                UpdateNonPlayerBody(m);
            }
        }).CallDeferred();
    }

    private void OnAreaEnteredRegion(Area2D area, BuildingRegion region)
    {
        if (area is Explosion exp)
        {
            //Kind of hacky, but an easy way to detect if a point is in the building. We want to check if the center of the explosion
            //is in the region, not if it just overlaps
            //TODO: If we use collision polygons instead of collisions shapes for building areas, we can use
            //https://docs.godotengine.org/en/4.2/classes/class_geometry2d.html#class-geometry2d-method-is-point-in-polygon
            var regionGroundSprite = region.GetNode<Sprite2D>("Ground");
            var topLeft = regionGroundSprite.GlobalPosition;
            var rectSize = regionGroundSprite.GetRect().Size * regionGroundSprite.Scale;
            if (regionGroundSprite.Centered)
            {
                topLeft -= rectSize / 2;
            }
            var regionBoundary = new Rect2(topLeft, rectSize);
            bool explosionInsideRegion = regionBoundary.HasPoint(area.GlobalPosition);
            exp.CurrentRegion = explosionInsideRegion ? region : exp.CurrentRegion;

            UpdateNonPlayerBody(exp);
        }
    }

    private void UpdateNonPlayerBody(IEntity body) {
        if (this.GetGameWorld().Players.Count == 0)
        {
            return;
        }
        BuildingRegion playerRegion = this.GetGameWorld().Players[0].CurrentRegion;

        bool playerOutside = playerRegion == null || !playerRegion.InteriorRegion;
        bool bodyOutside = body.CurrentRegion == null || !body.CurrentRegion.InteriorRegion;

        if (body.CurrentRegion == playerRegion || playerOutside && bodyOutside) {
            ((Node2D)body).Show();
        } else {
            ((Node2D)body).Hide();
        }
    }

    private void UpdateAllNonPlayerBodies() {
        var nodes = GetTree().GetNodesInGroup("Projectiles");
        nodes.AddRange(GetTree().GetNodesInGroup("Hostile"));
        nodes.AddRange(GetTree().GetNodesInGroup("NonHostile"));
        nodes.AddRange(GetTree().GetNodesInGroup("Explosions"));

        foreach (Node2D node in nodes) {
            UpdateNonPlayerBody(node as IEntity);
        }
    }

    private void ResetVisibility()
    {
        // Make sure everything is visible again (i.e. roof covers lower levels).
        foreach (var region in AllRegions)
        {
            region.Visible = true;
            region.ZIndex = 0;

            var facade = region.GetNodeOrNull<Node2D>("YSort/Facade");
            if (facade != null)
            {
                facade.Modulate = new Color(1, 1, 1, 1);
            }
        }
    }
}
