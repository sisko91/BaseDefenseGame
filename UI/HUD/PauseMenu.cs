using Godot;
using System;

// Pause Menu is... the pause menu. It shows up when you pause.
public partial class PauseMenu : CanvasLayer
{
    public PauseMenu() {
        // Only run _Process() when the tree is otherwise paused.
        this.ProcessMode = ProcessModeEnum.WhenPaused;
    }

    public void ToggleDisplay() {
        if(Visible) {
            Visible = false;
        }
        else {
            Visible = true;
        }
    }

    // Signal binding for button presses.
    public void QuitGame() {
        GetTree().Quit();
    }
}
