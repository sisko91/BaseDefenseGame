using Godot;
using System;

public partial class ShopItem : Resource
{
    [Export]
    public PackedScene Weapon;

    [Export]
    public string Name;

    [Export]
    public string Description;

    [Export]
    public int Price;
}