using System;
using System.Collections;
using DG.Tweening;
using HorseRacing.Race;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// Wire pages and HUD references on the Canvas in the Inspector.
    /// Flow: StartPage → InstructionsPage → game HUD (countdown → race).
    /// </summary>
    public sealed class NacdEnergizingUIManager : MonoBehaviour
    {
        /// <summary>One row of the fastest-times board, wired in the Inspector.</summary>
        [System.Serializable]
        sealed class LeaderboardRowView
        {
            public RectTransform root;
            public Image plate;
            public TMP_Text rankText;
            public TMP_Text nameText;
            public TMP_Text timeText;
        }

        enum FlowState
        {
            StartPage,
            InstructionsPage,
            Countdown,
            Racing,
            Results
        }

        [Header("Race")]
        [SerializeField] RaceSplineTapDriver raceDriver;
        [SerializeField] RaceSplineTapDriver raceDriverP2;
        [SerializeField] RaceViewLayout viewLayout;
        [SerializeField] StartGateDoors startGate;
        [Tooltip("2 = split screen. 1 = one full-screen view. The tablet overrides this per match.")]
        [Range(1, 2)]
        [SerializeField] int playerCount = 2;

        [Header("Menu pages")]
        [SerializeField] CanvasGroup menuBackgroundCG;
        [SerializeField] CanvasGroup startPageCG;
        [SerializeField] CanvasGroup instructionsPageCG;
        [SerializeField] Button startContinueButton;
        [SerializeField] Button instructionsStartButton;
        [SerializeField] TMP_Text instructionsBodyText;
        [TextArea(4, 10)]
        [SerializeField] string instructionsCopy =
            "Run As Fast As You Can — Your Horse Matches Your Pace All The Way To The Finish.\n\n" +
            "Race Side By Side In Split Screen. First Across The Line Wins!";

        [Header("Game HUD")]
        [SerializeField] CanvasGroup gameHudCG;
        [SerializeField] CanvasGroup countdownCG;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] TMP_Text raceTimerText;
        [Tooltip("Shared race clock pill. Shown while racing; hidden when per-player result pills appear.")]
        [SerializeField] RectTransform timerPill;
        [Tooltip("Gap below the nameplate that the shared clock drops into on a solo run.")]
        [SerializeField] float soloTimerGap = 8f;

        [Header("Results — per-player time pills (author in scene)")]
        [Tooltip("Shown at the finish under player 1. Position in the Scene view for split and solo.")]
        [SerializeField] RectTransform player1TimePill;
        [SerializeField] TMP_Text player1TimeText;
        [Tooltip("Shown at the finish under player 2 in split-screen runs.")]
        [SerializeField] RectTransform player2TimePill;
        [SerializeField] TMP_Text player2TimeText;

        [Header("Editor HUD preview")]
        [Tooltip("Toggle in the Inspector to preview solo vs split header layout while editing.")]
        [SerializeField] bool editorPreviewSoloLayout;

        [Header("Results — verdict on the nameplates")]
        [Tooltip("Separator between the rider name and the verdict, e.g. ALEX — WON.")]
        [SerializeField] string verdictSeparator = " — ";
        [SerializeField] string wonLabel = "WON";
        [SerializeField] string lostLabel = "LOST";
        [SerializeField] string tieLabel = "TIE";
        [Tooltip("Shown instead of WON/LOST when only one rider is racing.")]
        [SerializeField] string soloFinishLabel = "FINISHED";
        [SerializeField] Color winnerHighlightColor = new Color(0.85f, 0.55f, 0.46f, 1f);
        [SerializeField] Color loserHighlightColor = new Color(0.62f, 0.63f, 0.6f, 1f);
        [Tooltip("Dead heat window. Times inside this many seconds of each other count as a tie.")]
        [SerializeField] float tieToleranceSeconds = 0.05f;

        [Header("Results — leaderboard")]
        [SerializeField] CanvasGroup leaderboardCG;
        [SerializeField] RectTransform leaderboardPanel;
        [SerializeField] TMP_Text leaderboardFastestText;
        [SerializeField] LeaderboardRowView[] leaderboardRows = new LeaderboardRowView[5];
        [SerializeField] Color leaderboardRowColor = new Color(0.878f, 0.812f, 0.686f, 0.93f);
        [SerializeField] Color leaderboardRowHighlightColor = new Color(0.85f, 0.55f, 0.46f, 1f);
        [Tooltip("How many times the saved board keeps, even though fewer rows are shown.")]
        [SerializeField] int leaderboardCapacity = 10;
        [SerializeField] string leaderboardEmptyTime = "—";
        [Tooltip("Shown under a nameplate when that rider never reached the line after the winner finished.")]
        [SerializeField] string dnfLabel = "DNF";

        [Header("Results timing")]
        [Tooltip("After the winner finishes, how long to wait before showing DNF. A later crossing still replaces DNF with a real time.")]
        [SerializeField] float finishGraceSeconds = 2.5f;
        [SerializeField] float verdictHoldSeconds = 1.1f;
        [SerializeField] float resultsIntroDuration = 0.65f;
        [SerializeField] Ease resultsIntroEase = Ease.OutCubic;
        [SerializeField] float verdictPopScale = 1.08f;

        [Header("Race timer")]
        [Tooltip("TMP <mspace> width so digits do not shift as the clock ticks.")]
        [SerializeField] float timerMonospaceEm = 0.62f;

        [Header("Player nameplates (Instructions TitleBlock style)")]
        [SerializeField] TMP_Text player1NameText;
        [SerializeField] TMP_Text player2NameText;
        [SerializeField] RectTransform player1Nameplate;
        [SerializeField] RectTransform player2Nameplate;
        [SerializeField] RectTransform player1Underline;
        [SerializeField] RectTransform player2Underline;
        [SerializeField] RectTransform player1Plate;
        [SerializeField] RectTransform player2Plate;
        [SerializeField] float underlinePadding = 26f;
        [SerializeField] float platePadding = 104f;

        [Header("Player names")]
        [SerializeField] string player1Name = "PLAYER 1";
        [SerializeField] string player2Name = "PLAYER 2";

        [Header("Transitions")]
        [SerializeField] float fadeDuration = 0.5f;
        [SerializeField] float pageStagger = 0.08f;
        [SerializeField] Ease fadeOutEase = Ease.InCubic;
        [SerializeField] Ease fadeInEase = Ease.OutCubic;
        [SerializeField] float nameplateIntroOffsetY = 28f;
        [SerializeField] float nameplateIntroDuration = 0.55f;
        [SerializeField] Ease nameplateIntroEase = Ease.OutCubic;

        [Header("Countdown timing")]
        [SerializeField] float countdownSeconds = 3f;
        [SerializeField] float goHoldSeconds = 0.45f;
        [SerializeField] float countdownPopScale = 1.15f;
        [SerializeField] float countdownPopDuration = 0.22f;
        [SerializeField] Ease countdownPopEase = Ease.OutBack;

        FlowState _state = FlowState.StartPage;
        float _raceStartTime;
        long _raceStartUtcTicks;
        float _raceEndTime = -1f;
        float _player1Time = -1f;
        float _player2Time = -1f;
        Coroutine _flowCoroutine;
        Sequence _pageSequence;
        float _player1NameplateRestY;
        float _player2NameplateRestY;
        bool _nameplateRestCached;
        Vector2 _timerPillRestPos;
        bool _timerPillRestCached;
        RaceLeaderboardStore _leaderboard;
        int _player1BoardPosition;
        int _player2BoardPosition;
        Color _player1HighlightRestColor = Color.white;
        Color _player2HighlightRestColor = Color.white;
        bool _highlightRestCached;
        bool _showingVerdict;
        bool _nameplatesLayoutReady;

        /// <summary>Fired when the race clock starts (after countdown), with UTC ticks for tablet sync.</summary>
        public event Action<long> RaceStarted;

        /// <summary>Fired when the winner finishes, with UTC ticks so the tablet can freeze its clock.</summary>
        public event Action<long> RaceEnded;

        bool Solo => playerCount <= 1;

        bool PreviewSolo =>
#if UNITY_EDITOR
            !Application.isPlaying && editorPreviewSoloLayout;
#else
            false;
#endif

        bool LayoutSolo => Solo || PreviewSolo;

        void OnEnable()
        {
            enabled = true;
        }

        void Awake()
        {
            enabled = true;
            ResolveRaceDrivers();
            ResolveTimeTextRefs();
            CacheNameplateRestPositions();
            ApplyViewLayout();
            ConfigureRaceTimer();
            ApplyCountdownShadow(countdownText);
            CacheHighlightRestColors();
            LoadLeaderboard();
            SetLeaderboardVisible(false, true);
            if (Application.isPlaying)
            {
                HidePlayerTimePill(player1TimePill);
                HidePlayerTimePill(player2TimePill);
            }

            WireButton(startContinueButton, OnStartContinue);
            WireButton(instructionsStartButton, OnInstructionsStart);

            if (raceDriver)
            {
                raceDriver.onRaceFinished.AddListener(OnDriver1RaceFinished);
                ParkDriver(raceDriver);
            }

            if (raceDriverP2)
            {
                raceDriverP2.onRaceFinished.AddListener(OnDriver2RaceFinished);
                ParkDriver(raceDriverP2);
            }

            RefreshPlayerNames();
            ApplyState(FlowState.StartPage, true);
        }

        /// <summary>
        /// Holds a driver at the grid. The animal controller can throw from inside the
        /// third-party state machine while it is still initialising, and an exception
        /// escaping Awake makes Unity disable this component, which silently freezes the
        /// HUD clock. The HUD matters more than one driver's start state.
        /// </summary>
        void ParkDriver(RaceSplineTapDriver driver)
        {
            try
            {
                driver.SetRaceInputEnabled(false);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"{driver.name} could not be parked at the grid: {exception.Message}", driver);
            }
        }

        void ResolveRaceDrivers()
        {
            if (!raceDriver || !raceDriverP2)
            {
                var drivers = FindObjectsByType<RaceSplineTapDriver>(FindObjectsInactive.Include);
                foreach (var driver in drivers)
                {
                    if (!driver) continue;
                    if (!raceDriver && driver.gameObject.name == "Horse Realistic")
                        raceDriver = driver;
                    else if (!raceDriverP2 && driver.gameObject.name == "Horse Realistic P2")
                        raceDriverP2 = driver;
                }

                if (!raceDriver && drivers.Length > 0)
                    raceDriver = drivers[0];
                if (!raceDriverP2 && drivers.Length > 1)
                {
                    foreach (var driver in drivers)
                    {
                        if (driver != raceDriver)
                        {
                            raceDriverP2 = driver;
                            break;
                        }
                    }
                }
            }
        }

        void OnDestroy()
        {
            KillTweens();
            if (raceDriver)
                raceDriver.onRaceFinished.RemoveListener(OnDriver1RaceFinished);
            if (raceDriverP2)
                raceDriverP2.onRaceFinished.RemoveListener(OnDriver2RaceFinished);
        }

        void Start()
        {
            // Something in the project was leaving this Behaviour disabled on Canvas,
            // which freezes the HUD timer because Update never runs.
            enabled = true;
            RefreshRaceTimer(0f);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.ctrlKey.isPressed && keyboard.lKey.wasPressedThisFrame)
                ClearLeaderboardWithCsvBackup();
        }

        void LateUpdate()
        {
            if ((_state == FlowState.Racing || _state == FlowState.Results) && raceTimerText)
                RefreshRaceTimer();
        }

        void ConfigureRaceTimer()
        {
            if (!raceTimerText) return;

            raceTimerText.richText = true;
            raceTimerText.enableAutoSizing = false;
            raceTimerText.alignment = TextAlignmentOptions.Center;
            RefreshRaceTimer(0f);
        }

        public void OnStartContinue() => ApplyState(FlowState.InstructionsPage);

        public void OnInstructionsStart() => StartMatch();

        public void StartMatch()
        {
            StopFlow();
            _player1Time = -1f;
            _player2Time = -1f;
            _raceEndTime = -1f;
            _raceStartUtcTicks = 0;
            RefreshRaceTimer(0f);
            PrepareNameplateLayout();
            _flowCoroutine = StartCoroutine(RunSplitScreenRace());
        }

        IEnumerator RunSplitScreenRace()
        {
            RestartDrivers(inputEnabled: false);
            RaceCameraTarget.SnapAllAfterTeleport();

            // Names and plate widths must be settled before the HUD fades in.
            PrepareNameplateLayout();
            ApplyState(FlowState.Countdown);
            yield return RunCountdown();

            if (startGate)
                startGate.Open();

            SetDriversInputEnabled(true);
            _raceStartUtcTicks = DateTime.UtcNow.Ticks;
            _raceStartTime = Time.time;
            _raceEndTime = -1f;
            RefreshRaceTimer(0f);
            RaceStarted?.Invoke(_raceStartUtcTicks);

            ApplyState(FlowState.Racing);
            _flowCoroutine = null;
        }

        public void ApplyTabletRegistration(string player1, string player2, bool showInstructions)
            => ApplyTabletRegistration(player1, player2, showInstructions, playerCount);

        public void ApplyTabletRegistration(string player1, string player2, bool showInstructions, int players)
        {
            SetPlayerCount(players);
            SetPlayerNames(player1, player2);
            ApplyState(showInstructions ? FlowState.InstructionsPage : FlowState.StartPage);
        }

        /// <summary>1 = one full-screen view, 2 = split screen.</summary>
        public void SetPlayerCount(int players)
        {
            playerCount = Mathf.Clamp(players, 1, 2);
            ApplyViewLayout();
            PrepareNameplateLayout();
        }

        void ApplyViewLayout()
        {
            if (viewLayout && Application.isPlaying)
                viewLayout.Apply(playerCount);

            if (player2Nameplate)
                player2Nameplate.gameObject.SetActive(!LayoutSolo);

            // One rider gets the whole width so the plate sits centre screen instead of
            // hugging the left half where the split divider used to be.
            if (player1Nameplate)
            {
                player1Nameplate.anchorMin = new Vector2(0f, 1f);
                player1Nameplate.anchorMax = new Vector2(LayoutSolo ? 1f : 0.5f, 1f);
                player1Nameplate.anchoredPosition = new Vector2(0f, player1Nameplate.anchoredPosition.y);
            }

            ApplyResultTimePillLayout();
            ApplyTimerPillLayout();
            ApplyResultPillPreview();
        }

        void ResolveTimeTextRefs()
        {
            if (player1TimePill && !player1TimeText)
                player1TimeText = player1TimePill.GetComponentInChildren<TMP_Text>(true);
            if (player2TimePill && !player2TimeText)
                player2TimeText = player2TimePill.GetComponentInChildren<TMP_Text>(true);

            ConfigureResultTimeText(player1TimeText);
            ConfigureResultTimeText(player2TimeText);
        }

        static void ConfigureResultTimeText(TMP_Text text)
        {
            if (!text) return;
            text.richText = true;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            ResolveTimeTextRefs();
            ApplyViewLayout();
        }
#endif

        /// <summary>
        /// In the editor, keep the result pills visible with sample times so you can
        /// drag them into place for split and solo without entering Play Mode.
        /// </summary>
        void ApplyResultPillPreview()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            SetResultPillPreview(player1TimePill, player1TimeText, "0:12.3", true);
            SetResultPillPreview(player2TimePill, player2TimeText, "0:14.8", !LayoutSolo);
#endif
        }

        static void SetResultPillPreview(RectTransform pill, TMP_Text label, string sample, bool visible)
        {
            if (!pill)
                return;

            pill.gameObject.SetActive(visible);
            if (visible && label)
                label.text = sample;
        }

        RectTransform TimerPill
        {
            get
            {
                if (!timerPill && raceTimerText)
                    timerPill = raceTimerText.transform.parent as RectTransform;

                return timerPill;
            }
        }

        /// <summary>
        /// Result time pills follow the same horizontal lanes as the nameplates: centred
        /// for solo, left/right quarters for split.
        /// </summary>
        void ApplyResultTimePillLayout()
        {
            ApplyResultTimePill(player1TimePill, LayoutSolo ? 0.5f : 0.25f);
            ApplyResultTimePill(player2TimePill, 0.75f);
        }

        static void ApplyResultTimePill(RectTransform pill, float anchorX)
        {
            if (!pill)
                return;

            var yMin = pill.anchorMin.y;
            var yMax = pill.anchorMax.y;
            pill.anchorMin = new Vector2(anchorX, yMin);
            pill.anchorMax = new Vector2(anchorX, yMax);
            pill.anchoredPosition = new Vector2(0f, pill.anchoredPosition.y);
        }

        /// <summary>
        /// Two riders split the bar and leave the middle clear for the clock. A single
        /// rider spans the full width and lands on top of it, so the clock drops to its
        /// own row under the nameplate.
        /// </summary>
        void ApplyTimerPillLayout()
        {
            var pill = TimerPill;
            if (!pill)
                return;

            if (!_timerPillRestCached)
            {
                _timerPillRestPos = pill.anchoredPosition;
                _timerPillRestCached = true;
            }

            var drop = player1Nameplate ? player1Nameplate.rect.height + soloTimerGap : 0f;

            pill.anchoredPosition = LayoutSolo
                ? new Vector2(_timerPillRestPos.x, _timerPillRestPos.y - drop)
                : _timerPillRestPos;
        }

        /// <summary>
        /// At the finish the shared clock steps aside and each rider's authored pill
        /// under their nameplate shows their time.
        /// </summary>
        void ShowPlayerTimePills()
        {
            var pill = TimerPill;
            if (pill)
                pill.gameObject.SetActive(false);

            UpdatePlayerTimePill(player1TimePill, player1TimeText, _player1Time, true);
            UpdatePlayerTimePill(player2TimePill, player2TimeText, _player2Time,
                !Solo && raceDriverP2);
        }

        /// <summary>
        /// A rider who never reached the line after the winner finished shows DNF so the
        /// header stays balanced and the dash does not look like a missing clock.
        /// </summary>
        void UpdatePlayerTimePill(RectTransform pill, TMP_Text label, float seconds, bool raced)
        {
            if (!pill)
                return;

            if (!raced)
            {
                pill.gameObject.SetActive(false);
                return;
            }

            if (label)
                label.text = seconds >= 0f
                    ? FormatTime(seconds, timerMonospaceEm)
                    : (string.IsNullOrWhiteSpace(dnfLabel) ? leaderboardEmptyTime : dnfLabel);

            pill.gameObject.SetActive(true);
            pill.DOKill();
            pill.localScale = Vector3.one * 0.9f;
            pill.DOScale(1f, resultsIntroDuration)
                .SetEase(resultsIntroEase)
                .SetUpdate(true)
                .SetTarget(pill);
        }

        void HidePlayerTimePills()
        {
            HidePlayerTimePill(player1TimePill);
            HidePlayerTimePill(player2TimePill);

            var pill = TimerPill;
            if (pill)
                pill.gameObject.SetActive(true);
        }

        static void HidePlayerTimePill(RectTransform pill)
        {
            if (!pill)
                return;

            pill.DOKill();
            pill.gameObject.SetActive(false);
        }

        public void ApplyTabletShowInstructions() => ApplyState(FlowState.InstructionsPage);

        public void ApplyTabletStartRace() => StartMatch();

        public void ApplyTabletRestart()
        {
            StopFlow();
            _flowCoroutine = StartCoroutine(ReturnToStartPage());
        }

        /// <summary>
        /// Same registered riders — hide the race view behind the menu, re-grid horses,
        /// keep nameplates, ready for the next Start from the tablet.
        /// </summary>
        public void ApplyTabletNewRace(bool showInstructions)
        {
            StopFlow();
            _flowCoroutine = StartCoroutine(ReturnForNewRace(showInstructions));
        }

        /// <summary>
        /// The start page is a full-screen opaque page, so it goes up first and the grid
        /// reset happens behind it. Re-gridding while the race view is still on screen is
        /// what made the return read as the camera flying back down the course.
        /// </summary>
        IEnumerator ReturnToStartPage()
        {
            ApplyState(FlowState.StartPage);

            // TransitionPages delays each fade-in by fadeDuration * 0.35 + pageStagger,
            // so the page is only fully opaque a little after one whole fade.
            yield return WaitSeconds(fadeDuration * 1.35f + pageStagger + 0.05f);

            _player1Time = -1f;
            _player2Time = -1f;
            _raceEndTime = -1f;
            _raceStartUtcTicks = 0;
            _player1BoardPosition = 0;
            _player2BoardPosition = 0;
            PrepareRaceFieldForMenu();
            _flowCoroutine = null;
        }

        IEnumerator ReturnForNewRace(bool showInstructions)
        {
            ApplyState(showInstructions ? FlowState.InstructionsPage : FlowState.StartPage);
            yield return WaitSeconds(fadeDuration * 1.35f + pageStagger + 0.05f);

            _player1Time = -1f;
            _player2Time = -1f;
            _raceEndTime = -1f;
            _raceStartUtcTicks = 0;
            _player1BoardPosition = 0;
            _player2BoardPosition = 0;
            _showingVerdict = false;
            RestoreHighlightColors();
            ResetNameplateScales();
            PrepareNameplateLayout();
            PrepareRaceFieldForMenu();
            _flowCoroutine = null;
        }

        public void SetPlayerNames(string player1, string player2)
        {
            if (!string.IsNullOrWhiteSpace(player1))
                player1Name = player1.Trim();
            if (!string.IsNullOrWhiteSpace(player2))
                player2Name = player2.Trim();
            PrepareNameplateLayout();
        }

        void KillTweens()
        {
            _pageSequence?.Kill();
            _pageSequence = null;
            menuBackgroundCG?.DOKill();
            startPageCG?.DOKill();
            instructionsPageCG?.DOKill();
            gameHudCG?.DOKill();
            countdownCG?.DOKill();
            countdownText?.transform.DOKill();
            KillOutcomeTweens();
            if (player1Nameplate) player1Nameplate.DOKill();
            if (player2Nameplate) player2Nameplate.DOKill();
        }

        void KillOutcomeTweens()
        {
            leaderboardCG?.DOKill();
            if (leaderboardPanel) leaderboardPanel.DOKill();
            if (player1TimePill) player1TimePill.DOKill();
            if (player2TimePill) player2TimePill.DOKill();
            if (leaderboardRows == null) return;

            foreach (var row in leaderboardRows)
            {
                if (row?.root != null) row.root.DOKill();
            }
        }

        static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (!button) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        IEnumerator RunCountdown()
        {
            var count = countdownSeconds;
            while (count > 0f)
            {
                AnimateCountdownStep(Mathf.CeilToInt(count).ToString());
                var next = Mathf.CeilToInt(count) - 1;
                while (count > next)
                {
                    count -= Time.deltaTime;
                    yield return null;
                }
            }

            AnimateCountdownStep("GO!");
            yield return new WaitForSeconds(goHoldSeconds);
        }

        void AnimateCountdownStep(string text)
        {
            if (!countdownText) return;

            countdownText.text = text;
            var t = countdownText.transform;
            t.DOKill();
            t.localScale = Vector3.one * 0.82f;
            var cg = countdownCG;
            if (cg)
            {
                cg.DOKill(false);
                cg.alpha = 0f;
                cg.DOFade(1f, countdownPopDuration * 0.55f).SetEase(Ease.OutQuad);
            }

            t.DOScale(countdownPopScale, countdownPopDuration)
                .SetEase(countdownPopEase)
                .SetLoops(2, LoopType.Yoyo)
                .SetTarget(t);
        }

        void OnDriver1RaceFinished()
        {
            // Still accept a late finish after the winner ended the race — photo finishes
            // often land a frame or two after BeginResults has already left Racing.
            if (_player1Time >= 0f)
                return;
            if (_state != FlowState.Racing && _state != FlowState.Results)
                return;

            _player1Time = GetRaceElapsed();
            raceDriver?.SetRaceInputEnabled(false);

            if (_state == FlowState.Racing)
                BeginResults();
            else
                OnLateFinishRecorded();
        }

        void OnDriver2RaceFinished()
        {
            if (_player2Time >= 0f)
                return;
            if (_state != FlowState.Racing && _state != FlowState.Results)
                return;

            _player2Time = GetRaceElapsed();
            raceDriverP2?.SetRaceInputEnabled(false);

            if (_state == FlowState.Racing)
                BeginResults();
            else
                OnLateFinishRecorded();
        }

        /// <summary>
        /// A finish that landed after the grace UI already showed DNF — swap in the
        /// real time and refresh the board. Late crossings still count.
        /// </summary>
        void OnLateFinishRecorded()
        {
            if (_player1Time >= 0f && _player2Time >= 0f)
                _raceEndTime = WinningTime();

            RefreshRaceTimer();

            if (!_showingVerdict)
                return;

            SubmitLeaderboardTimes();
            ShowPlayerTimePills();
            RefreshLeaderboardRows();
        }

        /// <summary>
        /// The first horse over the line decides the winner. The other rider may keep
        /// racing for a real finish time; grace only controls when DNF first appears.
        /// </summary>
        void BeginResults()
        {
            if (_state != FlowState.Racing)
                return;

            _raceEndTime = WinningTime();
            var raceEndUtcTicks = _raceStartUtcTicks > 0
                ? _raceStartUtcTicks + (long)(_raceEndTime * TimeSpan.TicksPerSecond)
                : DateTime.UtcNow.Ticks;
            RaceEnded?.Invoke(raceEndUtcTicks);
            RefreshRaceTimer();
            // Stop the match coroutine only — do not freeze the unfinished horse.
            StopFlow(disableAllInput: false);
            _flowCoroutine = StartCoroutine(ShowResultsSequence());
        }

        /// <summary>Time on the line for the horse that ended the race.</summary>
        float WinningTime()
        {
            if (_player1Time < 0f) return _player2Time;
            if (_player2Time < 0f) return _player1Time;
            return Mathf.Min(_player1Time, _player2Time);
        }

        bool BothPlayersHaveTimes() =>
            _player1Time >= 0f && (Solo || !raceDriverP2 || _player2Time >= 0f);

        IEnumerator ShowResultsSequence()
        {
            ApplyState(FlowState.Results);

            // Winner is decided immediately; times fill in as riders cross.
            ApplyVerdicts();
            AnimateVerdictPop();

            if (!Solo && !BothPlayersHaveTimes() && finishGraceSeconds > 0.01f)
            {
                var remaining = finishGraceSeconds;
                while (remaining > 0f && !BothPlayersHaveTimes())
                {
                    remaining -= Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (BothPlayersHaveTimes())
                _raceEndTime = WinningTime();

            // Show time or provisional DNF — unfinished riders keep racing so a later
            // crossing still upgrades DNF via OnLateFinishRecorded.
            SubmitLeaderboardTimes();
            ShowPlayerTimePills();
            yield return WaitSeconds(verdictHoldSeconds);

            RefreshLeaderboardRows();
            SetLeaderboardVisible(true, false);
            AnimateLeaderboardIntro();

            // The board stays up. Only the tablet ends the run, because the riders are
            // looking at this screen and the operator is the one who knows they are done.
            _flowCoroutine = null;
        }

        /// <summary>
        /// Unscaled on purpose: these beats gate UI fades that themselves run unscaled,
        /// and a stalled time scale must never leave the flow parked mid-transition.
        /// </summary>
        static IEnumerator WaitSeconds(float seconds)
        {
            var remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void LoadLeaderboard()
        {
            _leaderboard = new RaceLeaderboardStore(Mathf.Max(1, leaderboardCapacity));
            _leaderboard.Load();
        }

        /// <summary>
        /// Archives the current board to CSV beside Leaderboard.json, clears the live
        /// board, and refreshes any rows already on screen.
        /// </summary>
        public void ClearLeaderboardWithCsvBackup()
        {
            if (_leaderboard == null)
                LoadLeaderboard();

            var backupPath = _leaderboard.ClearWithCsvBackup();
            _player1BoardPosition = 0;
            _player2BoardPosition = 0;

            if (!string.IsNullOrEmpty(backupPath))
                Debug.Log($"Leaderboard cleared. CSV backup saved to {backupPath}");
            else
                Debug.Log("Leaderboard cleared. No entries were archived.");

            RefreshLeaderboardRows();
        }

        void SubmitLeaderboardTimes()
        {
            if (_leaderboard == null)
                LoadLeaderboard();

            _player1BoardPosition = _player1Time > 0f
                ? _leaderboard.Submit(player1Name, _player1Time)
                : 0;

            _player2BoardPosition = !Solo && raceDriverP2 && _player2Time > 0f
                ? _leaderboard.Submit(player2Name, _player2Time)
                : 0;
        }

        /// <summary>
        /// Rewrites each nameplate as "NAME — WON" so the result lands in the styling the
        /// panel already uses, instead of a separate overlay fighting the race view.
        /// </summary>
        void ApplyVerdicts()
        {
            _showingVerdict = true;

            if (Solo || !raceDriverP2)
            {
                ApplyVerdict(player1NameText, player1Underline, player1Plate,
                    player1Name, soloFinishLabel, true);
                return;
            }

            var bothFinished = _player1Time >= 0f && _player2Time >= 0f;
            var tie = bothFinished &&
                      Mathf.Abs(_player1Time - _player2Time) <= Mathf.Max(0f, tieToleranceSeconds);
            var p1Won = !tie && _player1Time >= 0f &&
                        (_player2Time < 0f || _player1Time <= _player2Time);

            ApplyVerdict(player1NameText, player1Underline, player1Plate,
                player1Name, tie ? tieLabel : p1Won ? wonLabel : lostLabel, tie || p1Won);
            ApplyVerdict(player2NameText, player2Underline, player2Plate,
                player2Name, tie ? tieLabel : p1Won ? lostLabel : wonLabel, tie || !p1Won);
        }

        void ApplyVerdict(TMP_Text nameText, RectTransform underline, RectTransform plate,
            string name, string verdict, bool won)
        {
            if (!nameText) return;

            var label = string.IsNullOrWhiteSpace(name) ? "PLAYER" : name.Trim().ToUpperInvariant();
            nameText.text = $"{label}{verdictSeparator}{verdict}";
            FitNameplate(nameText, underline, plate);

            var highlight = underline ? underline.GetComponent<Image>() : null;
            if (highlight)
                highlight.color = won ? winnerHighlightColor : loserHighlightColor;
        }

        void AnimateVerdictPop()
        {
            PopNameplate(player1Nameplate);
            if (!Solo)
                PopNameplate(player2Nameplate);
        }

        void PopNameplate(RectTransform plate)
        {
            if (!plate) return;

            plate.DOKill();
            plate.localScale = Vector3.one;
            plate.DOScale(Mathf.Max(1f, verdictPopScale), resultsIntroDuration * 0.45f)
                .SetEase(resultsIntroEase)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .SetTarget(plate);
        }

        void CacheHighlightRestColors()
        {
            if (_highlightRestCached) return;

            var p1 = player1Underline ? player1Underline.GetComponent<Image>() : null;
            if (p1) _player1HighlightRestColor = p1.color;

            var p2 = player2Underline ? player2Underline.GetComponent<Image>() : null;
            if (p2) _player2HighlightRestColor = p2.color;

            _highlightRestCached = true;
        }

        void RestoreHighlightColors()
        {
            var p1 = player1Underline ? player1Underline.GetComponent<Image>() : null;
            if (p1) p1.color = _player1HighlightRestColor;

            var p2 = player2Underline ? player2Underline.GetComponent<Image>() : null;
            if (p2) p2.color = _player2HighlightRestColor;
        }

        void RefreshLeaderboardRows()
        {
            if (leaderboardRows == null) return;
            if (_leaderboard == null) LoadLeaderboard();

            var entries = _leaderboard.Model.Entries;

            if (leaderboardFastestText)
            {
                var fastest = _leaderboard.Model.Fastest;
                leaderboardFastestText.text = fastest != null
                    ? $"FASTEST {RaceLeaderboardModel.FormatSeconds(fastest.seconds)}"
                    : $"FASTEST {leaderboardEmptyTime}";
            }

            for (var i = 0; i < leaderboardRows.Length; i++)
            {
                var row = leaderboardRows[i];
                if (row == null) continue;

                var hasEntry = i < entries.Count;
                if (row.root)
                    row.root.gameObject.SetActive(true);

                if (row.rankText)
                    row.rankText.text = $"{i + 1}";
                if (row.nameText)
                    row.nameText.text = hasEntry ? entries[i].name : "—";
                if (row.timeText)
                {
                    row.timeText.text = hasEntry
                        ? FormatTime(entries[i].seconds, timerMonospaceEm)
                        : leaderboardEmptyTime;
                    row.timeText.richText = true;
                }

                var isNewRun = hasEntry &&
                               (i + 1 == _player1BoardPosition || i + 1 == _player2BoardPosition);
                if (row.plate)
                    row.plate.color = isNewRun ? leaderboardRowHighlightColor : leaderboardRowColor;
            }
        }

        void SetLeaderboardVisible(bool visible, bool instant)
        {
            if (leaderboardPanel)
                leaderboardPanel.gameObject.SetActive(visible);

            if (!leaderboardCG) return;

            leaderboardCG.DOKill();
            if (instant || fadeDuration <= 0f)
            {
                leaderboardCG.alpha = visible ? 1f : 0f;
                leaderboardCG.interactable = visible;
                leaderboardCG.blocksRaycasts = visible;
                return;
            }

            leaderboardCG.interactable = visible;
            leaderboardCG.blocksRaycasts = visible;
            leaderboardCG.DOFade(visible ? 1f : 0f, fadeDuration * 0.7f)
                .SetEase(visible ? fadeInEase : fadeOutEase)
                .SetUpdate(true);
        }

        void AnimateLeaderboardIntro()
        {
            if (!leaderboardPanel) return;

            leaderboardPanel.DOKill();
            leaderboardPanel.localScale = Vector3.one * 0.95f;
            leaderboardPanel.DOScale(1f, resultsIntroDuration)
                .SetEase(resultsIntroEase)
                .SetUpdate(true)
                .SetTarget(leaderboardPanel);

            if (leaderboardRows == null) return;

            for (var i = 0; i < leaderboardRows.Length; i++)
            {
                var row = leaderboardRows[i];
                if (row?.root == null) continue;

                var rowTransform = row.root;
                rowTransform.DOKill();
                var restX = rowTransform.anchoredPosition.x;
                rowTransform.anchoredPosition = new Vector2(restX - 40f, rowTransform.anchoredPosition.y);
                rowTransform.DOAnchorPosX(restX, resultsIntroDuration)
                    .SetDelay(pageStagger * i)
                    .SetEase(resultsIntroEase)
                    .SetUpdate(true)
                    .SetTarget(rowTransform);
            }
        }

        void RefreshRaceTimer(float? elapsedOverride = null)
        {
            if (!raceTimerText)
                return;

            var elapsed = elapsedOverride ?? (_state == FlowState.Results && _raceEndTime >= 0f
                ? _raceEndTime
                : GetRaceElapsed());
            raceTimerText.text = FormatTime(elapsed, timerMonospaceEm);
        }

        float GetRaceElapsed()
        {
            if (_raceStartUtcTicks > 0)
                return Mathf.Max(0f, (DateTime.UtcNow.Ticks - _raceStartUtcTicks) / (float)TimeSpan.TicksPerSecond);

            return Mathf.Max(0f, Time.time - _raceStartTime);
        }

        /// <summary>
        /// Returns horses to the grid and re-seats the chase cameras in the same frame,
        /// so the follow rig never interpolates across the course behind them.
        /// </summary>
        void PrepareRaceFieldForMenu()
        {
            RestartDrivers(inputEnabled: false);
            RaceCameraTarget.SnapAllAfterTeleport();
        }

        void RestartDrivers(bool inputEnabled)
        {
            if (startGate)
                startGate.Close();

            if (raceDriver)
            {
                raceDriver.RestartRace();
                raceDriver.SetRaceInputEnabled(inputEnabled);
            }

            if (raceDriverP2 && !Solo)
            {
                raceDriverP2.RestartRace();
                raceDriverP2.SetRaceInputEnabled(inputEnabled);
            }
        }

        void SetDriversInputEnabled(bool enabled)
        {
            raceDriver?.SetRaceInputEnabled(enabled);
            if (!Solo)
                raceDriverP2?.SetRaceInputEnabled(enabled);
        }

        void RefreshInstructions()
        {
            if (instructionsBodyText)
                instructionsBodyText.text = instructionsCopy;
        }

        void ResetNameplateScales()
        {
            if (player1Nameplate)
            {
                player1Nameplate.DOKill();
                player1Nameplate.localScale = Vector3.one;
            }

            if (!player2Nameplate) return;
            player2Nameplate.DOKill();
            player2Nameplate.localScale = Vector3.one;
        }

        void RefreshPlayerNames()
        {
            // The verdict text lives in the same labels, so plain names would overwrite
            // "ALEX — WON" the moment anything else refreshed the HUD.
            if (_showingVerdict) return;

            ApplyNameTexts();
            FitNameplatesIfActive();
        }

        void ApplyNameTexts()
        {
            if (player1NameText)
                player1NameText.text = string.IsNullOrWhiteSpace(player1Name) ? "PLAYER 1" : player1Name.ToUpperInvariant();
            if (player2NameText)
                player2NameText.text = string.IsNullOrWhiteSpace(player2Name) ? "PLAYER 2" : player2Name.ToUpperInvariant();
        }

        void FitNameplatesIfActive()
        {
            if (!NameplatesActiveForLayout())
                return;

            DoFitNameplates();
        }

        bool NameplatesActiveForLayout() =>
            player1Nameplate && player1Nameplate.gameObject.activeInHierarchy;

        void DoFitNameplates()
        {
            FitNameplate(player1NameText, player1Underline, player1Plate);
            FitNameplate(player2NameText, player2Underline, player2Plate);
        }

        /// <summary>
        /// Sizes underline/plate while nameplates are still off-screen so the HUD does not
        /// visibly reflow when countdown starts.
        /// </summary>
        void PrepareNameplateLayout()
        {
            if (_showingVerdict) return;

            var hideAfter = !ShowsRaceView(_state);
            var p1WasActive = player1Nameplate && player1Nameplate.gameObject.activeSelf;
            var p2WasActive = player2Nameplate && player2Nameplate.gameObject.activeSelf;

            if (player1Nameplate)
                player1Nameplate.gameObject.SetActive(true);
            if (player2Nameplate)
                player2Nameplate.gameObject.SetActive(!Solo);

            ApplyNameTexts();
            DoFitNameplates();
            Canvas.ForceUpdateCanvases();
            DoFitNameplates();

            _nameplatesLayoutReady = true;

            if (hideAfter)
            {
                if (!p1WasActive && player1Nameplate)
                    player1Nameplate.gameObject.SetActive(false);
                if (!p2WasActive && player2Nameplate)
                    player2Nameplate.gameObject.SetActive(false);
            }
        }

        /// <summary>Keeps the coral bar and cream plate hugging the name, however long it is.</summary>
        void FitNameplate(TMP_Text text, RectTransform underline, RectTransform plate)
        {
            if (!text) return;

            text.ForceMeshUpdate();
            var width = text.textBounds.size.x;
            if (width <= 0f || float.IsNaN(width)) return;

            if (underline)
                underline.sizeDelta = new Vector2(width + underlinePadding, underline.sizeDelta.y);
            if (plate)
                plate.sizeDelta = new Vector2(width + platePadding, plate.sizeDelta.y);
        }

        static string FormatTime(float seconds, float mspaceEm = 0f)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = seconds % 60f;
            var core = $"{minutes:0}:{secs:00.0}";
            return mspaceEm > 0.001f ? $"<mspace={mspaceEm:0.###}em>{core}</mspace>" : core;
        }

        void ApplyState(FlowState newState, bool instant = false)
        {
            var previous = _state;
            _state = newState;

            var inGame = newState == FlowState.Countdown || newState == FlowState.Racing;
            var showHud = inGame || newState == FlowState.Results;

            // Entering the race view after a menu page: the horses were re-gridded while
            // nobody was watching, so start the chase rig already seated behind them.
            if (showHud && !ShowsRaceView(previous))
                RaceCameraTarget.SnapAllAfterTeleport();

            if (newState == FlowState.InstructionsPage)
                RefreshInstructions();

            // Leaving Results clears the verdict wording so the plates read as plain
            // names again on the next match.
            if (newState != FlowState.Results && _showingVerdict)
            {
                _showingVerdict = false;
                RestoreHighlightColors();
                ResetNameplateScales();
            }

            if (!_showingVerdict)
            {
                if (newState == FlowState.Countdown && _nameplatesLayoutReady)
                    ApplyNameTexts();
                else
                    RefreshPlayerNames();
            }

            if (newState == FlowState.Countdown)
                _nameplatesLayoutReady = false;

            if (newState != FlowState.Results)
            {
                SetLeaderboardVisible(false, instant);
                HidePlayerTimePills();
            }

            if (player1Nameplate)
                player1Nameplate.gameObject.SetActive(showHud);
            if (player2Nameplate)
                player2Nameplate.gameObject.SetActive(showHud && !Solo);

            // Keep the live race view up; the verdict lands on the nameplates that are
            // already there rather than on a menu page over the top of the race.
            TransitionPages(
                menuBackgroundCG, newState == FlowState.InstructionsPage,
                startPageCG, newState == FlowState.StartPage,
                instructionsPageCG, newState == FlowState.InstructionsPage,
                gameHudCG, showHud,
                countdownCG, newState == FlowState.Countdown,
                instant);

            if (inGame && previous != FlowState.Countdown && previous != FlowState.Racing)
                PlayNameplateIntro(instant || newState == FlowState.Countdown);
        }

        static bool ShowsRaceView(FlowState state) =>
            state == FlowState.Countdown ||
            state == FlowState.Racing ||
            state == FlowState.Results;

        void TransitionPages(
            CanvasGroup menuBg, bool menuBgOn,
            CanvasGroup start, bool startOn,
            CanvasGroup instructions, bool instructionsOn,
            CanvasGroup hud, bool hudOn,
            CanvasGroup countdown, bool countdownOn,
            bool instant)
        {
            KillPageSequence();

            if (instant || fadeDuration <= 0f)
            {
                ApplyCgInstant(menuBg, menuBgOn);
                ApplyCgInstant(start, startOn);
                ApplyCgInstant(instructions, instructionsOn);
                ApplyCgInstant(hud, hudOn);
                ApplyCgInstant(countdown, countdownOn);
                return;
            }

            _pageSequence = DOTween.Sequence().SetUpdate(true);

            void FadeOut(CanvasGroup cg, bool shouldShow)
            {
                if (!cg || shouldShow) return;
                if (cg.alpha <= 0.001f)
                {
                    ApplyCgInstant(cg, false);
                    return;
                }

                cg.interactable = false;
                cg.blocksRaycasts = false;
                _pageSequence.Join(
                    cg.DOFade(0f, fadeDuration * 0.85f)
                        .SetEase(fadeOutEase));
            }

            void FadeIn(CanvasGroup cg, bool shouldShow, float delay)
            {
                if (!cg || !shouldShow) return;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                if (cg.alpha >= 0.999f)
                {
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    return;
                }

                _pageSequence.Insert(
                    delay,
                    cg.DOFade(1f, fadeDuration)
                        .SetEase(fadeInEase)
                        .OnStart(() =>
                        {
                            cg.gameObject.SetActive(true);
                        })
                        .OnComplete(() =>
                        {
                            cg.interactable = true;
                            cg.blocksRaycasts = true;
                        }));
            }

            FadeOut(start, startOn);
            FadeOut(instructions, instructionsOn);
            FadeOut(menuBg, menuBgOn);
            FadeOut(countdown, countdownOn);
            FadeOut(hud, hudOn);

            var inDelay = fadeDuration * 0.35f + pageStagger;
            FadeIn(menuBg, menuBgOn, inDelay);
            FadeIn(start, startOn, inDelay);
            FadeIn(instructions, instructionsOn, inDelay + pageStagger);
            FadeIn(hud, hudOn, inDelay);
            FadeIn(countdown, countdownOn, inDelay + pageStagger * 0.5f);
        }

        void CacheNameplateRestPositions()
        {
            if (player1Nameplate)
                _player1NameplateRestY = player1Nameplate.anchoredPosition.y;
            if (player2Nameplate)
                _player2NameplateRestY = player2Nameplate.anchoredPosition.y;
            _nameplateRestCached = true;
        }

        static void ApplyCountdownShadow(TMP_Text text)
        {
            if (!text) return;

            // Offset shade under the count, matching the countdown key art.
            var mat = text.fontMaterial;
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.08f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.16f, 0.24f, 0.28f, 0.45f));
            text.fontMaterial = mat;
        }

        void PlayNameplateIntro(bool instant)
        {
            if (!_nameplateRestCached)
                CacheNameplateRestPositions();

            AnimateNameplate(player1Nameplate, _player1NameplateRestY, instant, 0f);
            AnimateNameplate(player2Nameplate, _player2NameplateRestY, instant, pageStagger);
        }

        void AnimateNameplate(RectTransform plate, float restY, bool instant, float delay)
        {
            if (!plate) return;

            plate.DOKill();
            var x = plate.anchoredPosition.x;
            if (instant || nameplateIntroDuration <= 0f)
            {
                plate.anchoredPosition = new Vector2(x, restY);
                plate.localScale = Vector3.one;
                return;
            }

            plate.anchoredPosition = new Vector2(x, restY + nameplateIntroOffsetY);
            plate.localScale = Vector3.one * 0.96f;
            DOTween.Sequence()
                .SetDelay(delay)
                .SetUpdate(true)
                .Append(plate.DOAnchorPosY(restY, nameplateIntroDuration).SetEase(nameplateIntroEase))
                .Join(plate.DOScale(1f, nameplateIntroDuration).SetEase(nameplateIntroEase))
                .SetTarget(plate);
        }

        static void ApplyCgInstant(CanvasGroup cg, bool visible)
        {
            if (!cg) return;
            cg.DOKill();
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        void KillPageSequence()
        {
            _pageSequence?.Kill();
            _pageSequence = null;
            menuBackgroundCG?.DOKill();
            startPageCG?.DOKill();
            instructionsPageCG?.DOKill();
            gameHudCG?.DOKill();
            countdownCG?.DOKill();
        }

        void StopFlow(bool disableAllInput = true)
        {
            if (_flowCoroutine != null)
            {
                StopCoroutine(_flowCoroutine);
                _flowCoroutine = null;
            }

            if (disableAllInput)
                SetDriversInputEnabled(false);
        }
    }
}
