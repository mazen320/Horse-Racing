using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace HorseRacing.Registration
{
    /// <summary>
    /// Bridges Registration tablet TCP messages to the NACD horse-racing UI flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RegistrationGameBridge : MonoBehaviour
    {
        [SerializeField] RegistrationTcpServer server;
        [SerializeField] HorseRacing.UI.NacdEnergizingUIManager uiManager;
        [SerializeField] bool skipToInstructionsOnRegister = true;
        [SerializeField] bool autoStartRaceOnStartCommand = true;
        [SerializeField] bool appendRegistrationCsv = true;
        [SerializeField] float clientLossGraceSeconds = 25f;

        string _csvPath;
        Coroutine _clientLossReset;

        void Awake()
        {
            if (!server)
                server = GetComponent<RegistrationTcpServer>();
            if (!uiManager)
                uiManager = FindAnyObjectByType<HorseRacing.UI.NacdEnergizingUIManager>();

            _csvPath = Path.Combine(Application.persistentDataPath, "RegisteredUser.txt");
        }

        void OnEnable()
        {
            if (!server)
                return;

            server.RegistrationReceived += OnRegistrationReceived;
            server.StartCommandReceived += OnStartCommand;
            server.RestartCommandReceived += OnRestart;
            server.EndGameCommandReceived += OnEndGame;
            server.NewRaceCommandReceived += OnNewRace;
            server.ClientConnectionChanged += OnClientConnectionChanged;

            if (uiManager)
                uiManager.RaceStarted += OnRaceStarted;
            if (uiManager)
                uiManager.RaceEnded += OnRaceEnded;
        }

        void OnDisable()
        {
            CancelClientLossReset();

            if (!server)
                return;

            server.RegistrationReceived -= OnRegistrationReceived;
            server.StartCommandReceived -= OnStartCommand;
            server.RestartCommandReceived -= OnRestart;
            server.EndGameCommandReceived -= OnEndGame;
            server.NewRaceCommandReceived -= OnNewRace;
            server.ClientConnectionChanged -= OnClientConnectionChanged;

            if (uiManager)
            {
                uiManager.RaceStarted -= OnRaceStarted;
                uiManager.RaceEnded -= OnRaceEnded;
            }
        }

        void OnRaceStarted(long raceStartUtcTicks)
        {
            server?.BroadcastRaceStarted(raceStartUtcTicks);
        }

        void OnRaceEnded(long raceEndUtcTicks)
        {
            server?.BroadcastRaceEnded(raceEndUtcTicks);
        }

        void OnRegistrationReceived(RegisterEntryData data)
        {
            if (!uiManager || data?.entries == null || data.entries.Count == 0)
                return;

            var p1 = FindName(data, 1) ?? data.entries[0].name;
            var solo = data.entries.Count < 2;
            var p2 = solo
                ? "PLAYER 2"
                : FindName(data, 2) ?? data.entries[1].name;

            uiManager.ApplyTabletRegistration(p1, p2, skipToInstructionsOnRegister, solo ? 1 : 2);

            if (!appendRegistrationCsv)
                return;

            foreach (var entry in data.entries)
            {
                entry.SetTime();
                RegistrationCsvUtil.AppendRegistrationRow(_csvPath, entry.GetCsv());
            }
        }

        void OnStartCommand()
        {
            if (!uiManager)
                return;

            if (autoStartRaceOnStartCommand)
                uiManager.ApplyTabletStartRace();
            else
                uiManager.ApplyTabletShowInstructions();
        }

        void OnRestart()
        {
            // No restart echo back to the sender — the tablet already reset itself locally.
            uiManager?.ApplyTabletRestart();
        }

        void OnEndGame()
        {
            uiManager?.ApplyTabletRestart();
        }

        void OnNewRace()
        {
            // Same players: reset the field but keep names and go back to the post-register page.
            uiManager?.ApplyTabletNewRace(skipToInstructionsOnRegister);
        }

        void OnClientConnectionChanged(bool connected)
        {
            if (!uiManager)
                return;

            if (connected)
            {
                CancelClientLossReset();
                return;
            }

            // A tablet that reconnects (Wi-Fi blip, app resume) must not wipe the flow, so wait out
            // the grace period first and only fall back to idle if it never came back.
            if (_clientLossReset == null)
                _clientLossReset = StartCoroutine(ResetAfterClientLoss());
        }

        IEnumerator ResetAfterClientLoss()
        {
            yield return new WaitForSecondsRealtime(clientLossGraceSeconds);

            _clientLossReset = null;

            if (server && server.HasConnectedClient)
                yield break;

            uiManager?.ApplyTabletRestart();
        }

        void CancelClientLossReset()
        {
            if (_clientLossReset == null)
                return;

            StopCoroutine(_clientLossReset);
            _clientLossReset = null;
        }

        static string FindName(RegisterEntryData data, int userIndex)
        {
            foreach (var entry in data.entries)
            {
                if (entry.userIndex == userIndex && !string.IsNullOrWhiteSpace(entry.name))
                    return entry.name.Trim();
            }

            return null;
        }
    }
}
