using Godot;
using System;

public partial class NonPlayerCharacter : CharacterBody2D
{
    public const float MoveSpeed = 300.0f;

    // Cached reference to the collision shape defined on the .tscn
    public CollisionShape2D CollisionShape { get; private set; }

    public override void _Ready()
    {
        CollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        var collision = MoveAndCollide(Velocity);
        if(collision != null)
        {
            OnCollide(collision);
        }
    }

    public void OnCollide(KinematicCollision2D collision)
    {
        GD.Print($"{Name} walked into {collision.GetCollider()}");
    }

    // This function is called by bullet.cs if found during collisions.
    // TODO: We should probably define a stronger typed interface for processing collisions, damage, etc.
    public void ReceiveHit(Bullet bullet)
    {
        GD.Print("Ouch!");
        this.QueueFree();
    }
}
