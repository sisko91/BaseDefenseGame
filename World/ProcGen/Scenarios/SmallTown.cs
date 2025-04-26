using ExtensionMethods;
using Godot;
using System;
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
    // How far apart each point in the initial point cloud must be.
    [Export]
    public float PointCloudSpacing { get; protected set; } = 100.0f;
    // The group containing RegionRects specifying where the town is not allowed to place objects.
    [Export]
    public string ExcludedRegionsGroup { get; protected set; } = null;

    // Reference to another node in the scene that will serve as the container / parent for any nodes placed by the town's generation.
    // If this is null, all placed nodes will be children of this SmallTown node.
    [Export]
    public Node2D PlacementContainer { get; protected set; } = null;

    [ExportCategory("Debug")]
    // TODO: For now this lives here at compile-time because generation of the town happens before we have a chance to open the options
    //       menu and add config. Eventually we should consider supporting delayed generation / regeneration so that it's easier to
    //       observe / debug.
    [Export]
    public bool RenderDebugInfo { get; private set; } = false;
    [Export]
    public float DebugPointRadius = 15.0f;
    [Export]
    public Color ViablePointsColor = Colors.Blue;
    [Export]
    public Color NearPathMeshColor = Colors.Orange;
    [Export]
    public Color NearExclusionsColor = Colors.Red;

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
        var points = PointCloudFromBounds(boundingRect, PointCloudSpacing);
        int pointSizeInitial = points.Count;
        GD.Print($"{Name}[{GetType()}] generated {pointSizeInitial} candidate points.");

        // Remove points too close to the main path through the world, making sure no building is too close.
        points = RemovePointsNearPath(points, 
            pathMesh: MainPathMesh, 
            // pointSize: We want this to be at least as large as our footprint we plan to eventually place.
            pointSize: BuildingFootprint,
            // pathStepLength: We want this to be the smaller of the two footprint dimensions because this is the distance between 
            //                 consecutive points we are testing along the path's length. If it were larger than any span of the footprint,
            //                 we'd miss filtering out some points.
            pathStepLength: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y),
            // minDistanceBetween: Additional spacing between points and the path. This is added to the pointSize to ensure that even
            // a building placed as close as "possible" to the path still has at least minDistanceBetween its closest edge and the path's
            // boundary.
            minDistanceBetween: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));

        GD.Print($"{Name}[{GetType()}] filtered out {pointSizeInitial - points.Count} candidate points too close to the path.");

        // Remove points near exclusion zones.
        pointSizeInitial = points.Count;
        var excludedRegions = GetTree().GetTypedNodesInGroup<RectRegion>(ExcludedRegionsGroup);
        points = RemovePointsNearRects(
            pointCloud: points, 
            rects: excludedRegions, 
            // pointSize: We want this to be the building footprint so that points are excluded if we couldn't place the building there.
            pointSize: BuildingFootprint,
            // testPointAsCenter: We set this to false because the building's origin is the top-left corner.
            testPointAsCenter: false,
            // minDistanceBetween: We want some spacing between anything placed with this footprint and the exclusion zones.
            // If the building origin is the top-left corner, this moves the origin up and to the left by this amount, and moves the
            // bottom right corner of the footprint down and right by the same amount.
            minDistanceBetween: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));
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

        if (RenderDebugInfo) {
            foreach (var point in points) {
                this.DrawDebugPoint(point, DebugPointRadius, ViablePointsColor);
            }
        }

        foreach (var point in placements) {
            if(RenderDebugInfo) {
                this.DrawDebugRect(point, BuildingFootprint, Colors.Green, centerOrigin: false);
            }
        }
        if(BuildingSceneTemplate != null) {
            int placed = 0;
            var parent = PlacementContainer ?? this;
            foreach (var point in placements) {
                var newBuilding = BuildingSceneTemplate.Instantiate<Node2D>();
                parent.AddChild(newBuilding);
                newBuilding.GlobalPosition = point;

                placed++;
                if(placed >= DesiredBuildingCount) {
                    break;
                }
            }
        }
    }

    // Given a bounding region in world-coordinates, produce a uniform point cloud that can be further sampled / filtered / enriched
    // to determine viable locations for procedural generation.
    protected List<Vector2> PointCloudFromBounds(Rect2 bounds, float spacing = 1f) {
        var points = new List<Vector2>();
        float startX = bounds.Position.X;
        float startY = bounds.Position.Y;
        float endX = startX + bounds.Size.X;
        float endY = startY + bounds.Size.Y;

        for (float x = startX; x < endX; x += spacing) {
            for (float y = startY; y < endY; y += spacing) {
                points.Add(new Vector2(x, y));
            }
        }
        return points;
    }

    // Removes points from the point cloud which are too close to points along the PathMesh.
    // TODO: Convert this to an IEnumerable<> / predicates so that we can chain operations without copying the point cloud into new
    //       return values constantly.
    protected List<Vector2> RemovePointsNearPath(List<Vector2> pointCloud, PathMesh pathMesh, Vector2? pointSize = null, float pathStepLength = 20.0f, float minDistanceBetween = 0.0f) {
        float minClearance = (pathMesh.PathWidth / 2f);

        var testBounds = pointSize ?? Vector2.One;
        Vector2[] pathPoints = pathMesh.Path.Curve.TessellateEvenLength(toleranceLength: pathStepLength);
        return pointCloud.Where((candidate) => {
            // Any point on the curve that's close to the candidate means the candidate is rejected.
            var testRect = new Rect2(candidate, testBounds).Grow(minClearance + minDistanceBetween);
            foreach (var pathPoint in pathPoints) {
                if(testRect.HasPoint(pathPoint)) {
                    if (RenderDebugInfo) {
                        this.DrawDebugPoint(candidate, DebugPointRadius, NearPathMeshColor);
                    }
                    return false;
                }
            }
            // If no point was too close, candidate is okay.
            return true;
        }).ToList();
    }

    protected List<Vector2> RemovePointsNearRects(List<Vector2> pointCloud, IEnumerable<RectRegion> rects, Vector2? pointSize = null, bool testPointAsCenter = false, float minDistanceBetween = 0.0f) {
        if (RenderDebugInfo) {
            foreach (var rect in rects) {
                var globalRect = rect.GetGlobalRect();
                this.DrawDebugRect(globalRect.Position, globalRect.Size, Colors.Yellow, centerOrigin: false);
            }
        }
        var testBounds = pointSize ?? Vector2.One;
        return pointCloud.Where((candidate) => {
            foreach(var rect in rects) {
                var globalRect = rect.GetGlobalRect();
                // We want to "grow" the point from its origin to the pointSize differently depending on how this was called.
                var testRect = new Rect2(candidate, testBounds).Grow(minDistanceBetween);
                if(testPointAsCenter) {
                    testRect.Position -= testBounds / 2f;
                }

                if (globalRect.Intersects(testRect)) {
                    if (RenderDebugInfo) {
                        this.DrawDebugPoint(candidate, DebugPointRadius, NearExclusionsColor);
                    }
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
