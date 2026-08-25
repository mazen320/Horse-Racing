using HorseRacing.Race;
using UnityEditor;
using UnityEngine;

namespace HorseRacing.Editor
{
    [CustomEditor(typeof(TripoJockeyVisualMirror))]
    public sealed class TripoJockeyVisualMirrorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var mirror = (TripoJockeyVisualMirror)target;
            EditorGUILayout.Space(8);

            if (!mirror.DriveTransformsFromInspector)
            {
                EditorGUILayout.HelpBox(
                    "Manual mode: rotate JockeyVisual / mesh children with the Scene gizmo. " +
                    "Use Capture when you're happy, then turn Drive Transforms back on to lock values.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Inspector drives rotation. Change Mount / Mirror euler fields above, or unlock to use the Scene gizmo.",
                    MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Offsets Now"))
                mirror.ApplyVisualOffsets();

            if (GUILayout.Button("Capture From Scene"))
                mirror.CaptureSceneTransforms();
            EditorGUILayout.EndHorizontal();
        }
    }
}
