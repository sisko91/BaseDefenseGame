using Godot;
using System;

public partial class OptionsMenu : TabContainer
{
    // Cached reference to the control for the "Dev" tab in the options menu, defined on the .tscn.
    public Control DevOptionsTab { get; private set; }

    public override void _Ready() {
        base._Ready();

        DevOptionsTab = GetNode<Control>("Dev");
        SyncDevOptions(true);

        // Subscribe to hear when this menu's visibility changes, as that indicates it needs to be refreshed.
        VisibilityChanged += OnVisibilityChanged;
    }

    private void OnVisibilityChanged() {
        if(Visible) {
            SyncDevOptions(false);
        }
    }

    private void SyncDevOptions(bool bBindData) {
        CheckButton drawSteering = DevOptionsTab.GetNode<CheckButton>("DrawSteeringToggle");
        drawSteering.SetPressedNoSignal(DebugConfig.Instance.DRAW_STEERING);

        CheckButton drawNav = DevOptionsTab.GetNode<CheckButton>("DrawNavToggle");
        drawNav.SetPressedNoSignal(DebugConfig.Instance.DRAW_NAVIGATION);

        CheckButton drawCollisionBodies = DevOptionsTab.GetNode<CheckButton>("DrawCollisionBodyToggle");
        drawCollisionBodies.SetPressedNoSignal(DebugConfig.Instance.DRAW_COLLISION_BODY_RADIUS);

        if (bBindData) {
            drawSteering.Toggled += DebugConfig.Instance.SetDrawSteering;
            drawNav.Toggled += DebugConfig.Instance.SetDrawNavigation;
            drawCollisionBodies.Toggled += DebugConfig.Instance.SetDrawCollisionBodies;
        }
    }
}
