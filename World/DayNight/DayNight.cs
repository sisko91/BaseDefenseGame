using ExtensionMethods;
using Godot;
using System;

public partial class DayNight : Node
{

    GradientTexture2D LightGradient;
    Timer DayTimer;

    [Export]
    public int DayLengthSeconds = 300;

    [Export]
    public float DayStartTime = 0.5f; //0 = midnight, 0.5 = noon, 1 = midnight

    [Export]
    public bool FreezeTime = false;

    public Color DayNightColor { get; private set; }

    public override void _Ready()
    {
        LightGradient = GD.Load<GradientTexture2D>("res://art/World/daynight.tres");

        DayTimer = new Timer();
        DayTimer.WaitTime = DayLengthSeconds;
        DayTimer.Autostart = true;
        AddChild(DayTimer);

        DayNightColor = new Color(0, 0, 0, 0);
    }

    public override void _Process(double delta)
    {
        if (FreezeTime) {
            DayTimer.Paused = true;
            return;
        } else if (DayTimer.Paused) {
            DayTimer.Paused = false;
        }

        var dayPercent = 1 - DayTimer.TimeLeft / DayTimer.WaitTime;
        dayPercent = (dayPercent + DayStartTime) % 1;
        DayNightColor = LightGradient.Gradient.Sample((float)dayPercent);

        RenderingServer.GlobalShaderParameterSet("day_night_color", DayNightColor);
    }
}
