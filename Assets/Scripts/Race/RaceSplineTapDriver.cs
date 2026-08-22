using System;
using MalbersAnimations.Controller;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace HorseRacing.Race
{
    /// <summary>
    /// Keyboard taps select a Malbers gait. Native animation root motion supplies
    /// distance, while this component exclusively owns world position and yaw.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class RaceSplineTapDriver : MonoBehaviour
    {
        [Header("Refs")]
        public SplineContainer splineContainer;
        public MAnimal animal;
        public Animator animator;

        [Header("Keyboard tap bindings")]
        [SerializeField] Key[] tapKeys = { Key.Space, Key.W, Key.UpArrow };

        [Header("Tap effort (smooth)")]
        public float tapWindow = 1f;
        public float tapsPerSecondForMax = 4.5f;
        public float accelSmoothTime = 0.55f;
        public float coastSmoothTime = 1.25f;

        [Header("Gait thresholds (effort 0..1) — Malbers Ground: 1 Walk … 4 Gallop")]
        public float walkAt = 0.08f;
        public float trotAt = 0.28f;
        public float canterAt = 0.52f;
        public float gallopAt = 0.78f;
        [SerializeField, Range(0f, 0.2f)] float gaitHysteresis = 0.04f;

        [Header("Look ahead on track")]
        [Tooltip("How far ahead on the spline to look/face (meters).")]
        public float lookAheadMeters = 16f;
        public float turnSmoothTime = 0.32f;

        readonly TapEffortModel _effortModel = new TapEffortModel();
        readonly RootMotionDistanceAccumulator _rootMotion = new RootMotionDistanceAccumulator();
        readonly ConfigurableKeyboardTapInput _keyboardInput = new ConfigurableKeyboardTapInput();

        Rigidbody _rigidbody;
        Spline _spline;
        float _splineLength = 1f;
        float _normalizedT;
        float _yawVelocity;
        int _gait;
        bool _ready;
        bool _ownershipCaptured;

        bool _originalAnimalDisablePosition;
        bool _originalAnimalDisableRotation;
        bool _originalAnimalRootMotion;
        bool _originalRigidbodyKinematic;
        bool _originalRigidbodyUseGravity;
        RigidbodyConstraints _originalRigidbodyConstraints;
        bool _originalAnimatorApplyRootMotion;
        float _originalAnimatorSpeed;
        MalbersAnimations.HAP.Mount[] _mounts;
        MalbersAnimations.Scriptables.BoolReference[] _originalMountInputSettings;

        public float Effort => _effortModel.Effort;

        public void RegisterTap() => _effortModel.RegisterTap(Time.time);

        void Reset()
        {
            animal = GetComponent<MAnimal>();
            animator = GetComponent<Animator>();
        }

        void OnValidate()
        {
            tapWindow = Mathf.Max(0.05f, tapWindow);
            tapsPerSecondForMax = Mathf.Max(0.1f, tapsPerSecondForMax);
            accelSmoothTime = Mathf.Max(0f, accelSmoothTime);
            coastSmoothTime = Mathf.Max(0f, coastSmoothTime);

            walkAt = Mathf.Clamp01(walkAt);
            trotAt = Mathf.Clamp(trotAt, walkAt, 1f);
            canterAt = Mathf.Clamp(canterAt, trotAt, 1f);
            gallopAt = Mathf.Clamp(gallopAt, canterAt, 1f);
            gaitHysteresis = Mathf.Clamp(gaitHysteresis, 0f, 0.2f);

            lookAheadMeters = Mathf.Max(0f, lookAheadMeters);
            turnSmoothTime = Mathf.Max(0.01f, turnSmoothTime);

            if (Application.isPlaying)
                _keyboardInput.SetBindings(tapKeys);
        }

        void Awake()
        {
            if (!animal) animal = GetComponent<MAnimal>();
            if (!animator) animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();

            if (!splineContainer)
            {
                var splineObject = GameObject.Find("RaceTrackSpline");
                if (splineObject) splineContainer = splineObject.GetComponent<SplineContainer>();
            }

            _spline = splineContainer != null ? splineContainer.Spline : null;
            if (!animal || !animator || splineContainer == null || _spline == null || _spline.Count < 2)
            {
                Debug.LogError(
                    "RaceSplineTapDriver requires MAnimal, Animator, and a RaceTrackSpline with at least two knots.",
                    this);
                enabled = false;
                return;
            }

            var calculatedLength = splineContainer.CalculateLength();
            if (calculatedLength <= 0.01f || float.IsNaN(calculatedLength) || float.IsInfinity(calculatedLength))
            {
                Debug.LogError("RaceSplineTapDriver cannot run because RaceTrackSpline has invalid length.", this);
                enabled = false;
                return;
            }

            _splineLength = calculatedLength;
            _keyboardInput.SetBindings(tapKeys);

            CaptureOwnership();
            ConfigureMovementOwnership();
            DisableExternalControllers(transform);
            DisableStamina(transform);
            MuteHorseAudio(transform);

            _effortModel.Reset();
            _rootMotion.Reset();
            _gait = 0;
            _normalizedT = NearestT(transform.position);
            _ready = true;
            ApplyPose(true);
        }

        void CaptureOwnership()
        {
            _originalAnimalDisablePosition = animal.DisablePosition;
            _originalAnimalDisableRotation = animal.DisableRotation;
            _originalAnimalRootMotion = animal.RootMotion;
            _originalAnimatorApplyRootMotion = animator.applyRootMotion;
            _originalAnimatorSpeed = animator.speed;

            if (_rigidbody)
            {
                _originalRigidbodyKinematic = _rigidbody.isKinematic;
                _originalRigidbodyUseGravity = _rigidbody.useGravity;
                _originalRigidbodyConstraints = _rigidbody.constraints;
            }

            _ownershipCaptured = true;
        }

        void ConfigureMovementOwnership()
        {
            var splineAnimate = GetComponent<SplineAnimate>();
            if (splineAnimate) splineAnimate.enabled = false;

            animal.enabled = true;
            animal.RootMotion = true;
            animal.DisablePosition = true;
            animal.DisableRotation = true;
            animal.UseCameraInput = false;
            animal.Strafe = false;
            animal.UseSprint = false;
            animal.CanSprint = false;
            animal.AlwaysForward = false;
            animal.Sprint_Set(false);
            animal.SetAnimatorSpeed(1f);

            animator.applyRootMotion = true;
            animator.speed = 1f;

            if (_rigidbody)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        void DisableExternalControllers(Transform root)
        {
            _mounts = root.GetComponentsInChildren<MalbersAnimations.HAP.Mount>(true);
            _originalMountInputSettings = new MalbersAnimations.Scriptables.BoolReference[_mounts.Length];
            for (var index = 0; index < _mounts.Length; index++)
            {
                _originalMountInputSettings[index] = _mounts[index].Set_InputMount;
                _mounts[index].Set_InputMount = new MalbersAnimations.Scriptables.BoolReference(false);
            }

            foreach (var ai in root.GetComponentsInChildren<MalbersAnimations.Controller.AI.MAnimalAIControl>(true))
                ai.enabled = false;

            foreach (var inputLink in root.GetComponentsInChildren<MalbersAnimations.InputSystem.MInputLink>(true))
                inputLink.enabled = false;

            foreach (var playerInput in root.GetComponentsInChildren<PlayerInput>(true))
            {
                if (!playerInput.enabled) continue;
                try
                {
                    playerInput.enabled = false;
                }
                catch (ArgumentException)
                {
                    // Some third-party rider prefabs have an invalid unpaired-device
                    // counter. Unity has already disabled the Behaviour before this throws.
                }
            }

            foreach (var aim in root.GetComponentsInChildren<MalbersAnimations.Aim>(true))
            {
                aim.Active = false;
                aim.UseCamera = false;
                aim.enabled = false;
            }

            foreach (var lockOn in root.GetComponentsInChildren<MalbersAnimations.Utilities.LockOnTarget>(true))
                lockOn.enabled = false;
        }

        static void DisableStamina(Transform root)
        {
            foreach (var stats in root.GetComponentsInChildren<MalbersAnimations.Stats>(true))
            {
                var stamina = stats.Stat_Get("Stamina");
                if (stamina == null) continue;
                stamina.SetActive(false);
                stamina.SetDegeneration(false);
                stamina.SetRegeneration(false);
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0)
                    child.gameObject.SetActive(false);
            }
        }

        static void MuteHorseAudio(Transform root)
        {
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            {
                source.Stop();
                source.mute = true;
                source.volume = 0f;
                source.enabled = false;
            }

            foreach (var steps in root.GetComponentsInChildren<MalbersAnimations.StepsManager>(true))
                steps.enabled = false;
            foreach (var step in root.GetComponentsInChildren<MalbersAnimations.StepTrigger>(true))
                step.enabled = false;
        }

        void Update()
        {
            if (!_ready) return;

            if (_keyboardInput.WasPressedThisFrame(Keyboard.current)) RegisterTap();

            _effortModel.Tick(Time.time, Time.deltaTime, tapWindow, tapsPerSecondForMax,
                accelSmoothTime, coastSmoothTime);
            _gait = TapEffortModel.SelectGait(_effortModel.Effort, _gait,
                walkAt, trotAt, canterAt, gallopAt, gaitHysteresis);
            DriveGait(_gait);
        }

        void OnAnimatorMove()
        {
            if (_ready && _gait > 0 && animator)
                _rootMotion.Add(animator.deltaPosition);
        }

        void LateUpdate()
        {
            if (!_ready) return;

            var distance = _rootMotion.Consume();
            if (distance > 0.00001f)
                _normalizedT = Mathf.Repeat(_normalizedT + distance / _splineLength, 1f);
            ApplyPose(false);
        }

        void DriveGait(int gait)
        {
            if (!animal) return;

            animal.Grounded = true;
            animal.RootMotion = true;
            animal.UseCameraInput = false;
            animal.Strafe = false;
            animal.UseSprint = false;
            animal.CanSprint = false;
            animal.Sprint_Set(false);

            if (gait <= 0)
            {
                animal.AlwaysForward = false;
                animal.SetInputAxis(Vector3.zero);
                animal.StopMoving();
                if (animal.ActiveState == null || animal.ActiveState.ID.ID != 0)
                    animal.State_Activate(0);
                return;
            }

            animal.AlwaysForward = true;
            animal.SetInputAxis(Vector3.forward);
            if (animal.ActiveState == null || animal.ActiveState.ID.ID != 1)
                animal.State_Activate(1);
            if (animal.CurrentSpeedIndex != gait)
                animal.Speed_CurrentIndex_Set(gait);
        }

        void ApplyPose(bool instant)
        {
            var t = Mathf.Repeat(_normalizedT, 1f);
            transform.position = (Vector3)splineContainer.EvaluatePosition(t);

            var look = LookAlongSpline(t);
            var wantedYaw = Quaternion.LookRotation(look, Vector3.up).eulerAngles.y;
            var yaw = transform.eulerAngles.y;

            if (instant || Mathf.Abs(Mathf.DeltaAngle(yaw, wantedYaw)) > 45f)
            {
                yaw = wantedYaw;
                _yawVelocity = 0f;
            }
            else
            {
                yaw = Mathf.SmoothDampAngle(yaw, wantedYaw, ref _yawVelocity, turnSmoothTime);
            }

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        Vector3 LookAlongSpline(float t)
        {
            var current = TrackForward(t);
            var aheadT = Mathf.Repeat(t + lookAheadMeters / _splineLength, 1f);
            var ahead = TrackForward(aheadT);

            var look = Vector3.Slerp(current, ahead, 0.35f);
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f || Vector3.Dot(look, current) < 0.2f)
                return current;
            return look.normalized;
        }

        Vector3 TrackForward(float t)
        {
            var tangent = (Vector3)splineContainer.EvaluateTangent(Mathf.Repeat(t, 1f));
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.0001f)
                return tangent.normalized;

            const float epsilon = 0.004f;
            var start = (Vector3)splineContainer.EvaluatePosition(Mathf.Repeat(t, 1f));
            var end = (Vector3)splineContainer.EvaluatePosition(Mathf.Repeat(t + epsilon, 1f));
            var fallback = end - start;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        float NearestT(Vector3 worldPosition)
        {
            var localPosition = (float3)splineContainer.transform.InverseTransformPoint(worldPosition);
            SplineUtility.GetNearestPoint(_spline, localPosition, out _, out var t);
            return math.saturate(t);
        }

        void OnDisable()
        {
            _ready = false;
            _gait = 0;
            _effortModel.Reset();
            _rootMotion.Reset();

            if (!_ownershipCaptured) return;

            if (animal)
            {
                animal.SetInputAxis(Vector3.zero);
                animal.StopMoving();
                animal.DisablePosition = _originalAnimalDisablePosition;
                animal.DisableRotation = _originalAnimalDisableRotation;
                animal.RootMotion = _originalAnimalRootMotion;
            }

            if (animator)
            {
                animator.applyRootMotion = _originalAnimatorApplyRootMotion;
                animator.speed = _originalAnimatorSpeed;
            }

            if (_rigidbody)
            {
                _rigidbody.isKinematic = _originalRigidbodyKinematic;
                if (!_originalRigidbodyKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.useGravity = _originalRigidbodyUseGravity;
                _rigidbody.constraints = _originalRigidbodyConstraints;
            }

            if (_mounts != null && _originalMountInputSettings != null)
            {
                var count = Mathf.Min(_mounts.Length, _originalMountInputSettings.Length);
                for (var index = 0; index < count; index++)
                {
                    if (_mounts[index])
                        _mounts[index].Set_InputMount = _originalMountInputSettings[index];
                }
            }

            _ownershipCaptured = false;
        }
    }
}
