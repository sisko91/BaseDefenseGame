using Godot;
using System;
using System.Linq;

// A building has at least one interior region.
public partial class Building : Node2D
{
    private Godot.Collections.Array<InteriorRegion> _allRegions = null;
    public Godot.Collections.Array<InteriorRegion> AllRegions 
    { 
        get
        {
            if(_allRegions == null)
            {
                _allRegions = new Godot.Collections.Array<InteriorRegion>();
                foreach(var child in GetChildren())
                {
                    if(child is InteriorRegion region)
                    {
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
        // Configure our visibility reset timer, this is not started by default but gets (re)scheduled when players leave the building's
        // interior regions.
        visibilityResetTimer = new Timer();
        visibilityResetTimer.OneShot = true;
        AddChild(visibilityResetTimer);

        // Subscribe to events whenever a player enters a floor of this building.
        foreach (var region in AllRegions)
        {
            // Subscribe to each region's BodyEntered event, but also supply the region as well since the callback drops it.
            region.BodyEntered += (Node2D body) => OnBodyEnteredRegion(body, region);
            region.BodyExited += (Node2D body) => OnBodyExitedRegion(body, region);
        }
    }

    private void OnBodyEnteredRegion(Node2D body, InteriorRegion region)
    {
        body.ZIndex = 2;
        if (body is Player player)
        {
            // Regions may overlap each other at different elevations and we only want to look at events for the
            // player's current elevation.
            if(player.CurrentElevationLevel == region.ElevationLevel)
            {
                GD.Print($"{body.Name} entered {region.Name} (Elevation {region.ElevationLevel})");
                // Set all regions on this elevation to be visible, and all above this elevation to be invisible.
                foreach (var other in AllRegions)
                {
                    other.Visible = other.ElevationLevel == region.ElevationLevel;
                    if (other.Visible) {
                        //Render above weather layer so we dont show clouds and stuff inside
                        other.ZIndex = 2;
                    }
                }
            }
        }
    }

    private void OnBodyExitedRegion(Node2D body, InteriorRegion region)
    {
        body.ZIndex = 0;
        if (body is Player player)
        {
            if(player.CurrentElevationLevel == region.ElevationLevel)
            {
                GD.Print($"{body.Name} exited {region.Name} (Elevation {region.ElevationLevel})");

                // If there's time left on the timer then we already (recently) scheduled an occupancy check to reset visibility.
                if(visibilityResetTimer.TimeLeft <= 0)
                {
                    visibilityResetTimer.Timeout += () =>
                    {
                        if (!PlayerIsInBuilding(player))
                        {
                            ResetVisibility();
                        }
                    };
                    visibilityResetTimer.Start(0.25f);
                }
            }
        }
    }

    private bool PlayerIsInBuilding(Player player)
    {
        bool bPlayerStillInBuilding = false;
        foreach (var region in AllRegions)
        {
            bPlayerStillInBuilding |= region.OverlapsBody(player);
        }
        return bPlayerStillInBuilding;
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
