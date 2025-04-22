using ExtensionMethods;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

// Small town generates a random assortment of buildings along a general path defined for the scenario.
// TODO: Scenario base class?
public partial class SmallTown : Node2D
{
    // The PathMesh defining the main road / path through the town.
    [Export]
    public PathMesh MainPathMesh { get; protected set; } = null;

    [ExportCategory("Structures")]
    // The building scene to instantiate along the path.
    // TODO: Might need a self-describing format that doesn't require instantiation - like a Resource that we can query for size and other details.
    [Export]
    public PackedScene BuildingSceneTemplate { get; protected set; } = null;

    // Size of a bounding rectangle to use for approximating building sizes when placing buildings in the scenario. 
    // This needs to be at least as large as all geometry contained within BuildingSceneTemplate at standard 1:1 scale for correct behavior.
    [Export]
    public Vector2 BuildingFootprint { get; protected set; }

    // How many buildings to place.
    [Export]
    public int DesiredBuildingCount { get; protected set; } = 10;

    public override void _Ready() {
        base._Ready();

        if (this.GetGameWorld() == null) {
            GD.PrintErr($"No game World instance found in {Name}'s scene tree. Cannot generate scenario.");
            return;
        }

        // Ensure that generation runs after the world has been initially set up.
        Callable.From(Generate).CallDeferred();
    }

    protected void Generate() {
        var world = this.GetGameWorld();
        // Produce a set of candidate points, uniformly distributed across the bounds of the world.
        Rect2 boundingRect = new Rect2(world.GlobalPosition - world.RegionBounds/2f, world.RegionBounds);
        var points = PointCloudFromBounds(boundingRect, BuildingFootprint);
        GD.Print($"{Name}[{GetType()}] generated {points.Count} candidate points.");

        // Determine possible placements for the buildings we want to spawn in the world; Using a custom spacing rule to prevent
        // buildings from spawning too close together.
        var minBuildingSpacing = BuildingFootprint;
        minBuildingSpacing.Y *= 2; // make sure buildings aren't on top of each other.
        var placements = GetPlacementLocations(points, boundingRect, BuildingFootprint, DesiredBuildingCount, minBuildingSpacing);
        GD.Print($"{Name}[{GetType()}] found {placements.Count} viable placements matching criteria.");

        // TODO: Place buildings.
        foreach (var point in placements) {
            this.DrawDebugRect(point, new Rect2(point, BuildingFootprint), new Color(1, 0, 0), 5.0f); // only draw for first 5 seconds
        }
    }

    // Given a bounding region in world-coordinates, produce a uniform point cloud that can be further sampled / filtered / enriched
    // to determine viable locations for procedural generation.
    public List<Vector2> PointCloudFromBounds(Rect2 bounds, Vector2? inSpacing = null) {
        // Impose a default of (1,1) for spacing if none is provided.
        var spacing = inSpacing ?? Vector2.One;

        var points = new List<Vector2>();
        float startX = bounds.Position.X;
        float startY = bounds.Position.Y;
        float endX = startX + bounds.Size.X;
        float endY = startY + bounds.Size.Y;

        for (float x = startX; x < endX; x += spacing.X) {
            for (float y = startY; y < endY; y += spacing.Y) {
                points.Add(new Vector2(x, y));
            }
        }
        return points;
    }

    // Given a point cloud and bounding rectangle, attempts to select points where a rectangle with the indicated footprint (size) can be
    // placed the desired number of times without overlapping footprints.
    // Note this does NOT place anything in the world, it only identifies a set of points where rectangular regions can exist without
    // overlap.
    protected List<Vector2> GetPlacementLocations(List<Vector2> pointCloud, Rect2 boundaryRect, Vector2 footprint, 
        int desiredCount, Vector2? minFootprintSpacing = null) {

        var placed = new List<Vector2>();
        var shuffledPoints = pointCloud.OrderBy(_ => GD.Randf()).ToList();

        foreach (var point in shuffledPoints) {
            // Centered footprint rectangle with optional margin padding
            Vector2 paddedFootprint = footprint + (minFootprintSpacing ?? Vector2.Zero);
            Rect2 candidateRect = new Rect2(point - paddedFootprint / 2f, paddedFootprint);

            // 1. Check bounds
            if (!boundaryRect.Encloses(candidateRect)) {
                continue;
            } 

            // 2. Check for overlap or proximity (via intersecting padded rectangles)
            bool tooClose = placed.Any(existing => {
                var existingRect = new Rect2(existing - paddedFootprint / 2f, paddedFootprint);
                return existingRect.Intersects(candidateRect);
            });

            if (tooClose) {
                continue;
            }

            // Valid point
            placed.Add(point);

            if (placed.Count >= desiredCount) {
                break;
            }
        }
        return placed;
    }
}
