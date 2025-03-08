using Godot;
using System;

public partial class Character : CharacterBody2D
{
    [Export]
    public float MovementSpeed = 400.0f;

    // The character's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // characters enter their InteriorRegions.
    public int CurrentElevationLevel = 0;

    // Cached reference to the NearbyBodySensor defined on the .tscn
    public BodySensor NearbyBodySensor { get; protected set; }


    public override void _Ready()
    {
        NearbyBodySensor = GetNode<BodySensor>("NearbyBodySensor");
    }
}
