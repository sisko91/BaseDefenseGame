using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A building has at least one interior region.
public partial class Building : Node2D
{
    private HashSet<Node2D> entitiesInside = new HashSet<Node2D>();

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
        }
    }

    private void OnBodyEnteredRegion(Node2D body, InteriorRegion region)
    {
        // Regions may overlap each other at different elevations and we only want to look at events for the
        // body's current elevation.
        if (GetBodyElevationLevel(body) != region.ElevationLevel) {
            return;
        }

        entitiesInside.Add(body);

        body.ZIndex = 2;
        //GD.Print($"{body.Name} entered {region.Name} (Elevation {region.ElevationLevel})");
        if (body is Player player)
        {
            // Set all regions on this elevation to be visible, and all above this elevation to be invisible.
            foreach (var other in AllRegions)
            {
                other.Visible = other.ElevationLevel == region.ElevationLevel;
                if (other.Visible) {
                    //Render above weather layer so we dont show clouds and stuff inside
                    other.ZIndex = 2;
                }
            }

            UpdateAllNonPlayerBodies(true);
        } else {
            UpdateNonPlayerBody(body, region, true);
        }
    }

    private void OnBodyExitedRegion(Node2D body, InteriorRegion region)
    {
        if (GetBodyElevationLevel(body) != region.ElevationLevel) {
            return;
        }

        entitiesInside.Remove(body);

        //GD.Print($"{body.Name} exited {region.Name} (Elevation {region.ElevationLevel})");
        body.ZIndex = 0;
        if (body is Player player)
        {
            // If there's time left on the timer then we already (recently) scheduled an occupancy check to reset visibility.
            if(visibilityResetTimer.TimeLeft <= 0)
            {
                visibilityResetTimer.Start(0.25f);
            }
            UpdateAllNonPlayerBodies(false);
        } else {
            UpdateNonPlayerBody(body, region, false);
        }
    }

    private void UpdateNonPlayerBody(Node2D body, InteriorRegion region, bool entering) {
        //Show/hide projectiles and enemies depending if the player is in the same floor or not
        if (region.ZIndex == 2 && entering || region.ZIndex == 0 && !entering) {
            body.Show();
        } else {
            body.Hide();
        }
    }

    private void UpdateAllNonPlayerBodies(bool playerEntering) {
        foreach (NonPlayerCharacter c in GetTree().GetNodesInGroup("Hostile")) {
            if (IsInBuilding(c) && playerEntering || !IsInBuilding(c) && !playerEntering) {
                c.Show();
            } else {
                c.Hide();
            }
        }
    }

    private bool IsPlayerInBuilding() {
        foreach (Character c in entitiesInside) {
            if (c is Player) {
                return true;
            }
        }

        return false;
    }
    private bool IsInBuilding(Character c)
    {
        return entitiesInside.Contains(c);
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

    private int GetBodyElevationLevel(Node2D body) {
        int currentElevationLevel = 0;
        if (body is Character character) {
            currentElevationLevel = character.CurrentElevationLevel;
        } else if (body is Projectile projectile) {
            currentElevationLevel = projectile.CurrentElevationLevel;
        }

        return currentElevationLevel;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
