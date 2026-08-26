using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using UTool;
using UTool.PageSystem;

namespace UTool.Editor
{
    public class PageSystemContextMenu : MonoBehaviour
    {
        private const string pagePath = "Assets/UUtility/PrefabModules/PageSystem/Page/Page.prefab";

        [MenuItem("GameObject/UT/Page", false, 100)]
        private static void CreatePage(MenuCommand command)
        {
            GameObject utObject = UTContextMenuUtility.CreateAssetFromPath(command, pagePath);
            Page ut = utObject.GetComponent<Page>();
        }
    }
}