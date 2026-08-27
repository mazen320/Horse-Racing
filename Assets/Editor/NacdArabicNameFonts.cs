#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HorseRacing.Race.Editor
{
    /// <summary>
    /// Builds IBM Plex Sans Arabic TMP fonts and wires them as fallbacks on nameplate fonts
    /// so Latin Antipol stays primary while Arabic names render smoothly.
    /// </summary>
    public static class NacdArabicNameFonts
    {
        const string ArabicBoldOt = "Assets/Branding/Fonts/IBM_Plex_Sans_Arabic/IBMPlexSansArabic-Bold.otf";
        const string ArabicRegularOt = "Assets/Branding/Fonts/IBM_Plex_Sans_Arabic/IBMPlexSansArabic-Regular.otf";
        const string TmpDir = "Assets/Branding/Fonts/TMP";
        const string BoldSdfName = "NacdIBMPlexSansArabic-Bold SDF";
        const string RegularSdfName = "NacdIBMPlexSansArabic-Regular SDF";
        const string AntipolSdf = TmpDir + "/NacdAntipol-Bold SDF.asset";
        const string IbmSansSdf = TmpDir + "/NacdIBMPlexSans-Regular SDF.asset";

        const string SeedChars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?-'\"\n" +
            "ابتثجحخدذرزسشصضطظعغفقكلمنهويءآأؤإئىة٠١٢٣٤٥٦٧٨٩";

        [MenuItem("Horse Racing/Setup Arabic Name Fonts")]
        public static void Setup()
        {
            AssetDatabase.Refresh();

            var arabicBold = EnsureTmpFont(ArabicBoldOt, BoldSdfName);
            var arabicRegular = EnsureTmpFont(ArabicRegularOt, RegularSdfName);
            var segoeArabic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/UUtility/Plugins/RTLTMPro/Fonts/segoeui SDF Arabic.asset");
            if (!arabicBold || !arabicRegular)
            {
                Debug.LogError("[NacdArabicNameFonts] Failed to create IBM Plex Sans Arabic TMP assets.");
                return;
            }

            // Segoe first: includes Arabic Presentation Forms that RTLTMPro shaping needs.
            // IBM Plex second: brand-family coverage for logical Arabic / Latin mix.
            AddFallback(AntipolSdf, segoeArabic);
            AddFallback(AntipolSdf, arabicBold);
            AddFallback(IbmSansSdf, segoeArabic);
            AddFallback(IbmSansSdf, arabicRegular);

            WireUiManagerFonts(segoeArabic, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AntipolSdf));

            // Nameplates / leaderboard TMP that already use Antipol pick Arabic via fallback.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NacdArabicNameFonts] IBM Plex Sans Arabic ready; Segoe Arabic is primary shaped-name fallback.");
        }

        static void WireUiManagerFonts(TMP_FontAsset arabicFont, TMP_FontAsset latinFont)
        {
            var manager = Object.FindAnyObjectByType<HorseRacing.UI.NacdEnergizingUIManager>();
            if (!manager) return;

            var so = new SerializedObject(manager);
            var arabicProp = so.FindProperty("arabicNameFont");
            var latinProp = so.FindProperty("latinNameFont");
            if (arabicProp != null) arabicProp.objectReferenceValue = arabicFont;
            if (latinProp != null) latinProp.objectReferenceValue = latinFont;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        static TMP_FontAsset EnsureTmpFont(string fontPath, string assetName)
        {
            var assetPath = $"{TmpDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null && existing.material != null &&
                existing.atlasTextures is { Length: > 0 } && existing.atlasTextures[0] != null)
                return existing;

            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (!font)
            {
                Debug.LogError($"[NacdArabicNameFonts] Missing font: {fontPath}");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(TmpDir))
                AssetDatabase.CreateFolder("Assets/Branding/Fonts", "TMP");

            var fa = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);

            fa.TryAddCharacters(SeedChars, out _);
            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.material != null)
            {
                fa.material.name = $"{font.name} Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            if (fa.atlasTextures != null)
            {
                for (var i = 0; i < fa.atlasTextures.Length; i++)
                {
                    var tex = fa.atlasTextures[i];
                    if (!tex) continue;
                    tex.name = $"{font.name} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
            }

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }

        static void AddFallback(string hostPath, TMP_FontAsset fallback)
        {
            var host = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(hostPath);
            if (!host || !fallback) return;

            if (host.fallbackFontAssetTable == null)
                host.fallbackFontAssetTable = new List<TMP_FontAsset>();

            if (!host.fallbackFontAssetTable.Contains(fallback))
                host.fallbackFontAssetTable.Add(fallback);

            EditorUtility.SetDirty(host);
        }
    }
}
#endif
