using UnityEngine;
using UTool.TabSystem;

namespace HorseRacing.UI
{
    /// <summary>
    /// Tab-panel control to archive the live board to CSV and clear it for a new event day.
    /// </summary>
    [HasTabField]
    public sealed class RaceLeaderboardAdmin : MonoBehaviour
    {
        [SerializeField] NacdEnergizingUIManager uiManager;

        void Awake()
        {
            if (!uiManager)
                uiManager = FindAnyObjectByType<NacdEnergizingUIManager>();
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.L))
                ClearLeaderboard();
        }

        [TabButton]
        public void ClearLeaderboard()
        {
            if (!uiManager)
            {
                Debug.LogWarning("RaceLeaderboardAdmin: no NacdEnergizingUIManager assigned.");
                return;
            }

            uiManager.ClearLeaderboardWithCsvBackup();
        }
    }
}
