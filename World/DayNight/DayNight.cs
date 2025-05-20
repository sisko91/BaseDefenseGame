using ExtensionMethods;
using Godot;
using System;

public partial class DayNight : Node
{
    public static DayNight Instance;

    private GradientTexture2D LightGradient;
    private Timer DayTimer;

    [Export]
    public int DayLengthSeconds = 300;

    //Slider that snaps to 1 hour increments (1 / 24)
    [Export(PropertyHint.Range, "0,1,0.041666")]
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
        Instance = this;
    }

    public override void _Process(double delta)
    {
        DayNightColor = LightGradient.Gradient.Sample((float)GetDayTime());
        RenderingServer.GlobalShaderParameterSet("day_night_color", DayNightColor);

        if (FreezeTime)
        {
            DayTimer.Paused = true;
            return;
        }
        else if (DayTimer.Paused)
        {
            DayTimer.Paused = false;
        }
    }

    //0 = midnight, 0.5 = noon, 1 = midnight
    public double GetDayTime()
    {
        var dayPercent = 1 - DayTimer.TimeLeft / DayTimer.WaitTime;
        return (dayPercent + DayStartTime) % 1;
    }
}
