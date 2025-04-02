using Godot;
using System;

// The barber fires barbs at targets. The barbs do minor damage on impact but stick into the target that they hit. After a short delay stuck barbs will detonate.
public partial class Barber : Weapon
{
    [Export]
    public PackedScene BarbTemplate;

    [Export]
    public float MaxRoundSpreadDegrees = 5.0f;

    public Barber() {
        // Default barb to fire.
        BarbTemplate = GD.Load<PackedScene>("res://Weapons/Barber/barb_proj.tscn");
    }

    public override void Fire() {
        base.Fire();

        var barb = BarbTemplate.Instantiate<Barb>();
        var rot = GlobalRotation;
        var offsetDeg = new Random().NextDouble() * MaxRoundSpreadDegrees * 2.0f - MaxRoundSpreadDegrees;
        rot += (float) (offsetDeg * Math.PI / 180.0f);
        barb.Start(GlobalPosition, rot, Instigator);
    }
}
