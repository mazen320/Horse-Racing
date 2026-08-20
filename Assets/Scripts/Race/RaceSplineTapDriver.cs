using System.Collections.Generic;
using MalbersAnimations.Controller;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

/// <summary>
/// Tap rate selects Malbers gait (Walk→Sprint). Malbers keeps full root-motion
/// locomotion feel; we only redirect that travel distance along RaceTrackSpline.
/// Horse faces spline tangent (not camera).
/// </summary>
[DefaultExecutionOrder(100)] // after MAnimal
public class RaceSplineTapDriver : MonoBehaviour
{
    [Header("Refs")]
    public SplineContainer splineContainer;
    public MAnimal animal;
    public Animator animator;

    [Header("Tap")]
    public float tapWindow = 1.0f;
    public float tapsPerSecondForMax = 5f;
    public float decayPerSecond = 0.7f;

    [Header("Gait energy bands (0..1)")]
    public float walkEnergy = 0.08f;
    public float trotEnergy = 0.25f;
    public float canterEnergy = 0.45f;
    public float gallopEnergy = 0.7f;

    readonly List<float> _tapTimes = new List<float>(64);
    float _energy;
    float _normalizedT;
    float _splineLength = 1f;
    int _gaitIndex;
    float _lastSpeed;

    void Reset()
    {
        animal = GetComponent<MAnimal>();
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (!animal) animal = GetComponent<MAnimal>();
        if (!animator) animator = GetComponent<Animator>();

        if (!splineContainer)
        {
            var go = GameObject.Find("RaceTrackSpline");
            if (go) splineContainer = go.GetComponent<SplineContainer>();
        }

        var sa = GetComponent<SplineAnimate>();
        if (sa) sa.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Keep Malbers walk/run feel — root motion stays on.
        if (animator)
            animator.applyRootMotion = true;

        if (animal)
        {
            animal.enabled = true;
            animal.RootMotion = true;
            animal.UseCameraInput = false; // not camera-relative
            animal.LockMovement = false;
            animal.LockForwardMovement = false;
            animal.LockHorizontalMovement = true;
            animal.LockUpDownMovement = true;
            animal.Grounded = true;
            animal.UseSprint = true;
            animal.Strafe = false;
            animal.AlwaysForward = false;
        }

        var aim = GetComponent<MalbersAnimations.Aim>();
        if (aim) aim.enabled = false;

        var input = GetComponent<MalbersAnimations.InputSystem.MInputLink>();
        if (input) input.enabled = false;

        var ai = GetComponentInChildren<MalbersAnimations.Controller.AI.MAnimalAIControl>(true);
        if (ai) ai.enabled = false;

        RecacheLength();
        _normalizedT = 0f;
        SnapToSpline();
    }

    void RecacheLength()
    {
        if (splineContainer != null && splineContainer.Spline != null && splineContainer.Spline.Count > 1)
            _splineLength = Mathf.Max(1f, splineContainer.CalculateLength());
        else
            _splineLength = 1f;
    }

    void Update()
    {
        if (WasTapThisFrame())
            RegisterTap();

        PruneOldTaps();

        float tps = _tapTimes.Count / Mathf.Max(0.05f, tapWindow);
        float targetEnergy = Mathf.Clamp01(tps / Mathf.Max(0.1f, tapsPerSecondForMax));

        if (targetEnergy > _energy)
            _energy = targetEnergy;
        else
            _energy = Mathf.MoveTowards(_energy, targetEnergy, decayPerSecond * Time.deltaTime);

        _gaitIndex = ResolveGait(_energy);
        ApplyGait(_gaitIndex);
    }

    void LateUpdate()
    {
        if (splineContainer == null) return;

        // Prefer animator root-motion delta (native stride). Fall back to Malbers speed.
        float dist = 0f;
        if (animator != null)
            dist = animator.deltaPosition.magnitude;

        if (dist < 0.00001f && animal != null)
        {
            if (animal.DeltaPos.sqrMagnitude > 0.0000001f)
                dist = animal.DeltaPos.magnitude;
            else if (animal.HorizontalSpeed > 0.01f)
                dist = animal.HorizontalSpeed * Time.deltaTime;
        }

        if (_gaitIndex > 0 && dist > 0.00001f && _splineLength > 1f)
        {
            _normalizedT += dist / _splineLength;
            while (_normalizedT >= 1f) _normalizedT -= 1f;
            _lastSpeed = dist / Mathf.Max(Time.deltaTime, 0.0001f);
        }
        else
        {
            _lastSpeed = 0f;
        }

        SnapToSpline();

        // Keep Malbers' internal last-pos in sync after we teleport onto the spline
        if (animal != null)
            animal.Teleport(transform.position);
    }

    void SnapToSpline()
    {
        if (splineContainer == null) return;

        float t = Mathf.Repeat(_normalizedT, 1f);
        var pos = (Vector3)splineContainer.EvaluatePosition(t);
        var tan = EvaluateTangentSafe(t);
        transform.position = pos;
        if (tan.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(tan.normalized, Vector3.up);
    }

    Vector3 EvaluateTangentSafe(float t)
    {
        var tan = (Vector3)splineContainer.EvaluateTangent(t);
        if (tan.sqrMagnitude > 0.0001f)
            return tan;

        // Linear knots can yield zero tangent — sample nearby
        for (int i = 1; i <= 8; i++)
        {
            float dt = i * 0.002f;
            tan = (Vector3)splineContainer.EvaluateTangent(Mathf.Repeat(t + dt, 1f));
            if (tan.sqrMagnitude > 0.0001f) return tan;
            tan = (Vector3)splineContainer.EvaluateTangent(Mathf.Repeat(t - dt + 1f, 1f));
            if (tan.sqrMagnitude > 0.0001f) return tan;
        }

        var a = (Vector3)splineContainer.EvaluatePosition(Mathf.Repeat(t, 1f));
        var b = (Vector3)splineContainer.EvaluatePosition(Mathf.Repeat(t + 0.01f, 1f));
        return b - a;
    }

    public void RegisterTap()
    {
        _tapTimes.Add(Time.time);
    }

    int ResolveGait(float energy)
    {
        if (energy < walkEnergy) return 0;
        if (energy < trotEnergy) return 1;
        if (energy < canterEnergy) return 2;
        if (energy < gallopEnergy) return 3;
        if (energy < 0.9f) return 4;
        return 5;
    }

    void ApplyGait(int gait)
    {
        if (animal == null || !animal.enabled) return;

        animal.Grounded = true;
        animal.UseCameraInput = false;
        animal.Strafe = false;

        if (gait <= 0)
        {
            animal.AlwaysForward = false;
            animal.SetInputAxis(Vector3.zero);
            animal.StopMoving();
            if (animal.ActiveState == null || animal.ActiveState.ID.ID != 0)
                animal.State_Activate(0);
            return;
        }

        // Native Malbers "keep going forward" — drives Vertical / locomotion blends
        animal.AlwaysForward = true;

        if (animal.ActiveState == null || animal.ActiveState.ID.ID != 1)
            animal.State_Activate(1);

        int speedIndex = Mathf.Clamp(gait, 1, 5);
        if (animal.CurrentSpeedIndex != speedIndex)
            animal.Speed_CurrentIndex_Set(speedIndex);
    }

    void PruneOldTaps()
    {
        float cutoff = Time.time - tapWindow;
        int remove = 0;
        while (remove < _tapTimes.Count && _tapTimes[remove] < cutoff)
            remove++;
        if (remove > 0)
            _tapTimes.RemoveRange(0, remove);
    }

    static bool WasTapThisFrame()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.wKey.wasPressedThisFrame ||
             Keyboard.current.upArrowKey.wasPressedThisFrame))
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null)
        {
            foreach (var t in Touchscreen.current.touches)
            {
                if (t.press.wasPressedThisFrame)
                    return true;
            }
        }

        return false;
    }

    public float Energy => _energy;
    public float CurrentSpeed => _lastSpeed;
    public int GaitIndex => _gaitIndex;
}
