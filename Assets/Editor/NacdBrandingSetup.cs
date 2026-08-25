using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace HorseRacing.Race.Editor
{
    /// <summary>
    /// Imports NACD brand fonts as TMP assets and builds the Instructions page layout.
    /// </summary>
    public static class NacdBrandingSetup
    {
        const string BrandingRoot = "Assets/Branding";
        const string TmpDir = BrandingRoot + "/Fonts/TMP";
        const string SpritesDir = BrandingRoot + "/Sprites";
        const string AntipolFont = BrandingRoot + "/Fonts/Antipol/antipol-font-family-26-font/Antipol-Bold-iF666b9e3fded63.otf";
        const string IbmFont = BrandingRoot + "/Fonts/IBM_Plex_Sans/static/IBMPlexSans-Regular.ttf";
        const string MenuBgSprite = SpritesDir + "/NacdBackground.png";

        static readonly Color TitleColor = new(0.29f, 0.40f, 0.45f, 1f);
        static readonly Color BodyColor = new(0.38f, 0.49f, 0.54f, 1f);
        static readonly Color UnderlineColor = new(0.85f, 0.55f, 0.46f, 1f);
        static readonly Color ButtonColor = new(0.91f, 0.55f, 0.18f, 1f);

        const string InstructionsBodyCopy =
            "Run as fast as you can — your horse matches your pace all the way to the finish.\n\n" +
            "Race side by side in split screen. First across the line wins!";

        [MenuItem("Horse Racing/Restore Instructions Text Body")]
        public static void RestoreInstructionsTextBodyFromMenu() => RestoreInstructionsTextBody();

        public static void RestoreInstructionsTextBody()
        {
            var antipol = EnsureTmpFont(AntipolFont, "NacdAntipol-Bold SDF");
            var ibm = EnsureTmpFont(IbmFont, "NacdIBMPlexSans-Regular SDF");
            var instructions = GameObject.Find("Canvas/InstructionsPage");
            if (!instructions)
            {
                Debug.LogError("[NacdBrandingSetup] Canvas/InstructionsPage not found.");
                return;
            }

            var oldPanel = instructions.transform.Find("StepsPanel");
            if (oldPanel) Object.DestroyImmediate(oldPanel.gameObject);

            var body = instructions.transform.Find("Body")?.GetComponent<TMP_Text>();
            if (!body)
            {
                body = MakeTmp(instructions.transform, "Body", InstructionsBodyCopy, ibm, 34, BodyColor,
                    new Vector2(0.5f, 0.5f), new Vector2(1180, 420));
                body.lineSpacing = 12f;
                body.margin = new Vector4(24, 0, 24, 0);
            }
            else
            {
                body.text = InstructionsBodyCopy;
                body.font = ibm;
                body.fontSharedMaterial = ibm.material;
                body.lineSpacing = 12f;
                body.alignment = TextAlignmentOptions.Center;
            }

            var startBtn = instructions.transform.Find("StartButton");
            if (startBtn)
            {
                startBtn.gameObject.SetActive(true);
                var btnRt = startBtn.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
                btnRt.pivot = new Vector2(0.5f, 0f);
                btnRt.sizeDelta = new Vector2(340, 88);
                btnRt.anchoredPosition = new Vector2(0, 120);
            }

            var manager = Object.FindAnyObjectByType<HorseRacing.UI.NacdEnergizingUIManager>();
            if (manager)
            {
                var so = new SerializedObject(manager);
                SetRef(so, "instructionsBodyText", body);
                SetRef(so, "instructionsStartButton", startBtn?.GetComponent<Button>());
                var copyProp = so.FindProperty("instructionsCopy");
                if (copyProp != null)
                    copyProp.stringValue = InstructionsBodyCopy;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManagerMarkDirty();
            Debug.Log("[NacdBrandingSetup] Instructions body restored (text only, split-screen copy).");
        }

        const string TmpCharacterSet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            " .,!?—-'\"\n" +
            "Run as fast as you can your horse matches your pace all the way to the finish " +
            "Race side by side in split screen First across the line wins INSTRUCTIONS START";

        [MenuItem("Horse Racing/Fix NACD Branding Fonts")]
        public static void FixFonts() => RegenerateFonts();

        static void RegenerateFonts()
        {
            var antipol = EnsureTmpFont(AntipolFont, "NacdAntipol-Bold SDF", forceRebuild: true);
            var ibm = EnsureTmpFont(IbmFont, "NacdIBMPlexSans-Regular SDF", forceRebuild: true);
            RewireInstructionFonts(antipol, ibm);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NacdBrandingSetup] Rebuilt NACD TMP font assets.");
        }

        static void RewireInstructionFonts(TMP_FontAsset antipol, TMP_FontAsset ibm)
        {
            var instructions = GameObject.Find("Canvas/InstructionsPage");
            if (!instructions) return;

            var title = instructions.transform.Find("TitleBlock/Title")?.GetComponent<TMP_Text>();
            var body = instructions.transform.Find("Body")?.GetComponent<TMP_Text>();
            var btnLabel = instructions.transform.Find("StartButton/Label")?.GetComponent<TMP_Text>();

            if (title && antipol)
            {
                title.font = antipol;
                title.fontSharedMaterial = antipol.material;
            }

            if (body && ibm)
            {
                body.font = ibm;
                body.fontSharedMaterial = ibm.material;
            }

            if (btnLabel && antipol)
            {
                btnLabel.font = antipol;
                btnLabel.fontSharedMaterial = antipol.material;
            }

            EditorSceneManagerMarkDirty();
        }

        [MenuItem("Horse Racing/Setup NACD Branding + Instructions Page")]
        public static void SetupAll()
        {
            EnsureSprite(MenuBgSprite, "Assets/UI/NACD/Sprites/NacdBackground.png");
            EnsureSprite(SpritesDir + "/NacdStartPage.png", "Assets/UI/NACD/Sprites/NacdStartPage.png");

            var menuBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBgSprite);
            EnsureMenuBackground(menuBgSprite);

            var antipol = EnsureTmpFont(AntipolFont, "NacdAntipol-Bold SDF", forceRebuild: true);
            var ibm = EnsureTmpFont(IbmFont, "NacdIBMPlexSans-Regular SDF", forceRebuild: true);

            var instructions = GameObject.Find("Canvas/InstructionsPage");
            if (!instructions)
            {
                Debug.LogError("[NacdBrandingSetup] Canvas/InstructionsPage not found.");
                return;
            }

            BuildInstructionsPage(instructions.transform, antipol, ibm);

            var manager = Object.FindAnyObjectByType<HorseRacing.UI.NacdEnergizingUIManager>();
            if (manager)
            {
                var so = new SerializedObject(manager);
                var title = instructions.transform.Find("TitleBlock/Title")?.GetComponent<TMP_Text>();
                var body = instructions.transform.Find("Body")?.GetComponent<TMP_Text>();
                var btn = instructions.transform.Find("StartButton")?.GetComponent<Button>();
                SetRef(so, "instructionsBodyText", body);
                SetRef(so, "instructionsStartButton", btn);
                var menuBgCg = GameObject.Find("Canvas/BG")?.GetComponent<CanvasGroup>();
                if (!menuBgCg)
                    menuBgCg = GameObject.Find("Canvas/MenuBackground")?.GetComponent<CanvasGroup>();
                SetRef(so, "menuBackgroundCG", menuBgCg);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManagerMarkDirty();
            Debug.Log("[NacdBrandingSetup] Branding fonts + Instructions page ready.");
        }

        static void BuildInstructionsPage(Transform root, TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
        {
            ClearChildren(root);

            var rootImg = root.GetComponent<Image>();
            if (rootImg)
            {
                rootImg.color = new Color(0, 0, 0, 0);
                rootImg.raycastTarget = false;
            }

            var titleBlock = MakeRect(root, "TitleBlock", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900, 160), new Vector2(0, -220));

            var underline = MakeImage(titleBlock.transform, "Underline",
                new Vector2(0.5f, 0.35f), new Vector2(520, 18), UnderlineColor);

            var title = MakeTmp(titleBlock.transform, "Title", "INSTRUCTIONS", titleFont, 92, TitleColor,
                new Vector2(0.5f, 0.55f), new Vector2(900, 120));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 4f;

            var body = MakeTmp(root, "Body", InstructionsBodyCopy,
                bodyFont, 34, BodyColor, new Vector2(0.5f, 0.5f), new Vector2(1180, 420));
            body.lineSpacing = 12f;
            body.margin = new Vector4(24, 0, 24, 0);

            var btnGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(root, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.sizeDelta = new Vector2(340, 88);
            btnRt.anchoredPosition = new Vector2(0, 120);
            btnGo.GetComponent<Image>().color = ButtonColor;

            var btnLabel = MakeTmp(btnGo.transform, "Label", "START", titleFont, 36, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(320, 80));
            btnLabel.fontStyle = FontStyles.Bold;

            underline.transform.SetAsFirstSibling();
        }

        static void EnsureMenuBackground(Sprite sprite)
        {
            var canvas = GameObject.Find("Canvas");
            if (!canvas) return;

            var bg = canvas.transform.Find("BG") ?? canvas.transform.Find("MenuBackground");
            if (!bg)
            {
                var go = new GameObject("MenuBackground", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(canvas.transform, false);
                go.transform.SetAsFirstSibling();
                bg = go.transform;
            }

            bg.gameObject.SetActive(true);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = bg.GetComponent<Image>() ?? bg.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;

            if (!bg.TryGetComponent<CanvasGroup>(out _))
                bg.gameObject.AddComponent<CanvasGroup>();
        }

        static TMP_FontAsset EnsureTmpFont(string fontPath, string assetName, bool forceRebuild = false)
        {
            var assetPath = $"{TmpDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null && !forceRebuild && HasValidAtlas(existing))
                return existing;

            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (!font)
            {
                Debug.LogError($"[NacdBrandingSetup] Missing font: {fontPath}");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(TmpDir))
            {
                if (!AssetDatabase.IsValidFolder(BrandingRoot + "/Fonts/TMP"))
                    AssetDatabase.CreateFolder(BrandingRoot + "/Fonts", "TMP");
            }

            var fa = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);

            if (!fa.TryAddCharacters(TmpCharacterSet, out var missing) && !string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[NacdBrandingSetup] Missing glyphs in {assetName}: {missing}");

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
                    if (tex == null) continue;
                    tex.name = $"{font.name} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
            }

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }

        static bool HasValidAtlas(TMP_FontAsset fontAsset) =>
            fontAsset != null &&
            fontAsset.material != null &&
            fontAsset.atlasTextures is { Length: > 0 } &&
            fontAsset.atlasTextures[0] != null;

        static void EnsureSprite(string destPath, string fallbackSource)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(destPath))
                return;

            if (!AssetDatabase.IsValidFolder(SpritesDir))
                AssetDatabase.CreateFolder(BrandingRoot, "Sprites");

            if (!string.IsNullOrEmpty(fallbackSource) && AssetDatabase.LoadAssetAtPath<Object>(fallbackSource))
            {
                AssetDatabase.CopyAsset(fallbackSource, destPath);
                ConfigureSpriteImporter(destPath);
            }
        }

        static void ConfigureSpriteImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        static GameObject MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return go;
        }

        static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text MakeTmp(Transform parent, string name, string text, TMP_FontAsset font, float size,
            Color color, Vector2 anchor, Vector2 rectSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = rectSize;
            rt.anchoredPosition = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        static void EditorSceneManagerMarkDirty()
        {
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
