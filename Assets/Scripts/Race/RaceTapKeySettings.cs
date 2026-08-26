using UnityEngine;
using UTool.TabSystem;

namespace HorseRacing.Race
{
    /// <summary>
    /// Exposes per-player tap keys in the UTool panel. Key text is parsed case-insensitively
    /// so "a", "A", and "space" all bind correctly at runtime.
    /// </summary>
    [HasTabField]
    public sealed class RaceTapKeySettings : MonoBehaviour
    {
        [SerializeField] RaceSplineTapDriver player1Driver;
        [SerializeField] RaceSplineTapDriver player2Driver;

        [TabField(nameof(ApplyTapKeys))]
        [SerializeField] string player1TapKey = "A";

        [TabField(nameof(ApplyTapKeys))]
        [SerializeField] string player2TapKey = "L";

        void Awake()
        {
            if (!player1Driver || !player2Driver)
                AutoAssignDrivers();

            SyncFromDrivers();
        }

        void Start() => ApplyTapKeys();

        public void ApplyTapKeys() => ApplyTapKeys(VariableUpdateType.Applied);

        public void ApplyTapKeys(VariableUpdateType updateType)
        {
            ApplyToDriver(player1Driver, player1TapKey, "Player 1");
            ApplyToDriver(player2Driver, player2TapKey, "Player 2");
        }

        void SyncFromDrivers()
        {
            if (player1Driver)
                player1TapKey = player1Driver.GetPrimaryTapKeyLabel();
            if (player2Driver)
                player2TapKey = player2Driver.GetPrimaryTapKeyLabel();
        }

        void AutoAssignDrivers()
        {
            var drivers = FindObjectsByType<RaceSplineTapDriver>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

            for (var i = 0; i < drivers.Length; i++)
            {
                if (!player1Driver && drivers[i].name.Contains("P2") == false)
                    player1Driver = drivers[i];
                else if (!player2Driver && drivers[i].name.Contains("P2"))
                    player2Driver = drivers[i];
            }

            if (!player1Driver && drivers.Length > 0)
                player1Driver = drivers[0];
            if (!player2Driver && drivers.Length > 1)
                player2Driver = drivers[1];
        }

        static void ApplyToDriver(RaceSplineTapDriver driver, string keyText, string label)
        {
            if (!driver)
            {
                Debug.LogWarning($"RaceTapKeySettings: no driver assigned for {label}.");
                return;
            }

            if (!driver.SetPrimaryTapKey(keyText))
                Debug.LogWarning($"RaceTapKeySettings: '{keyText}' is not a valid tap key for {label}.", driver);
        }
    }
}
