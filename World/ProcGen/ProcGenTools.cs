using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Gurdy.ProcGen
{
    public static class Extensions
    {
        // Given a bounding region in world-coordinates, produce a uniform point cloud that can be further sampled / filtered / enriched
        // to determine viable locations for procedural generation.
        //
        // The returned IEnumerable is lazy-initialized as needed. Snapshot the result by calling `.ToList()` or similar to ensure the point cloud is only generated when
        // necessarry.
        public static IEnumerable<Vector2> GeneratePoints(Rect2 bounds, float spacing = 1f) {
            float startX = bounds.Position.X;
            float startY = bounds.Position.Y;
            float endX = startX + bounds.Size.X;
            float endY = startY + bounds.Size.Y;

            for (float x = startX; x < endX; x += spacing) {
                for (float y = startY; y < endY; y += spacing) {
                    yield return new Vector2(x, y);
                }
            }
        }

        // Produce a set of points, uniformly distributed across the bounds of the world for use during ProcGen routines.
        public static PointCloud2D GeneratePoints(this World worldNode, float spacing = 1f) {
            return new PointCloud2D(new Rect2(worldNode.GlobalPosition - worldNode.RegionBounds / 2f, worldNode.RegionBounds), spacing);
        }
    }

    public static class Filters {
        // Used to combine multiple point filter tests for a single PointCloud.FilterIn/Out call.
        public static PointFilter MatchAll(params PointFilter[] filters) {
            return (pc, p) => filters.All(filter => filter(pc, p));
        }

        // Used to combine multiple point filter tests for a single PointCloud.FilterIn/Out call.
        public static PointFilterWithWeights MatchAll(params PointFilterWithWeights[] filters) {
            return (pc, p) => filters.All(filter => filter(pc, p));
        }

        // Used to combine multiple point filter tests for a single PointCloud.FilterIn/Out call.
        public static PointFilter MatchAny(params PointFilter[] filters) {
            return (pc, p) => filters.Any(filter => filter(pc, p));
        }

        // Used to combine multiple point filter tests for a single PointCloud.FilterIn/Out call.
        public static PointFilterWithWeights MatchAny(params PointFilterWithWeights[] filters) {
            return (pc, p) => filters.Any(filter => filter(pc, p));
        }

        // Wraps a filter function with a callback action to be invoked for each filtering result as the point cloud is iterated.
        // Use with caution, this is mostly good for logging and other idempotent / lightweight operations that are PROBABLY only relevant to development of the game.
        // TODO: Consider compiling this out of a "production" build using #ifdef/#endif.
        public static PointFilter WithCallback(this PointFilter filter, Action<PointCloud2D, Vector2, bool> callbackAction) {
            return (cp, p) => {
                var result = filter(cp, p);
                callbackAction(cp, p, result);
                return result;
            };
        }

        // Wraps a filter function with a callback action to be invoked for each filtering result as the point cloud is iterated.
        // Use with caution, this is mostly good for logging and other idempotent / lightweight operations that are PROBABLY only relevant to development of the game.
        // TODO: Consider compiling this out of a "production" build using #ifdef/#endif.
        public static PointFilterWithWeights WithCallback(this PointFilterWithWeights filter, Action<PointCloud2D, Vector3, bool> callbackAction) {
            return (cp, p) => {
                var result = filter(cp, p);
                callbackAction(cp, p, result);
                return result;
            };
        }

        // Convenience variant of FilterWithCallback taking a callback action that does not accept the point cloud reference.
        public static PointFilter WithCallback(this PointFilter filter, Action<Vector2, bool> callbackAction) {
            return (cp, p) => {
                var result = filter(cp, p);
                callbackAction(p, result);
                return result;
            };
        }

        // Convenience variant of FilterWithCallback taking a callback action that does not accept the point cloud reference.
        public static PointFilterWithWeights WithCallback(this PointFilterWithWeights filter, Action<Vector3, bool> callbackAction) {
            return (cp, p) => {
                var result = filter(cp, p);
                callbackAction(p, result);
                return result;
            };
        }

        // Inverts this filter to return true when it would return false and vice versa.
        public static PointFilter Inverted(this PointFilter filter) {
            return (cp, p) => { return !filter(cp, p); };
        }

        // Inverts this filter to return true when it would return false and vice versa.
        public static PointFilterWithWeights Inverted(this PointFilterWithWeights filter) {
            return (cp, p) => { return !filter(cp, p); };
        }

        // Evaluates points within the point cloud based on being contained within the specified RectRegion(s).
        //
        // Note: This function processes points according to their configured PointSize within the point cloud;
        //       Points will be anchored according to how AnchorPointAtCenter is configured.
        //
        // If additionalPointSkirt > 0, each point will be grown by this amount on each side as it is expanded
        // to PointSize during tests. This is a bit subtle but even if the AnchorPointAtCenter is false, and the
        // point's origin is its top-left corner, this skirt is *still* added (with negative position values) to
        // the left and above the point's top-left origin. E.g.:
        //
        //   sssssssssss
        //   s*        s    <= '*' is the point's position (and origin, if AnchorPointAtCenter==false).
        //   s         s    <= 'blank' is the rect defined by PointSize (from origin, regardless of anchor).
        //   sssssssssss    <= 's' is the skirt grown around the point/rect.
        //
        public static PointFilter OverlapsAnyRectRegion(IEnumerable<RectRegion> rectRegions, float additionalPointSkirt = 0.0f) {
            return (pointCloud, candidate) => {
                var testRect = new Rect2(candidate, pointCloud.PointSize).Grow(additionalPointSkirt);
                if (pointCloud.AnchorPointAtCenter) {
                    testRect.Position -= pointCloud.PointSize / 2f;
                }
                return rectRegions.Any(region => region.GetGlobalRect().Intersects(testRect));
            };
        }

        // Evaluates points within the point cloud based on overlapping a PathMesh instance in the same world.
        //
        // Note: This function processes points according to their configured PointSize within the point cloud;
        //       Points will be anchored according to how AnchorPointAtCenter is configured.
        //
        // If additionalPointSkirt > 0, each point will be grown by this amount on each side as it is expanded
        // to PointSize during tests. This is a bit subtle but even if the AnchorPointAtCenter is false, and the
        // point's origin is its top-left corner, this skirt is *still* added (with negative position values) to
        // the left and above the point's top-left origin. E.g.:
        //
        //   sssssssssss
        //   s*        s    <= '*' is the point's position (and origin, if AnchorPointAtCenter==false).
        //   s         s    <= 'blank' is the rect defined by PointSize (from origin, regardless of anchor).
        //   sssssssssss    <= 's' is the skirt grown around the point/rect.
        //
        public static PointFilter OverlapsPathMesh(PathMesh pathMesh, float pathStepLength, float additionalPointSkirt = 0.0f) {
            float minClearance = (pathMesh.PathWidth / 2f);

            Vector2[] pathPoints = pathMesh.Path.Curve.TessellateEvenLength(toleranceLength: pathStepLength);
            return (pointCloud, candidate) => {
                // Any point on the curve that's close to the candidate means the candidate is rejected.
                var testRect = new Rect2(candidate, pointCloud.PointSize).Grow(minClearance + additionalPointSkirt);
                if(pointCloud.AnchorPointAtCenter) {
                    testRect.Position -= pointCloud.PointSize / 2f;
                }

                return pathPoints.Any(pathPoint => testRect.HasPoint(pathPoint));
            };
        }

        // Evaluates points within the point cloud based on being fully contained within the specified boundary region.
        //
        // Note: This function processes points according to their configured PointSize within the point cloud;
        //       Points will be anchored according to how AnchorPointAtCenter is configured.
        public static PointFilter WithinBounds(Rect2 bounds, float additionalPointSkirt = 0.0f) {
            return (pointCloud, candidate) => {
                var testRect = new Rect2(candidate, pointCloud.PointSize).Grow(additionalPointSkirt);
                if (pointCloud.AnchorPointAtCenter) {
                    testRect.Position -= pointCloud.PointSize / 2f;
                }
                return bounds.Encloses(testRect);
            };
        } 
    }
}

