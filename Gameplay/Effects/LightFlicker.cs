using ExtensionMethods;
using Godot;
using System;

public partial class LightFlicker : Node2D
{
    [Export]
    private double FlickerMinTime = 0.05;
    [Export]
    private double FlickerMaxTime = 0.3;

    private Timer FlickerTimer;

    [Export]
    private double Energy = 3.0;
    [Export]
    private double TextureScale = 3.0;
    [Export]
    private double FlickerMinMultipler = 0.9;
    [Export]
    private double FlickerMaxMultiplier = 1.1;
    [Export]
    private double FlickerMultiplier = 1.0;

    private PointLight2D light;
    private AnimatedSprite2D sprite;

    //Automatically turn on/off depending on time of day
    [Export]
    private bool AutoEnableTimeOfDay = false;

    //Dim the light during the day for a better indoor lighting look
    [Export]
    private bool DimTimeOfDay = false;

    public override void _Ready()
    {
        base._Ready();
        FlickerTimer = new Timer();
        FlickerTimer.OneShot = true;
        FlickerTimer.Timeout += Flicker;
        AddChild(FlickerTimer);

        light = GetNode<PointLight2D>("PointLight2D");
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }
    public override void _Process(double delta)
    {
        base._Process(delta);

        var dayTime = this.GetGameWorld().DayNight.GetDayTime();
        if (AutoEnableTimeOfDay && dayTime > 0.25 && dayTime < 0.75)
        {
            sprite.Animation = "off";
            light.Visible = false;
            return;
        }
        sprite.Animation = "on";
        light.Visible = true;

        if (FlickerTimer.TimeLeft == 0) {
            var nextFlickerTime = new Random().NextDouble() * (FlickerMaxTime - FlickerMinTime) + FlickerMaxTime;
            FlickerTimer.WaitTime = nextFlickerTime;
            FlickerTimer.Start();
        }

        float energyMult = 1;
        if (DimTimeOfDay) {
            var distFromNoon = 2 * Math.Abs(0.5f - DayNight.Instance.GetDayTime());
            energyMult = (float)(0.1 + distFromNoon);
        }

        light.Energy = Mathf.Lerp(light.Energy, (float)(Energy * energyMult * FlickerMultiplier), 0.05f);
        light.TextureScale = Mathf.Lerp(light.TextureScale, (float)(TextureScale * FlickerMultiplier), 0.05f);
    }

    private void Flicker()
    {
        FlickerMultiplier = new Random().NextDouble() * (FlickerMaxMultiplier - FlickerMinMultipler) + FlickerMaxMultiplier;
    }
}
