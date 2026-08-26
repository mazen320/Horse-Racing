using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing.Race
{
    /// <summary>
    /// Releases the starting gate. Each stall in the gate prefab carries its own
    /// Animator with an authored open clip, so opening just fires the trigger.
    /// Closing has to restore the panels by hand: the controller's idle state holds
    /// no clip, so leaving it there would freeze the gate wide open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartGateDoors : MonoBehaviour
    {
        [Tooltip("Gate prefab root to search. Falls back to this transform.")]
        [SerializeField] Transform gateRoot;
        [Tooltip("Trigger parameter on the stall Animator controller.")]
        [SerializeField] string openTrigger = "Open";
        [Tooltip("Clipless idle state used to stop the open clip driving the panels.")]
        [SerializeField] string closedStateName = "New State";
        [SerializeField] string leftPanelName = "Door_L";
        [SerializeField] string rightPanelName = "Door_R";

        readonly List<Animator> _stalls = new();
        readonly List<Panel> _panels = new();
        bool _captured;

        struct Panel
        {
            public Transform Transform;
            public Vector3 RestLocalPosition;
            public Quaternion RestLocalRotation;
        }

        public int StallCount
        {
            get
            {
                Capture();
                return _stalls.Count;
            }
        }

        public int PanelCount
        {
            get
            {
                Capture();
                return _panels.Count;
            }
        }

        void Awake() => Capture();

        /// <summary>Fires the authored swing on every stall.</summary>
        public void Open()
        {
            Capture();

            for (var i = 0; i < _stalls.Count; i++)
            {
                var stall = _stalls[i];
                if (!stall || !stall.runtimeAnimatorController) continue;

                stall.ResetTrigger(openTrigger);
                stall.SetTrigger(openTrigger);
            }
        }

        /// <summary>Snaps every stall shut so the next race starts from a closed gate.</summary>
        public void Close()
        {
            Capture();

            for (var i = 0; i < _stalls.Count; i++)
            {
                var stall = _stalls[i];
                if (!stall || !stall.runtimeAnimatorController) continue;

                stall.ResetTrigger(openTrigger);
                stall.Play(closedStateName, 0, 0f);

                // Evaluate now so the open clip stops writing to the panels before
                // the rest pose below is restored.
                if (Application.isPlaying)
                    stall.Update(0f);
            }

            for (var i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                if (!panel.Transform) continue;

                panel.Transform.localPosition = panel.RestLocalPosition;
                panel.Transform.localRotation = panel.RestLocalRotation;
            }
        }

        void Capture()
        {
            if (_captured) return;
            _captured = true;

            var root = gateRoot ? gateRoot : transform;

            _stalls.Clear();
            root.GetComponentsInChildren(true, _stalls);

            // Awake runs before the Animators pose anything, so whatever the panels
            // read now is the authored closed pose.
            _panels.Clear();
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != leftPanelName && candidate.name != rightPanelName)
                    continue;

                _panels.Add(new Panel
                {
                    Transform = candidate,
                    RestLocalPosition = candidate.localPosition,
                    RestLocalRotation = candidate.localRotation
                });
            }
        }
    }
}
