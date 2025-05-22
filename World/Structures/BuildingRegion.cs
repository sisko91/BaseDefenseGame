using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Interior Regions define areas "within" a structure. They detect characters and players entering / leaving and provide other data and callbacks related to
// defining inner zones.
public partial class BuildingRegion : Area2D
{
    public static string INDOOR_GROUP_NAME = "IndoorNodes";

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

    private int WindowsOpen = 0;
    private HashSet<Node2D> IndoorNodes = new HashSet<Node2D>();

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

            if (child is Window window && window.Open) {
                WindowsOpen += 1;
            }

            if (child.IsInGroup(INDOOR_GROUP_NAME)) {
                IndoorNodes.Add((Node2D) child);
            }
        }
    }

    public override void _Process(double delta) {
        base._Process(delta);

        var bodies = GetOverlappingBodies().Where(node => node is Moveable).ToList();
        var distFromNoon = 2 * Math.Abs(0.5f - DayNight.Instance.GetDayTime());
        foreach (Node2D node in IndoorNodes.Concat(bodies)) {
            node.Material.Set("shader_parameter/window_light_strength", 0.2 + (1 - distFromNoon) * 0.2 * WindowsOpen);
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

    //Area2D.OverlapsBody is delayed 1 frame, so it's not useful for checking things the same frame they spawn in
    //This is hardcoded for rectangle shapes, but can be modified to support collision polygons
    public bool OverlapsBodyAccurate(Node2D node) {
        var boundary = GetNode<CollisionShape2D>("Boundary");
        RectangleShape2D shape = (RectangleShape2D)boundary.Shape;
        var start = boundary.GlobalPosition - new Vector2(shape.Size.X / 2, shape.Size.Y / 2);
        var end = boundary.GlobalPosition + new Vector2(shape.Size.X / 2, shape.Size.Y / 2);

        return node.GlobalPosition.X >= start.X && node.GlobalPosition.Y >= start.Y && node.GlobalPosition.X <= end.X && node.GlobalPosition.Y <= end.Y;
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
