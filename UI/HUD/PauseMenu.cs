using Godot;
using System;

// Pause Menu is... the pause menu. It shows up when you pause.
public partial class PauseMenu : CanvasLayer
{
    public Container MainMenu { get; private set; }
    public Container OptionsPage { get; private set; }

    public override void _Ready() {
        MainMenu = GetNode<Container>("%MainMenu");
        OptionsPage = GetNode<Container>("%OptionsPage");
        OptionsPage.Visible = false;

        OptionsPage.VisibilityChanged += OnVisibilityChange;
    }

    public PauseMenu() {
        this.ProcessMode = ProcessModeEnum.Always;
    }

    //"For gameplay input, Node._unhandled_input() is generally a better fit, because it allows the GUI to intercept the events"
    //https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause_menu")) {
            ToggleDisplay();
        }
    }

    // Convenience toggle for binding to buttons in the UI.
    public void ToggleDisplay() {
        Visible = !Visible;
        if (Visible) {
            MainMenu.GrabFocus();
        }
    }

    // Signal binding for button presses.
    public void QuitGame() {
        GetTree().Quit();
    }

    private void OnVisibilityChange()
    {
        if (OptionsPage.Visible) {
            MainMenu.Hide();
        } else {
            MainMenu.Show();
        }
    }
}
