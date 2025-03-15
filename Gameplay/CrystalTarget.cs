using Godot;
using System;

public partial class CrystalTarget : Character
{
    public override void _Ready()
    {
        base._Ready();

        AddToGroup("Crystal");
    }
}
