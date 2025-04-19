using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A building has at least one interior region.
public partial class Building : Node2D
{
    [Export]
    public int BuildingHeight;

    private HashSet<Node2D> entitiesInside = new HashSet<Node2D>();

    private Godot.Collections.Array<BuildingRegion> _allRegions = null;

    public List<Door> Exits;

    private static int BUILDING_Z_LAYER = 1;
    private static int WEATHER_Z_LAYER = 10;
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

    // Timer maintained by the Building for checking occupancy and resetting visibility for the building regions.
    private Timer visibilityResetTimer = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Exits = new List<Door>();

        // Configure our visibility reset timer, this is not started by default but gets (re)scheduled when players leave the building's
        // interior regions.
        visibilityResetTimer = new Timer();
        visibilityResetTimer.OneShot = true;
        visibilityResetTimer.Timeout += () =>
        {
            if (!IsPlayerInBuilding()) {
                ResetVisibility();
            }
        };
        AddChild(visibilityResetTimer);

        // Subscribe to events whenever a player enters a floor of this building.
        foreach (var region in AllRegions)
        {
            // Subscribe to each region's BodyEntered event, but also supply the region as well since the callback drops it.
            region.BodyEntered += (Node2D body) => OnBodyEnteredRegion(body, region);
            region.BodyExited += (Node2D body) => OnBodyExitedRegion(body, region);

            region.AreaEntered += (Area2D area) => OnAreaEnteredRegion(area, region);

            Exits.AddRange(region.GetExits());
        }
    }

    private void OnBodyEnteredRegion(Node2D body, BuildingRegion region)
    {
        //Reparent has to be deferred since this is called from a physics process
        //Wrapping the rest of the area change logic in this block to avoid reparent issues (e.g. reparent causes an additional enter/exit event on
        //regions and stairs)
        Callable.From(() => {
            Moveable m = body as Moveable;
            HashSet<uint> ignoredLayers = new HashSet<uint>() {0, (uint)Math.Pow(2, m.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR + CollisionConfig.INTERACTIONS_LAYER - 1) };
            if (m == null || ignoredLayers.Contains(m.CollisionLayer) || m.IsQueuedForDeletion()) {
                return;
            }

            // We only want to look at events for the body's current elevation, and only bodies that have not yet been reparented to this region
            if (m.CurrentElevationLevel != region.ElevationLevel || m.GetParent() == region) {
                return;
            }

            if (m.GetParent() is not BuildingRegion) {
                m.ZIndex += 1; //Provide a "background" layer hide things behind the player/enemies
            }
            m.Reparent(region);

            entitiesInside.Add(body);

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
                        other.ZIndex = region.InteriorRegion ? WEATHER_Z_LAYER + 1 : BUILDING_Z_LAYER;
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
        Moveable m = body as Moveable;
        //Some projectiles disable collisions as part of their functionality (e.g. grenades, barbs)
        //This triggers an exit event, but we don't want to remove these from the region. Same for expiring projectiles
        HashSet<uint> ignoredLayers = new HashSet<uint>() { 0, (uint)Math.Pow(2, m.CurrentElevationLevel * CollisionConfig.LAYERS_PER_FLOOR + CollisionConfig.INTERACTIONS_LAYER - 1) };
        if (m == null || ignoredLayers.Contains(m.CollisionLayer) || m.IsQueuedForDeletion()) {
            return;
        }

        // We only want to look at events for the body's current elevation, and only bodies that have been reparented to this region
        if (m.CurrentElevationLevel != region.ElevationLevel || m.GetParent() != region) {
            return;
        }

        Callable.From(() => {
            m.ZIndex -= 1;

            if (m is Player)
            {
                m.Reparent(this.GetGameWorld().PlayerContainerNode);
            }
            else if(m is NonPlayerCharacter) {
                m.Reparent(this.GetGameWorld().NPCContainerNode);
            }
            else {
                m.Reparent(this.GetGameWorld());
            }

            entitiesInside.Remove(body);
            //Fell off the roof
            if (!region.OverlapsBody(m))
            {
                m.CurrentRegion = null;
                m.ChangeFloor(0);
                m.GlobalPosition = m.GlobalPosition + new Vector2(0, BuildingHeight);
            }

            m.SetInside(m.CurrentRegion != null && m.CurrentRegion.InteriorRegion);

            if (body is Player player)
            {
                // If there's time left on the timer then we already (recently) scheduled an occupancy check to reset visibility.
                if (visibilityResetTimer.TimeLeft <= 0)
                {
                    visibilityResetTimer.Start(0.25f);
                }

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
            var regionGroundSprite = region.GetNode<Sprite2D>("Ground");
            var regionBoundary = new Rect2(ToGlobal(regionGroundSprite.Position), regionGroundSprite.GetRect().Size * regionGroundSprite.Scale);
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
        nodes.AddRange(GetTree().GetNodesInGroup("Explosions"));

        foreach (Node2D node in nodes) {
            UpdateNonPlayerBody(node as IEntity);
        }
    }

    private bool IsPlayerInBuilding() {
        foreach (Node2D node in entitiesInside) {
            if (node is Player) {
                return true;
            }
        }

        return false;
    }

    private void ResetVisibility()
    {
        // Make sure everything is visible again (i.e. roof covers lower levels).
        foreach (var region in AllRegions)
        {
            region.Visible = true;
            region.ZIndex = BUILDING_Z_LAYER;
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
