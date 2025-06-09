using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;

public interface IWorldLifecycleListener
{
    public void PostWorldInit(World gameWorld);
}

// WLM is a global singleton subsystem for managing the various stages of world/level startup and other major game
// events. This is autoloaded ahead of everything else in the game.
public partial class WorldLifecycleManager : Node
{
    // Various states the currently-active level could be in.
    public enum WorldLifecycleState
    {
        // World is currently initializing.
        LifecycleInit,
        // World has finished initializing and play has begun.
        LifecyclePlay,
    };

    public WorldLifecycleState CurrentState = WorldLifecycleState.LifecycleInit;

    // All listeners in existence right now.
    private List<IWorldLifecycleListener> listeners = [];
    
    // Listeners which have already been notified PostWorldInit.
    private readonly HashSet<WeakReference<IWorldLifecycleListener>> notifiedInitListeners = new();

    private World gameWorld = null;
    public override void _EnterTree()
    {
        var existingWorld = GetTree().Root.GetChildrenOfType<World>().FirstOrDefault();
        if (existingWorld != null)
        {
            GD.PushError($"A world ({existingWorld.Name}) was already present in the root scene when WLM was initialized. Lifecycle events will not function correctly if this world is already initialized.");
        }
    }

    public override void _Ready()
    {
        CurrentState = WorldLifecycleState.LifecycleInit;
        GetTree().NodeAdded += OnNodeAddedToTree;
    }

    private void OnNodeAddedToTree(Node addedNode)
    {
        if (addedNode is World addedWorld)
        {
            if (gameWorld == null)
            {
                gameWorld = addedWorld;
                gameWorld.WorldInitialized += HandleWorldInitialized;
            }
            
            // TODO: Check and handle if the world is already initialized?
            return;
        }

        if (addedNode is IWorldLifecycleListener listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
            
            // Call PostWorldInit on any new listener that hasn't heard it yet.
            if (CurrentState == WorldLifecycleState.LifecyclePlay)
            {
                foreach (var weak in notifiedInitListeners)
                {
                    if (weak.TryGetTarget(out var target) && target == listener)
                    {
                        // this listener has already been notified, so we're done.
                        return;
                    }
                }
                // Tell the new object that the world is already initialized. We always do this on the next frame,
                // because the node was likely added to the scene during its _Ready() and may need one cycle to be
                // ready for this callback.
                void announce()
                {
                    listener.PostWorldInit(gameWorld);
                    notifiedInitListeners.Add(new WeakReference<IWorldLifecycleListener>(listener));
                }
                Callable.From(announce).CallDeferred();
            }
        }
    }

    public void HandleWorldInitialized(World _)
    {
        if (CurrentState != WorldLifecycleState.LifecycleInit)
        {
            GD.PushError($"World already initialized. Ignoring.");
            return;
        }
        
        CurrentState = WorldLifecycleState.LifecyclePlay;

        // Announce to any registered listeners that we're moving to PostWorldInit.
        foreach (var listener in listeners)
        {
            listener.PostWorldInit(gameWorld);
            notifiedInitListeners.Add(new WeakReference<IWorldLifecycleListener>(listener));
        }
    }
}
