using ExtensionMethods;
using Godot;
using Gurdy;
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
    public bool GenerateDebugInfo { get; private set; } = false;
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
        if (!world.GlobalScale.IsEqualApprox(Vector2.One) || !world.GlobalPosition.IsZeroApprox()) {
            GD.PushError($"SmallTown ProcGen only works with an identity coordinate system. (Current Scale={world.GlobalScale}, Current Position={world.GlobalPosition}");
            return;
        }
        // Produce a set of candidate points, uniformly distributed across the bounds of the world.
        var points = world.GeneratePoints(PointCloudSpacing);
        // Our points are shaped and sized according to the footprint of the buildings we're placing.
        points.PointSize = BuildingFootprint;
        // Our BuildingFootprint and BuildingTemplate both assume a top-left anchor point.
        points.AnchorPointAtCenter = false;

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

        if(GenerateDebugInfo) {
            // Wrap the filters in callbacks that use different colors so that we can see what points are identified by each.
            excludedRegionsFilter = excludedRegionsFilter.WithCallback((point, filtered) => {
                if (filtered) {
                    this.DrawDebugPoint(point, DebugPointRadius, NearExclusionsColor);
                }
            });

            mainPathFilter = mainPathFilter.WithCallback((point, filtered) => {
                if(filtered) {
                    this.DrawDebugPoint(point, DebugPointRadius, NearPathMeshColor);
                }
            });
        }

        // Remove all points that match one of the filters we created above.
        points = points.FilterOut(Filters.MatchAny(excludedRegionsFilter, mainPathFilter));
        
        // NOTE: viablePoints is a LIST which causes ALL of the IEnumerable<> calls before this to finally run. No loops actually happen
        // until something converts them to a definitive collection.
        var viablePoints = points.Points2D.ToList();

        // Determine possible placements for the buildings we want to spawn in the world; Using a custom spacing rule to prevent
        // buildings from spawning too close together.
        var placements = GetPlacementLocations(viablePoints, 
            boundaryRect: new Rect2(world.GlobalPosition - world.RegionBounds / 2f, world.RegionBounds),
            footprint: BuildingFootprint, 
            desiredCount: DesiredBuildingCount, 
            // minFootprintSpacing: We use the footprint dimensions again because we don't want buildings right on top of each other.
            minFootprintSpacing: Mathf.Min(BuildingFootprint.X, BuildingFootprint.Y));

        GD.Print($"{Name}[{GetType()}] selected {placements.Count} placements from {viablePoints.Count} viable points matching criteria.");

        if (GenerateDebugInfo) {
            foreach (var point in viablePoints) {
                this.DrawDebugPoint(point, DebugPointRadius, ViablePointsColor);
            }
        }

        foreach (var point in placements) {
            if(GenerateDebugInfo) {
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

    // Given a point cloud and bounding rectangle, attempts to select points where a rectangle with the indicated footprint (size) can be
    // placed the desired number of times without overlapping footprints.
    // Note this does NOT place anything in the world, it only identifies a set of points where rectangular regions can exist without
    // overlap.
    protected List<Vector2> GetPlacementLocations(IEnumerable<Vector2> pointCloud, Rect2 boundaryRect, Vector2 footprint, 
        int desiredCount, float minFootprintSpacing = 0.0f) {

        var placed = new List<Vector2>();
        var shuffledPoints = pointCloud.OrderBy(_ => GD.Randf());

        foreach (var point in shuffledPoints) {
            // Footprint extends from point in top-left corner. Growing the rect by minFootprintSpacing gives it equal spacing
            // on all sides.
            Rect2 candidateRect = new Rect2(point, footprint).Grow(minFootprintSpacing);

            // 1. Check bounds
            if (!boundaryRect.Encloses(candidateRect)) {
                continue;
            } 

            // 2. Check for overlap or proximity (via intersecting padded rectangles)
            bool tooClose = placed.Any(existing => {
                var existingRect = new Rect2(existing, footprint).Grow(minFootprintSpacing);
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
