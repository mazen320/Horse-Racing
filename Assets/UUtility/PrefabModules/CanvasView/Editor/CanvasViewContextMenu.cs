using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using UTool;
using UTool.PageSystem;

namespace UTool.Editor
{
    public class CanvasViewContextMenu
    {
        private const string workingPath = "Assets/UUtility/PrefabModules/CanvasView";
        private static string leaderboardPath => $"{workingPath}/CanvasView.prefab";

        [MenuItem("GameObject/UT/CanvasView", false, 206)]
        private static void CreateLeaderboardPage(MenuCommand command)
        {
            GameObject osk = UTContextMenuUtility.CreateAssetFromPath(command, leaderboardPath, prefabLink: false);
        }
    }
}