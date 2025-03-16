using ExtensionMethods;
using Godot;
using System;

public partial class DayNight : Node2D
{

    GradientTexture2D lightGradient;
    Timer dayTimer;

    [Export]
    public int dayLengthSeconds = 300;

    [Export]
    public float dayStartTime = 0.5f; //0 = midnight, 0.5 = noon, 1 = midnight
    public override void _Ready()
    {
        lightGradient = GD.Load<GradientTexture2D>("res://art/World/daynight.tres");

        dayTimer = new Timer();
        dayTimer.WaitTime = dayLengthSeconds;
        dayTimer.Autostart = true;
        AddChild(dayTimer);
    }

    public override void _Process(double delta)
    {
        var dayPercent = 1 - dayTimer.TimeLeft / dayTimer.WaitTime;
        dayPercent = (dayPercent + dayStartTime) % 1;
        var dayColor = lightGradient.Gradient.Sample((float)dayPercent);
        if (Material != null) //Dunno how this is null sometimes
        {
            Material.Set("shader_parameter/tint", dayColor);
        }
    }
}
