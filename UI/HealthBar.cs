using Godot;
using System;

public partial class HealthBar : Node2D
{
	// When true, the health bar is hidden until/while the character is at full health.
	[Export]
	public bool bInvisibleWhenFull = false;

	// Cached value of the original (global) relative-position of this node, so it can be positioned consistently each frame.
	private Vector2 globalOffset;

	// Cached value of the original (global) rotation of this node, so it can be oriented consistently each frame.
	private float globalRotation;

	// Cached reference to the child ColorRect representing the filling of the health bar.
	private ColorRect barFillerRect;
	// Cached value of the original health bar's full size, for calculating the current fullness.
	private Vector2 fullBarSize;

	// What percentage of the bar is currently full.
	public float PercentageFull { get; private set; } = 1.0f;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		globalOffset = Position;
		globalRotation = GlobalRotation;
		PercentageFull = 1.0f;

		barFillerRect = GetNode<ColorRect>("BarFiller");
		fullBarSize = barFillerRect.Size;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Re-set the position and rotation each frame so that no matter where the parent moves we stay above it.
		var parentPosition = GetParent<Node2D>().GlobalPosition;
		GlobalPosition = parentPosition + globalOffset;
        GlobalRotation = globalRotation;

		if(bInvisibleWhenFull)
		{
            bool bFull = Mathf.IsEqualApprox(PercentageFull, 1.0f);
			Visible = !bFull;
        }
		else
		{
			Visible = true;
		}
    }

	public void OnCharacterHealthChanged(NonPlayerCharacter character, float newHealth, float oldHealth)
	{
		PercentageFull = Mathf.Clamp(newHealth / character.MaxHealth, 0, 1.0f);
		// Adjust the width of the health rectange, but keep its height the same.
		var newSize = fullBarSize * PercentageFull;
		newSize.Y = fullBarSize.Y;
		barFillerRect.Size = newSize;
	}
}
