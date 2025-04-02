using ExtensionMethods;
using Godot;
using System;

public partial class Scattergun : Weapon
{
    [Export]
    public PackedScene RoundTemplate = null;

    [Export]
    public int RoundsPerFire = 4;

    // Maximum angle - in degrees - that any round within a fired payload may deviate from straight ahead (in either direction).
    [Export]
    public float MaxRoundSpreadDegrees = 20.0f;

    public override void Fire()
    {
        base.Fire();

        var roundSpreadRads = Mathf.DegToRad(Mathf.Abs(MaxRoundSpreadDegrees));
        for (int i = 0; i < RoundsPerFire; i++)
        {
            var round = RoundTemplate.Instantiate<Projectile>();
            var randNegativeOneToOne = (GD.Randf() - 0.5f) * 2;
            // Less randomness for the first rounds in the payload. This just makes the cone tighter toward the center with falloff.
            float randInfluence = (float)(RoundsPerFire - i) / (float)RoundsPerFire;
            float rotationOffset = roundSpreadRads * randNegativeOneToOne * randInfluence;
            round.Start(GlobalPosition, GlobalRotation + rotationOffset, Instigator);
            // Allow some rounds to fire up to 5% faster.
            round.Velocity *= 1.0f + GD.Randf() * 0.05f;
        }
    }
}
