using Godot;
using System;

// InteractionArea is where interactions happen. These areas detect characters entering and exiting and coordinate interactions.
public partial class InteractionArea : Area2D
{
    [Signal]
    public delegate void CharacterEnteredEventHandler(InteractionArea area, Character character);
    [Signal]
    public delegate void CharacterExitedEventHandler(InteractionArea area, Character character);
    [Signal]
    public delegate void InteractedEventHandler(InteractionArea area, Character character);

    // Configure this to point to a Control subnode that should be presented when a player character is in range.
    [Export]
    public Control InteractionPrompt = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public void Interact(Character character)
    {
        OnInteract(character);
        EmitSignal(SignalName.Interacted, this, character);
    }

    private void OnBodyExited(Node2D body)
    {
        if(body is Character character)
        {
            character.NearbyInteractions.Remove(this);
            EmitSignal(SignalName.CharacterExited, this, character);
            if (character is Player player)
            {
                if (InteractionPrompt != null)
                {
                    InteractionPrompt.Visible = false;
                }
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Character character)
        {
            character.NearbyInteractions.Add(this);
            EmitSignal(SignalName.CharacterEntered, this, character);
            if(character is Player player)
            {
                if(InteractionPrompt != null)
                {
                    InteractionPrompt.Visible = true;
                }
            }
        }
    }

    // Children can override this for bespoke interaction behavior.
    protected virtual void OnInteract(Character character)
    {
        GD.Print($"{character.Name} interacted with {GetParent()?.Name}::{Name}");
    }
}
