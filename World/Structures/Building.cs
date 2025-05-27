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

    //It's a lot simpler if all floors/platforms are the same level up within a strictly 2D framework
    //Can explore a more complex solution to support different floor heights for other buildings later
    public static int FloorHeight = 120;

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

            foreach (var window in region.Windows) {
                window.VisibleArea.BodyEntered += (Node2D body) => OnBodyEnteredRegion(body, region);
                window.VisibleArea.BodyExited += (Node2D body) => OnBodyExitedRegion(body, region);
            }

        }
    }

    private void OnBodyEnteredRegion(Node2D body, BuildingRegion region)
    {
        Moveable m = body as Moveable;
        if (m == null) {
            return;
        }
        if (m.Falling && m.FallTime == 0) {
            m.Falling = false;
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
            if (m.CurrentElevationLevel != region.ElevationLevel || m.GetParent().GetParent() == region) {
                return;
            }

            m.CurrentRegion = region;
            var inside = region != null && region.InteriorRegion && region.OverlapsBody(m);
            m.SetInside(inside);

            if (region.OverlapsBody(m)) {
                var layer = m is Projectile ? region.GetNodeOrNull("Foreground") : region.GetNodeOrNull("Middleground");
                region.AddMonitoringException(body);
                m.Reparent(layer != null ? layer : region);
                region.RemoveMonitoringException(body);
            }

            if (body is Player player)
            {
                ResetVisibility();
                // Set all regions on this elevation to be visible, and all above this elevation to be invisible.
                foreach (var other in AllRegions)
                {
                    other.Visible = other.ElevationLevel <= region.ElevationLevel;
                    if (other.Visible && other.ElevationLevel == region.ElevationLevel)
                    {
                        //Render above weather layer so we dont show clouds and stuff inside
                        other.ZIndex = region.ElevationLevel + (inside ? WEATHER_Z_LAYER + 1 : 0);

                        //Hide parts of this region that should be hidden when the player is inside
                        var facade = other.GetNodeOrNull<Node2D>("Foreground/Facade");
                        if (facade != null) {
                            facade.Modulate = new Color(1, 1, 1, 0.5f);
                        }

                        //Light RangeZMin/Max is not relative, so we need to set it to the absolute value our building will be at when the player is inside
                        foreach (Node child in region.GetAllChildren()) {
                            if (child is Light2D light) {
                                light.RangeZMin = other.ZIndex;
                                light.RangeZMax = other.ZIndex;
                            }
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
            m.Reparent(this.GetGameWorld().Middleground);
            region.RemoveMonitoringException(body);

            if (!region.OverlapsBody(m))
            {
                if (m.CurrentElevationLevel > 0) {
                    m.Falling = true;
                }

                m.CurrentRegion = null;

                m.SetInside(m.CurrentRegion != null && m.CurrentRegion.InteriorRegion);

                if (m is Player player) {
                    ResetVisibility();
                    UpdateAllNonPlayerBodies();
                } else {
                    UpdateNonPlayerBody(m);
                }
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
        var player = this.GetGameWorld().Players[0];
        BuildingRegion playerRegion = player.CurrentRegion;

        bool playerOutside = playerRegion == null || !playerRegion.InteriorRegion || !playerRegion.OverlapsBody(player);
        bool bodyOutside = body.CurrentRegion == null || !body.CurrentRegion.InteriorRegion || !body.CurrentRegion.OverlapsBody((Node2D) body);

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
            region.ZIndex = region.ElevationLevel;

            var facade = region.GetNodeOrNull<Node2D>("Foreground/Facade");
            if (facade != null)
            {
                facade.Modulate = new Color(1, 1, 1, 1);
            }
        }
    }
}
