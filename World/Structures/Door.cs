using Godot;
using System;

// A door is a subcomponent of a Building that typically connects adjacent InteriorRegions.
// Doors place and remove a collision object to prohibit or enable passage.
// Doors can be opened or closed.
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
	private PhysicsBody2D blockage;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		blockage = GetNode<PhysicsBody2D>("Blockage");
        originalCollisionLayers = blockage.CollisionLayer;
    }

	public void SetOpen(bool value)
	{
		isOpen = value;

		if(isOpen)
		{
			// Disable rendering.
			Visible = false;
			// Disable physics.
			blockage.CollisionLayer = 0;
        }
		else
		{
			Visible = true;
			blockage.CollisionLayer = originalCollisionLayers;
		}
	}

	// This callback is defined in case the door is connected to a signal from an interaction.
	public void ToggleOnInteract(Interactable interactable, Player player)
	{
		Open = !Open;
	}
}
