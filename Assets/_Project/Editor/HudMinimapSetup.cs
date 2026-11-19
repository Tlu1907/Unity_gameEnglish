using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Builds editable UI and bakes semantic terrain; no screenshot of the map is used as the minimap.</summary>
[InitializeOnLoad]
public static class HudMinimapSetup
{
    private const string Request = "Library/HudMinimapSetup.v4.request";
    private const string Art = "Assets/_Project/UI/Art/HUD";
    private static bool running;
    private static Sprite rounded;
    private static TMP_FontAsset font;
    private static readonly Color Panel = new Color(.027f, .055f, .075f, .96f);
    private static readonly Color Border = new Color(.17f, .30f, .34f, .98f);
    private static readonly Color Text = new Color(.88f, .95f, .96f);
    private static readonly Color Muted = new Color(.53f, .68f, .72f);
    static HudMinimapSetup() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Build();
    }
    [MenuItem("Tools/UI/Build or Refresh Health Armor and Minimap")]
    public static void Build()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try
        {
            var scene = SceneManager.GetActiveScene();
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerMovement>(true)).FirstOrDefault();
            if (player == null || player.GetComponent<Health>() == null) throw new InvalidOperationException("Current scene needs a Player with Health.");
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory("Assets/_Project/UI/Prefabs/HUD");
            AssetDatabase.Refresh();
            rounded = Icon("Panel", true);
            var icons = new[] { Icon("Player"), Icon("Enemy"), Icon("Npc"), Icon("Tower"), Icon("Lab") };
            font = Font();
            var tilemaps = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Tilemap>(true))
                .Where(layer => layer.gameObject.activeInHierarchy && layer.name.StartsWith("Terrain_") && layer.GetUsedTilesCount() > 0).ToArray();
            if (tilemaps.Length == 0) throw new InvalidOperationException("No painted Terrain_* layers found for the schematic map.");
            Bounds bounds = tilemaps[0].GetComponent<TilemapRenderer>().bounds;
            foreach (var layer in tilemaps.Skip(1)) bounds.Encapsulate(layer.GetComponent<TilemapRenderer>().bounds);
            var texture = BakeMap(scene, tilemaps, bounds);
            var existingVitals = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerVitalsHUD>(true)).FirstOrDefault();
            var existingCanvas = existingVitals != null ? existingVitals.GetComponentInParent<Canvas>() : null;
            var existing = existingCanvas != null ? existingCanvas.gameObject : null;
            GameObject hud;
            if (existing == null)
            {
                hud = new GameObject("GameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(hud, scene);
                hud.layer = LayerMask.NameToLayer("UI");
                var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
                var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
                Vitals(hud.transform);
                Minimap(hud.transform, texture, bounds, icons);
                Undo.RegisterCreatedObjectUndo(hud, "Create health armor and minimap HUD");
            }
            else
            {
                hud = existing;
                var minimap = hud.GetComponentInChildren<SchematicMinimap>(true);
                if (minimap == null) throw new InvalidOperationException("Existing HUD has no minimap component.");
                Undo.RecordObject(minimap, "Refresh schematic map");
                minimap.SetMap(texture, bounds);
            }
            HideOldVitals(scene, hud, player);
            MoveJournal(scene);
            string prefabPath = "Assets/_Project/UI/Prefabs/HUD/" + scene.name + "_HUD.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud, prefabPath, InteractionMode.AutomatedAction, out bool saved);
            if (!saved) throw new IOException("Could not save HUD prefab.");
            Verify(hud, bounds);
            EditorSceneManager.MarkSceneDirty(scene);
            Canvas.ForceUpdateCanvases();
            var minimapView = hud.GetComponentInChildren<SchematicMinimap>(true);
            typeof(SchematicMinimap).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(minimapView, null);
            Capture(hud, scene);
            string report = "SUCCESS\nHUD prefab: " + prefabPath + "\nMap texture: " + AssetDatabase.GetAssetPath(texture) +
                "\nMap bounds: " + bounds + "\nPASS: health/armor damage and healing events; map projection at corners/center; no portrait; all UI references assigned.\n" +
                "Player/colliders/gameplay state were not modified by verification. Save the scene to retain the HUD and journal layout overrides.\n" +
                "Re-run Tools > UI > Build or Refresh Health Armor and Minimap after changing terrain or building positions. Existing HUD layout is preserved on refresh.";
            File.WriteAllText("Library/HudMinimapSetup.result", report);
            File.WriteAllText("Assets/_Project/Docs/HUD_MINIMAP.txt", report);
            SceneView.RepaintAll();
            Debug.Log("Health, armor and schematic minimap HUD ready: " + prefabPath);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/HudMinimapSetup.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
        finally { running = false; }
    }

    private static void Vitals(Transform parent)
    {
        var panel = Card(parent, "PlayerVitals", new Vector2(0, 1), new Vector2(28, -28), new Vector2(376, 140));
        var accent = Image(panel, "Accent", new Color(.31f, .85f, .81f)); Place(accent.rectTransform, 0, -18, 3, 104);
        Label(panel, "HealthLabel", "MÁU", 18, TextAlignmentOptions.Left, 22, -15, 120, 24);
        var hpText = Label(panel, "HealthValue", "200 / 200", 21, TextAlignmentOptions.Right, 190, -13, 164, 26);
        var hpFill = Bar(panel, "Health", -45, new Color(.94f, .25f, .34f));
        Label(panel, "ArmorLabel", "GIÁP", 18, TextAlignmentOptions.Left, 22, -75, 120, 24);
        var armorText = Label(panel, "ArmorValue", "0 / 100", 21, TextAlignmentOptions.Right, 190, -73, 164, 26);
        var armorFill = Bar(panel, "Armor", -105, new Color(.25f, .66f, .98f));
        panel.gameObject.AddComponent<PlayerVitalsHUD>().Configure(hpFill, armorFill, hpText, armorText);
    }
    private static Image Bar(Transform parent, string name, float y, Color color)
    {
        var track = Image(parent, name + "Track", new Color(.015f, .025f, .04f, 1), rounded);
        Place(track.rectTransform, 22, y, 332, 14);
        var fill = Image(track.transform, "Fill", color, rounded); Stretch(fill.rectTransform, 1);
        var glint = Image(fill.transform, "Highlight", new Color(1, 1, 1, .18f));
        glint.rectTransform.anchorMin = new Vector2(0, 1); glint.rectTransform.anchorMax = Vector2.one;
        glint.rectTransform.offsetMin = new Vector2(4, -3); glint.rectTransform.offsetMax = new Vector2(-4, -1);
        for (int i = 1; i < 5; i++)
        {
            var line = Image(track.transform, "Tick" + i, new Color(.01f, .02f, .025f, .45f));
            line.rectTransform.anchorMin = new Vector2(i / 5f, 0); line.rectTransform.anchorMax = new Vector2(i / 5f, 1);
            line.rectTransform.offsetMin = new Vector2(-.6f, 2); line.rectTransform.offsetMax = new Vector2(.6f, -2);
        }
        return fill;
    }
    private static void Minimap(Transform parent, Texture texture, Bounds bounds, Sprite[] icons)
    {
        var panel = Card(parent, "Minimap", new Vector2(1, 1), new Vector2(-28, -28), new Vector2(336, 420));
        Label(panel, "Title", "BẢN ĐỒ", 20, TextAlignmentOptions.Left, 18, -12, 220, 26);
        var north = Label(panel, "North", "N", 16, TextAlignmentOptions.Center, 294, -13, 24, 24); north.color = Muted;
        var edge = Image(panel, "MapFrame", Border, rounded); Place(edge.rectTransform, 16, -48, 304, 304);
        var viewport = Image(edge.transform, "Viewport", new Color(.025f, .05f, .07f)); Stretch(viewport.rectTransform, 2);
        viewport.gameObject.AddComponent<RectMask2D>();
        var imageObject = new GameObject("Schematic", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        imageObject.layer = panel.gameObject.layer; imageObject.transform.SetParent(viewport.transform, false);
        var map = imageObject.GetComponent<RawImage>(); map.raycastTarget = false; map.texture = texture;
        var fit = imageObject.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = bounds.size.x / bounds.size.y;
        var markers = new GameObject("Markers", typeof(RectTransform)); markers.layer = panel.gameObject.layer;
        markers.transform.SetParent(imageObject.transform, false); Stretch(markers.GetComponent<RectTransform>(), 0);
        var captions = new[] { "BẠN", "QUÁI", "NPC", "MỐC" };
        var colors = new[] { new Color(.35f, 1, .91f), new Color(1, .33f, .38f), new Color(1, .84f, .32f), new Color(.42f, .74f, 1) };
        for (int i = 0; i < 4; i++)
        {
            var dot = Image(panel, "LegendIcon" + i, colors[i], icons[Mathf.Min(i, 3)]); Place(dot.rectTransform, 18 + i * 77, -365, 12, 12);
            var text = Label(panel, "Legend" + i, captions[i], 14, TextAlignmentOptions.Left, 34 + i * 77, -361, 56, 21); text.color = Muted;
        }
        var divider = Image(panel, "Divider", new Color(.2f, .35f, .39f, .4f)); Place(divider.rectTransform, 16, -390, 304, 1);
        var objective = Label(panel, "Objective", "Tìm tín hiệu tại tháp", 15, TextAlignmentOptions.Left, 18, -396, 300, 20);
        objective.color = new Color(.94f, .82f, .5f);
        panel.gameObject.AddComponent<SchematicMinimap>().Configure(texture, bounds, map, markers.GetComponent<RectTransform>(), objective,
            icons[0], icons[1], icons[2], icons[3], icons[4]);
    }
    private static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var image = Image(parent, name, Border, rounded); var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        var inner = Image(rect, "Background", Panel, rounded); Stretch(inner.rectTransform, 1.5f); inner.transform.SetAsFirstSibling();
        return rect;
    }
    private static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = LayerMask.NameToLayer("UI"); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        if (sprite != null) { image.sprite = sprite; image.type = sprite.border.sqrMagnitude > 0 ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple; }
        return image;
    }
    private static TMP_Text Label(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = LayerMask.NameToLayer("UI"); go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = Text;
        text.alignment = alignment; text.raycastTarget = false; text.richText = false; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
    }
    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * padding; rect.offsetMax = Vector2.one * -padding;
    }

    private static Texture2D BakeMap(Scene scene, Tilemap[] layers, Bounds bounds)
    {
        const int size = 512;
        Tilemap Layer(string name) => layers.FirstOrDefault(layer => layer.name == name);
        var ground = Layer("Terrain_Ground"); var roads = Layer("Terrain_Roads"); var water = Layer("Terrain_Water");
        var bridge = Layer("Terrain_Bridges"); var stairs = Layer("Terrain_Stairs"); var station = Layer("Terrain_Station");
        bool Has(Tilemap layer, Vector3 point) => layer != null && layer.HasTile(layer.WorldToCell(point));
        var buildings = new List<Bounds>(); var owners = new HashSet<Transform>();
        foreach (var sprite in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)))
        {
            if (!sprite.gameObject.activeInHierarchy || sprite.sprite == null || sprite.GetComponentInParent<EnemyBrain>() != null) continue;
            var depth = sprite.GetComponentInParent<WorldDepthSort>();
            var owner = depth != null ? depth.transform : sprite.transform;
            if (!Regex.IsMatch(owner.name, @"(^|_)(house|lab|tower|station|item_47)", RegexOptions.IgnoreCase) || !owners.Add(owner)) continue;
            var collider = owner.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(item => !item.isTrigger);
            Bounds footprint = collider != null && collider.bounds.size.sqrMagnitude > .001f ? collider.bounds : sprite.bounds;
            buildings.Add(footprint);
        }
        var pixels = new Color32[size * size];
        var background = new Color32(10, 22, 32, 255); var land = new Color32(32, 65, 60, 255);
        var road = new Color32(107, 139, 149, 255); var river = new Color32(22, 66, 96, 255);
        var crossing = new Color32(191, 166, 112, 255); var platform = new Color32(72, 95, 110, 255);
        var building = new Color32(18, 35, 46, 255); var outline = new Color32(76, 108, 116, 255);
        float border = Mathf.Max(bounds.size.x, bounds.size.y) / size * 1.4f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var point = new Vector3(bounds.min.x + (x + .5f) / size * bounds.size.x, bounds.min.y + (y + .5f) / size * bounds.size.y);
            Color32 color = background;
            if (Has(ground, point)) color = land;
            if (Has(roads, point)) color = road;
            if (Has(station, point)) color = platform;
            if (Has(water, point)) color = river;
            if (Has(stairs, point) || Has(bridge, point)) color = crossing;
            foreach (var block in buildings)
                if (point.x >= block.min.x && point.x <= block.max.x && point.y >= block.min.y && point.y <= block.max.y)
                {
                    bool edge = point.x - block.min.x < border || block.max.x - point.x < border || point.y - block.min.y < border || block.max.y - point.y < border;
                    color = edge ? outline : building; break;
                }
            pixels[y * size + x] = color;
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); texture.SetPixels32(pixels); texture.Apply();
        string path = Art + "/" + scene.name + "_Schematic.png"; File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Sprite Icon(string name, bool panel = false)
    {
        string path = Art + "/" + name + ".png";
        if (!File.Exists(path))
        {
            const int size = 64; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size; bool inside;
                if (panel)
                {
                    float dx = Mathf.Max(.18f - u, 0, u - .82f), dy = Mathf.Max(.18f - v, 0, v - .82f);
                    inside = dx * dx + dy * dy <= .18f * .18f;
                }
                else if (name == "Enemy") inside = (new Vector2(u, v) - Vector2.one * .5f).sqrMagnitude < .18f;
                else if (name == "Npc") inside = Mathf.Abs(u - .5f) + Mathf.Abs(v - .5f) < .44f;
                else if (name == "Player") inside = v > .12f && v < .93f && Mathf.Abs(u - .5f) < (.93f - v) * .53f && (v > .35f || Mathf.Abs(u - .5f) > (.35f - v) * .4f);
                else if (name == "Tower") inside = u > .24f && u < .76f && v > .15f && v < .65f || v > .60f && v < .86f && (u > .18f && u < .34f || u > .43f && u < .57f || u > .66f && u < .82f);
                else inside = u > .39f && u < .61f && v > .48f && v < .87f || (new Vector2(u, v) - new Vector2(.5f, .35f)).sqrMagnitude < .09f && v < .58f;
                pixels[y * size + x] = new Color(1, 1, 1, inside ? 1 : 0);
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
        importer.spriteBorder = panel ? Vector4.one * 14 : Vector4.zero; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static TMP_FontAsset Font()
    {
        string path = Art + "/HUD_Font.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset == null)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (sourceFont == null) throw new InvalidOperationException("LiberationSans source font is missing.");
            asset = TMP_FontAsset.CreateFontAsset(sourceFont, 42, 5, GlyphRenderMode.SDFAA, 1024, 1024);
            if (asset == null) throw new InvalidOperationException("Could not create HUD font from LiberationSans.");
            asset.name = "HUD_Font"; AssetDatabase.CreateAsset(asset, path);
        }
        var characters = new StringBuilder(); for (char c = ' '; c <= '~'; c++) characters.Append(c);
        string vietnamese = "ÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ";
        characters.Append(vietnamese).Append(vietnamese.ToLowerInvariant());
        asset.ReadFontAssetDefinition();
        string needed = new string(characters.ToString().Distinct().Where(c => !asset.HasCharacter(c)).ToArray());
        if (needed.Length > 0 && !asset.TryAddCharacters(needed, out string missing)) throw new InvalidOperationException("HUD font is missing characters: " + missing);
        if (!AssetDatabase.Contains(asset.material)) { asset.material.name = "HUD_Font_Material"; AssetDatabase.AddObjectToAsset(asset.material, asset); }
        for (int i = 0; i < asset.atlasTextures.Length; i++)
            if (asset.atlasTextures[i] != null && !AssetDatabase.Contains(asset.atlasTextures[i]))
            { asset.atlasTextures[i].name = "HUD_Font_Atlas_" + i; AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset); }
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static void HideOldVitals(Scene scene, GameObject hud, PlayerMovement player)
    {
        foreach (var old in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HealthBarUI>(true)))
        {
            if (old.transform.IsChildOf(hud.transform)) continue;
            Undo.RecordObject(old, "Replace legacy health HUD"); old.enabled = false; Record(old);
            if (old.fill != null)
            {
                Transform owner = old.fill.transform;
                while (owner.parent != null && owner.GetComponent<Canvas>() == null && owner.name != "Health" && owner.name != "Hearts") owner = owner.parent;
                if (owner.name == "Health" || owner.name == "Hearts") { Undo.RecordObject(owner.gameObject, "Hide legacy health panel"); owner.gameObject.SetActive(false); Record(owner.gameObject); }
                else old.fill.gameObject.SetActive(false);
            }
            if (old.label != null) old.label.gameObject.SetActive(false);
        }
        var armor = player.GetComponent<PlayerArmorBar>();
        if (armor != null) { Undo.RecordObject(armor, "Use new armor HUD"); armor.enabled = false; Record(armor); }
        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && !inventory.enabled) { Undo.RecordObject(inventory, "Enable equipment armor synchronization"); inventory.enabled = true; Record(inventory); }
    }
    private static void MoveJournal(Scene scene)
    {
        foreach (var journal in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StoryClueJournal>(true)))
        {
            var settings = new SerializedObject(journal);
            var toggle = settings.FindProperty("toggleButton").objectReferenceValue as Button;
            if (toggle != null)
            {
                var rect = toggle.GetComponent<RectTransform>(); Undo.RecordObject(rect, "Place journal below minimap");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-28, -460); rect.sizeDelta = new Vector2(336, 40); Record(rect);
            }
            var notebook = settings.FindProperty("notebook").objectReferenceValue as GameObject;
            if (notebook != null)
            {
                var rect = notebook.GetComponent<RectTransform>(); Undo.RecordObject(rect, "Keep notebook clear of minimap");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-28, -510); rect.sizeDelta = new Vector2(520, 500); Record(rect);
            }
        }
    }
    private static void Record(Object value)
    {
        EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }

    private static void Verify(GameObject hud, Bounds bounds)
    {
        if (hud.GetComponentsInChildren<PlayerVitalsHUD>(true).Length != 1 || hud.GetComponentsInChildren<SchematicMinimap>(true).Length != 1) throw new InvalidOperationException("Duplicate HUD components.");
        if (hud.GetComponentsInChildren<Transform>(true).Any(item => item.name.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0)) throw new InvalidOperationException("Portrait was not requested.");
        var map = hud.GetComponentInChildren<SchematicMinimap>();
        if (Vector2.Distance(map.WorldToMap(bounds.min), Vector2.zero) > .001f || Vector2.Distance(map.WorldToMap(bounds.max), Vector2.one) > .001f || Vector2.Distance(map.WorldToMap(bounds.center), Vector2.one * .5f) > .001f) throw new InvalidOperationException("Minimap projection is incorrect.");
        var state = typeof(GameplayState).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(field => !field.IsInitOnly).ToDictionary(field => field, field => field.GetValue(null));
        var go = new GameObject("Vitals verification", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            GameplayState.Reset(); var health = go.AddComponent<Health>(); health.Configure(100, 100);
            var hp = Image(go.transform, "HP", Color.red); Stretch(hp.rectTransform, 0);
            var armor = Image(go.transform, "Armor", Color.blue); Stretch(armor.rectTransform, 0);
            var hpText = Label(go.transform, "HPText", "", 18, TextAlignmentOptions.Left, 0, 0, 100, 30);
            var armorText = Label(go.transform, "ArmorText", "", 18, TextAlignmentOptions.Left, 0, -30, 100, 30);
            var view = go.AddComponent<PlayerVitalsHUD>(); var settings = new SerializedObject(view); settings.FindProperty("source").objectReferenceValue = health; settings.ApplyModifiedPropertiesWithoutUndo();
            view.Configure(hp, armor, hpText, armorText);
            health.SetArmorEquipped(true); health.TakeDamage(30);
            if (!Mathf.Approximately(armor.rectTransform.anchorMax.x, .7f) || !Mathf.Approximately(hp.rectTransform.anchorMax.x, 1)) throw new InvalidOperationException("Armor damage display failed: armor=" + health.Armor + ", HP=" + health.Current + ", bound=" + (view.Source != null ? view.Source.name : "null") + ", armorFill=" + armor.rectTransform.anchorMax.x + ", hpFill=" + hp.rectTransform.anchorMax.x);
            health.TakeDamage(100); health.Heal(20);
            if (!Mathf.Approximately(hp.rectTransform.anchorMax.x, .9f) || !Mathf.Approximately(armor.rectTransform.anchorMax.x, 0) || hpText.text != "90 / 100" || armorText.text != "0 / 100") throw new InvalidOperationException("Health damage/healing display failed.");
        }
        finally { Object.DestroyImmediate(go); foreach (var pair in state) pair.Key.SetValue(null, pair.Value); }
    }
    private static void Capture(GameObject hud, Scene scene)
    {
        var original = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault(camera => camera.CompareTag("MainCamera"));
        if (original == null) return;
        var go = new GameObject("HUD preview camera") { hideFlags = HideFlags.HideAndDontSave }; var camera = go.AddComponent<Camera>();
        camera.CopyFrom(original); camera.enabled = false; go.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation); camera.aspect = 16f / 9f;
        var texture = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active; var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var canvas = hud.GetComponent<Canvas>(); var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        try
        {
            texture.Create(); camera.targetTexture = texture; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); foreach (var text in hud.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes("Library/HudMinimapPreview.png", pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance; Record(canvas); Canvas.ForceUpdateCanvases();
            RenderTexture.active = previous; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels); Object.DestroyImmediate(go);
        }
    }
}
