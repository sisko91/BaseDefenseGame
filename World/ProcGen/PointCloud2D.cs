using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Gurdy.ProcGen
{
    // PointFilters are predicate functions that operate on a single point at a time in a point cloud.
    public delegate bool PointFilter(PointCloud2D cloud, Vector2 point);

    // PointFilterWithWeights is a PointFilter which operates over each point and also considers its computed weight value.
    // The weight is stored within the Z value of each Vector3 point provided.
    public delegate bool PointFilterWithWeights(PointCloud2D cloud, Vector3 point);

    // PointTransforms are mutator functions that operate on a single point at a time in a point cloud and produce a new point value.
    public delegate Vector2 PointTransform(PointCloud2D cloud, Vector2 point);

    // PointTransformWithWeights is a PointTransform which operates over each point and also considers its computed weight value.
    // The weight is stored within the Z value of each Vector3 point provided. The transformed point this function returns likewise
    // records its new weight value in the Z axis.
    public delegate Vector3 PointTransformWithWeights(PointCloud2D cloud, Vector3 point);

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
                if(Points != null) {
                    GD.PushWarning("Replacing existing 3D point data with 2D point source, existing weights will be lost.");
                }
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

        // Runs the specified 2D point filter over each point in the cloud and retains points which pass the filter (it returns true).
        // Note: The weights assigned to each point remain untouched by this operation.
        public PointCloud2D FilterIn(PointFilter filter) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points (ignoring weights).
            newPointCloud.Points = Points.Where((p) => filter(newPointCloud, new Vector2(p.X, p.Y)));
            return newPointCloud;
        }

        // Runs the specified weighted point filter over each point in the cloud and retains points which pass the filter (it returns true).
        // Note: The weights assigned to each point remain untouched by this operation.
        public PointCloud2D FilterIn(PointFilterWithWeights filter) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points.
            newPointCloud.Points = Points.Where((p) => filter(newPointCloud, p));
            return newPointCloud;
        }

        // Runs the specified 2D point filter over each point in the cloud and removes points which pass the filter (it returns true).
        // Note: The weights assigned to each point remain untouched by this operation.
        public PointCloud2D FilterOut(PointFilter filter) {
            // Invert the predicate for In.
            return FilterIn((pc,p) => !filter(pc,p));
        }

        // Runs the specified weighted point filter over each point in the cloud and removes points which pass the filter (it returns true).
        // Note: The weights assigned to each point remain untouched by this operation.
        public PointCloud2D FilterOut(PointFilterWithWeights filter) {
            // Invert the predicate for In.
            return FilterIn((pc, p) => !filter(pc, p));
        }

        public PointCloud2D Transform(PointTransform transformer) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points (ignoring weights).
            newPointCloud.Points = null; // avoid a warning about reassigning points from point2D and losing weight data.
            newPointCloud.Points2D = Points.Select((p) => transformer(newPointCloud, new Vector2(p.X, p.Y)));
            return newPointCloud;
        }

        public PointCloud2D Transform(PointTransformWithWeights transformer) {
            // Copy PointCloud (doesn't copy Points).
            var newPointCloud = this;
            // Process new cloud's points as a filter over the original's points (ignoring weights).
            newPointCloud.Points = Points.Select((p) => transformer(newPointCloud, p));
            return newPointCloud;
        }
    };
}

