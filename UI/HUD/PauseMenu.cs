using Godot;
using System;

// Pause Menu is... the pause menu. It shows up when you pause.
public partial class PauseMenu : CanvasLayer
{
    public Container PauseOverview { get; private set; }
    public Container OptionsPage { get; private set; }

    public override void _Ready() {
        base._Ready();

        PauseOverview = GetNode<Container>("PauseOverview");
        PauseOverview.Visible = true;
        OptionsPage = GetNode<Container>("OptionsPage");
        OptionsPage.Visible = false;
    }

    public PauseMenu() {
        this.ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event.IsActionPressed("pause_menu")) {
            ToggleDisplay();
        }
    }

    // Convenience toggle for binding to buttons in the UI.
    public void ToggleDisplay() {
        Visible = !Visible;
    }

    // Convenience toggle for binding to buttons in the UI.
    public void ToggleOptionsPage() {
        OptionsPage.Visible = !OptionsPage.Visible;
    }

    // Signal binding for button presses.
    public void QuitGame() {
        GetTree().Quit();
    }
}
