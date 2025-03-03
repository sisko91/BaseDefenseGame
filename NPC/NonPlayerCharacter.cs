using ExtensionMethods;
using Godot;

public partial class NonPlayerCharacter : CharacterBody2D
{
    [Export]
    public float MoveSpeed = 100.0f;

    [Export]
    public float MaxHealth = 100.0f;

    // Which logical grouping of characters in the scene this character is part of.
    // TODO: Should probably be an enum? Suggested values: [Hostile, Friendly, Player, Neutral].
    [Export]
    public string Group = "Hostile";

    public float CurrentHealth { get; private set; }

    public NavigationAgent2D NavAgent { get; private set; } = null;

    public Vector2 MovementTarget
    {
        get { return NavAgent.TargetPosition; }
        set { NavAgent.TargetPosition = value; }
    }

    private Node2D enemyTarget = null;

    // Cached reference to the collision shape defined on the .tscn
    public CollisionShape2D CollisionShape { get; private set; }

    public override void _Ready()
    {
        AddToGroup(Group, true);
        CollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        CurrentHealth = MaxHealth;

        // SetupNavAgent awaits() a signal so we want to make sure we don't call it from _Ready().
        Callable.From(SetupNavAgent).CallDeferred();
    }

    private async void SetupNavAgent()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        NavAgent = new NavigationAgent2D();
        NavAgent.AvoidanceEnabled = true;
        //NavAgent.DebugEnabled = true;
        NavAgent.PathDesiredDistance = 5.0f;
        NavAgent.TargetDesiredDistance = 5.0f;
        AddChild(NavAgent);
    }

    public override void _Process(double delta)
    {
        if(enemyTarget == null)
        {
            enemyTarget = FindTarget();
        }

        if(NavAgent != null)
        {
            UpdateNavigation();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (enemyTarget != null)
        {
            Vector2 nextPathPosition = NavAgent.GetNextPathPosition();
            var direction = (nextPathPosition - GlobalPosition).Normalized();
            Velocity = direction * MoveSpeed;
        }
        else
        {
            // No targets, stop moving.
            Velocity = Vector2.Zero;
        }

        // Orient to face the direction the NPC is moving.
        GlobalRotation = Velocity.Angle();

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

    // Process an incoming impact from the sourceNode. The impact is calculated by the other collider, i.e. impact.Collider == this.
    public void ReceiveHit(KinematicCollision2D impact, Node2D sourceNode, float damage)
    {
        // TODO: Probably should have this on the bullet.
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);

        if(CurrentHealth > 0)
        {
            // knockback - neither of the computations of kbDirection below actually give us what we want so we just calculate
            // the incident angle from positions.
            //var kbDirection = bullet.Velocity.Normalized();
            //var kbDirection = impact.GetAngle() - Mathf.Pi;
            var kbDirection = (GlobalPosition - sourceNode.GlobalPosition).Normalized();
            // Just use the damage as the momentum transferred, essentially.
            var kbVelocity = kbDirection * damage;

            // Render the impact angle if debugging is enabled.
            //this.DrawDebugLine(GlobalPosition, GlobalPosition + kbVelocity, new Color(1, 0, 0), 2.0f);
            //this.DrawDebugLine(GlobalPosition - bullet.Velocity, GlobalPosition, new Color(0, 1, 0), 2.0f);

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

    private void UpdateNavigation()
    {
        if(enemyTarget == null && NavAgent.IsNavigationFinished())
        {
            GD.Print("reached");
            return;
        }

        if (enemyTarget != null)
        {
            MovementTarget = enemyTarget.GlobalPosition;
        }
    }
}
