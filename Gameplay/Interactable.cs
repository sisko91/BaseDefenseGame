using Godot;
using System;

// Interactable is a node that can be attached to any parent PhysicsBody2D in order to make it interactable within the scene.
public partial class Interactable : Node2D
{
    [Signal]
    public delegate void InteractionEventHandler(Interactable interactable, Player player);

    public override void _EnterTree()
    {
        if (GetParent() is not PhysicsBody2D)
        {
            GD.PushError($"Interactable must be a child of a PhysicsBody2D, not {GetParent()?.GetType()}! Removing from {GetParent()?.Name}.");
            QueueFree();
        }
    }
    
    // TODO: Make this work for NPCs too.
    public void Interact(Player player)
    {
        OnInteract(player);
        EmitSignal(SignalName.Interaction, this, player);
    }

    // Children can override this for bespoke interaction behavior.
    protected virtual void OnInteract(Player player)
    {
        GD.Print($"{player.Name} interacted with {GetParent()?.Name}::{Name}");
    }
}

namespace ExtensionMethods
{
    public static class InteractableNodeExtensions
    {
        // Returns true if any of this PhysicsBody2D's children is an Interactable node.
        public static bool IsInteractable(this PhysicsBody2D physNode)
        {
            return physNode.GetInteractables().Count > 0;
        } 

        // Collects any and all Interactable child nodes of this PhysicsBody2D.
        public static Godot.Collections.Array<Interactable> GetInteractables(this PhysicsBody2D physNode)
        {
            var interactables = new Godot.Collections.Array<Interactable>();
            foreach(var child in physNode.GetChildren())
            {
                if(child is Interactable interactable)
                {
                    interactables.Add(interactable);
                }
            }
            return interactables;
        }
    }
}
