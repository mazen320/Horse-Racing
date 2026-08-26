using System.Collections;
using DG.Tweening;
using HorseRacing.Race;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// Wire pages and HUD references on the Canvas in the Inspector.
    /// Flow: StartPage → InstructionsPage → game HUD (countdown → race).
    /// </summary>
    public sealed class NacdEnergizingUIManager : MonoBehaviour
    {
        enum FlowState
        {
            StartPage,
            InstructionsPage,
            Countdown,
            Racing
        }

        [Header("Race")]
        [SerializeField] RaceSplineTapDriver raceDriver;
        [SerializeField] RaceSplineTapDriver raceDriverP2;

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

        [Header("Player 1 header")]
        [SerializeField] Image player1HeaderBg;
        [SerializeField] TMP_Text player1NameText;
        [SerializeField] TMP_Text player1StatusText;

        [Header("Player 2 header")]
        [SerializeField] Image player2HeaderBg;
        [SerializeField] TMP_Text player2NameText;
        [SerializeField] TMP_Text player2StatusText;

        [Header("Player names")]
        [SerializeField] string player1Name = "PLAYER 1";
        [SerializeField] string player2Name = "PLAYER 2";

        [Header("Transitions")]
        [SerializeField] float fadeDuration = 0.35f;
        [SerializeField] Ease fadeEase = Ease.OutQuad;

        [Header("Countdown timing")]
        [SerializeField] float countdownSeconds = 3f;
        [SerializeField] float goHoldSeconds = 0.45f;
        [SerializeField] float countdownPopScale = 1.15f;
        [SerializeField] float countdownPopDuration = 0.22f;
        [SerializeField] Ease countdownPopEase = Ease.OutBack;

        [Header("Header colours")]
        [SerializeField] Color playerActiveColor = new(1f, 0.78f, 0.28f, 1f);
        [SerializeField] Color playerInactiveColor = new(1f, 1f, 1f, 0.42f);
        [SerializeField] Color playerActiveBgColor = new(1f, 0.78f, 0.28f, 0.14f);
        [SerializeField] Color playerInactiveBgColor = new(1f, 1f, 1f, 0.04f);

        FlowState _state = FlowState.StartPage;
        float _raceStartTime;
        float _player1Time = -1f;
        float _player2Time = -1f;
        Coroutine _flowCoroutine;

        void Awake()
        {
            ResolveRaceDrivers();

            WireButton(startContinueButton, OnStartContinue);
            WireButton(instructionsStartButton, OnInstructionsStart);

            if (raceDriver)
            {
                raceDriver.onRaceFinished.AddListener(OnDriver1RaceFinished);
                raceDriver.SetRaceInputEnabled(false);
            }

            if (raceDriverP2)
            {
                raceDriverP2.onRaceFinished.AddListener(OnDriver2RaceFinished);
                raceDriverP2.SetRaceInputEnabled(false);
            }

            RefreshPlayerHeader();
            ApplyState(FlowState.StartPage, true);
        }

        void ResolveRaceDrivers()
        {
            if (!raceDriver || !raceDriverP2)
            {
                var drivers = FindObjectsByType<RaceSplineTapDriver>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        void Update()
        {
            if (_state == FlowState.Racing && raceTimerText)
                raceTimerText.text = FormatTime(Time.time - _raceStartTime);
        }

        public void OnStartContinue() => ApplyState(FlowState.InstructionsPage);

        public void OnInstructionsStart() => StartMatch();

        public void StartMatch()
        {
            StopFlow();
            _player1Time = -1f;
            _player2Time = -1f;
            RefreshPlayerHeader();
            _flowCoroutine = StartCoroutine(RunSplitScreenRace());
        }

        IEnumerator RunSplitScreenRace()
        {
            RestartDrivers(inputEnabled: false);

            ApplyState(FlowState.Countdown);
            yield return RunCountdown();

            SetDriversInputEnabled(true);
            _raceStartTime = Time.time;

            ApplyState(FlowState.Racing);
            _flowCoroutine = null;
        }

        public void ApplyTabletRegistration(string player1, string player2, bool showInstructions)
        {
            SetPlayerNames(player1, player2);
            ApplyState(showInstructions ? FlowState.InstructionsPage : FlowState.StartPage);
        }

        public void ApplyTabletShowInstructions() => ApplyState(FlowState.InstructionsPage);

        public void ApplyTabletStartRace() => StartMatch();

        public void ApplyTabletRestart()
        {
            StopFlow();
            _player1Time = -1f;
            _player2Time = -1f;
            RestartDrivers(inputEnabled: false);
            ApplyState(FlowState.StartPage, true);
        }

        public void SetPlayerNames(string player1, string player2)
        {
            if (!string.IsNullOrWhiteSpace(player1))
                player1Name = player1.Trim();
            if (!string.IsNullOrWhiteSpace(player2))
                player2Name = player2.Trim();
            RefreshPlayerHeader();
        }

        void KillTweens()
        {
            menuBackgroundCG?.DOKill();
            startPageCG?.DOKill();
            instructionsPageCG?.DOKill();
            gameHudCG?.DOKill();
            countdownCG?.DOKill();
            countdownText?.transform.DOKill();
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
            t.localScale = Vector3.one;
            t.DOScale(countdownPopScale, countdownPopDuration)
                .SetEase(countdownPopEase)
                .SetLoops(2, LoopType.Yoyo)
                .SetTarget(t);
        }

        void OnDriver1RaceFinished()
        {
            if (_state != FlowState.Racing || _player1Time >= 0f)
                return;

            _player1Time = Time.time - _raceStartTime;
            raceDriver?.SetRaceInputEnabled(false);
            RefreshPlayerHeader();
            if (_player2Time >= 0f || !raceDriverP2)
                ApplyState(FlowState.StartPage);
        }

        void OnDriver2RaceFinished()
        {
            if (_state != FlowState.Racing || _player2Time >= 0f)
                return;

            _player2Time = Time.time - _raceStartTime;
            raceDriverP2?.SetRaceInputEnabled(false);
            RefreshPlayerHeader();
            if (_player1Time >= 0f || !raceDriver)
                ApplyState(FlowState.StartPage);
        }

        void RestartDrivers(bool inputEnabled)
        {
            if (raceDriver)
            {
                raceDriver.RestartRace();
                raceDriver.SetRaceInputEnabled(inputEnabled);
            }

            if (raceDriverP2)
            {
                raceDriverP2.RestartRace();
                raceDriverP2.SetRaceInputEnabled(inputEnabled);
            }
        }

        void SetDriversInputEnabled(bool enabled)
        {
            raceDriver?.SetRaceInputEnabled(enabled);
            raceDriverP2?.SetRaceInputEnabled(enabled);
        }

        void RefreshInstructions()
        {
            if (instructionsBodyText)
                instructionsBodyText.text = instructionsCopy;
        }

        void RefreshPlayerHeader()
        {
            if (player1NameText) player1NameText.text = player1Name;
            if (player2NameText) player2NameText.text = player2Name;

            SetPlayerSlot(0, player1HeaderBg, player1NameText, player1StatusText, _player1Time);
            SetPlayerSlot(1, player2HeaderBg, player2NameText, player2StatusText, _player2Time);
        }

        void SetPlayerSlot(int index, Image bg, TMP_Text name, TMP_Text status, float finishTime)
        {
            var inRace = _state == FlowState.Countdown || _state == FlowState.Racing;

            if (bg)
                bg.color = inRace ? playerActiveBgColor : playerInactiveBgColor;

            if (name)
            {
                name.color = inRace ? playerActiveColor : playerInactiveColor;
                name.fontStyle = inRace ? FontStyles.Bold : FontStyles.Normal;
            }

            if (!status) return;

            if (finishTime >= 0f)
            {
                status.text = FormatTime(finishTime);
                status.color = playerActiveColor;
                return;
            }

            if (inRace)
            {
                status.text = _state == FlowState.Countdown ? "GET READY" : "RACING";
                status.color = playerActiveColor;
                return;
            }

            status.text = "READY";
            status.color = playerInactiveColor;
        }

        static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        void ApplyState(FlowState newState, bool instant = false)
        {
            _state = newState;

            var inGame = newState == FlowState.Countdown || newState == FlowState.Racing;

            // Start page ships as a full-screen art export; beige menu BG is instructions-only.
            SetCg(menuBackgroundCG, newState == FlowState.InstructionsPage, instant);
            SetCg(startPageCG, newState == FlowState.StartPage, instant);
            SetCg(instructionsPageCG, newState == FlowState.InstructionsPage, instant);
            SetCg(gameHudCG, inGame, instant);
            SetCg(countdownCG, newState == FlowState.Countdown, instant);

            if (newState == FlowState.InstructionsPage)
                RefreshInstructions();

            RefreshPlayerHeader();
        }

        void SetCg(CanvasGroup cg, bool visible, bool instant = false)
        {
            if (!cg) return;

            cg.DOKill();
            var targetAlpha = visible ? 1f : 0f;

            if (instant || fadeDuration <= 0f)
            {
                cg.alpha = targetAlpha;
                cg.interactable = visible;
                cg.blocksRaycasts = visible;
                return;
            }

            cg.interactable = visible;
            cg.blocksRaycasts = visible;
            cg.DOFade(targetAlpha, fadeDuration)
                .SetEase(fadeEase)
                .SetTarget(cg);
        }

        void StopFlow()
        {
            if (_flowCoroutine != null)
            {
                StopCoroutine(_flowCoroutine);
                _flowCoroutine = null;
            }

            SetDriversInputEnabled(false);
        }
    }
}
