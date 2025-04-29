using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gurdy.ProcGen
{
    // PointFilters are predicate functions that operate on a single point at a time in a point cloud.
    public delegate bool PointFilter(PointCloud cloud, Vector2 point);

    // A collection of 2d points that provides various operations to process those points into more meaningful data.
    // The point cloud stores points with amortized state as much as possible, keeping the points themselves minimal and fast to process.
    public struct PointCloud
    {
        // The actual points contained within the point cloud. Since this is an enumerable there is no guarantee that the points are
        // cached in memory prior to enumerating them.
        public IEnumerable<Vector2> Points;

        // The configured size of each point, stored on the cloud as all points share a consistent size for the same operation.
        public Vector2 PointSize;

        // When point size > 1, this determines whether the point's size extends from a central origin or from the top-left corner.
        public bool AnchorPointAtCenter;

        public PointCloud(Rect2 bounds, float spacing = 1f, Vector2? pointSize = null, bool anchorPointAtCenter = false) {
            PointSize = pointSize ?? Vector2.One;
            AnchorPointAtCenter = anchorPointAtCenter;
            Points = Extensions.GeneratePoints(bounds, spacing);
        }

        public PointCloud FilterIn(PointFilter filter) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points.
            newPointCloud.Points = Points.Where((p) => filter(newPointCloud, p));
            return newPointCloud;
        }

        public PointCloud FilterOut(PointFilter filter) {
            // Invert the predicate for In.
            return FilterIn((pc,p) => !filter(pc,p));
        }
    };
}

