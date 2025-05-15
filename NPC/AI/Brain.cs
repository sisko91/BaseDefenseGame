using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

// The brain belongs to an NPC and is responsible for deciding what the NPC does on both _Process() and _PhysicsProcess().
[GlobalClass]
public partial class Brain : Resource
{
    [Export]
    public bool CanOpenDoors = false;

    // Distance after which a perfectly viable enemy target is forgotten (unless there's nothing better to focus on).
    [Export]
    public float AggroResetRange = 500.0f;

    public NonPlayerCharacter Owner { get; private set; }

    // What target - if any - the brain is currently focused on.
    public Character EnemyTarget { get; set; }

    //Context-based steering
    private int Directions = 16;
    private List<float> Interest;
    private List<float> Danger;

    private Vector2 lastNavPathDirection = Vector2.Zero;

    // All actions this Brain has to select from.
    [Export]
    protected Godot.Collections.Array<AI.Action> actions;

    // The currently-selected AI action this brain is executing.
    private AI.Action currentAction;

    private bool justStunned = false;

    public Brain() : base()
    {
        // The Brain should always be local to the scene because otherwise every instance of an NPC will have the same brain (it doesn't work well).
        ResourceLocalToScene = true;
    }

    public void Initialize(NonPlayerCharacter owner)
    {
        Owner = owner;

        Interest = new List<float>();
        Danger = new List<float>();

        for (int i = 0; i < Directions; i++)
        {
            Interest.Add(0);
            Danger.Add(0);
        }

        // Initialize AI Action set.
        if(actions == null) {
            actions = new Godot.Collections.Array<AI.Action>();
        }
        foreach (var action in actions) {
            action.Initialize(this);
        }
    }

    // Think() mirrors the intent of _Process() for Godot nodes. NPCs will delegate much of their processing to this function.
    public virtual void Think(double deltaTime)
    {
        if(Owner == null)
        {
            return;
        }

        EnemyTarget = FindTarget();

        // Pick a new action if necessary
        if(currentAction != null && !currentAction.IsActive)
        {
            currentAction = null;
        }

        var nextAction = currentAction;
        // Find the highest-scoring action.
        float nextActionScore = currentAction == null ? 0 : currentAction.CalculateScore();
        foreach (var candidate in actions) {
            if (currentAction == candidate) {
                //continue; // don't pick the same action twice in a row if possible.
            }

            float candidateScore = candidate.CalculateScore();
            //GD.Print($"\t[{candidateScore}]{candidate.GetType().Name}");
            if (candidateScore > nextActionScore) {
                nextAction = candidate;
                nextActionScore = candidateScore;
            }
        }
        if (nextActionScore == 0) {
            // if none of the actions score anything at all, don't run anything at all.
            nextAction = null;
        }

        // If we're changing actions and there's a current action in progress, we can't swap without interrupting first.
        bool changingActions = nextAction != null && nextAction != currentAction;
        if (changingActions) {
            if (currentAction == null || currentAction.TryInterrupt()) {
                currentAction = nextAction;
            }
        }

        // Either tick the current action or Activate the new one.
        if (currentAction != null)
        {
            if(currentAction.IsActive)
            {
                currentAction.Update(deltaTime);
            }
            else
            {
                currentAction.Activate();
            }
        }
    }

    private Character FindTarget()
    {
        // TODO: Consider adding a "DefaultTarget" that the AI falls back to and setting the crystal there instead of all this other logic.
        // Maintain any existing (non-crystal) target if they are still within aggro distance.
        if(EnemyTarget != null && EnemyTarget is not CrystalTarget && EnemyTarget.CurrentHealth > 0) {
            if(EnemyTarget.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) < AggroResetRange*AggroResetRange) {
                return EnemyTarget;
            }
        }

        // Pick any nearby player to aggro.
        foreach (var player in Owner.NearbyBodySensor.Players) {
            if (player != null) {
                return player;
            }
        }

        // Find the nearest crystal to attack.
        CrystalTarget nearestCrystal = null;
        float nearest = 0;
        foreach(var crystal in Owner.GetGameWorld().Crystals)
        {
            var candidate = (CrystalTarget)crystal;
            if(candidate.CurrentHealth <= 0)
            {
                // Skip dead crystals
                continue;
            }

            if (nearestCrystal == null)
            {
                nearestCrystal = candidate;
                nearest = candidate.GlobalPosition.DistanceTo(Owner.GlobalPosition);
            }
            else
            {
                var distance = candidate.GlobalPosition.DistanceTo(Owner.GlobalPosition);
                if (distance < nearest)
                {
                    nearestCrystal = candidate;
                    nearest = distance;
                }
            }
        }

        // Pick any player at all when there's no living crystal (though this is probably game over).
        if(nearestCrystal == null || nearestCrystal.CurrentHealth <= 0)
        {
            foreach (var p in Owner.GetGameWorld().Players)
            {
                var player = p as Player;
                if (player != null)
                {
                    return player;
                }
            }
        }
        
        return nearestCrystal;
    }

    // ThinkPhysics() mirrors the intent of _PhysicsProcess() for Godot nodes. NPCs will delegate some of their physics-related processing to this function.
    public virtual void ThinkPhysics(double deltaTime)
    {
        if(Owner == null || Owner.NavAgent == null)
        {
            return;
        }

        Owner.ClearDebugDrawCallGroup(Owner.GetPath());
        lastNavPathDirection = Owner.GlobalPosition.DirectionTo(Owner.NavAgent.GetNextPathPosition());

        // Always update interests and dangers.
        SetInterest(lastNavPathDirection);
        SetDanger();

        if (!Owner.Stunned) {
            justStunned = false;
        }
        if (!justStunned && Owner.Stunned) {
            justStunned = true;
            Owner.Velocity = new Vector2(0f, 0f);
            Owner.Velocity += Owner.Knockback;
        }
        else if (Owner.Velocity.Length() > Owner.MovementSpeed + 0.1 || Owner.Stunned)
        {
            Owner.Velocity += Owner.Knockback;
            Owner.Velocity = (Owner.Velocity + -Owner.Velocity.Normalized() * 5f);
        }
        else if(currentAction != null && currentAction.IsActive && currentAction.PausesMotionWhileActive)
        {
            Owner.Velocity = Vector2.Zero;
        }
        else
        {
            // Otherwise continue navigating 
            var direction = ChooseDirection();
            Owner.Velocity = (Owner.Velocity + direction * Owner.MoveAccel).LimitLength(Owner.MovementSpeed);
        }

        Owner.Knockback = Owner.Knockback.Lerp(Vector2.Zero, 0.4f);
        Owner.RotationGoal = GetRotationGoal();
    }

    private float GetRotationGoal() {
        var current = Owner.RotationGoal;

        if(currentAction != null && currentAction.IsActive && currentAction.PausesMotionWhileActive) {
            // Maintain current angle while the action is active.
            // TODO: Separate the notions here. There are skills which lock the current rotation and skills which lock the current
            //       *target* of the rotation (but want to keep facing that target while they are active).
            return current;
        }

        // Look at the enemy if they exist and are nearby.
        if (EnemyTarget != null && EnemyTarget is Player player && Owner.NearbyBodySensor.Players.Contains(player)) {
            return Owner.GlobalPosition.DirectionTo(player.GlobalPosition).Angle();
        }

        // look along the nav path if stuck
        if (Owner.GetRealVelocity().Length() < 0.1f * Owner.MovementSpeed) {
            return lastNavPathDirection.Angle();
        }

        // Orient to face the direction the NPC is moving by default.
        return Owner.Velocity.Angle();
    }

    public void ClearNavigationTarget()
    {
        Owner.NavAgent.TargetPosition = Owner.GlobalPosition;
        Owner.NavAgent.TargetDesiredDistance = NavigationConfig.DEFAULT_TARGET_DESIRED_DISTANCE;
    }

    private Vector2 ChooseDirection()
    {
        Vector2 direction = Vector2.Zero;
        Vector2 highestInterestDirection = Vector2.Zero;
        float maxInterest = 0;

        //Interest[0] = 0;
        for (int i = 0; i < Directions; i++)
        {
            //TODO: Can make this more complex than just canceling out. e.g. enemies further away subtract less interest
            if (Danger[i] > 0)
            {
                Interest[i] = 0;
            }

            float angle = i * 2 * (float)Math.PI / Directions;
            Vector2 interestDirection = Vector2.Right.Rotated(angle).Rotated(Owner.Rotation);
            direction += interestDirection * Interest[i];

            if (DebugConfig.Instance.DRAW_STEERING)
            {
                var color = Interest[i] <= 0 ? new Color(1, 0, 0) : new Color(0, 1, 0);
                var line = (interestDirection * Math.Abs(Interest[i]));
                if (Interest[i] == 0)
                {
                    color = new Color(1, 0, 0);
                    line = interestDirection.Normalized();
                }
                Owner.DrawDebugLine(Owner.GlobalPosition, Owner.GlobalPosition + line * 100, color, 0.1, Owner.GetPath());
            }

            if (Interest[i] > maxInterest)
            {
                maxInterest = Interest[i];
                highestInterestDirection = interestDirection;
            }
        }

        //If the average direction of interest is towards a danger, go in the direction of highest interest instead
        if (IsDangerInDirection(direction))
        {
            direction = highestInterestDirection;
        }

        if (DebugConfig.Instance.DRAW_STEERING)
        {
            Owner.DrawDebugLine(Owner.GlobalPosition, Owner.GlobalPosition + direction.Normalized() * 150, new Color(1, 1, 0), 0.1, Owner.GetPath());
        }

        return direction.Normalized();
    }

    private void SetInterest(Vector2 pathDirection)
    {
        for (int i = 0; i < Directions; i++)
        {
            var angle = i * 2 * Math.PI / Directions;
            Vector2 interestDirection = Vector2.Right.Rotated((float)angle).Rotated(Owner.Rotation);
            Interest[i] = Math.Max(0.1f, interestDirection.Dot(pathDirection));
            //this.DrawDebugLine(GlobalPosition, Position + interestDirection * Interest[i] * 100, new Color(0, 1, 0), 0.1, GetPath());
        }
    }

    private void SetDanger()
    {
        for (int i = 0; i < Directions; i++)
        {
            Danger[i] = 0;
        }

        List<Node2D> potentialDangers = new List<Node2D>();
        potentialDangers.AddRange(Owner.NearbyBodySensor.NPCs);
        potentialDangers.AddRange(Owner.NearbyBodySensor.Walls);

        foreach (Node2D potentialDanger in potentialDangers)
        {
            //TODO: Have body sensor exclude npc it's attached to
            if (Owner == potentialDanger)
            {
                continue;
            }

            //Include wall sides as points to avoid in addition to the center point
            if (potentialDanger is StaticBody2D wallDanger)
            {
                if (!wallDanger.HasNode("CollisionShape2D"))
                {
                    continue;
                }

                //TODO: A raycast solution would be simpler and more effective
                foreach (Node node in wallDanger.GetChildren())
                {
                    if (node is CollisionShape2D collisionShape)
                    {
                        var shape = collisionShape.Shape;

                        if (shape is RectangleShape2D)
                        {
                            var rectShape = shape as RectangleShape2D;

                            var wallDangerRange = 15;
                            //Add the extents
                            CheckAndAddDanger(collisionShape.GlobalPosition + new Vector2(rectShape.Size.X / 2 * wallDanger.Scale.X, 0).Rotated(wallDanger.Rotation), wallDangerRange);
                            CheckAndAddDanger(collisionShape.GlobalPosition + new Vector2(-rectShape.Size.X / 2 * wallDanger.Scale.X, 0).Rotated(wallDanger.Rotation), wallDangerRange);
                            CheckAndAddDanger(collisionShape.GlobalPosition + new Vector2(0, rectShape.Size.Y / 2 * wallDanger.Scale.Y).Rotated(wallDanger.Rotation), wallDangerRange);
                            CheckAndAddDanger(collisionShape.GlobalPosition + new Vector2(0, -rectShape.Size.Y / 2 * wallDanger.Scale.Y).Rotated(wallDanger.Rotation), wallDangerRange);
                        }
                    }
                }
            }
            else
            {
                var enemyDangerRange = 100;
                CheckAndAddDanger(potentialDanger.GlobalPosition, enemyDangerRange);
            }
        }
    }

    private void CheckAndAddDanger(Vector2 dangerGlobalPosition, float dangerMinRange)
    {
        if (DebugConfig.Instance.DRAW_STEERING)
        {
            Owner.DrawDebugLine(dangerGlobalPosition, dangerGlobalPosition, new Color(1, 1, 1), 0.1, Owner.GetPath());
        }

        var myRadius = 25; //TODO: Get programatically or in config
        var distTo = Owner.GlobalPosition.DistanceTo(dangerGlobalPosition);
        if (distTo > myRadius + dangerMinRange) {
            return;
        }

        var dirTo = Owner.GlobalPosition.DirectionTo(dangerGlobalPosition);
        var bucketAngle = dirTo.Angle() - Owner.GlobalRotation;
        //Shift angle up for easier bucketing. For instance, with 8 directions, the first bucket should be everything from -337.5 degress to 22.5 degrees. This would shift those values to 0 - 45 degrees
        bucketAngle += (float)(Math.PI / Directions);
        bucketAngle = GetBoundedAngle(bucketAngle);

        //Put in multiple buckets depending on angle. Raycasting would see a close object at multiple angles, so simulating that
        //50% overlap on grouping min, scale with distance
        var distScale = 1 - distTo / 128; //TODO: This not good math. Figure out a better way to approximate raycast behavior as a scale of distance
        var modifier = 1.5 + 3 * distScale;

        for (int i = 0; i < Directions; i++)
        {
            var angle = i * 2 * Math.PI / Directions;

            if (Math.Abs(bucketAngle - angle) < (modifier * Math.PI / Directions))
            {
                Danger[i] = 1; //TODO: Can make this more complex, e.g. scaling for distance
                Interest[Mod(i + Directions / 2, Directions)] = 0.5f; //Give an interest boost to the opposite direction
            }
        }
    }

    private bool IsDangerInDirection(Vector2 direction)
    {
        return Danger[GetDangerBucket(direction.Angle())] > 0;
    }

    //Modifier increases the search range
    private int GetDangerBucket(float bucketAngle)
    {
        for (int i = 0; i < Directions; i++)
        {
            var angle = i * 2 * Math.PI / Directions;

            if (Math.Abs(bucketAngle - angle) < (Math.PI / Directions))
            {
                return i;
            }
        }

        //Should never return
        return 0;
    }

    private float GetBoundedAngle(float angle)
    {
        while (angle < 0)
        {
            angle += (float)(2 * Math.PI);
        }
        while (angle > 2 * Math.PI)
        {
            angle -= (float)(2 * Math.PI);
        }

        return angle;
    }
    private int Mod(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }
}
