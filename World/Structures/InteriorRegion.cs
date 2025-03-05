using Godot;
using System;

// Interior Regions define areas "within" a structure. They detect characters and players entering / leaving and provide other data and callbacks related to
// defining inner zones.
public partial class InteriorRegion : Area2D
{
    // What "floor" of elevation this region is located on. 0 is the ground floor.
    [Export]
    public int ElevationLevel = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
