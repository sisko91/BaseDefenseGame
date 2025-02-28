using Godot;
using System;

public partial class Bullet : CharacterBody2D
{
    public int Speed = 750;
    public float LifetimeSeconds = 3f;

    public void Start(Vector2 position, float direction) {
        Rotation = direction;
        Position = position;
        Velocity = new Vector2(Speed, 0).Rotated(Rotation);
    }

    public override void _PhysicsProcess(double delta) {
        var collision = MoveAndCollide(Velocity * (float)delta);
        if (collision != null) {
            //Make bullets bounce off walls
            Velocity = Velocity.Bounce(collision.GetNormal());

            if (collision.GetCollider() is Player) {
                GD.Print("Hit player");

                //For now, despawn projectiles that hit the player (could implement friendly fire)
                //It would be good to learn how to impart physics from this impact before removing the projectile
                QueueFree();
            } else if (collision.GetCollider() is NonPlayerCharacter npc)
            {
                npc.ReceiveHit(this);
                QueueFree();
            }
        }
    }
}
