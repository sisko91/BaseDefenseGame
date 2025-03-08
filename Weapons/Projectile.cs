using ExtensionMethods;
using Godot;
using System;

public partial class Projectile : CharacterBody2D
{
    [Export]
    public float InitialSpeed = 750.0f;

    [Export]
    public float LifetimeSeconds = 3.0f;

    [Export]
    public float Damage = 20.0f;

    // The instigator is used for attribution. If a weapon fires a projectile, the Player holding that weapon would be the logical instigator.
    // (This is an old concept stolen from Unreal Engine)
    public Node Instigator = null;

    // Adds the projectile to the gameworld and initializes its position, velocity, etc.
    public void Start(Vector2 worldPosition, float worldDirection, Node instigator)
    {
        GlobalRotation = worldDirection;
        GlobalPosition = worldPosition;
        Velocity = new Vector2(InitialSpeed, 0).Rotated(GlobalRotation);
        Instigator = instigator;

        var timer = this.GetGameWorld().GetTree().CreateTimer(LifetimeSeconds);
        timer.Timeout += QueueFree;

        this.GetGameWorld().AddChild(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        var collision = MoveAndCollide(Velocity * (float)delta);
        if (collision != null)
        {
            if (collision.GetCollider() is Character character)
            {
                character.ReceiveHit(collision, Damage, this);
            }
            OnCollide(collision);
        }
    }

    // TODO: Maybe we should just have this base type detect certain things, and have explicit OnCollideNPC(), OnCollidePlayer(), etc.?
    protected virtual void OnCollide(KinematicCollision2D collision)
    {
        QueueFree();
    }
}
