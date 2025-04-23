using ExtensionMethods;
using Godot;
using System.Collections.Generic;
using System.Linq;

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

    [ExportCategory("Advanced")]
    // Rectangular regions (specified in world coordinates) where buildings cannot be placed/spawned.
    // TODO: This is generalizable to any type of procedurally-placed node, but we'll want some basic type enumerations to go with it.
    [Export]
    public Godot.Collections.Array<RectRegion> BuildingExclusionZones = [];

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
        var points = PointCloudFromBounds(boundingRect, BuildingFootprint/2f);
        int pointSizeInitial = points.Count;
        GD.Print($"{Name}[{GetType()}] generated {pointSizeInitial} candidate points.");

        // Remove points too close to the main path through the world, making sure no building is too close.
        points = RemovePointsNearPath(points, 
            pathMesh: MainPathMesh, 
            // additionalPathPadding: We want this slightly larger than the largest span of the footprint, so that we don't place anything
            //                        so close that it's practically touching the edge of the path.
            additionalPathPadding: BuildingFootprint.Length() * 1.5f,
            // pathStepLength: We want this to be the smaller of the two footprint dimensions because this is the distance between 
            //                 consecutive points we are testing along the path's length. If it were larger than any span of the footprint,
            //                 we'd miss filtering out some points.
            pathStepLength: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));

        GD.Print($"{Name}[{GetType()}] filtered out {pointSizeInitial - points.Count} candidate points too close to the path.");

        // Remove points near exclusion zones.
        pointSizeInitial = points.Count;
        points = RemovePointsNearRects(points, BuildingExclusionZones, BuildingFootprint.Length() * 1.5f);
        GD.Print($"{Name}[{GetType()}] filtered out {pointSizeInitial - points.Count} candidate points too close to an exclusion zone.");

        // Determine possible placements for the buildings we want to spawn in the world; Using a custom spacing rule to prevent
        // buildings from spawning too close together.
        var placements = GetPlacementLocations(points, 
            boundaryRect: boundingRect, 
            footprint: BuildingFootprint, 
            desiredCount: DesiredBuildingCount, 
            // minFootprintSpacing: We use the footprint again because we don't want buildings right on top of each other.
            minFootprintSpacing: BuildingFootprint);
        GD.Print($"{Name}[{GetType()}] found {placements.Count} viable placements matching criteria.");

        // TODO: Place buildings.
        foreach (var point in placements) {
            this.DrawDebugRect(point, new Rect2(point, BuildingFootprint), new Color(1, 0, 0), 5.0f); // only draw for first 5 seconds
        }
    }

    // Given a bounding region in world-coordinates, produce a uniform point cloud that can be further sampled / filtered / enriched
    // to determine viable locations for procedural generation.
    protected List<Vector2> PointCloudFromBounds(Rect2 bounds, Vector2? inSpacing = null) {
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

    // Removes points from the point cloud which are too close to points along the PathMesh.
    // TODO: Convert this to an IEnumerable<> / predicates so that we can chain operations without copying the point cloud into new
    //       return values constantly.
    protected List<Vector2> RemovePointsNearPath(List<Vector2> pointCloud, PathMesh pathMesh, float additionalPathPadding = 0.0f, float pathStepLength = 20.0f) {
        float minClearance = (pathMesh.PathWidth / 2f) + additionalPathPadding;
        Vector2[] pathPoints = pathMesh.Path.Curve.TessellateEvenLength(toleranceLength: pathStepLength);
        return pointCloud.Where((candidate) => {
            // Any point on the curve that's close to the candidate means the candidate is rejected.
            foreach (var pathPoint in pathPoints) {
                if (candidate.DistanceSquaredTo(pathPoint) < (minClearance*minClearance)) {
                    return false;
                }
            }
            // If no point was too close, candidate is okay.
            return true;
        }).ToList();
    }

    protected List<Vector2> RemovePointsNearRects(List<Vector2> pointCloud, Godot.Collections.Array<RectRegion> rects, float additionalRectPadding = 0.0f) {
        return pointCloud.Where((candidate) => {
            foreach(var rect in rects) {
                if(rect.Region.Grow(additionalRectPadding).HasPoint(candidate)) {
                    return false;
                }
            }
            return true;
        }).ToList();
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
