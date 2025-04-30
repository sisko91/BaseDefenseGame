using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Gurdy.ProcGen
{
    // PointFilters are predicate functions that operate on a single point at a time in a point cloud.
    public delegate bool PointFilter(PointCloud2D cloud, Vector2 point);

    // PointFilterWithWeights is a PointFilter which accepts a third parameter containing the most recent weight assigned to each point.
    public delegate bool PointFilterWithWeights(PointCloud2D cloud, Vector2 point, float pointWeight);

    // A collection of 2d points that provides various operations to process those points into more meaningful data.
    // The point cloud stores points with amortized state as much as possible, keeping the points themselves minimal and fast to process.
    public struct PointCloud2D
    {
        // The actual points contained within the point cloud. Each point is a Vector3 but records a Vector2 within (X,Y) and holds a
        // per-point weight value as Z.
        // Since this is an enumerable there is no guarantee that the points are cached in memory prior to enumerating them.
        public IEnumerable<Vector3> Points;

        // Access the PointCloud's points as pure Vector2 values without weights attached. Use Points if you need access to weight data.
        // Note: Assigning this value will zero all existing weights associated with points in the point cloud. Assign directly to Points
        //       in order to specify non-zero weight data.
        public IEnumerable<Vector2> Points2D {
            get {
                return Points.Select((p) => new Vector2(p.X, p.Y));
            }
            set {
                // When assigned from a list of Vector2 values we zero the weights on each point.
                Points = value.Select((p) => new Vector3(p.X, p.Y, 0));
            }
        }

        // The configured size of each point, stored on the cloud as all points share a consistent size for the same operation.
        public Vector2 PointSize;

        // When point size > 1, this determines whether the point's size extends from a central origin or from the top-left corner.
        public bool AnchorPointAtCenter;

        public PointCloud2D(Rect2 bounds, float spacing = 1f, Vector2? pointSize = null, bool anchorPointAtCenter = false) {
            PointSize = pointSize ?? Vector2.One;
            AnchorPointAtCenter = anchorPointAtCenter;
            Points2D = Extensions.GeneratePoints(bounds, spacing);
        }

        public PointCloud2D FilterIn(PointFilter filter) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points (ignoring weights).
            newPointCloud.Points = Points.Where((p) => filter(newPointCloud, new Vector2(p.X, p.Y)));
            return newPointCloud;
        }

        public PointCloud2D FilterIn(PointFilterWithWeights filter) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points + weights.
            newPointCloud.Points = Points.Where((p) => filter(newPointCloud, new Vector2(p.X, p.Y), p.Z));
            return newPointCloud;
        }

        public PointCloud2D FilterOut(PointFilter filter) {
            // Invert the predicate for In.
            return FilterIn((pc,p) => !filter(pc,p));
        }

        public PointCloud2D FilterOut(PointFilterWithWeights filter) {
            // Invert the predicate for In.
            return FilterIn((pc, p, pw) => !filter(pc, p, pw));
        }
    };
}

