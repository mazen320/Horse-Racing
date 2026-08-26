using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Switches the race between one full-screen view and the two-up split.
    /// Solo runs hide the divider and the second actor entirely so nothing
    /// stray is left standing in the gate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceViewLayout : MonoBehaviour
    {
        [Header("Cameras")]
        [SerializeField] Camera player1Camera;
        [SerializeField] Camera player2Camera;

        [Header("Split dressing")]
        [SerializeField] GameObject splitDivider;

        [Header("Second actor")]
        [SerializeField] GameObject player2Horse;
        [SerializeField] GameObject player2Rider;

        static readonly Rect FullView = new(0f, 0f, 1f, 1f);
        static readonly Rect LeftHalf = new(0f, 0f, 0.5f, 1f);
        static readonly Rect RightHalf = new(0.5f, 0f, 0.5f, 1f);

        public bool IsSolo { get; private set; }

        public void Apply(int playerCount)
        {
            IsSolo = playerCount <= 1;

            if (player1Camera)
                player1Camera.rect = IsSolo ? FullView : LeftHalf;

            if (player2Camera)
            {
                player2Camera.rect = RightHalf;
                player2Camera.gameObject.SetActive(!IsSolo);
            }

            if (splitDivider)
                splitDivider.SetActive(!IsSolo);

            if (player2Horse)
                player2Horse.SetActive(!IsSolo);

            if (player2Rider)
                player2Rider.SetActive(!IsSolo);
        }
    }
}
