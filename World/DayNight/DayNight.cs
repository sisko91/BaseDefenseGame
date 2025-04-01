using ExtensionMethods;
using Godot;
using System;

public partial class DayNight : Node
{

    GradientTexture2D lightGradient;
    Timer dayTimer;

    [Export]
    public int dayLengthSeconds = 300;

    [Export]
    public float dayStartTime = 0.5f; //0 = midnight, 0.5 = noon, 1 = midnight

    public Color dayNightColor { get; private set; }

    public override void _Ready()
    {
        lightGradient = GD.Load<GradientTexture2D>("res://art/World/daynight.tres");

        dayTimer = new Timer();
        dayTimer.WaitTime = dayLengthSeconds;
        dayTimer.Autostart = true;
        AddChild(dayTimer);

        dayNightColor = new Color(0, 0, 0, 0);
    }

    public override void _Process(double delta)
    {
        var dayPercent = 1 - dayTimer.TimeLeft / dayTimer.WaitTime;
        dayPercent = (dayPercent + dayStartTime) % 1;
        dayNightColor = lightGradient.Gradient.Sample((float)dayPercent);

        RenderingServer.GlobalShaderParameterSet("day_night_color", dayNightColor);
    }
}
