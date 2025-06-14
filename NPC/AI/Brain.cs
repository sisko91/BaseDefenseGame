using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

// The brain belongs to an NPC and is responsible for deciding what the NPC does on both _Process() and _PhysicsProcess().
[GlobalClass]
public partial class Brain : Node2D
{
    [Export]
    public bool CanOpenDoors = false;

    public NonPlayerCharacter OwnerNpc => GetParent() as NonPlayerCharacter;

    //Context-based steering
    private int Directions = 16;
    private List<float> Interest;
    private List<float> Danger;

    private Vector2 lastNavPathDirection = Vector2.Zero;

    private bool justStunned = false;

    public override void _Ready()
    {
        Interest = new List<float>();
        Danger = new List<float>();

        for (int i = 0; i < Directions; i++)
        {
            Interest.Add(0);
            Danger.Add(0);
        }
    }

    // This is sealed so that children cannot override it and must override Think() instead.
    // _Process() is called by children AFTER their parent. That means the NonPlayerCharacter who owns this brain moves
    // before the brain has a chance to think about what it should do. Instead the NonPlayerCharacter calls Think()
    // before it moves.
    public sealed override void _Process(double delta)
    {
        base._Process(delta);
    }

    // Think() mirrors the intent of _Process() for Godot nodes. NPCs will delegate much of their processing to this function.
    public virtual void Think(double deltaTime)
    {
        // nothing in base.
    }
    
    // This is sealed so that children cannot override it and must override ThinkPhysics() instead.
    // _PhysicsProcess() is called by children AFTER their parent. That means the NonPlayerCharacter who owns this brain 
    // moves before the brain has a chance to think about what it should do. Instead, the NonPlayerCharacter calls 
    // Think() before it moves.
    public sealed override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    // ThinkPhysics() mirrors the intent of _PhysicsProcess() for Godot nodes. NPCs will delegate some of their physics-related processing to this function.
    public virtual void ThinkPhysics(double deltaTime)
    {
        if(OwnerNpc == null || OwnerNpc.NavAgent == null)
        {
            return;
        }

        OwnerNpc.ClearDebugDrawCallGroup(OwnerNpc.GetPath());

        // Always update interests and dangers.
        if (!OwnerNpc.NavAgent.IsNavigationFinished())
        {
            lastNavPathDirection = OwnerNpc.GlobalPosition.DirectionTo(OwnerNpc.NavAgent.GetNextPathPosition());
            SetInterest(lastNavPathDirection);
        }
        
        SetDanger();

        if (!OwnerNpc.Stunned) {
            justStunned = false;
        }
        if (!justStunned && OwnerNpc.Stunned) {
            justStunned = true;
            OwnerNpc.Velocity = new Vector2(0f, 0f);
            OwnerNpc.Velocity += OwnerNpc.Knockback;
        }
        else if (OwnerNpc.Velocity.Length() > OwnerNpc.MovementSpeed + 0.1 || OwnerNpc.Stunned)
        {
            OwnerNpc.Velocity += OwnerNpc.Knockback;
            OwnerNpc.Velocity = (OwnerNpc.Velocity + -OwnerNpc.Velocity.Normalized() * 5f);
        }
        else
        {
            // Otherwise continue navigating 
            var direction = ChooseDirection();
            OwnerNpc.Velocity = (OwnerNpc.Velocity + direction * OwnerNpc.MoveAccel).LimitLength(OwnerNpc.MovementSpeed);
        }

        OwnerNpc.Knockback = OwnerNpc.Knockback.Lerp(Vector2.Zero, 0.4f);
        OwnerNpc.LookAtAngle = GetLookAtAngle();
    }

    protected virtual float GetLookAtAngle() {
        // look along the nav path if stuck
        //TODO: Fix this, just need to implement some version of GetRealVelocity since it only works with
        //MoveAndSlide
        /*
        if (Owner.GetRealVelocity().Length() < 0.1f * Owner.MovementSpeed) {
            return lastNavPathDirection.Angle();
        }
        */

        // Look in the direction the NPC is moving by default.
        return OwnerNpc.Velocity.Angle();
    }

    public void ClearNavigationTarget()
    {
        OwnerNpc.NavAgent.TargetPosition = OwnerNpc.GlobalPosition;
        OwnerNpc.NavAgent.TargetDesiredDistance = NavigationConfig.DEFAULT_TARGET_DESIRED_DISTANCE;
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
            Vector2 interestDirection = Vector2.Right.Rotated(angle).Rotated(OwnerNpc.Rotation);
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
                OwnerNpc.DrawDebugLine(OwnerNpc.GlobalPosition, OwnerNpc.GlobalPosition + line * 100, color, 0.1, OwnerNpc.GetPath());
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
            OwnerNpc.DrawDebugLine(OwnerNpc.GlobalPosition, OwnerNpc.GlobalPosition + direction.Normalized() * 150, new Color(1, 1, 0), 0.1, OwnerNpc.GetPath());
        }

        return direction.IsZeroApprox() ? Vector2.Zero : direction.Normalized();
    }

    private void SetInterest(Vector2 pathDirection)
    {
        for (int i = 0; i < Directions; i++)
        {
            var angle = i * 2 * Math.PI / Directions;
            Vector2 interestDirection = Vector2.Right.Rotated((float)angle).Rotated(OwnerNpc.Rotation);
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
        potentialDangers.AddRange(OwnerNpc.NearbyBodySensor.NPCs);
        potentialDangers.AddRange(OwnerNpc.NearbyBodySensor.Walls);

        foreach (Node2D potentialDanger in potentialDangers)
        {
            //TODO: Have body sensor exclude npc it's attached to
            if (OwnerNpc == potentialDanger)
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

                            var wallDangerRange = 10;
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
            OwnerNpc.DrawDebugCircle(dangerGlobalPosition, dangerMinRange, new Color(1, 1, 1), false, 1, OwnerNpc.GetPath());
        }

        var myRadius = 25; //TODO: Get programatically or in config
        var distTo = OwnerNpc.GlobalPosition.DistanceTo(dangerGlobalPosition);
        if (distTo > myRadius + dangerMinRange) {
            return;
        }

        var dirTo = OwnerNpc.GlobalPosition.DirectionTo(dangerGlobalPosition);
        var bucketAngle = dirTo.Angle() - OwnerNpc.GlobalRotation;
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
