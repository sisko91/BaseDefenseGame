using Godot;
using System;

public partial class Bullet : Projectile
{
    public Bullet() {
        // Bullets bounce so by default they shouldn't be destroyed when they hit something.
        DestroyOnNextCollision = false;
    }

    protected override void OnCollide(KinematicCollision2D collision)
    {
        base.OnCollide(collision);
        // Make bullets bounce off walls (really anything that isn't a player, NPC, etc.)
        bool bounced = !(collision.GetCollider() is Character or Projectile);
        if (bounced)
        {
            Velocity = Velocity.Bounce(collision.GetNormal());
        }
        else
        {
            // Destroy the projectile when it hits something it doesn't bounce into.
            QueueFree();
        }
    }
}
