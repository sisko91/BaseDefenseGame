using Godot;
using System;

public partial class Bullet : Projectile
{
    protected override void OnCollide(KinematicCollision2D collision)
    {
        // Make bullets bounce off walls (really anything that isn't a player, NPC, etc.)
        bool bMightBeAWall = !(collision.GetCollider() is Character or Projectile);
        if (bMightBeAWall)
        {
            Velocity = Velocity.Bounce(collision.GetNormal());
        }
        else
        {
            // Will handle applying damage to players/npcs, as well as deleting the projectile.
            base.OnCollide(collision);
        }
    }
}
