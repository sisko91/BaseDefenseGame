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
    private static int WEATHER_Z_LAYER = 3;
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
        Moveable m = body as Moveable;
        if (m == null || m.CollisionLayer == 0 || m.IsQueuedForDeletion()) {
            return;
        }

        // We only want to look at events for the body's current elevation, and only bodies that have not yet been reparented to this region
        if (m.CurrentElevationLevel != region.ElevationLevel || m.GetParent() == region)
        {
            return;
        }

        //Reparent has to be deferred since this is called from a physics process
        //Wrapping the rest of the area change logic in this block to avoid reparent issues (e.g. reparent causes an additional enter/exit event on
        //regions and stairs)
        Callable.From(() => {
            m.Reparent(region);
            m.ZIndex += 1; //Provide a "background" layer hide things behind the player/enemies

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

                foreach (Area2D area in region.GetOverlappingAreas())
                {
                    if (area is Explosion)
                    {
                        area.Show();
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
        if (m == null || m.CollisionLayer == 0 || m.IsQueuedForDeletion()) {
            return;
        }

        // We only want to look at events for the body's current elevation, and only bodies that have been reparented to this region
        if (m.CurrentElevationLevel != region.ElevationLevel || m.GetParent() != region) {
            return;
        }

        Callable.From(() => {
            if (m is Player)
            {
                m.Reparent(this.GetGameWorld().PlayerContainerNode);
            }
            else
            {
                m.Reparent(this.GetGameWorld());
            }
            m.ZIndex -= 1;

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

                foreach (Area2D area in region.GetOverlappingAreas())
                {
                    if (area is Explosion)
                    {
                        area.Hide();
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

    private void OnAreaEnteredRegion(Area2D area, BuildingRegion region)
    {
        bool playerInsideRegion = this.GetGameWorld().Players[0].CurrentRegion == region;
        if (area is Explosion)
        {
            //Kind of hacky, but an easy way to detect if a point is in the building
            var regionGround = region.GetNode<Sprite2D>("Ground").GetRect();
            var regionBoundary = new Rect2(ToGlobal(regionGround.Position), regionGround.Size);
            bool explosionInsideRegion = regionBoundary.HasPoint(area.GlobalPosition);

            area.Visible = explosionInsideRegion == playerInsideRegion ;
        }
    }

    private void UpdateNonPlayerBody(Moveable body) {
        var playerNodes = GetTree().GetNodesInGroup("Player").Cast<Player>().ToArray();
        if (playerNodes.Length < 1) {
            return;
        }

        Player player = playerNodes[0];
        bool playerOutside = player.CurrentRegion == null || !player.CurrentRegion.InteriorRegion;
        bool bodyOutside = body.CurrentRegion == null || !body.CurrentRegion.InteriorRegion;

        if (body.CurrentRegion == player.CurrentRegion || playerOutside && bodyOutside) {
            body.Show();
        } else {
            body.Hide();
        }
    }

    private void UpdateAllNonPlayerBodies() {
        var nodes = GetTree().GetNodesInGroup("Projectiles");
        nodes.AddRange(GetTree().GetNodesInGroup("Hostile"));

        foreach (Node2D node in nodes) {
            UpdateNonPlayerBody(node as Moveable);
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
