using Godot;
using System;

public partial class OptionsMenu : TabContainer
{
    public override void _Ready() {
        base._Ready();
        SyncDevOptions();

        VisibilityChanged += UpdateFocus;
    }

    private void UpdateFocus()
    {
        if (Visible) {
            GrabFocus();
        } else {
            ReleaseFocus();
        }
    }

    private void SyncDevOptions() {
        CheckButton drawSteering = GetNode<CheckButton>("%DrawSteeringToggle");
        drawSteering.SetPressedNoSignal(DebugConfig.Instance.DRAW_STEERING);
        drawSteering.Toggled += DebugConfig.Instance.SetDrawSteering;

        CheckButton drawNav = GetNode<CheckButton>("%DrawNavToggle");
        drawNav.SetPressedNoSignal(DebugConfig.Instance.DRAW_NAVIGATION);
        drawNav.Toggled += DebugConfig.Instance.SetDrawNavigation;

        CheckButton drawCollisionBodies = GetNode<CheckButton>("%DrawCollisionBodyToggle");
        drawCollisionBodies.SetPressedNoSignal(DebugConfig.Instance.DRAW_COLLISION_BODY_RADIUS);
        drawCollisionBodies.Toggled += DebugConfig.Instance.SetDrawCollisionBodies;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event.IsActionPressed("Cancel")) {
            AcceptEvent();
            Visible = false;
        }
    }
}
