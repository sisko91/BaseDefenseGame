using ExtensionMethods;
using Godot;
using System;

// A door is a subcomponent of a Building that typically connects adjacent InteriorRegions.
// Doors place and remove a collision object to prohibit or enable passage.
public partial class Door : Node2D
{
    private bool isOpen = false;
    public bool Open
    {
        get
        {
            return isOpen;
        }
        set
        {
            SetOpen(value);
        }
    }

    private uint originalCollisionLayers;

    // Cached reference to the Blockage defined on the Door subtree.
    private CollisionObject2D blockage;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        blockage = GetNode<CollisionObject2D>("Blockage");
        originalCollisionLayers = blockage.CollisionLayer;
    }

    public void SetOpen(bool value)
    {
        isOpen = value;

        if(isOpen)
        {
            // Disable rendering.
            blockage.Visible = false;
            // Disable physics.
            blockage.CollisionLayer = 0;
        }
        else
        {
            blockage.Visible = true;
            blockage.CollisionLayer = originalCollisionLayers;
        }

        this.GetGameWorld().RebakeNavMesh();
    }

    // This callback is defined in case the door is connected to a signal from an interaction area.

    public void OnCharacterCanInteract(InteractionArea area, Character character)
    {
        if (character is Player)
        {
            GD.Print($"{character.Name} may now interact with {GetParent()?.Name}::{Name}");
        }
    }

    // This callback is defined in case the door is connected to a signal from an interaction area.
    public void OnCharacterCannotInteract(InteractionArea area, Character character)
    {
        if (character is Player)
        {
            GD.Print($"{character.Name} can no longer interact with {GetParent()?.Name}::{Name}");
        }
    }
    // This callback is defined in case the door is connected to a signal from an interaction area.
    public void OnToggleInteract(InteractionArea area, Character character)
    {
        Open = !Open;
        GD.Print($"{character.Name} {(Open ? "opened" : "closed")} {GetParent()?.Name}::{Name}");
    }
}
