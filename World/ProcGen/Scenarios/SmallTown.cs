using ExtensionMethods;
using Godot;
using Gurdy.ProcGen;
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

    [ExportCategory("Forest")]
    [Export]
    public PackedScene TreeSceneTemplate { get; protected set; } = null;
    [Export]
    public Vector2 TreeFootprint { get; protected set; }

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
    // Set to true to enable the generation of debug information during SmallTown build. This generates a significant number of debug
    // draw calls and should probably be compiled out for a "shipping" build of the game./
    private bool GenerateDebugInfo { get; set; } = true;
    [Export]
    public float DebugPointRadius = 15.0f;
    [Export]
    public Color ViablePointsColor = Colors.Blue;
    [Export]
    public Color NearPathMeshColor = Colors.Orange;
    [Export]
    public Color NearExclusionsColor = Colors.Red;

    private const string DebugDrawCallGroup_Buildings = "SmallTown.Buildings";
    private const string DebugDrawCallGroup_Trees = "SmallTown.Trees";

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
        if (!world.GlobalScale.IsEqualApprox(Vector2.One) || !world.GlobalPosition.IsZeroApprox()) {
            GD.PushError($"SmallTown ProcGen only works with an identity coordinate system. (Current Scale={world.GlobalScale}, Current Position={world.GlobalPosition}");
            return;
        }
        // Produce a set of candidate points, uniformly distributed across the bounds of the world.
        var points = world.GeneratePoints(PointCloudSpacing);

        // Generate a neighborhood of buildings placed around the world, using the initial point cloud as a basis.
        var placedBuildingRects = GenerateNeighborhood(world, points);
        // Place trees throughout the world, using the same initial point cloud as the basis and avoiding any areas where buildings have
        // already been placed by earlier steps.
        GenerateTrees(world, points, placedBuildingRects);
    }

    // Procedurally generates and places a neighborhood of buildings within the world using the provided pointcloud as a basis.
    // Returns a list of (global) Rect2s containing the positions and bounds of any placed buildings.
    protected List<Rect2> GenerateNeighborhood(World world, PointCloud2D points) {
        if (GenerateDebugInfo) {
            DebugNodeExtensions.ClearDebugDrawCallGroup(DebugDrawCallGroup_Buildings);
            // Always start with the debug draw calls disabled, they can be enabled later if/when needed.
            DebugNodeExtensions.DisableDebugDrawCallGroup(DebugDrawCallGroup_Buildings);
        }

        // Our points are shaped and sized according to the footprint of the buildings we're placing.
        points.PointSize = BuildingFootprint;
        // Our BuildingFootprint and BuildingTemplate both assume a center anchor point.
        points.PointTestOffset = BuildingFootprint / 2f;

        // Construct a filter for removing points from the cloud if a building wouldn't fit there while being fully inside the world.
        var worldBoundsFilter = Filters.WithinBounds(new Rect2(world.GlobalPosition - world.RegionBounds / 2f, world.RegionBounds))
                                       .Inverted();

        // Construct a filter for removing points from the cloud if they overlap any of our exclusion regions.
        var excludedRegions = GetTree().GetTypedNodesInGroup<RectRegion>(ExcludedRegionsGroup);
        var excludedRegionsFilter = Filters.OverlapsAnyRectRegion(
            rectRegions: excludedRegions,
            // additionalPointSkirt: Additional spacing between points and the path. This is added to the pointSize to ensure that even
            //                       a building placed as close as "possible" to the path still has at least minDistanceBetween its
            //                       closest edge and the path's boundary.
            additionalPointSkirt: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));

        // Construct a filter for removing points if they overlap the main path (or are too close).
        var mainPathFilter = Filters.OverlapsPathMesh(
            pathMesh: MainPathMesh,
            // pathStepLength: We want this to be the smaller of the two footprint dimensions because this is the distance between 
            //                 consecutive points we are testing along the path's length. If it were larger than any span of the footprint,
            //                 we'd miss filtering out some points.
            pathStepLength: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y),
            // additionalPointSkirt: Same as on the excludedRegionsFilter above.
            additionalPointSkirt: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));

        if (GenerateDebugInfo) {
            // Wrap the filters in callbacks that use different colors so that we can see what points are identified by each.
            excludedRegionsFilter = excludedRegionsFilter.WithCallback((point, filtered) => {
                if (filtered) {
                    this.DrawDebugPoint(point, DebugPointRadius, NearExclusionsColor, group: DebugDrawCallGroup_Buildings);
                }
            });

            mainPathFilter = mainPathFilter.WithCallback((point, filtered) => {
                if (filtered) {
                    this.DrawDebugPoint(point, DebugPointRadius, NearPathMeshColor, group: DebugDrawCallGroup_Buildings);
                }
            });
        }

        // Remove all points that match one of the filters we created above.
        points = points.FilterOut(Filters.MatchAny(worldBoundsFilter, excludedRegionsFilter, mainPathFilter));

        // Weight the remaining points according to how close they are to the path and the center of the world (to keep things close).
        var maxDistanceFromPath = BuildingFootprint.Length() * 3;
        points = points.Transform(WeightPointsForPlacementFunc(maxDistanceFromMainPath: BuildingFootprint.Length() * 3));

        // Reorder the points in the point cloud according to their weight, with higher weights earlier in the sequence.
        points.Points = points.Points.OrderBy(p => 1.0 - p.Z);

        // Determine possible placements for the buildings we want to spawn in the world; Using a custom spacing rule to prevent
        // buildings from spawning too close together.
        var placements = GetPlacementLocations(points,
            boundaryRect: new Rect2(world.GlobalPosition - world.RegionBounds / 2f, world.RegionBounds),
            footprint: BuildingFootprint,
            // minFootprintSpacing: We use the footprint dimensions again because we don't want buildings right on top of each other.
            minFootprintSpacing: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));

        foreach (var point in placements) {
            if (GenerateDebugInfo) {
                this.DrawDebugRect(point, BuildingFootprint, Colors.Green, centerOrigin: true, group: DebugDrawCallGroup_Buildings);
            }
        }
        List<Rect2> placed = [];
        if (BuildingSceneTemplate != null) {
            var parent = PlacementContainer ?? this;
            foreach (var point in placements) {
                var newBuilding = BuildingSceneTemplate.Instantiate<Node2D>();
                parent.AddChild(newBuilding);
                newBuilding.GlobalPosition = point;

                // The points were tested as centerpoints for the building based on PointTestOffset, so the rects we record here
                // for use during tree placement need to be adjusted based on that expectation.
                placed.Add(new Rect2(point-points.PointTestOffset, BuildingFootprint));
                if (placed.Count >= DesiredBuildingCount) {
                    break;
                }
            }
            if (GenerateDebugInfo) {
                GD.Print($"{Name}[{GetType()}] placed {placed.Count} buildings {placements.Count} options matching criteria.");
            }
        }
        return placed;
    }

    protected void GenerateTrees(World world, PointCloud2D points, IEnumerable<Rect2> placedBuildingRects) {
        if (GenerateDebugInfo) {
            DebugNodeExtensions.ClearDebugDrawCallGroup(DebugDrawCallGroup_Trees);
            // Always start with the debug draw calls disabled, they can be enabled later if/when needed.
            DebugNodeExtensions.DisableDebugDrawCallGroup(DebugDrawCallGroup_Trees);
        }

        // Our points are shaped and sized according to the footprint of the trees we're placing.
        points.PointSize = TreeFootprint;
        // Our trees have their origin point centered.
        // TODO: This is defined on the tree scene. Figure out how to determine it once.
        points.PointTestOffset = TreeFootprint/2f;

        // Construct a filter for removing points from the cloud if a point wouldn't fit there while being fully inside the world.
        var worldBoundsFilter = Filters.WithinBounds(new Rect2(world.GlobalPosition - world.RegionBounds / 2f, world.RegionBounds))
                                       .Inverted();

        // Compose a list of all excluded region rects based on 1) excluded RectRegions configured on SmallTown, and 2) any rects for
        // buildings already placed.
        var excludedRegions = GetTree().GetTypedNodesInGroup<RectRegion>(ExcludedRegionsGroup);
        var allExcludedRects = placedBuildingRects.Concat(excludedRegions.Select((regionRect) => regionRect.GetGlobalRect()));
        // Construct a filter for removing points from the cloud if they overlap any of our exclusion rects.
        var excludedRectsFilter = Filters.OverlapsAnyRect(
            rects: allExcludedRects);

        // Construct a filter for removing points if they overlap the main path (or are too close).
        var mainPathFilter = Filters.OverlapsPathMesh(
            pathMesh: MainPathMesh,
            pathStepLength: TreeFootprint.Length(),
            additionalPointSkirt: TreeFootprint.Length());

        if (GenerateDebugInfo) {
            // Wrap the filters in callbacks that use different colors so that we can see what points are identified by each.
            excludedRectsFilter = excludedRectsFilter.WithCallback((point, filtered) => {
                if (filtered) {
                    this.DrawDebugPoint(point, DebugPointRadius, NearExclusionsColor, group: DebugDrawCallGroup_Trees);
                }
            });

            mainPathFilter = mainPathFilter.WithCallback((point, filtered) => {
                if (filtered) {
                    this.DrawDebugPoint(point, DebugPointRadius, NearPathMeshColor, group: DebugDrawCallGroup_Trees);
                }
            });
        }

        // Transform (translate) candidate tree positions by a random amount to add variety.
        points = points.Transform((pc, p) => { return p + TreeFootprint * new Vector2(GD.RandRange(-1, 1), GD.RandRange(-1, 1)); });

        // Filter out points where the tree would be outside of the world.
        points = points.FilterOut(Filters.MatchAny(worldBoundsFilter, excludedRectsFilter, mainPathFilter));

        // Determine possible placements for the trees we want to spawn in the world; Using a custom spacing rule to prevent
        // trees from spawning too close together.
        var placements = GetPlacementLocations(points,
            boundaryRect: new Rect2(world.GlobalPosition - world.RegionBounds / 2f, world.RegionBounds),
            footprint: TreeFootprint,
            // minFootprintSpacing: We use the footprint dimensions again because we don't want buildings right on top of each other.
            minFootprintSpacing: Mathf.Min(TreeFootprint.X, TreeFootprint.Y)/8f);

        List<Rect2> placed = [];
        if (TreeSceneTemplate != null) {
            var parent = PlacementContainer ?? this;
            foreach (var point in placements) {
                var tree = TreeSceneTemplate.Instantiate<Node2D>();
                parent.AddChild(tree);
                tree.GlobalPosition = point;

                placed.Add(new Rect2(point, TreeFootprint));

                if (GenerateDebugInfo) {
                    this.DrawDebugRect(point, TreeFootprint, Colors.HotPink, group: DebugDrawCallGroup_Trees, centerOrigin: true);
                }
            }
        }

    }

    // Returns a PointTransform function that can be used to weight all points in the point cloud based on criteria for placing buildings:
    // 1. How close a point is to the main path (not too close, not too far) to ensure buildings spawn along the path.
    // 2. How close a point is to the origin of the world (not too far) to ensure that buildings spawn closer to where the player is.
    // Both weights 1 & 2 are multiplied together to produce a final weight.
    protected PointTransformWithWeights WeightPointsForPlacementFunc(float maxDistanceFromMainPath) {
        var minDistanceFromPath = MainPathMesh.PathWidth / 2f;
        var world = this.GetGameWorld();
        float maxDistanceFromOrigin = world.RegionBounds.Length() / 2f;
        Vector2[] pathPoints = MainPathMesh.Path.Curve.TessellateEvenLength(toleranceLength: 100.0f);
        var nonViablePointsColor = new Color(ViablePointsColor, 0.1f);
        return (PointCloud2D pc, Vector3 p) => {
            Vector2 point2D = new Vector2(p.X, p.Y);
            Vector2 closestPathPoint = pathPoints.OrderBy((pathPoint) => {
                return pathPoint.DistanceSquaredTo(point2D);
            }).First();

            float distanceToPath = closestPathPoint.DistanceTo(point2D);
            float pathWeight = Mathf.Clamp(1.0f - ((distanceToPath - minDistanceFromPath) / (maxDistanceFromMainPath - minDistanceFromPath)), 0, 1);

            float distanceToOrigin = point2D.DistanceTo(world.GlobalPosition);
            float originWeight = Mathf.Clamp(1.0f - (distanceToOrigin / maxDistanceFromOrigin), 0, 1);
            p.Z = pathWeight * originWeight;

            if (GenerateDebugInfo) {
                this.DrawDebugPoint(point2D, radius: DebugPointRadius, 
                    color: ViablePointsColor.Lerp(nonViablePointsColor, 1.0f - p.Z),
                    group: DebugDrawCallGroup_Buildings);
            }
            return p;
        };
    }

    // Given a point cloud and bounding rectangle, attempts to select points where a rectangle with the indicated footprint (size) can be
    // placed the desired number of times without overlapping footprints.
    // Note this does NOT place anything in the world, it only identifies a set of points where rectangular regions can exist without
    // overlap.
    protected List<Vector2> GetPlacementLocations(PointCloud2D pointCloud, Rect2 boundaryRect, Vector2 footprint, float minFootprintSpacing = 0.0f) {
        var placed = new List<Vector2>();
        var count = 0;
        // We iterate 2D points because weights don't matter here. We just use the cloud in the order it was sorted in so far.
        foreach (var weightedPoint in pointCloud.Points) {
            count++;
            var point = weightedPoint.XY();
            // Footprint extends from point in top-left corner. Growing the rect by minFootprintSpacing gives it equal spacing
            // on all sides.
            Rect2 candidateRect = new Rect2(point - pointCloud.PointTestOffset, footprint).Grow(minFootprintSpacing);

            // 1. Check for overlap or proximity (via intersecting padded rectangles)
            bool tooClose = placed.Any(existing => {
                var existingRect = new Rect2(existing, footprint).Grow(minFootprintSpacing);
                return existingRect.Intersects(candidateRect);
            });

            if (tooClose) {
                continue;
            }

            // Valid point
            placed.Add(point);
        }

        if(GenerateDebugInfo) {
            GD.Print($"{Name}[{GetType()}] selected {placed.Count} placements from {count} viable points matching criteria.");
        }
        return placed;
    }
}
