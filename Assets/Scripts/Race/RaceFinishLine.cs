using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace HorseRacing.Race
{
    /// <summary>
    /// The winning post. Paints a line across the running surface and tells every
    /// driver to finish there, so a race ends on a fixed straight instead of back
    /// inside the starting stalls.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class RaceFinishLine : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] SplineContainer spline;
        [Tooltip("Metres along the track from the starting gate. The line snaps itself to the spline here.")]
        [SerializeField, Min(0f)] float metersFromStart = 102.7f;
        [Tooltip("Whole laps the field completes before it reaches the line.")]
        [SerializeField, Min(0)] int lapsBeforeFinish = 1;
        [Tooltip("Sideways shift off the racing line so the paint covers the whole running surface. Negative moves toward the outer rail.")]
        [SerializeField] float offsetFromRacingLine = -9.21f;

        [Header("Paint")]
        [Tooltip("How far the line reaches across the track, in metres.")]
        [SerializeField, Min(1f)] float trackWidth = 27f;
        [Tooltip("Thickness of the painted band along the direction of travel, in metres.")]
        [SerializeField, Min(0.05f)] float lineDepth = 1.1f;
        [Tooltip("Lift above the turf. Enough to beat z-fighting with the track mesh, small enough to read as paint.")]
        [SerializeField, Min(0f)] float heightAboveTrack = 0.02f;
        [Tooltip("Layers treated as ground when sampling the surface height.")]
        [SerializeField] LayerMask surfaceMask = ~0;

        [Header("Race wiring")]
        [Tooltip("Drivers whose finish distance is derived from this line.")]
        [SerializeField] RaceSplineTapDriver[] drivers;

        Mesh _paint;

        public float MetersFromStart => metersFromStart;
        public int LapsBeforeFinish => lapsBeforeFinish;

        void OnEnable()
        {
            EnsurePaintMesh();
            Align();
            ApplyFinishDistances();
        }

        void Start()
        {
            if (Application.isPlaying)
                ApplyFinishDistances();
        }

        void OnValidate()
        {
            EnsurePaintMesh();
            Align();
        }

        /// <summary>
        /// Converts the line's position into a race distance for every driver. Drivers
        /// measure progress as spline arc length from their own grid slot, so each one
        /// gets its own figure even though they all finish on the same line.
        /// </summary>
        [ContextMenu("Apply finish distances to drivers")]
        public void ApplyFinishDistances()
        {
            if (drivers == null || !spline || spline.Spline == null) return;

            // Measured off the container rather than the driver: a driver that has not
            // run yet still reports its placeholder spline length of 1 metre.
            var length = spline.CalculateLength();
            if (length <= 0.01f) return;

            for (var i = 0; i < drivers.Length; i++)
            {
                var driver = drivers[i];
                if (!driver) continue;

                // A running driver owns the authoritative grid slot. In the editor it has
                // not captured one yet, so use the slot the horse is parked in.
                var startT = Application.isPlaying
                    ? driver.StartNormalizedT
                    : NearestT(driver.transform.position);

                var lineT = Mathf.Repeat(metersFromStart / length, 1f);
                var toLine = Mathf.Repeat(lineT - startT, 1f) * length;
                var distance = lapsBeforeFinish * length + toLine;

                if (!driver.raceFullSpline &&
                    Mathf.Abs(driver.raceDistanceMeters - distance) < 0.01f)
                    continue;

                driver.raceFullSpline = false;
                driver.raceDistanceMeters = distance;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(driver);
#endif
            }
        }

        float NearestT(Vector3 worldPosition)
        {
            var local = (float3)spline.transform.InverseTransformPoint(worldPosition);
            SplineUtility.GetNearestPoint(spline.Spline, local, out _, out var t);
            return math.saturate(t);
        }

        void Align()
        {
            if (!spline) return;

            var length = spline.CalculateLength();
            if (length <= 0.01f) return;

            var t = Mathf.Repeat(metersFromStart / length, 1f);
            var centre = (Vector3)spline.EvaluatePosition(t);

            var forward = (Vector3)spline.EvaluateTangent(t);
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            // Matches the lane convention the drivers use, so a lateral offset here
            // reads the same way as their lateralOffsetMeters.
            var right = Vector3.Cross(forward, Vector3.up);
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

            var position = centre + right * offsetFromRacingLine;
            position.y = SampleSurfaceHeight(position, centre.y) + heightAboveTrack;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = new Vector3(trackWidth, 1f, lineDepth);
        }

        float SampleSurfaceHeight(Vector3 position, float fallback)
        {
            var origin = new Vector3(position.x, position.y + 30f, position.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, 120f, surfaceMask)
                ? hit.point.y
                : fallback;
        }

        /// <summary>
        /// Builds the quad flat in the XZ plane with upward normals. Rotating a stock
        /// Quad would leave the paint facing sideways or backwards depending on the
        /// primitive's winding, so the geometry is authored here instead.
        /// </summary>
        void EnsurePaintMesh()
        {
            if (_paint == null)
            {
                _paint = new Mesh
                {
                    name = "FinishLinePaint",
                    hideFlags = HideFlags.DontSave
                };

                _paint.SetVertices(new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(-0.5f, 0f, 0.5f)
                });
                _paint.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                _paint.SetUVs(0, new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                });
                _paint.SetTriangles(new[] { 0, 3, 2, 0, 2, 1 }, 0);
                _paint.RecalculateBounds();
            }

            var filter = GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh != _paint)
                filter.sharedMesh = _paint;
        }
    }
}
