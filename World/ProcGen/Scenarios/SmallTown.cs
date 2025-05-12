using ExtensionMethods;
using Godot;
using Gurdy.ProcGen;
using System.Collections.Generic;
using System.Linq;
using Gurdy;

// SmallTown is a Placeable node which generates a random assortment of buildings along a general path defined for the scenario. The surrounding area is populated by trees and other foliage.
// SmallTown's PlacedFootprint defines the boundary within which ProcGen entities may be generated and placed.
// TODO: Scenario base class?
public partial class SmallTown : Placeable
{
    // The PathMesh defining the main road / path through the town.
    [Export]
    public PathMesh MainPathMesh { get; protected set; } = null;

    // Nothing will be spawned in regions discovered containing this tag.
    [Export] public string GlobalExclusionTag { get; protected set; } = "ProcGen.Exclude.All";

    [ExportCategory("Structures")]
    // The building scene to instantiate along the path.
    [Export]
    public PackedScene BuildingSceneTemplate { get; protected set; } = null;

    // How many buildings to place.
    [Export]
    public int DesiredBuildingCount { get; protected set; } = 10;

    [ExportCategory("Forest")]
    [Export]
    public PackedScene TreeSceneTemplate { get; protected set; } = null;
    
    // The forest will not be spawned in regions discovered containing this tag.
    [Export] public string ForestExclusionTag { get; protected set; } = "ProcGen.Exclude.Forest";

    [ExportCategory("Advanced")]
    // How far apart each point in the initial point cloud must be.
    [Export]
    public float PointCloudSpacing { get; protected set; } = 100.0f;

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

    // A running list of every Placeable this scenario has instantiated within the world (not counting dummy instances used for data extraction).
    protected List<Placeable> AllPlaceables = [];

    // Returns all RectRegions defined on any placeable this scenario created which contain the matching tag. Both PlacedFootprint and SecondaryFootprints are included in the tag check.
    protected IEnumerable<RectRegion> PlaceableRegionsWithTag(string regionTag)
    {
        foreach (var placeable in AllPlaceables)
        {
            if (placeable.PlacedFootprint.Tags.Contains(regionTag))
            {
                yield return placeable.PlacedFootprint;
            }
            foreach (var footprint in placeable.SecondaryFootprints)
            {
                if (footprint.Tags.Contains(regionTag))
                {
                    yield return footprint;
                }
            }
        }
    }

    private const string DebugDrawCallGroup_Buildings = "SmallTown.Buildings";
    private const string DebugDrawCallGroup_Trees = "SmallTown.Trees";
    
    private SimpleProfiler Profiler = null;

    public override void _Ready() {
        base._Ready();

        if (this.GetGameWorld() == null) {
            GD.PrintErr($"No game World instance found in {Name}'s scene tree. Cannot generate scenario.");
            return;
        }
        
        // SmallTown itself is a placeable, and we examine its Placed and Secondary footprints along with everything else.
        AllPlaceables.Add(this);

        // Ensure that generation runs after the world has been initially set up.
        Callable.From(Generate).CallDeferred();
    }

    protected void Generate()
    {
        if (GenerateDebugInfo)
        {
            Profiler = new SimpleProfiler("SmallTown");
        }
        var world = this.GetGameWorld();
        if (!world.GlobalScale.IsEqualApprox(Vector2.One)) {
            GD.PushError($"SmallTown ProcGen only works with an identity world scale. (Current Scale={world.GlobalScale}");
            return;
        }
        // Produce a set of candidate points, uniformly distributed across the bounds of the world.
        Profiler?.BeginBlock("Generate Points");
        var points = new PointCloud2D(PlacedFootprint.GetGlobalRect(), PointCloudSpacing);
        Profiler?.EndBlock();

        // Generate a neighborhood of buildings placed around the world, using the initial point cloud as a basis.
        Profiler?.BeginBlock("Generate Neighborhood");
        GenerateNeighborhood(points);
        Profiler?.EndBlock();

        // Place trees throughout the world, using the same initial point cloud as the basis and avoiding any areas where buildings have
        // already been placed by earlier steps.
        Profiler?.BeginBlock("Generate Forest");
        GenerateTrees(points);
        Profiler?.EndBlock();
        
        if (GenerateDebugInfo)
        {
            GD.Print($"ProcGen Timings for {Name}[{GetType()}]:\n{Profiler?.EndAndReport()}");
        }
    }

    // Procedurally generates and places a neighborhood of buildings within the world using the provided pointcloud as a basis.
    protected void GenerateNeighborhood(PointCloud2D points)
    {
        if (GenerateDebugInfo) {
            DebugNodeExtensions.ClearDebugDrawCallGroup(DebugDrawCallGroup_Buildings);
            // Always start with the debug draw calls disabled, they can be enabled later if/when needed.
            DebugNodeExtensions.DisableDebugDrawCallGroup(DebugDrawCallGroup_Buildings);
        }
        
        // Instantiate a single building from the template so that we can extract information about it.
        var buildingPrefabScene = BuildingSceneTemplate.Instantiate<Placeable>();
        var footprintSize = buildingPrefabScene.PlacedFootprint.Size;

        // Our points are shaped and sized according to the footprint of the buildings we're placing.
        points.PointSize = footprintSize;
        // The building footprint's local position is our offset.
        points.PointTestOffset = -buildingPrefabScene.PlacedFootprint.Position;
        
        Profiler?.BeginBlock("Construct Filters");

        // Construct a filter for removing points from the cloud if a building wouldn't fit there while being fully inside the scenario.
        var scenarioBoundsFilter = Filters.WithinBounds(PlacedFootprint.GetGlobalRect()).Inverted();

        // Construct a filter for removing points from the cloud if they overlap any of our exclusion regions.
        var excludedRegions = PlaceableRegionsWithTag(GlobalExclusionTag);
        var excludedRegionsFilter = Filters.OverlapsAnyRectRegion(
            rectRegions: excludedRegions,
            // additionalPointSkirt: Additional spacing between points and the path. This is added to the pointSize to ensure that even
            //                       a building placed as close as "possible" to the path still has at least minDistanceBetween its
            //                       closest edge and the path's boundary.
            additionalPointSkirt: Mathf.Min(footprintSize.X, footprintSize.Y));

        // Construct a filter for removing points if they overlap the main path (or are too close).
        var mainPathFilter = Filters.OverlapsPathMesh(
            pathMesh: MainPathMesh,
            // pathStepLength: We want this to be the smaller of the two footprint dimensions because this is the distance between 
            //                 consecutive points we are testing along the path's length. If it were larger than any span of the footprint,
            //                 we'd miss filtering out some points.
            pathStepLength: Mathf.Min(footprintSize.X, footprintSize.Y),
            // additionalPointSkirt: Same as on the excludedRegionsFilter above.
            additionalPointSkirt: Mathf.Min(footprintSize.X, footprintSize.Y));

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

        Profiler?.EndBlock();
        Profiler?.BeginBlock("Execute Filters");
        // Remove all points that match one of the filters we created above.
        points = points.FilterOut(Filters.MatchAny(scenarioBoundsFilter, excludedRegionsFilter, mainPathFilter));
        Profiler?.EndBlock();
        
        Profiler?.BeginBlock("Place Buildings");
        // Weight the remaining points according to how close they are to the path and the center of the world (to keep things close).
        points = points.Transform(WeightPointsForPlacementFunc(maxDistanceFromMainPath: footprintSize.Length() * 3));

        // Reorder the points in the point cloud according to their weight, with higher weights earlier in the sequence.
        points.Points = points.Points.OrderBy(p => 1.0 - p.Z);

        // Determine possible placements for the buildings we want to spawn in the world; Using a custom spacing rule to prevent
        // buildings from spawning too close together.
        var placements = GetPlacementLocations(points,
            footprint: footprintSize,
            // minFootprintSpacing: We use the footprint dimensions again because we don't want buildings right on top of each other.
            minFootprintSpacing: Mathf.Min(footprintSize.X, footprintSize.Y));

        foreach (var point in placements) {
            if (GenerateDebugInfo) {
                this.DrawDebugRect(point-points.PointTestOffset, footprintSize, Colors.Green, centerOrigin: false, group: DebugDrawCallGroup_Buildings);
            }
        }
        List<Placeable> placed = [];
        if (BuildingSceneTemplate != null) {
            var parent = PlacementContainer ?? this;
            foreach (var point in placements) {
                var newBuilding = BuildingSceneTemplate.Instantiate<Placeable>();
                parent.AddChild(newBuilding);
                newBuilding.GlobalPosition = point;
                placed.Add(newBuilding);
                if (placed.Count >= DesiredBuildingCount) {
                    break;
                }
            }
            if (GenerateDebugInfo) {
                GD.Print($"{Name}[{GetType()}] placed {placed.Count} buildings {placements.Count} options matching criteria.");
            }
        }
        AllPlaceables.AddRange(placed);
        Profiler?.EndBlock();
    }

    protected void GenerateTrees(PointCloud2D points) {
        if (GenerateDebugInfo) {
            DebugNodeExtensions.ClearDebugDrawCallGroup(DebugDrawCallGroup_Trees);
            // Always start with the debug draw calls disabled, they can be enabled later if/when needed.
            DebugNodeExtensions.DisableDebugDrawCallGroup(DebugDrawCallGroup_Trees);
        }
        
        // Instantiate a single building from the template so that we can extract information about it.
        var treePrefabScene = TreeSceneTemplate.Instantiate<Placeable>();
        var footprintSize = treePrefabScene.PlacedFootprint.Size;

        // Our points are shaped and sized according to the footprint of the trees we're placing.
        points.PointSize = footprintSize;
        points.PointTestOffset = -treePrefabScene.PlacedFootprint.Position;
        
        Profiler?.BeginBlock("Construct Filters");

        // Construct a filter for removing points from the cloud if a point wouldn't fit there while being fully inside the scenario.
        var scenarioBoundsFilter = Filters.WithinBounds(PlacedFootprint.GetGlobalRect()).Inverted();

        // Compose a list of all excluded region rects based on 1) global exclusions within SmallTown, and 2) any regions for Placeables already placed.
        // OPTIMIZATION: From profiling this code, it's important to cache the full List of rects returned by PlaceableRegionsWithTag because the associated calls
        //               look up nodes in the scene which is inefficient to do many times (and this code iterates a lot).
        var globalExcludes = PlaceableRegionsWithTag(GlobalExclusionTag).ToList();
        var forestExcludes = PlaceableRegionsWithTag(ForestExclusionTag).ToList();
        var allExcludedRects = globalExcludes.Concat(forestExcludes).Select(regionRect => regionRect.GetGlobalRect());
        
        // Construct a filter for removing points from the cloud if they overlap any of our exclusion rects.
        var excludedRectsFilter = Filters.OverlapsAnyRect(
            rects: allExcludedRects);

        // Construct a filter for removing points if they overlap the main path (or are too close).
        var mainPathFilter = Filters.OverlapsPathMesh(
            pathMesh: MainPathMesh,
            pathStepLength: footprintSize.Length());

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
        
        Profiler?.EndBlock();

        Profiler?.BeginBlock("Jitter Tree Locations");
        // Transform (translate) candidate tree positions by a random amount to add variety.
        points = points.Transform((pc, p) => { return p + footprintSize * new Vector2(GD.RandRange(-1, 1), GD.RandRange(-1, 1)); });

        Profiler?.EndBlock();

        Profiler?.BeginBlock("Execute Filters");
        // Filter out points where the tree would be outside of the world.
        points = points.FilterOut(Filters.MatchAny(scenarioBoundsFilter, excludedRectsFilter, mainPathFilter));

        Profiler?.EndBlock();
        
        Profiler?.BeginBlock("Place Trees");
        // Determine possible placements for the trees we want to spawn in the world; Using a custom spacing rule to prevent
        // trees from spawning too close together.
        var placements = GetPlacementLocations(points,
            footprint: footprintSize,
            // minFootprintSpacing: We use the footprint dimensions again because we don't want buildings right on top of each other.
            minFootprintSpacing: Mathf.Min(footprintSize.X, footprintSize.Y)/8f);

        List<Placeable> placed = [];
        if (TreeSceneTemplate != null) {
            var parent = PlacementContainer ?? this;
            foreach (var point in placements) {
                var tree = TreeSceneTemplate.Instantiate<Placeable>();
                parent.AddChild(tree);
                tree.GlobalPosition = point;
                placed.Add(tree);

                if (GenerateDebugInfo) {
                    this.DrawDebugRect(point-points.PointTestOffset, footprintSize, Colors.HotPink, group: DebugDrawCallGroup_Trees, centerOrigin: false);
                }
            }
        }
        AllPlaceables.AddRange(placed);
        Profiler?.EndBlock();
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
    protected List<Vector2> GetPlacementLocations(PointCloud2D pointCloud, Vector2 footprint, float minFootprintSpacing = 0.0f) {
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
