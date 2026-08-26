using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UTool;
using UTool.Utility;

public class CanvasView : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Canvas canvas;
    [SpaceArea]
    [SerializeField] public RenderTexture renderTexture;
    [SerializeField] private Texture2D capturedTexture;

    private void OnValidate()
    {
        if (!renderTexture)
            return;

        viewCamera.targetTexture = renderTexture;
        viewCamera.RecordPrefabChanges();
        this.gameObject.ForceRecordPrefabChanges();
    }

    public void Capture(Action<Texture2D, string> capturedCallback)
    {
        StartCoroutine(CaptureCamera(capturedCallback));
    }

    IEnumerator CaptureCamera(Action<Texture2D, string> capturedCallback)
    {
        viewCamera.gameObject.SetActive(true);

        yield return new WaitForEndOfFrame();

        capturedTexture = renderTexture.ToTexture2D();
        viewCamera.gameObject.SetActive(false);

        string folderPath = $@"{UT.dataPath}\Images\Poloroid";
        string fileName = $@"Poloroid_{DateTime.Now.ToddMMyyyyhhmmss()}.png";
        string path = $@"{folderPath}\{fileName}";

        UUtility.CheckAndCreateDirectory(folderPath);
        File.WriteAllBytes(path, capturedTexture.EncodeToPNG());

        capturedCallback?.Invoke(capturedTexture, path);
    }

    private void OnDestroy()
    {
        if (capturedTexture)
            Destroy(capturedTexture);
    }
}
