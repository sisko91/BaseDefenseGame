using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;

// Action-oriented Brains evaluate a set of dynamically-configured actions each tick and execute selected actions to
// affect change on the owning NPC or the world.
public partial class ActionOrientedBrain : Brain, IWorldLifecycleListener
{
    // Distance after which a perfectly viable enemy target is forgotten (unless there's nothing better to focus on).
    [Export]
    public float AggroResetRange = 500.0f;

    // The groups of characters that this brain is hostile towards. May be empty.
    [Export] public Godot.Collections.Array<string> HostileGroups = [];
    
    // Design-time action templates. These are duplicated per-brain instance and initialized for each character.
    [Export]
    protected Godot.Collections.Array<AI.Action> Actions;

    // All actions this Brain has to select from at runtime.
    protected List<AI.Action> currentActions = [];

    // The currently-selected AI action this brain is executing.
    private AI.Action currentAction;

    public override void _Ready()
    {
        base._Ready();
        
        // Initialize AI Action set.
        if(Actions == null) {
            Actions = new Godot.Collections.Array<AI.Action>();
        }
        // Since actions are resources they need to be duplicated per-brain. The ones that are set in the editor
        // may not have LocalToScene configured properly (even when they do it doesn't seem to work consistently).
        foreach (var action in Actions)
        {
            currentActions.Add(action.Duplicate(true) as AI.Action);
        }
    }
    
    // Satisfies IWorldLifecycleListener; Handles initializing the actions list once the world is fully created above
    // this node.
    public void PostWorldInit(World world)
    {
        // We initialize actions on the next frame so that any setup to the parent hierarchy here is complete.
        for (int i = 0; i < currentActions.Count; i++)
        {
            currentActions[i].Initialize(this);
        }
    }
    
    public override void Think(double deltaTime)
    {
        base.Think(deltaTime);
        
        EnemyTarget = FindTarget();
        // Pick a new action if necessary
        if(currentAction != null && !currentAction.IsActive)
        {
            currentAction = null;
        }

        var nextAction = currentAction;
        // Find the highest-scoring action.
        float nextActionScore = currentAction == null ? 0 : currentAction.CalculateScore();
        foreach (var candidate in currentActions) {
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
            //GD.Print($"{GetPath()} changing actions {nextAction}");
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
                //GD.Print($"{GetPath()} activating {currentAction}");
                currentAction.Activate();
            }
        }
    }
    private Character FindTarget()
    {
        // TODO: Consider adding a "DefaultTarget" that the AI falls back to and setting the crystal there instead of all this other logic.
        // Maintain any existing target if they are still within aggro distance.
        if(EnemyTarget is { CurrentHealth: > 0 }) {
            if(EnemyTarget.GlobalPosition.DistanceSquaredTo(OwnerNpc.GlobalPosition) < AggroResetRange*AggroResetRange) {
                return EnemyTarget;
            }
        }

        // Pick the closest character in a hostile group to aggro.
        // TODO: Character Factions instead of raw groups.
        //       Ref: https://app.asana.com/1/1209778638119403/project/1209778597616183/task/1210503004879774
        Character closestHostile = null;
        float closestSquared = float.MaxValue;
        foreach (var character in this.GetGameWorld().Characters)
        {
            // Is the character hostile?
            if (character.GetGroups().Any(g => HostileGroups.Contains(g)))
            {
                float distSquared = character.GlobalPosition.DistanceSquaredTo(OwnerNpc.GlobalPosition);
                if (distSquared < closestSquared)
                {
                    closestSquared = distSquared;
                    closestHostile = character;
                }
            }
        }

        return closestHostile;
    }

    public override void ThinkPhysics(double deltaTime)
    {
        // Brain.ThinkPhysics() handles navigation.
        base.ThinkPhysics(deltaTime);
        
        // Halt movement if the current action says to.
        if(currentAction != null && currentAction.IsActive && currentAction.PausesMotionWhileActive)
        {
            OwnerNpc.Velocity = Vector2.Zero;
        }
    }

    protected override float GetLookAtAngle()
    {
        var current = OwnerNpc.LookAtAngle;

        if(currentAction != null && currentAction.IsActive && currentAction.PausesMotionWhileActive) {
            // Maintain current angle while the action is active.
            // TODO: Separate the notions here. There are skills which lock the current rotation and skills which lock the current
            //       *target* of the rotation (but want to keep facing that target while they are active).
            return current;
        }

        return base.GetLookAtAngle();
    }
}
