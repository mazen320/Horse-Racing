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
        float _player1Time = -1f;
        float _player2Time = -1f;
        Coroutine _flowCoroutine;
        Sequence _pageSequence;
        float _player1NameplateRestY;
        float _player2NameplateRestY;
        bool _nameplateRestCached;

        void Awake()
        {
            ResolveRaceDrivers();
            CacheNameplateRestPositions();
            ApplyCountdownShadow(countdownText);

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

            RefreshPlayerNames();
            ApplyState(FlowState.StartPage, true);
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
            RefreshPlayerNames();
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
            RefreshPlayerNames();
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
            if (player1Nameplate) player1Nameplate.DOKill();
            if (player2Nameplate) player2Nameplate.DOKill();
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
            if (_state != FlowState.Racing || _player1Time >= 0f)
                return;

            _player1Time = Time.time - _raceStartTime;
            raceDriver?.SetRaceInputEnabled(false);
            if (_player2Time >= 0f || !raceDriverP2)
                ApplyState(FlowState.StartPage);
        }

        void OnDriver2RaceFinished()
        {
            if (_state != FlowState.Racing || _player2Time >= 0f)
                return;

            _player2Time = Time.time - _raceStartTime;
            raceDriverP2?.SetRaceInputEnabled(false);
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

        void RefreshPlayerNames()
        {
            if (player1NameText)
                player1NameText.text = string.IsNullOrWhiteSpace(player1Name) ? "PLAYER 1" : player1Name.ToUpperInvariant();
            if (player2NameText)
                player2NameText.text = string.IsNullOrWhiteSpace(player2Name) ? "PLAYER 2" : player2Name.ToUpperInvariant();

            FitNameplate(player1NameText, player1Underline, player1Plate);
            FitNameplate(player2NameText, player2Underline, player2Plate);
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

        static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        void ApplyState(FlowState newState, bool instant = false)
        {
            var previous = _state;
            _state = newState;

            var inGame = newState == FlowState.Countdown || newState == FlowState.Racing;

            if (newState == FlowState.InstructionsPage)
                RefreshInstructions();

            RefreshPlayerNames();

            // Start page ships as a full-screen art export; beige menu BG is instructions-only.
            TransitionPages(
                menuBackgroundCG, newState == FlowState.InstructionsPage,
                startPageCG, newState == FlowState.StartPage,
                instructionsPageCG, newState == FlowState.InstructionsPage,
                gameHudCG, inGame,
                countdownCG, newState == FlowState.Countdown,
                instant);

            if (inGame && previous != FlowState.Countdown && previous != FlowState.Racing)
                PlayNameplateIntro(instant);
        }

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
