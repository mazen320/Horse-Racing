using Unity.Cinemachine;
using UnityEngine;

namespace HorseRacing.Race
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [ExecuteAlways]
    public sealed class RaceCameraTarget : MonoBehaviour
    {
        [Header("Per-player references")]
        [SerializeField] Transform positionAnchor;
        [SerializeField] Transform headingSource;
        [SerializeField] RaceSplineTapDriver speedSource;
        [SerializeField] CinemachineCamera[] controlledCameras;

        [Header("Turn presentation")]
        [SerializeField, Min(0.001f)] float yawSmoothTime = 0.08f;
        [SerializeField, Range(0f, 20f)] float maxYawLagDegrees = 8f;
        [SerializeField, Range(10f, 180f)] float snapAngleDegrees = 45f;

        [Header("Premium chase lens")]
        [SerializeField, Range(35f, 60f)] float baseFieldOfView = 42.5f;
        [SerializeField, Range(35f, 60f)] float sprintFieldOfView = 44.5f;
        [SerializeField, Min(0.05f)] float fieldOfViewSmoothTime = 0.45f;

        Transform[] _originalTargets;
        float[] _originalFieldOfViews;
        float _yaw;
        float _yawVelocity;
        float _fieldOfView;
        float _fieldOfViewVelocity;
        bool _initialized;
        bool _ownsTargets;

        public Transform PositionAnchor => positionAnchor;
        public Transform HeadingSource => headingSource;
        public RaceSplineTapDriver SpeedSource => speedSource;
        public float MaxYawLagDegrees => maxYawLagDegrees;
        public float BaseFieldOfView => baseFieldOfView;
        public float SprintFieldOfView => sprintFieldOfView;
        public float YawErrorDegrees => headingSource
            ? Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,
                headingSource.eulerAngles.y))
            : 0f;

        void OnValidate()
        {
            yawSmoothTime = Mathf.Max(0.001f, yawSmoothTime);
            maxYawLagDegrees = Mathf.Clamp(maxYawLagDegrees, 0f, 20f);
            snapAngleDegrees = Mathf.Clamp(snapAngleDegrees,
                Mathf.Max(10f, maxYawLagDegrees), 180f);
            baseFieldOfView = Mathf.Clamp(baseFieldOfView, 35f, 60f);
            sprintFieldOfView = Mathf.Clamp(sprintFieldOfView,
                baseFieldOfView, 60f);
            fieldOfViewSmoothTime = Mathf.Max(0.05f, fieldOfViewSmoothTime);
        }

        void OnEnable()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            if (Application.isPlaying)
                BeginPlayMode();
            else
                SnapBehindSubject();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                SnapBehindSubject();
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            ApplyPose(deltaTime, false);
            ApplyLens(deltaTime, false);
        }

        void BeginPlayMode()
        {
            _originalTargets = new Transform[controlledCameras.Length];
            _originalFieldOfViews = new float[controlledCameras.Length];
            SnapBehindSubject();
            for (var i = 0; i < controlledCameras.Length; i++)
            {
                _originalTargets[i] = controlledCameras[i].Target.TrackingTarget;
                _originalFieldOfViews[i] = controlledCameras[i].Lens.FieldOfView;
                controlledCameras[i].Target.TrackingTarget = transform;
            }
            ApplyLens(0f, true);
            _ownsTargets = true;
        }

        public void SnapBehindSubject() => ApplyPose(0f, true);

        void ApplyPose(float deltaTime, bool forceSnap)
        {
            if (!positionAnchor || !headingSource) return;
            var targetYaw = headingSource.eulerAngles.y;
            _yaw = RaceCameraHeadingModel.StepYaw(_yaw, targetYaw,
                ref _yawVelocity, deltaTime, yawSmoothTime,
                maxYawLagDegrees, snapAngleDegrees,
                forceSnap || !_initialized);
            transform.SetPositionAndRotation(positionAnchor.position,
                Quaternion.Euler(0f, _yaw, 0f));
            _initialized = true;
        }

        void ApplyLens(float deltaTime, bool forceSnap)
        {
            if (!speedSource || controlledCameras == null) return;

            var speedBlend = Mathf.InverseLerp(
                speedSource.gallopMetersPerSecond,
                speedSource.sprintMetersPerSecond,
                speedSource.TravelSpeed);
            speedBlend = Mathf.SmoothStep(0f, 1f, speedBlend);
            var targetFieldOfView = Mathf.Lerp(baseFieldOfView,
                sprintFieldOfView, speedBlend);

            if (forceSnap)
            {
                _fieldOfView = targetFieldOfView;
                _fieldOfViewVelocity = 0f;
            }
            else
            {
                _fieldOfView = Mathf.SmoothDamp(_fieldOfView,
                    targetFieldOfView, ref _fieldOfViewVelocity,
                    fieldOfViewSmoothTime, Mathf.Infinity, deltaTime);
            }

            for (var i = 0; i < controlledCameras.Length; i++)
            {
                var camera = controlledCameras[i];
                if (!camera) continue;
                var lens = camera.Lens;
                lens.FieldOfView = _fieldOfView;
                camera.Lens = lens;
            }
        }

        bool ValidateConfiguration()
        {
            if (!positionAnchor || !headingSource || !speedSource || controlledCameras == null ||
                controlledCameras.Length == 0)
            {
                Debug.LogError($"{nameof(RaceCameraTarget)} on {name} is missing per-player references.", this);
                return false;
            }

            for (var i = 0; i < controlledCameras.Length; i++)
            {
                if (controlledCameras[i]) continue;
                Debug.LogError($"{nameof(RaceCameraTarget)} on {name} has an empty camera reference.", this);
                return false;
            }
            return true;
        }

        void OnDisable()
        {
            if (!Application.isPlaying || !_ownsTargets || _originalTargets == null) return;
            for (var i = 0; i < controlledCameras.Length; i++)
            {
                if (controlledCameras[i] &&
                    controlledCameras[i].Target.TrackingTarget == transform)
                    controlledCameras[i].Target.TrackingTarget = _originalTargets[i];

                if (controlledCameras[i] && _originalFieldOfViews != null &&
                    i < _originalFieldOfViews.Length)
                {
                    var lens = controlledCameras[i].Lens;
                    lens.FieldOfView = _originalFieldOfViews[i];
                    controlledCameras[i].Lens = lens;
                }
            }
            _ownsTargets = false;
            _initialized = false;
            _yawVelocity = 0f;
            _fieldOfViewVelocity = 0f;
        }
    }
}
