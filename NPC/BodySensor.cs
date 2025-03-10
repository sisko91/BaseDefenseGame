using ExtensionMethods;
using Godot;
using System;

public partial class BodySensor : Area2D
{
    public Godot.Collections.Array<Player> Players { get; private set; }
    public Godot.Collections.Array<NonPlayerCharacter> NPCs { get; private set; }
    public Godot.Collections.Array<StaticBody2D> Walls { get; private set; }

    // A Signal that other elements can (be) subscribe(d) to in order to hear about Players newly entering or exiting the sensor.
    [Signal]
    public delegate void PlayerSensedEventHandler(Player player, bool bSensed);

    // A Signal that other elements can (be) subscribe(d) to in order to hear about NPCs newly entering or exiting the sensor.
    [Signal]
    public delegate void NpcSensedEventHandler(NonPlayerCharacter npc, bool bSensed);


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Players = new Godot.Collections.Array<Player>();
        NPCs = new Godot.Collections.Array<NonPlayerCharacter>();
        Walls = new Godot.Collections.Array<StaticBody2D>();

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node2D body)
    {
        switch (body)
        {
            case Player player:
                Players.Add(player);
                EmitSignal(SignalName.PlayerSensed, player, true);
                break;
            case NonPlayerCharacter npc:
                NPCs.Add(npc);
                EmitSignal(SignalName.NpcSensed, npc, true);
                break;
            case StaticBody2D staticBody:
                Walls.Add(staticBody);
                break;
            default:
                break;
        }

        //GD.Print($"{body.Name} entered {GetParent().Name}'s Sensor");
    }

    private void OnBodyExited(Node2D body)
    {
        switch(body)
        {
            case Player player:
                Players.Remove(player);
                EmitSignal(SignalName.PlayerSensed, player, false);
                break;
            case NonPlayerCharacter npc:
                NPCs.Remove(npc);
                EmitSignal(SignalName.NpcSensed, npc, false);
                break;
            case StaticBody2D staticBody:
                Walls.Remove(staticBody);
                break;
            default:
                break;
        }

        //GD.Print($"{body.Name} exited {GetParent().Name}'s Sensor");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
