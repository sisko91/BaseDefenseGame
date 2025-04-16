using Godot;
using System;

// AreaEffects are instigated Area2D effects that register overlaps with other bodies in the game. 
public partial class AreaEffect : Area2D, IInstigated, IEntity
{
    // Instigator property satisfies IInstigated interface.
    public Character Instigator { get; set; }

    //Satisfy the IEntity interface
    public BuildingRegion CurrentRegion { get; set; }

    // The outer-most distance that the effect will cover, in world units.
    [Export]
    public float MaximumRadius { get; set; } = 100.0f;

    // The filter to use for determining which nodes are under the influence of this area effect. Null is a valid value and indicates
    // no additional filtering besides the initial collider/overlap check.
    [Export]
    public AreaEffectFilter InfluenceFilter { get; set; } = null;

    // How long in seconds before this effect is removed from the scene and all behavior halted.
    [Export]
    public float CleanupLifetime = 3.0f;

    // All physical bodies detected in the vicinity of the effect. This list is a performance optimization as the AreaEffect will
    // filter out bodies according to InfluenceFilter, and GetOverlappingBodies() would still return them even once removed.
    public Godot.Collections.Array<PhysicsBody2D> NearbyBodies { get; private set; }

    // All character bodies detected in the vicinity of the effect.
    public Godot.Collections.Array<Character> NearbyCharacters { get; private set; }

    // Nodes/bodies that are excluded from influence due to manual request.
    private Godot.Collections.Array<Node2D> InfluenceExceptions;

    public double SpawnEpochSeconds { get; private set; }

    public override void _Ready() {
        base._Ready();

        // Set up to detect bodies in radius.
        NearbyBodies = new Godot.Collections.Array<PhysicsBody2D>();
        NearbyCharacters = new Godot.Collections.Array<Character>();
        InfluenceExceptions = new Godot.Collections.Array<Node2D>();

        // Start expiration timer.
        SpawnEpochSeconds = Time.GetTicksMsec() / 1000.0;
        if (CleanupLifetime > 0) {
            var timer = new Timer();
            timer.WaitTime = CleanupLifetime;
            timer.OneShot = true;
            timer.Timeout += QueueFree;
            AddChild(timer);
            timer.Start();
        }
    }

    // Override to handle what happens when bodies are detected and pass the influence filter. Physics bodies are already recorded
    // to the NearbyBodies list automatically, and Characters are similarly added to NearbyCharacters.
    protected virtual void OnBodyEntered(PhysicsBody2D body) {
    }

    // Override to handle what happens when bodies are no longer influenced by this area effect. Physics bodies are already removed
    // from the NearbyBodies list automatically, and characters are similarly removed from NearbyCharacters.
    protected virtual void OnBodyExited(PhysicsBody2D body) {
    }

    protected double GetTimeSeconds() {
        return Time.GetTicksUsec() / 1000000.0;
    }

    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);

        // Every physics tick we iterate all overlapping bodies and determine which ones are newly inside/outside of the filter.
        // Subclasses will receive callbacks via OnBodyEntered() / OnBodyExited() for any physics bodies that satisfy the filter.
        foreach(var body in GetOverlappingBodies()) {
            if(body is PhysicsBody2D physBody) {
                bool seen = NearbyBodies.Contains(physBody);
                bool excluded = InfluenceExceptions.Contains(body);
                bool passedFilter = InfluenceFilter == null || InfluenceFilter.FilterNode(body, this);
                if (seen) {
                    if(excluded || !passedFilter) {
                        NearbyBodies.Remove(physBody);
                        if (physBody is Character character) {
                            NearbyCharacters.Remove(character);
                        }
                        OnBodyExited(physBody);
                    }
                }
                else {
                    if(!excluded && passedFilter) {
                        NearbyBodies.Add(physBody);
                        if(physBody is Character character) {
                            NearbyCharacters.Add(character);
                        }
                        OnBodyEntered(physBody);
                    }
                }
            }
        }
    }

    // Removes and prohibits a physics body from registering as under the influence of this AreaEffect. If permanent is true the
    // body can never reenter the AreaEffect even if it newly collides/overlaps.
    public void AddInfluenceExceptionFor(PhysicsBody2D body, bool permanent = true) {
        // Only do work here if we don't already have an exception.
        if(!InfluenceExceptions.Contains(body)) {
            if(permanent) {
                InfluenceExceptions.Add(body);
            }

            // We want to remove the body from any lists it's recorded to. That means NearbyBodies, NearbyCharacters, AND any lists
            // that subclasses might maintain themselves (managed via OnBodyEntered() / OnBodyExited()).
            if(NearbyBodies.Contains(body)) {
                NearbyBodies.Remove(body);
                if (body is Character character) {
                    NearbyCharacters.Remove(character);
                }
                OnBodyExited(body);
            }
        }
    }
}