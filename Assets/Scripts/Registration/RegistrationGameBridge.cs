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

        string _csvPath;

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
        }

        void OnDisable()
        {
            if (!server)
                return;

            server.RegistrationReceived -= OnRegistrationReceived;
            server.StartCommandReceived -= OnStartCommand;
            server.RestartCommandReceived -= OnRestart;
            server.EndGameCommandReceived -= OnEndGame;
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
                File.AppendAllText(_csvPath, entry.GetCsv() + System.Environment.NewLine);
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
            uiManager?.ApplyTabletRestart();
            server?.SendRestart();
        }

        void OnEndGame()
        {
            uiManager?.ApplyTabletRestart();
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
