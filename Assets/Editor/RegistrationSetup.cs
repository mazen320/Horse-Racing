using HorseRacing.Registration;
using HorseRacing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorseRacing.Registration.Editor
{
    public static class RegistrationSetup
    {
        const string BridgeName = "RegistrationBridge";

        [MenuItem("Horse Racing/Setup Registration TCP Bridge")]
        public static void SetupBridge()
        {
            var bridge = GameObject.Find(BridgeName);
            if (!bridge)
                bridge = new GameObject(BridgeName);

            var server = bridge.GetComponent<RegistrationTcpServer>();
            if (!server)
                server = bridge.AddComponent<RegistrationTcpServer>();

            var gameBridge = bridge.GetComponent<RegistrationGameBridge>();
            if (!gameBridge)
                gameBridge = bridge.AddComponent<RegistrationGameBridge>();

            var ui = Object.FindAnyObjectByType<NacdEnergizingUIManager>();
            if (!ui)
            {
                Debug.LogError("[RegistrationSetup] NacdEnergizingUIManager not found in scene.");
                return;
            }

            var serverSo = new SerializedObject(server);
            serverSo.FindProperty("autoStart").boolValue = true;
            serverSo.FindProperty("listenAddress").stringValue = "0.0.0.0";
            serverSo.FindProperty("port").intValue = 1234;
            serverSo.FindProperty("discoveryPort").intValue = 3738;
            serverSo.FindProperty("logTraffic").boolValue = true;
            serverSo.ApplyModifiedPropertiesWithoutUndo();

            var bridgeSo = new SerializedObject(gameBridge);
            bridgeSo.FindProperty("server").objectReferenceValue = server;
            bridgeSo.FindProperty("uiManager").objectReferenceValue = ui;
            bridgeSo.FindProperty("skipToInstructionsOnRegister").boolValue = true;
            bridgeSo.FindProperty("autoStartRaceOnStartCommand").boolValue = true;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(bridge.scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log(
                "[RegistrationSetup] Registration bridge ready. " +
                "Game listens TCP :1234 (server), UDP discovery :3738. " +
                "Point Registration tablet client to this machine (Automatic discovery or same LAN IP).");
        }
    }
}
