using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using UTool;
using UTool.PageSystem;

namespace UTool.Editor
{
    public class LeaderboardContextMenu
    {
        private const string workingPath = "Assets/UUtility/PrefabModules/Leaderboard";
        private static string leaderboardPath => $"{workingPath}/LeaderboardPage.prefab";

        [MenuItem("GameObject/UT/Leaderboard", false, 205)]
        private static void CreateLeaderboardPage(MenuCommand command)
        {
            GameObject osk = UTContextMenuUtility.CreateAssetFromPath(command, leaderboardPath, prefabLink: false);
        }
    }
}