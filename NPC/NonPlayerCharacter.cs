using ExtensionMethods;
using Godot;

public partial class NonPlayerCharacter : CharacterBody2D
{
    [Export]
    public float MoveSpeed = 100.0f;

    [Export]
    public float MaxHealth = 100.0f;

    public float CurrentHealth { get; private set; }

    // Which logical grouping of characters in the scene this character is part of.
    // TODO: Should probably be an enum? Suggested values: [Hostile, Friendly, Player, Neutral].
    [Export]
    public string Group = "Hostile";

    private Node2D enemyTarget = null;

    // Cached reference to the collision shape defined on the .tscn
    public CollisionShape2D CollisionShape { get; private set; }

    public override void _Ready()
    {
        AddToGroup(Group, true);
        CollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        CurrentHealth = MaxHealth;
    }

    public override void _Process(double delta)
    {
        if(enemyTarget == null)
        {
            enemyTarget = FindTarget();
        }

        if(enemyTarget != null)
        {
            var direction = (enemyTarget.GlobalPosition - GlobalPosition).Normalized();
            Velocity = direction * MoveSpeed;
        }
        else
        {
            // No targets, stop moving.
            Velocity = Vector2.Zero;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var collision = MoveAndCollide(Velocity * (float)delta);
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
    public void ReceiveHit(Bullet bullet, KinematicCollision2D impact)
    {
        // TODO: Probably should have this on the bullet.
        const float damage = 30.0f;
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);

        if(CurrentHealth > 0)
        {
            // knockback
            //var kbDirection = bullet.Velocity.Normalized();
            var kbDirection = impact.GetAngle() - Mathf.Pi;
            // Just use the damage as the momentum transferred, essentially.
            var kbVelocity = Vector2.FromAngle(kbDirection) * damage*3;

            // Render the impact angle if debugging is enabled.
            this.DrawDebugLine(GlobalPosition, GlobalPosition + kbVelocity, new Color(1, 0, 0), 2.0f);

            var collision = MoveAndCollide(kbVelocity);
            if(collision != null)
            {
                OnCollide(collision);
            }
        }
        else
        {
            //die
            QueueFree();
        }
    }

    private Node2D FindTarget()
    {
        foreach(var player in this.GetGameWorld().Players)
        {
            if(player != null)
            {
                // TODO: Only select players that are within an aggro radius.
                return (Node2D)player;
            }
        }

        // TODO: Target the crystal(s) if no player is within aggro radius.
        return null;
    }
}
