using Godot;
using System;

public partial class Character : CharacterBody2D
{
    [Export]
    public float MovementSpeed = 400.0f;

    // The character's current elevation in the world. Defaults to 0 which is ground level. This is updated by Buildings when
    // characters enter their InteriorRegions.
    public int CurrentElevationLevel = 0;
}
