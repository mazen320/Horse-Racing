using UnityEngine;

/// <summary>
/// Procedural eyelid blink for the client FreerideHorse skeleton (g_* lid bones).
/// Open/closed poses are sampled from the export's riding_horse_rub clip.
/// Runs in LateUpdate so it wins over idle clips that leave lids static.
/// </summary>
[DisallowMultipleComponent]
public sealed class ClientHorseEyeBlink : MonoBehaviour
{
    [Header("Bones (auto-found by name if empty)")]
    [SerializeField] Transform leftLidTop;
    [SerializeField] Transform leftLidBot;
    [SerializeField] Transform rightLidTop;
    [SerializeField] Transform rightLidBot;

    [Header("Timing")]
    [SerializeField] Vector2 blinkInterval = new Vector2(1.8f, 4.5f);
    [SerializeField] float blinkCloseDuration = 0.06f;
    [SerializeField] float blinkHoldDuration = 0.04f;
    [SerializeField] float blinkOpenDuration = 0.1f;
    [SerializeField] [Range(0f, 1f)] float doubleBlinkChance = 0.18f;

    // Open / closed local rotations from riding_horse_rub (~t=0 open, ~t=3.57 closed).
    static readonly Quaternion LeftTopOpen = new Quaternion(-0.308f, -0.055f, 0.940f, -0.134f);
    static readonly Quaternion LeftTopClosed = new Quaternion(-0.333f, 0.193f, 0.922f, -0.042f);
    static readonly Quaternion LeftBotOpen = new Quaternion(-0.282f, -0.200f, 0.920f, -0.184f);
    static readonly Quaternion LeftBotClosed = new Quaternion(-0.238f, -0.373f, 0.864f, -0.240f);
    static readonly Quaternion RightTopOpen = new Quaternion(0.912f, 0.192f, -0.303f, 0.199f);
    static readonly Quaternion RightTopClosed = new Quaternion(0.827f, 0.265f, -0.242f, 0.433f);
    static readonly Quaternion RightBotOpen = new Quaternion(0.932f, 0.142f, -0.330f, 0.054f);
    static readonly Quaternion RightBotClosed = new Quaternion(0.924f, 0.075f, -0.351f, -0.128f);

    float _nextBlinkAt;
    float _blinkAmount;
    bool _blinking;
    bool _closing;
    bool _holding;
    bool _pendingDouble;
    float _phaseElapsed;

    void Awake()
    {
        CacheBones();
        ScheduleNextBlink(Random.Range(blinkInterval.x * 0.35f, blinkInterval.y * 0.6f));
    }

    void OnValidate() => CacheBones();

    void CacheBones()
    {
        if (leftLidTop == null) leftLidTop = FindDeep("g_left_lid_top");
        if (leftLidBot == null) leftLidBot = FindDeep("g_left_lid_bot");
        if (rightLidTop == null) rightLidTop = FindDeep("g_right_lid_top");
        if (rightLidBot == null) rightLidBot = FindDeep("g_right_lid_bot");
    }

    Transform FindDeep(string boneName)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == boneName)
                return transforms[i];
        }
        return null;
    }

    void ScheduleNextBlink(float delay)
    {
        _nextBlinkAt = Time.time + Mathf.Max(0.2f, delay);
    }

    void LateUpdate()
    {
        if (leftLidTop == null && rightLidTop == null)
            return;

        if (!_blinking && Time.time >= _nextBlinkAt)
            BeginBlink(Random.value < doubleBlinkChance);

        if (_blinking)
            TickBlink();

        ApplyLids(_blinkAmount);
    }

    void BeginBlink(bool doubleBlink)
    {
        _blinking = true;
        _closing = true;
        _holding = false;
        _pendingDouble = doubleBlink;
        _phaseElapsed = 0f;
    }

    void TickBlink()
    {
        _phaseElapsed += Time.deltaTime;

        if (_closing)
        {
            float t = blinkCloseDuration <= 0.0001f ? 1f : Mathf.Clamp01(_phaseElapsed / blinkCloseDuration);
            _blinkAmount = t;
            if (t >= 1f)
            {
                _closing = false;
                _holding = true;
                _phaseElapsed = 0f;
            }
            return;
        }

        if (_holding)
        {
            _blinkAmount = 1f;
            if (_phaseElapsed >= blinkHoldDuration)
            {
                _holding = false;
                _phaseElapsed = 0f;
            }
            return;
        }

        float openT = blinkOpenDuration <= 0.0001f ? 1f : Mathf.Clamp01(_phaseElapsed / blinkOpenDuration);
        _blinkAmount = 1f - openT;
        if (openT < 1f)
            return;

        _blinking = false;
        _blinkAmount = 0f;

        if (_pendingDouble)
        {
            _pendingDouble = false;
            BeginBlink(false);
            return;
        }

        ScheduleNextBlink(Random.Range(blinkInterval.x, blinkInterval.y));
    }

    void ApplyLids(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (leftLidTop) leftLidTop.localRotation = Quaternion.Slerp(LeftTopOpen, LeftTopClosed, amount);
        if (leftLidBot) leftLidBot.localRotation = Quaternion.Slerp(LeftBotOpen, LeftBotClosed, amount);
        if (rightLidTop) rightLidTop.localRotation = Quaternion.Slerp(RightTopOpen, RightTopClosed, amount);
        if (rightLidBot) rightLidBot.localRotation = Quaternion.Slerp(RightBotOpen, RightBotClosed, amount);
    }
}
