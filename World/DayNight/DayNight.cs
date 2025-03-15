using ExtensionMethods;
using Godot;
using System;

public partial class DayNight : Node2D
{

    GradientTexture2D lightGradient;
    Timer dayTimer;

    [Export]
    public int dayLengthSeconds = 15;
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
        var dayColor = lightGradient.Gradient.Sample((float)dayPercent);
        if (Material != null) //Dunno how this is null sometimes
        {
            Material.Set("shader_parameter/tint", dayColor);
        }
    }
}
