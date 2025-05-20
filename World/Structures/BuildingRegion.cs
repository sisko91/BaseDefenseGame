using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;

// Interior Regions define areas "within" a structure. They detect characters and players entering / leaving and provide other data and callbacks related to
// defining inner zones.
public partial class BuildingRegion : Area2D
{
    // What "floor" of elevation this region is located on. 0 is the ground floor.
    [Export]
    public int ElevationLevel = 0;

    [Export]
    public bool InteriorRegion = true;

    public bool HasExit = false;

    public Godot.Collections.Array<Stairs> Stairs;
    public Godot.Collections.Array<Door> Doors;

    public Building OwningBuilding { get; set; }

    private HashSet<Node2D> IgnoreMonitoringForNodes = new HashSet<Node2D>();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Stairs = new Godot.Collections.Array<Stairs>();
        Doors = new Godot.Collections.Array<Door>();
        this.GetGameWorld();
        foreach (var child in this.GetAllChildren()) {
            if (child is Stairs stairs) {
                Stairs.Add(stairs);
                stairs.OwningRegion = this;
            }

            if (child is Door door) {
                if (door.IsExit) {
                    HasExit = true;
                }
                Doors.Add(door);
            }
        }
    }

    public List<Door> GetExits() {
        List<Door> exits = new List<Door>();
        foreach (Door d in Doors) {
            if (d.IsExit) {
                exits.Add(d);
            }
        }

        return exits;
    }

    public void AddMonitoringException(Node2D node) {
        IgnoreMonitoringForNodes.Add(node);
    }

    public void RemoveMonitoringException(Node2D node) {
        IgnoreMonitoringForNodes.Remove(node);
    }

    public bool ShouldIgnoreNode(Node2D node) {
        return IgnoreMonitoringForNodes.Contains(node);
    }
}
