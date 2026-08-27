using System;
using System.Collections.Generic;
using MalbersAnimations.Controller;
using MalbersAnimations.HAP;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace HorseRacing.Race
{
    /// <summary>
    /// Keyboard taps select a Malbers gait. Native animation root motion supplies
    /// distance when available; conservative gait speeds support in-place clips.
    /// This component exclusively owns world position and yaw.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class RaceSplineTapDriver : MonoBehaviour
    {
        [Header("Refs")]
        public SplineContainer splineContainer;
        public MAnimal animal;
        public Animator animator;
        public Animator riderAnimator;
        [Tooltip("Optional. When set, this driver mounts/anim-syncs only this rider (needed for multi-horse).")]
        [SerializeField] MRider rider;

        [Header("Keyboard tap bindings")]
        [SerializeField] Key[] tapKeys = { Key.Space, Key.W, Key.UpArrow };

        [Header("Lane / split-screen")]
        [Tooltip("Meters to the track's right (+) / left (−). Same spline progress, parallel lane.")]
        public float lateralOffsetMeters;

        [Header("Short event race")]
        [Tooltip("Finish after one complete lap of the closed presentation spline. Disable to use the custom distance below.")]
        public bool raceFullSpline = true;
        [Tooltip("Distance from the horse's starting point to the event finish. 125 m is roughly a 15-second sprint, while the full presentation spline is over 1.1 km.")]
        [Min(10f)] public float raceDistanceMeters = 125f;
        [Tooltip("Small event boost applied to spline travel without changing gait thresholds. 1.35x keeps sprint motion believable and completes the full 1.14 km lap in roughly 90-100 seconds at sustained effort.")]
        [Range(1f, 5f)] public float courseSpeedMultiplier = 1.35f;
        [Tooltip("Invoked once when the horse reaches the event finish. Connect results UI, lights, or panel feedback here.")]
        public UnityEvent onRaceFinished = new UnityEvent();

        [Header("Tap effort (smooth)")]
        public float tapWindow = 1f;
        public float tapsPerSecondForMax = 4.5f;
        public float accelSmoothTime = 0.55f;
        public float coastSmoothTime = 1.25f;

        [Header("Overdrive — reward pace above the top gait")]
        [Tooltip("Without this a runner tapping well past the top-gait requirement travels no faster than one who only just reaches it.")]
        public bool enableOverdrive = true;
        [Tooltip("Tap rate that earns the full overdrive bonus. Must sit above Taps Per Second For Max.")]
        [Min(0.2f)] public float tapsPerSecondForFullOverdrive = 6f;
        [Tooltip("Track speed multiplier at full overdrive. 1 disables the bonus.")]
        [Range(1f, 2.5f)] public float overdriveSpeedMultiplier = 1.6f;
        [Tooltip("Animation playback multiplier at full overdrive, so the legs keep up with the extra ground speed instead of skating.")]
        [Range(1f, 2f)] public float overdriveAnimationSpeedMultiplier = 1.35f;
        [Tooltip("Share of the overdrive bonus that keeps accruing above the full overdrive rate, with diminishing returns. 0 restores the hard cap, which reads as a speed wall to anyone running faster than that rate.")]
        [Range(0f, 1f)] public float overdriveTailStrength = 0.45f;

        [Header("Finish run-out")]
        [Tooltip("Seconds the horse keeps travelling past the winning post while easing down through the gaits. 0 stops dead on the line.")]
        [Min(0f)] public float finishRunOutSeconds = 3.4f;

        [Header("Gait thresholds (effort 0..1) — Malbers Ground: 1 Walk … 5 Sprint")]
        public float walkAt = 0.08f;
        public float trotAt = 0.28f;
        public float canterAt = 0.52f;
        public float gallopAt = 0.78f;
        public float sprintAt = 0.92f;
        [SerializeField, Range(0f, 0.2f)] float gaitHysteresis = 0.06f;

        [Header("Track travel (meters/second)")]
        [Tooltip("Use animation-authored distance only for clips with clean forward root motion. Keep disabled for the preferred in-place horse.")]
        public bool useAnimationRootMotionDistance;
        [Tooltip("Conservative fallback speeds for horse clips that animate in place.")]
        public float walkMetersPerSecond = 1.6f;
        public float trotMetersPerSecond = 3.2f;
        public float canterMetersPerSecond = 5.2f;
        public float gallopMetersPerSecond = 7.2f;
        [Tooltip("Safe event range. The existing Malbers Sprint clip runs at about 1.1x, matching a target near 9.25 m/s.")]
        [Range(8.5f, GaitTravelSpeedModel.MaximumRecommendedSprintSpeed)]
        public float sprintMetersPerSecond = 8.5f;
        [Tooltip("How quickly track speed catches the selected gait.")]
        public float travelAcceleration = 4.5f;
        [Tooltip("How quickly travel slows after effort drops.")]
        public float travelDeceleration = 3f;

        [Header("Look ahead on track")]
        [Tooltip("How far ahead on the spline to look/face (meters).")]
        public float lookAheadMeters = 16f;
        [Tooltip("How quickly the horse rotates into bends. Lower values prevent visible sideways drift on tighter turns.")]
        public float turnSmoothTime = 0.16f;

        readonly TapEffortModel _effortModel = new TapEffortModel();
        readonly RootMotionDistanceAccumulator _rootMotion = new RootMotionDistanceAccumulator();
        readonly GaitTravelSpeedModel _travelSpeed = new GaitTravelSpeedModel();
        readonly ConfigurableKeyboardTapInput _keyboardInput = new ConfigurableKeyboardTapInput();
        readonly EventRaceProgressModel _raceProgress = new EventRaceProgressModel();

        Rigidbody _rigidbody;
        Spline _spline;
        float _splineLength = 1f;
        float _normalizedT;
        float _startNormalizedT;
        float _yawVelocity;
        int _gait;
        int _animationGait;
        bool _ready;
        bool _raceInputEnabled = true;
        bool _ownershipCaptured;
        bool _runningOut;
        float _runOutTimer;
        float _runOutStartSpeed;
        float _appliedAnimatorSpeed = 1f;

        bool _originalAnimalDisablePosition;
        bool _originalAnimalDisableRotation;
        bool _originalAnimalRootMotion;
        bool _originalAnimalLockForwardMovement;
        bool _originalAnimalLockUpDownMovement;
        bool _originalAnimalUseSprint;
        bool _originalAnimalSprint;
        bool _originalRigidbodyKinematic;
        bool _originalRigidbodyUseGravity;
        RigidbodyConstraints _originalRigidbodyConstraints;
        bool _originalAnimatorApplyRootMotion;
        float _originalAnimatorSpeed;
        AnimatorUpdateMode _originalAnimatorUpdateMode;
        AnimatorUpdateMode _originalRiderAnimatorUpdateMode;
        AnimatorCullingMode _originalAnimatorCullingMode;
        AnimatorCullingMode _originalRiderAnimatorCullingMode;
        MalbersAnimations.HAP.Mount[] _mounts;
        MalbersAnimations.Scriptables.BoolReference[] _originalMountInputSettings;

        readonly List<MalbersAnimations.Stats> _staminaOwners = new List<MalbersAnimations.Stats>();
        readonly List<MalbersAnimations.Stat> _staminaStats = new List<MalbersAnimations.Stat>();
        readonly List<GameObject> _staminaWidgets = new List<GameObject>();
        readonly List<Transform> _staminaScan = new List<Transform>();
        int _staminaHierarchyCount = -1;

        public float Effort => _effortModel.Effort;
        public float TravelSpeed => _travelSpeed.Speed;
        public int RequestedGait => _gait;
        public int AnimationGait => _animationGait;
        public float DistanceTravelled => _raceProgress.DistanceTravelled;
        public float ActiveRaceDistance => raceFullSpline ? _splineLength : raceDistanceMeters;
        public float SplineLength => _splineLength;
        public float StartNormalizedT => _startNormalizedT;
        public float RaceProgress => _raceProgress.Progress(ActiveRaceDistance);
        public bool IsFinished => _raceProgress.IsFinished;
        public bool RaceInputEnabled => _raceInputEnabled;
        public bool IsRunningOut => _runningOut;

        public string GetPrimaryTapKeyLabel()
        {
            if (tapKeys == null || tapKeys.Length == 0 || tapKeys[0] == Key.None)
                return string.Empty;

            return TapKeyParser.Format(tapKeys[0]);
        }

        /// <summary>
        /// Replaces keyboard bindings from plain text. Matching is case-insensitive ("a" == "A").
        /// </summary>
        public bool SetPrimaryTapKey(string keyText)
        {
            var parsed = TapKeyParser.ParseBindings(keyText);
            if (parsed.Length == 0)
                return false;

            tapKeys = parsed;
            _keyboardInput.SetBindings(tapKeys);
            return true;
        }

        public float TapsPerSecond => _effortModel.TapsPerSecond;

        /// <summary>0 at the top-gait tap rate, 1 once the player hits the full overdrive rate.</summary>
        public float Overdrive => Mathf.Clamp01(MeasureOverdrivePace());

        public float EstimatedBestTimeSeconds => ActiveRaceDistance /
            Mathf.Max(0.01f, sprintMetersPerSecond * courseSpeedMultiplier * MaxOverdriveMultiplier);

        /// <summary>
        /// How far past the full overdrive rate the pace estimate still climbs. Pace has
        /// to be tracked beyond that rate or the tail below has nothing left to read.
        /// </summary>
        const float TrackedOverdrivePace = 2.5f;

        /// <summary>
        /// Ceiling on how much the animation is sped up. The legs can only stretch so far
        /// before the clip reads as fast-forwarded film rather than a horse extending.
        /// </summary>
        const float MaxAnimationOverdriveResponse = 1.15f;

        float MaxOverdriveMultiplier => OverdriveTravelMultiplier(TrackedOverdrivePace);

        float OverdriveRateSpan => Mathf.Max(0.01f,
            Mathf.Max(tapsPerSecondForMax + 0.01f, tapsPerSecondForFullOverdrive) -
            Mathf.Max(0.1f, tapsPerSecondForMax));

        float DriveCeiling => enableOverdrive
            ? Mathf.Max(1f, (Mathf.Max(0.1f, tapsPerSecondForMax) +
                             OverdriveRateSpan * TrackedOverdrivePace) /
                            Mathf.Max(0.1f, tapsPerSecondForMax))
            : 1f;

        /// <summary>
        /// Pace above the top-gait rate, measured in full-overdrive spans. 1 is the full
        /// overdrive rate; it deliberately keeps rising past that.
        /// </summary>
        float MeasureOverdrivePace()
        {
            if (!enableOverdrive) return 0f;

            var lower = Mathf.Max(0.1f, tapsPerSecondForMax);
            return Mathf.Max(0f, (_effortModel.TapsPerSecond - lower) / OverdriveRateSpan);
        }

        public void SetRaceInputEnabled(bool enabled)
        {
            _raceInputEnabled = enabled;
            if (enabled) return;

            _effortModel.Reset();

            // A horse pulling up after the post keeps its own momentum. Cutting input
            // here would freeze it mid-stride the instant the results flow starts.
            if (_runningOut) return;

            _gait = 0;
            _animationGait = 0;
            _travelSpeed.Reset();
            DriveGait(0);
        }

        public void RegisterTap()
        {
            if (_ready && _raceInputEnabled && !IsFinished)
                _effortModel.RegisterTap(Time.time);
        }

        void Reset()
        {
            animal = GetComponent<MAnimal>();
            animator = GetComponent<Animator>();
        }

        void OnValidate()
        {
            tapWindow = Mathf.Max(0.05f, tapWindow);
            tapsPerSecondForMax = Mathf.Max(0.1f, tapsPerSecondForMax);
            tapsPerSecondForFullOverdrive = Mathf.Max(
                tapsPerSecondForMax + 0.1f, tapsPerSecondForFullOverdrive);
            overdriveSpeedMultiplier = Mathf.Clamp(overdriveSpeedMultiplier, 1f, 2.5f);
            overdriveAnimationSpeedMultiplier = Mathf.Clamp(overdriveAnimationSpeedMultiplier, 1f, 2f);
            overdriveTailStrength = Mathf.Clamp01(overdriveTailStrength);
            finishRunOutSeconds = Mathf.Max(0f, finishRunOutSeconds);
            accelSmoothTime = Mathf.Max(0f, accelSmoothTime);
            coastSmoothTime = Mathf.Max(0f, coastSmoothTime);
            raceDistanceMeters = Mathf.Max(10f, raceDistanceMeters);
            courseSpeedMultiplier = Mathf.Clamp(courseSpeedMultiplier, 1f, 5f);

            walkAt = Mathf.Clamp01(walkAt);
            trotAt = Mathf.Clamp(trotAt, walkAt, 1f);
            canterAt = Mathf.Clamp(canterAt, trotAt, 1f);
            gallopAt = Mathf.Clamp(gallopAt, canterAt, 1f);
            sprintAt = Mathf.Clamp(sprintAt, gallopAt, 1f);
            gaitHysteresis = Mathf.Clamp(gaitHysteresis, 0f, 0.2f);

            walkMetersPerSecond = Mathf.Max(0f, walkMetersPerSecond);
            trotMetersPerSecond = Mathf.Max(walkMetersPerSecond, trotMetersPerSecond);
            canterMetersPerSecond = Mathf.Max(trotMetersPerSecond, canterMetersPerSecond);
            gallopMetersPerSecond = Mathf.Max(canterMetersPerSecond, gallopMetersPerSecond);
            sprintMetersPerSecond = GaitTravelSpeedModel.ClampSprintSpeed(
                gallopMetersPerSecond, sprintMetersPerSecond);
            travelAcceleration = Mathf.Max(0.01f, travelAcceleration);
            travelDeceleration = Mathf.Max(0.01f, travelDeceleration);

            lookAheadMeters = Mathf.Max(0f, lookAheadMeters);
            turnSmoothTime = Mathf.Max(0.01f, turnSmoothTime);

            if (Application.isPlaying)
                _keyboardInput.SetBindings(tapKeys);
        }

        void Awake()
        {
            if (!animal) animal = GetComponent<MAnimal>();
            if (!animator) animator = GetComponent<Animator>();
            if (!rider) rider = ResolveRider();
            if (!riderAnimator && rider) riderAnimator = rider.Anim;
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
            SuppressStamina();
            MuteHorseAudio(transform);

            _effortModel.Reset();
            _rootMotion.Reset();
            _travelSpeed.Reset();
            _gait = 0;
            _animationGait = 0;
            _normalizedT = NearestT(transform.position);
            _startNormalizedT = _normalizedT;
            _raceProgress.Reset();
            _ready = true;
            ApplyPose(true);
        }

        void CaptureOwnership()
        {
            _originalAnimalDisablePosition = animal.DisablePosition;
            _originalAnimalDisableRotation = animal.DisableRotation;
            _originalAnimalRootMotion = animal.RootMotion;
            _originalAnimalLockForwardMovement = animal.LockForwardMovement;
            _originalAnimalLockUpDownMovement = animal.LockUpDownMovement;
            _originalAnimalUseSprint = animal.UseSprint;
            _originalAnimalSprint = animal.Sprint;
            _originalAnimatorApplyRootMotion = animator.applyRootMotion;
            _originalAnimatorSpeed = animator.speed;
            _originalAnimatorUpdateMode = animator.updateMode;
            _originalAnimatorCullingMode = animator.cullingMode;
            if (riderAnimator)
            {
                _originalRiderAnimatorUpdateMode = riderAnimator.updateMode;
                _originalRiderAnimatorCullingMode = riderAnimator.cullingMode;
            }

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
            animal.LockForwardMovement = false;
            animal.LockUpDownMovement = true;
            animal.UseSprint = false;
            animal.CanSprint = false;
            animal.AlwaysForward = false;
            animal.Sprint_Set(false);
            animal.SetAnimatorSpeed(1f);

            animator.applyRootMotion = true;
            animator.speed = 1f;
            animator.updateMode = AnimatorUpdateMode.Normal;

            // Culled animators stop writing bone transforms, which reads as a hitch
            // when a horse crosses the edge of either split-screen viewport.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (riderAnimator)
            {
                riderAnimator.updateMode = AnimatorUpdateMode.Normal;
                riderAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

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

        /// <summary>
        /// Holds Malbers stamina off so it never throttles the gait mid-race. The
        /// targets are cached because this runs every frame: riders are parented in
        /// after Awake, so the rig is rescanned only when it gains or loses objects.
        /// </summary>
        void SuppressStamina()
        {
            var hierarchy = transform.hierarchyCount;
            if (hierarchy != _staminaHierarchyCount)
                CaptureStaminaTargets(hierarchy);

            for (var i = 0; i < _staminaOwners.Count; i++)
            {
                var owner = _staminaOwners[i];
                if (owner && !owner.enabled) owner.enabled = true;
            }

            for (var i = 0; i < _staminaStats.Count; i++)
            {
                var stamina = _staminaStats[i];
                if (stamina == null) continue;
                stamina.SetActive(false);
                stamina.SetDegeneration(false);
                stamina.SetRegeneration(false);
            }

            for (var i = 0; i < _staminaWidgets.Count; i++)
            {
                var widget = _staminaWidgets[i];
                if (widget && widget.activeSelf) widget.SetActive(false);
            }
        }

        void CaptureStaminaTargets(int hierarchy)
        {
            _staminaHierarchyCount = hierarchy;
            _staminaOwners.Clear();
            _staminaStats.Clear();
            _staminaWidgets.Clear();

            transform.GetComponentsInChildren(true, _staminaOwners);
            for (var i = 0; i < _staminaOwners.Count; i++)
            {
                var owner = _staminaOwners[i];
                if (!owner) continue;
                if (!owner.enabled) owner.enabled = true;

                var stamina = owner.Stat_Get("Stamina");
                if (stamina != null) _staminaStats.Add(stamina);
            }

            _staminaScan.Clear();
            transform.GetComponentsInChildren(true, _staminaScan);
            for (var i = 0; i < _staminaScan.Count; i++)
            {
                var child = _staminaScan[i];
                if (child && child.name.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0)
                    _staminaWidgets.Add(child.gameObject);
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
            if (!_ready || !_raceInputEnabled) return;
            if (IsFinished) return;

            if (_keyboardInput.WasPressedThisFrame(Keyboard.current)) RegisterTap();

            _effortModel.Tick(Time.time, Time.deltaTime, tapWindow, tapsPerSecondForMax,
                accelSmoothTime, coastSmoothTime, DriveCeiling);
            _gait = SelectRequestedGait();
            _animationGait = GaitTravelSpeedModel.SelectAnimationGait(
                _gait, _travelSpeed.Speed, walkMetersPerSecond, trotMetersPerSecond,
                canterMetersPerSecond, gallopMetersPerSecond, sprintMetersPerSecond);
            DriveGait(_animationGait);
        }

        void Start()
        {
            if (!_ready) return;
            animal.Grounded = true;
            EnsureRiderMounted();
            DriveGait(0);
        }

        void EnsureRiderMounted()
        {
            if (!rider) rider = ResolveRider();
            if (rider == null || rider.IsRiding) return;
            rider.Start_Mounted(gameObject);
        }

        MRider ResolveRider()
        {
            if (rider) return rider;

            // Prefer a rider already linked to this horse's Mount.
            var mount = GetComponentInChildren<Mount>(true);
            var riders = FindObjectsByType<MRider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (mount != null)
            {
                foreach (var candidate in riders)
                {
                    if (candidate != null && candidate.Montura == mount)
                        return candidate;
                }
            }

            // Fallback: single-rider scenes keep previous behavior.
            return riders != null && riders.Length == 1 ? riders[0] : null;
        }

        void OnAnimatorMove()
        {
            if (_ready && useAnimationRootMotionDistance && _animationGait > 0 && animator)
                _rootMotion.Add(animator.deltaPosition);
        }

        void LateUpdate()
        {
            if (!_ready) return;

            if (_runningOut)
            {
                TickRunOut(Time.deltaTime);
                ApplyPose(false);
                return;
            }

            // Hold the grid pose while the countdown runs. Without this the animal
            // controller's own gravity settles the horse a little lower each frame and
            // the first race frame snaps it back up to the spline.
            if (!_raceInputEnabled)
            {
                ApplyPose(true);
                return;
            }

            if (IsFinished)
            {
                ApplyPose(false);
                return;
            }

            var deltaTime = Time.deltaTime;
            var nativeDistance = useAnimationRootMotionDistance ? _rootMotion.Consume() : 0f;
            if (!useAnimationRootMotionDistance)
                _rootMotion.Reset();
            var fallbackDistance = _travelSpeed.StepToTarget(CurrentTargetSpeed(), deltaTime,
                travelAcceleration, travelDeceleration);

            // Some horse packs use true root motion, while this preferred horse's
            // clips animate in place. Never combine both sources or travel doubles.
            var distance = nativeDistance > 0.00001f
                ? Mathf.Min(nativeDistance, fallbackDistance)
                : fallbackDistance;
            if (nativeDistance > 0.00001f)
                _travelSpeed.FollowNative(distance, deltaTime);

            var overdrivePace = MeasureOverdrivePace();
            distance *= courseSpeedMultiplier * OverdriveTravelMultiplier(overdrivePace);
            ApplyOverdriveAnimationSpeed(overdrivePace);

            _gait = SelectRequestedGait();
            _animationGait = GaitTravelSpeedModel.SelectAnimationGait(
                _gait, _travelSpeed.Speed, walkMetersPerSecond, trotMetersPerSecond,
                canterMetersPerSecond, gallopMetersPerSecond, sprintMetersPerSecond);
            DriveGait(_animationGait);

            distance = _raceProgress.Advance(distance, ActiveRaceDistance);
            if (distance > 0.00001f)
                _normalizedT = Mathf.Repeat(_normalizedT + distance / _splineLength, 1f);
            ApplyPose(false);

            if (IsFinished)
                CompleteRace();
        }

        /// <summary>
        /// Ground speed the current effort asks for. Reading it from effort rather than
        /// from the selected gait is what keeps every extra tap worth something.
        /// </summary>
        float CurrentTargetSpeed() => GaitTravelSpeedModel.TargetSpeedForEffort(
            _effortModel.Effort, walkAt, trotAt, canterAt, gallopAt, sprintAt,
            walkMetersPerSecond, trotMetersPerSecond, canterMetersPerSecond,
            gallopMetersPerSecond, sprintMetersPerSecond);

        int SelectRequestedGait()
        {
            var gait = TapEffortModel.SelectGait(_effortModel.Effort, _gait,
                walkAt, trotAt, canterAt, gallopAt, sprintAt, gaitHysteresis);

            // Effort inside the hysteresis band commands no ground speed at all, so the
            // legs must not keep cycling in place once the horse has actually stopped.
            if (gait > 0 && _travelSpeed.Speed <= 0.001f && CurrentTargetSpeed() <= 0f)
                gait = 0;

            return gait;
        }

        void CompleteRace()
        {
            BeginRunOut();
            onRaceFinished?.Invoke();
        }

        /// <summary>
        /// Ends this horse's race without a finish of its own. The field is pulled up the
        /// moment the first horse crosses the line, and easing down beats freezing
        /// mid-stride.
        /// </summary>
        public void PullUpAndStopRacing()
        {
            if (!_ready) return;

            if (!IsFinished && !_runningOut)
                BeginRunOut();

            SetRaceInputEnabled(false);
        }

        void BeginRunOut()
        {
            // Speed carried over the line, in track metres per second, so the pull-up
            // starts from whatever pace the player actually finished on.
            _runOutStartSpeed = _travelSpeed.Speed * courseSpeedMultiplier *
                OverdriveTravelMultiplier(MeasureOverdrivePace());
            _runOutTimer = 0f;
            _runningOut = finishRunOutSeconds > 0.01f && _runOutStartSpeed > 0.1f;

            _effortModel.Reset();
            _rootMotion.Reset();

            if (_runningOut) return;

            _gait = 0;
            _animationGait = 0;
            _travelSpeed.Reset();
            DriveGait(0);
        }

        /// <summary>
        /// Carries the horse past the winning post and eases it down through the gaits.
        /// Progress is written straight to the spline because the race distance model is
        /// already full, and the clock has already been stopped on the line.
        /// </summary>
        void TickRunOut(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            _runOutTimer += deltaTime;
            var k = Mathf.Clamp01(_runOutTimer / Mathf.Max(0.01f, finishRunOutSeconds));

            // Squared falloff reads as a rider sitting up and letting the horse coast
            // rather than braking, and it lands on zero exactly at the end of the window.
            var remaining = 1f - k;
            var speed = _runOutStartSpeed * remaining * remaining;

            if (speed > 0.05f)
            {
                var step = speed * deltaTime;
                _normalizedT = Mathf.Repeat(_normalizedT + step / _splineLength, 1f);

                // Pull-up starts before the line for the loser — keep counting race
                // distance so crossing during the ease-down still records a finish time.
                if (!IsFinished)
                {
                    _raceProgress.Advance(step, ActiveRaceDistance);
                    if (IsFinished)
                        onRaceFinished?.Invoke();
                }
            }

            var gaitSpeed = speed / Mathf.Max(0.01f, courseSpeedMultiplier);
            _travelSpeed.FollowNative(gaitSpeed * deltaTime, deltaTime);
            _animationGait = speed <= 0.05f
                ? 0
                : GaitTravelSpeedModel.SelectAnimationGait(0, gaitSpeed,
                    walkMetersPerSecond, trotMetersPerSecond, canterMetersPerSecond,
                    gallopMetersPerSecond, sprintMetersPerSecond);
            _gait = _animationGait;
            ApplyOverdriveAnimationSpeed(0f);
            DriveGait(_animationGait);

            if (k < 1f) return;

            _runningOut = false;
            _gait = 0;
            _animationGait = 0;
            _travelSpeed.Reset();
            DriveGait(0);
        }

        float OverdriveTravelMultiplier(float overdrivePace)
        {
            return enableOverdrive
                ? 1f + OverdriveResponse(overdrivePace) *
                  (Mathf.Max(1f, overdriveSpeedMultiplier) - 1f)
                : 1f;
        }

        /// <summary>
        /// Rises linearly to 1 at the full overdrive rate, then keeps climbing slowly
        /// instead of stopping. A runner already past that rate used to gain nothing at
        /// all for going faster, which is the wall players describe hitting on the panel.
        /// </summary>
        float OverdriveResponse(float overdrivePace)
        {
            if (overdrivePace <= 1f) return Mathf.Max(0f, overdrivePace);
            return 1f + Mathf.Log(overdrivePace) * Mathf.Clamp01(overdriveTailStrength);
        }

        void ApplyOverdriveAnimationSpeed(float overdrivePace)
        {
            if (!animal) return;

            // Only the top two gaits get sped up. Anything slower would look like the
            // horse is scrabbling rather than extending.
            var wanted = enableOverdrive && _animationGait >= 4
                ? 1f + Mathf.Min(OverdriveResponse(overdrivePace), MaxAnimationOverdriveResponse) *
                  (Mathf.Max(1f, overdriveAnimationSpeedMultiplier) - 1f)
                : 1f;

            if (Mathf.Abs(wanted - _appliedAnimatorSpeed) < 0.01f) return;

            _appliedAnimatorSpeed = wanted;
            animal.SetAnimatorSpeed(wanted);
        }

        /// <summary>Returns the horse to its start for the next event player.</summary>
        public void RestartRace()
        {
            if (!_ready) return;

            _normalizedT = _startNormalizedT;
            _yawVelocity = 0f;
            _gait = 0;
            _animationGait = 0;
            _runningOut = false;
            _runOutTimer = 0f;
            _runOutStartSpeed = 0f;
            _effortModel.Reset();
            _rootMotion.Reset();
            _travelSpeed.Reset();
            _raceProgress.Reset();
            _appliedAnimatorSpeed = 1f;
            if (animal) animal.SetAnimatorSpeed(1f);
            ApplyPose(true);
            DriveGait(0);
        }

        void DriveGait(int gait)
        {
            if (!animal) return;

            animal.Grounded = true;
            animal.RootMotion = true;
            animal.UseCameraInput = false;
            animal.Strafe = false;
            animal.LockForwardMovement = false;
            animal.LockUpDownMovement = true;
            animal.UseSprint = false;
            animal.CanSprint = false;
            SuppressStamina();

            if (gait <= 0)
            {
                animal.AlwaysForward = false;
                animal.Sprint_Set(false);
                animal.SetInputAxis(Vector3.zero);
                animal.StopMoving();
                ForceState(0);
                return;
            }

            animal.AlwaysForward = true;
            animal.SetInputAxis(Vector3.forward);
            ForceState(1);

            animal.Sprint_Set(false);
            var speedIndex = Mathf.Clamp(gait >= 5 ? 4 : gait, 1, 4);
            if (animal.CurrentSpeedIndex != speedIndex)
                animal.Speed_CurrentIndex_Set(speedIndex);
        }

        /// <summary>
        /// Forces a Malbers state only once the controller owns a live state. Called
        /// before MAnimal has initialised, State_Force reaches its debug logger with an
        /// unset state and throws, which used to escape callers' Awake and get their
        /// component disabled by Unity.
        /// </summary>
        void ForceState(int stateId)
        {
            if (animal.ActiveState == null) return;
            if (animal.ActiveState.ID != null && animal.ActiveState.ID.ID == stateId) return;

            animal.State_Force(stateId);
        }

        void ApplyPose(bool instant)
        {
            var t = Mathf.Repeat(_normalizedT, 1f);
            var look = LookAlongSpline(t);
            transform.position = EvaluateLanePosition(t, look);

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

        Vector3 EvaluateLanePosition(float t, Vector3 trackForward)
        {
            var center = (Vector3)splineContainer.EvaluatePosition(t);
            if (Mathf.Abs(lateralOffsetMeters) < 0.0001f)
                return center;

            var right = Vector3.Cross(trackForward, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            else
                right.Normalize();

            return center + right * lateralOffsetMeters;
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
            _animationGait = 0;
            _runningOut = false;
            _runOutTimer = 0f;
            _runOutStartSpeed = 0f;
            _appliedAnimatorSpeed = 1f;
            _effortModel.Reset();
            _rootMotion.Reset();
            _travelSpeed.Reset();
            _raceProgress.Reset();

            if (!_ownershipCaptured) return;

            if (animal)
            {
                animal.Sprint_Set(false);
                animal.SetInputAxis(Vector3.zero);
                animal.StopMoving();
                animal.UseSprint = _originalAnimalUseSprint;
                if (_originalAnimalSprint) animal.Sprint_Set(true);
                animal.DisablePosition = _originalAnimalDisablePosition;
                animal.DisableRotation = _originalAnimalDisableRotation;
                animal.RootMotion = _originalAnimalRootMotion;
                animal.LockForwardMovement = _originalAnimalLockForwardMovement;
                animal.LockUpDownMovement = _originalAnimalLockUpDownMovement;
            }

            if (animator)
            {
                animator.applyRootMotion = _originalAnimatorApplyRootMotion;
                animator.speed = _originalAnimatorSpeed;
                animator.updateMode = _originalAnimatorUpdateMode;
                animator.cullingMode = _originalAnimatorCullingMode;
            }

            if (riderAnimator)
            {
                riderAnimator.updateMode = _originalRiderAnimatorUpdateMode;
                riderAnimator.cullingMode = _originalRiderAnimatorCullingMode;
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
