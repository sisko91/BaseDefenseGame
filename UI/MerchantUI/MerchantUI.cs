using Godot;
using System;

public partial class MerchantUI : Control
{
    public override void _Ready()
    {
        base._Ready();
        FocusMode = FocusModeEnum.All;
        VisibilityChanged += UpdateFocus;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event.IsActionPressed("Cancel")) {
            AcceptEvent();
            Hide();
        }

        //Steal mouse input while open - the player can still move, but cant shoot
        //Otherwise they shoot when they click buttons, which is jank
        if (@event is InputEventMouseButton) {
            AcceptEvent();
        }
    }

    private void UpdateFocus()
    {
        if (Visible) {
            GrabFocus();
        } else {
            ReleaseFocus();
        }
    }
}
