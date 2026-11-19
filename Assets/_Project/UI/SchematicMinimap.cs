using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Baked terrain schematic with live, mission-aware world markers. No second gameplay camera.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class SchematicMinimap : MonoBehaviour
{
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RectTransform markerLayer;
    [SerializeField] private Bounds worldBounds;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private Sprite playerIcon, enemyIcon, npcIcon, towerIcon, labIcon;
    [Header("Expanded map")]
    [SerializeField] private KeyCode toggleMapKey = KeyCode.M;
    [SerializeField, Range(.3f, .95f)] private float expandedScreenFraction = .85f;
    private bool expanded;
    private Vector2 compactAnchorMin, compactAnchorMax, compactPivot, compactPosition;
    private Vector3 compactScale;
    private int compactSibling;
    public bool IsExpanded => expanded;
    private enum Kind { Enemy, Npc, Tower, Lab, Player }
    private sealed class Marker
    {
        public Transform target;
        public Kind kind;
        public RectTransform rect;
        public Image image, halo;
        public Collider2D feet;
        public Health health;
        public EnemyBrain enemy;
        public NPCDialogueTrigger npc;
        public StoryNPC storyNpc;
        public CharacterAnimation facing;
    }
    private readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();
    private readonly List<int> remove = new List<int>();
    private float nextScan;
    public Bounds WorldBounds => worldBounds;
    public void SetMap(Texture texture, Bounds bounds)
    {
        worldBounds = bounds;
        if (mapImage != null)
        {
            mapImage.texture = texture;
            var aspect = mapImage.GetComponent<AspectRatioFitter>();
            if (aspect != null) aspect.aspectRatio = Mathf.Max(.001f, bounds.size.x / Mathf.Max(.001f, bounds.size.y));
        }
        nextScan = 0;
    }
    public Vector2 WorldToMap(Vector3 world) => new Vector2(
        (world.x - worldBounds.min.x) / Mathf.Max(.001f, worldBounds.size.x),
        (world.y - worldBounds.min.y) / Mathf.Max(.001f, worldBounds.size.y));

    public void Configure(Texture texture, Bounds bounds, RawImage image, RectTransform layer, TMP_Text objective,
        Sprite player, Sprite enemy, Sprite npc, Sprite tower, Sprite lab)
    {
        mapImage = image; mapImage.texture = texture; worldBounds = bounds;
        markerLayer = layer; objectiveText = objective;
        playerIcon = player; enemyIcon = enemy; npcIcon = npc; towerIcon = tower; labIcon = lab;
        ClearViews(); nextScan = 0;
    }
    private void OnEnable() { ClearViews(); nextScan = 0; UpdateTitle(); }
    private void OnDisable() { SetExpanded(false); }
    private void Update()
    {
        if (!Application.isPlaying) return;
        var selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        bool typing = selected != null && (selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null);
        if (typing) return;
        if (Input.GetKeyDown(toggleMapKey)) SetExpanded(!expanded);
        else if (expanded && Input.GetKeyDown(KeyCode.Escape)) SetExpanded(false);
    }
    public void ToggleExpanded() => SetExpanded(!expanded);
    public void SetExpanded(bool value)
    {
        if (value == expanded) return;
        var panel = transform as RectTransform;
        var parent = panel != null ? panel.parent as RectTransform : null;
        if (panel == null || parent == null) return;
        if (value)
        {
            compactAnchorMin = panel.anchorMin; compactAnchorMax = panel.anchorMax;
            compactPivot = panel.pivot; compactPosition = panel.anchoredPosition;
            compactScale = panel.localScale; compactSibling = panel.GetSiblingIndex();
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
            panel.anchoredPosition = Vector2.zero;
            float scale = Mathf.Min(parent.rect.width / Mathf.Max(1, panel.rect.width),
                parent.rect.height / Mathf.Max(1, panel.rect.height)) * expandedScreenFraction;
            panel.localScale = Vector3.one * scale;
            panel.SetAsLastSibling();
        }
        else
        {
            panel.anchorMin = compactAnchorMin; panel.anchorMax = compactAnchorMax;
            panel.pivot = compactPivot; panel.anchoredPosition = compactPosition;
            panel.localScale = compactScale; panel.SetSiblingIndex(compactSibling);
        }
        expanded = value;
        UpdateTitle();
    }
    private void UpdateTitle()
    {
        var title = transform.Find("Title");
        var label = title != null ? title.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = expanded ? "BẢN ĐỒ · " + toggleMapKey + "/Esc" : "BẢN ĐỒ · " + toggleMapKey;
    }
    private void ClearViews()
    {
        markers.Clear();
        if (markerLayer == null) return;
        for (int i = markerLayer.childCount - 1; i >= 0; i--)
        {
            var child = markerLayer.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
    private void LateUpdate()
    {
        if (mapImage == null || markerLayer == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
        if (Time.realtimeSinceStartup >= nextScan) { Scan(); nextScan = Time.realtimeSinceStartup + 1f; }
        remove.Clear();
        foreach (var pair in markers)
        {
            var marker = pair.Value;
            if (marker.target == null) { remove.Add(pair.Key); continue; }
            UpdateMarker(marker);
        }
        foreach (int key in remove)
        {
            if (markers[key].rect != null)
            {
                if (Application.isPlaying) Destroy(markers[key].rect.gameObject); else DestroyImmediate(markers[key].rect.gameObject);
            }
            markers.Remove(key);
        }
        if (objectiveText != null) objectiveText.text = Hint();
    }
    private void Scan()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component is PlayerMovement player) Add(player.transform, Kind.Player);
            else if (component is EnemyBrain enemy) Add(enemy.transform, Kind.Enemy, enemy);
            else if (component is NPCDialogueTrigger npc && npc.IsMissionNpc) Add(npc.transform.parent != null ? npc.transform.parent : npc.transform, Kind.Npc, null, npc);
            else if (component is StoryNPC story) Add(story.transform.parent != null ? story.transform.parent : story.transform, Kind.Npc, null, null, story);
            else if (component is TowerDialogueTrigger tower) Add(tower.transform, Kind.Tower);
            else if (component is LabBuildingInteraction lab) Add(lab.transform, Kind.Lab);
        }
    }
    private void Add(Transform target, Kind kind, EnemyBrain enemy = null, NPCDialogueTrigger npc = null, StoryNPC story = null)
    {
        int key = target.GetEntityId().GetHashCode() * 8 + (int)kind;
        if (markers.ContainsKey(key)) return;
        Sprite icon = kind == Kind.Player ? playerIcon : kind == Kind.Enemy ? enemyIcon : kind == Kind.Npc ? npcIcon : kind == Kind.Tower ? towerIcon : labIcon;
        var go = new GameObject("Marker_" + kind + "_" + target.name, typeof(RectTransform), typeof(Image));
        go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        go.layer = gameObject.layer; go.transform.SetParent(markerLayer, false);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = Vector2.one * (kind == Kind.Enemy ? 15 : kind == Kind.Player ? 24 : 23);
        var halo = go.GetComponent<Image>(); halo.sprite = icon; halo.raycastTarget = false;
        var inner = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        inner.layer = go.layer; inner.transform.SetParent(go.transform, false);
        var innerRect = inner.GetComponent<RectTransform>(); innerRect.anchorMin = Vector2.zero; innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = Vector2.one * 2; innerRect.offsetMax = Vector2.one * -2;
        var image = inner.GetComponent<Image>(); image.sprite = icon; image.raycastTarget = false;
        markers.Add(key, new Marker { target = target, kind = kind, rect = rect, image = image, halo = halo,
            feet = target.GetComponent<Collider2D>(), health = target.GetComponent<Health>(), enemy = enemy,
            npc = npc, storyNpc = story, facing = target.GetComponent<CharacterAnimation>() });
        if (kind == Kind.Player) rect.SetAsLastSibling();
    }
    private void UpdateMarker(Marker marker)
    {
        bool visible = marker.target.gameObject.activeInHierarchy;
        if (marker.health != null && marker.health.IsDead) visible = false;
        if (marker.enemy != null && !marker.enemy.IsEncounterActive) visible = false;
        if (marker.npc != null && StoryProgress.IsCompleted(marker.npc.MissionBeatId)) visible = false;
        if (marker.storyNpc != null && (!marker.storyNpc.HasUnreadDialogue || marker.storyNpc.CurrentDialogue == null)) visible = false;
        Vector3 position = marker.feet != null && marker.feet.enabled && marker.feet.bounds.size.sqrMagnitude > .001f ? marker.feet.bounds.center : marker.target.position;
        Vector2 point = WorldToMap(position);
        if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) visible = false;
        if (marker.rect.gameObject.activeSelf != visible) marker.rect.gameObject.SetActive(visible);
        if (!visible) return;
        point = new Vector2(Mathf.Clamp(point.x, .025f, .975f), Mathf.Clamp(point.y, .025f, .975f));
        marker.rect.anchorMin = marker.rect.anchorMax = point; marker.rect.anchoredPosition = Vector2.zero;
        bool towerObjective = marker.kind == Kind.Tower && !StoryProgress.HasReceivedArchaeologistSignal;
        bool labObjective = marker.kind == Kind.Lab && StoryProgress.IsCompleted("q2_archaeologist") && !StoryProgress.HasDecodedResearchDocuments;
        bool quest = towerObjective || labObjective || marker.kind == Kind.Npc;
        Color tint = marker.kind == Kind.Player ? new Color(.35f, 1f, .91f) : marker.kind == Kind.Enemy ? new Color(1f, .33f, .38f) :
            marker.kind == Kind.Npc ? new Color(1f, .84f, .32f) : marker.kind == Kind.Tower ? new Color(.93f, .79f, .48f) : new Color(.42f, .74f, 1f);
        marker.image.color = tint;
        marker.halo.color = quest ? new Color(1f, .89f, .48f, .5f + .25f * Mathf.Sin(Time.realtimeSinceStartup * 3)) : new Color(.025f, .06f, .09f, .95f);
        marker.rect.localScale = Vector3.one * (quest ? 1f + .08f * Mathf.Sin(Time.realtimeSinceStartup * 3) : 1f);
        if (marker.kind == Kind.Player && marker.facing != null)
        {
            Vector2 facing = marker.facing.Facing;
            marker.rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90);
            marker.rect.SetAsLastSibling();
        }
    }
    private static string Hint()
    {
        if (!StoryProgress.IsCompleted("q1_spirit")) return "Tìm tín hiệu tại tháp";
        if (!StoryProgress.HasClearedTowerEnemies) return "Hạ nhóm quái quanh tháp";
        if (!StoryProgress.HasReceivedArchaeologistSignal) return "Nhận tín hiệu từ Linh";
        if (!StoryProgress.IsCompleted("q2_archaeologist")) return "Giải cứu nhà khảo cổ";
        if (!StoryProgress.HasDecodedResearchDocuments) return "Đọc tài liệu phòng nghiên cứu";
        return "Đã giải mã tài liệu";
    }
}
