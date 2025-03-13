using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A building has at least one interior region.
public partial class Building : Node2D
{
    private HashSet<Node2D> entitiesInside = new HashSet<Node2D>();

    private Godot.Collections.Array<BuildingRegion> _allRegions = null;

    public List<Door> Exits;
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

            Exits.AddRange(region.GetExits());
        }
    }

    private void OnBodyEnteredRegion(Node2D body, BuildingRegion region)
    {
        Moveable m = body as Moveable;
        if (m == null) {
            return;
        }

        // We only want to look at events for the body's current elevation
        if (m.CurrentElevationLevel != region.ElevationLevel) {
            return;
        }

        entitiesInside.Add(body);
        body.ZIndex = 2;
        if (body is Player player)
        {
            player.CurrentRegion = region;
            // Set all regions on this elevation to be visible, and all above this elevation to be invisible.
            foreach (var other in AllRegions)
            {
                other.Visible = other.ElevationLevel == region.ElevationLevel;
                if (other.Visible) {
                    //Render above weather layer so we dont show clouds and stuff inside
                    other.ZIndex = 2;
                }
            }

            UpdateAllNonPlayerBodies();
        }
        else
        {
            m.CurrentRegion = region;
            UpdateNonPlayerBody(m);
        }
    }

    private void OnBodyExitedRegion(Node2D body, BuildingRegion region)
    {
        Moveable m = body as Moveable;
        if (m == null) {
            return;
        }

        // We only want to look at events for the body's current elevation
        if (m.CurrentElevationLevel != region.ElevationLevel) {
            return;
        }

        entitiesInside.Remove(body);
        body.ZIndex = 0;
        if (!region.OverlapsBody(m)) {
            m.CurrentRegion = null;
            m.ChangeFloor(0);
        }

        if (body is Player player)
        {
            // If there's time left on the timer then we already (recently) scheduled an occupancy check to reset visibility.
            if (visibilityResetTimer.TimeLeft <= 0)
            {
                visibilityResetTimer.Start(0.25f);
            }
            UpdateAllNonPlayerBodies();
        } else {
            UpdateNonPlayerBody(m);
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
            region.ZIndex = 0;
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
