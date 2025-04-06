using Godot;
using System;

public partial class MerchantUI : Control
{
    public override void _Ready()
    {
        base._Ready();
        FocusMode = FocusModeEnum.All;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Input.IsActionJustPressed("Exit"))
        {
            AcceptEvent();
            Visible = false;
            ReleaseFocus();
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if (!Visible)
        {
            return;
        }

        if (@event.IsActionPressed("Exit"))
        {
            AcceptEvent();
            Visible = false;
            ReleaseFocus();
        }
    }

    public void Load()
    {
        Visible = true;
        GrabFocus();
    }

    public void Unload()
    {
        Visible = false;
        ReleaseFocus();
    }
}
