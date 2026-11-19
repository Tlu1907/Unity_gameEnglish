using TMPro;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Session notebook, shared across scenes. Only pages actually encountered are recorded.</summary>
public static class LearningJournal
{
    public sealed class MapNotes
    {
        public readonly string Id;
        public string Title { get; internal set; }
        private readonly List<string> grammar = new List<string>();
        private readonly List<string> vocabulary = new List<string>();
        private readonly HashSet<string> grammarKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> wordKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string> Grammar => grammar.AsReadOnly();
        public IReadOnlyList<string> Vocabulary => vocabulary.AsReadOnly();
        internal MapNotes(string id, string title) { Id = id; Title = title; }
        internal bool Add(ResearchDocument.Vocabulary[] words, string formula)
        {
            bool changed = false;
            if (!string.IsNullOrWhiteSpace(formula) && grammarKeys.Add(formula.Trim()))
            { grammar.Add(formula.Trim()); changed = true; }
            if (words != null)
                foreach (var word in words)
                {
                    if (word == null || string.IsNullOrWhiteSpace(word.word)) continue;
                    string line = word.word.Trim() + (string.IsNullOrWhiteSpace(word.meaning) ? "" : ": " + word.meaning.Trim());
                    if (wordKeys.Add(line)) { vocabulary.Add(line); changed = true; }
                }
            return changed;
        }
    }
    private static readonly List<MapNotes> maps = new List<MapNotes>();
    public static IReadOnlyList<MapNotes> Maps => maps.AsReadOnly();
    public static event Action Changed;
    public static void Record(string mapId, string title, ResearchDocument.Vocabulary[] words, string grammar)
    {
        if (string.IsNullOrWhiteSpace(mapId)) return;
        if (string.IsNullOrWhiteSpace(grammar) && (words == null || Array.TrueForAll(words,
            word => word == null || string.IsNullOrWhiteSpace(word.word)))) return;
        MapNotes map = maps.Find(item => item.Id == mapId);
        if (map == null) { map = new MapNotes(mapId, string.IsNullOrWhiteSpace(title) ? mapId : title); maps.Add(map); }
        bool changed = map.Add(words, grammar);
        if (!string.IsNullOrWhiteSpace(title) && map.Title != title) { map.Title = title; changed = true; }
        if (changed) Changed?.Invoke();
    }
    private static string Plain(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
    public static string BuildBook()
    {
        var book = new StringBuilder("SỔ TAY HÀNH TRÌNH\n\nNHIỆM VỤ HIỆN TẠI\n").Append(Plain(StoryProgress.Objective));
        if (StoryProgress.Clues.Count > 0)
        {
            book.Append("\n\nMANH MỐI\n");
            foreach (string clue in StoryProgress.Clues) book.Append("• ").AppendLine(Plain(clue));
        }
        if (maps.Count == 0) book.Append("\n\nChưa có ghi chú học tập. Trò chuyện và đọc tài liệu để ghi lại kiến thức.");
        foreach (var map in maps)
        {
            if (map.Grammar.Count > 0)
            {
                book.Append("<page>").Append(Plain(map.Title)).Append("\n\nNGỮ PHÁP\n\n");
                for (int i = 0; i < map.Grammar.Count; i++)
                    book.Append(i + 1).Append(". ").AppendLine(Plain(map.Grammar[i])).AppendLine();
            }
            if (map.Vocabulary.Count > 0)
            {
                book.Append("<page>").Append(Plain(map.Title)).Append("\n\nTỪ VỰNG\n\n");
                foreach (string word in map.Vocabulary) book.Append("• ").AppendLine(Plain(word));
            }
        }
        return book.ToString();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { maps.Clear(); Changed = null; }
}

/// <summary>Editable paged notebook. TextMeshPro computes page count from the authored text area.</summary>
public sealed class StoryClueJournal : MonoBehaviour
{
    [SerializeField] private GameObject notebook;
    [SerializeField] private TMP_Text entries;
    [SerializeField] private TMP_Text grammarEntries;
    [SerializeField] private TMP_Text buttonLabel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text pageLabel;
    private int currentPage = 1;
    private bool pausedGameplay;
    private Vector2 lastPageSize;
    private Vector2 lastGrammarSize;
    private readonly List<JournalPage> pages = new List<JournalPage>();
    private sealed class JournalPage
    {
        public string Vocabulary, Grammar;
        public int Subpage;
    }
    private static StoryClueJournal activeJournal;
    public static bool IsOpen => activeJournal != null && activeJournal.notebook != null && activeJournal.notebook.activeInHierarchy;
    public int CurrentPage => currentPage;
    public int PageCount { get; private set; } = 1;

    public void Configure(TMP_FontAsset font)
    {
        var buttonObject = new GameObject("Clue Journal", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = LayerMask.NameToLayer("UI");
        buttonObject.transform.SetParent(transform, false);
        Place((RectTransform)buttonObject.transform, new Vector2(.69f, .92f), new Vector2(.97f, .98f));
        buttonObject.GetComponent<Image>().color = new Color(.08f, .13f, .20f, .96f);
        buttonLabel = Text(buttonObject.transform, font);
        buttonLabel.alignment = TextAlignmentOptions.Center;
        toggleButton = buttonObject.GetComponent<Button>();

        notebook = new GameObject("Clue Entries", typeof(RectTransform), typeof(Image));
        notebook.layer = buttonObject.layer;
        notebook.transform.SetParent(transform, false);
        Place((RectTransform)notebook.transform, new Vector2(.57f, .53f), new Vector2(.97f, .91f));
        notebook.GetComponent<Image>().color = new Color(.04f, .075f, .12f, .98f);
        entries = Text(notebook.transform, font);
        entries.margin = new Vector4(18, 18, 18, 18);
        entries.name = "Vocabulary Text";
        Place(entries.rectTransform, new Vector2(.04f, .14f), new Vector2(.49f, .88f));
        grammarEntries = Text(notebook.transform, font);
        grammarEntries.name = "Grammar Text";
        grammarEntries.margin = entries.margin;
        Place(grammarEntries.rectTransform, new Vector2(.51f, .14f), new Vector2(.96f, .88f));
        Place((RectTransform)notebook.transform, new Vector2(.18f, .12f), new Vector2(.82f, .88f));
        previousButton = Navigation("Previous Page", "← Trước", .05f, .27f, font);
        nextButton = Navigation("Next Page", "Sau →", .73f, .95f, font);
        closeButton = Navigation("Close Book", "Đóng · J", .73f, .95f, font);
        Place((RectTransform)closeButton.transform, new Vector2(.73f, .9f), new Vector2(.95f, .97f));
        pageLabel = Text(notebook.transform, font);
        pageLabel.name = "Page Number";
        pageLabel.alignment = TextAlignmentOptions.Center;
        Place(pageLabel.rectTransform, new Vector2(.3f, .04f), new Vector2(.7f, .11f));
        notebook.SetActive(false);
        if (isActiveAndEnabled) { Unbind(); Bind(); }
        Refresh();
    }

    private Button Navigation(string name, string caption, float left, float right, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = notebook.layer;
        go.transform.SetParent(notebook.transform, false);
        Place((RectTransform)go.transform, new Vector2(left, .04f), new Vector2(right, .11f));
        go.GetComponent<Image>().color = new Color(.08f, .13f, .2f, 1);
        var text = Text(go.transform, font);
        text.alignment = TextAlignmentOptions.Center;
        text.text = caption;
        return go.GetComponent<Button>();
    }

    private static TMP_Text Text(Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, Vector2.zero, Vector2.one);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = 23; text.color = Color.white;
        text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 23;
        text.raycastTarget = false; text.richText = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public void Toggle()
    {
        if (notebook == null) return;
        if (notebook.activeSelf) { Close(); return; }
        if (activeJournal != null && activeJournal != this) activeJournal.Close();
        activeJournal = this;
        pausedGameplay = GameplayState.CanAct;
        if (pausedGameplay) GameplayState.Pause();
        notebook.SetActive(true);
        notebook.transform.SetAsLastSibling();
        Refresh();
    }
    public void Close()
    {
        if (notebook != null) notebook.SetActive(false);
        if (pausedGameplay) GameplayState.Resume();
        pausedGameplay = false;
        if (activeJournal == this) activeJournal = null;
    }
    public void NextPage() { currentPage = Mathf.Min(PageCount, currentPage + 1); UpdatePage(); }
    public void PreviousPage() { currentPage = Mathf.Max(1, currentPage - 1); UpdatePage(); }
    private void Update()
    {
        // Do not steal typing from the API answer field.
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.GetComponent<TMP_InputField>() != null) return;
        }
        if (Input.GetKeyDown(KeyCode.J)) Toggle();
        if (notebook == null || !notebook.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.PageDown)) NextPage();
        if (Input.GetKeyDown(KeyCode.PageUp)) PreviousPage();
    }
    private void OnEnable()
    {
        StoryProgress.MissionChanged += Refresh;
        LearningJournal.Changed += Refresh;
        Unbind(); Bind();
        Refresh();
    }
    private void OnDisable()
    {
        StoryProgress.MissionChanged -= Refresh;
        LearningJournal.Changed -= Refresh;
        Unbind();
        Close();
    }
    private void Bind()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (previousButton != null) previousButton.onClick.AddListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }
    private void Unbind()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        if (previousButton != null) previousButton.onClick.RemoveListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }
    private void Refresh()
    {
        if (entries == null || buttonLabel == null) return;
        buttonLabel.text = "J · Sổ tay học tập (" + LearningJournal.Maps.Count + " map)";
        if (notebook == null || !notebook.activeInHierarchy) return;
        Canvas.ForceUpdateCanvases();
        pages.Clear();
        var mission = new StringBuilder("NHIỆM VỤ HIỆN TẠI\n\n").Append(StoryProgress.Objective);
        foreach (string clue in StoryProgress.Clues) mission.Append("\n\n• ").Append(clue);
        AddPages(mission.ToString(), "SỔ TAY HỌC TẬP\n\nTrò chuyện và đọc tài liệu để ghi lại từ vựng, ngữ pháp của từng map.");
        foreach (var map in LearningJournal.Maps)
        {
            var words = new StringBuilder(map.Title).Append("\n\nTỪ VỰNG\n\n");
            foreach (string word in map.Vocabulary) words.AppendLine(word);
            var formulas = new StringBuilder(map.Title).Append("\n\nNGỮ PHÁP\n\n");
            for (int i = 0; i < map.Grammar.Count; i++)
                formulas.Append(i + 1).Append(". ").AppendLine(
                    map.Grammar[i].Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '));
            AddPages(words.ToString(), formulas.ToString());
        }
        PageCount = Mathf.Max(1, pages.Count);
        currentPage = Mathf.Clamp(currentPage, 1, PageCount);
        UpdatePage();
    }
    private void UpdatePage()
    {
        if (pages.Count > 0)
        {
            var page = pages[Mathf.Clamp(currentPage - 1, 0, pages.Count - 1)];
            SetColumn(entries, page.Vocabulary, page.Subpage);
            SetColumn(grammarEntries, page.Grammar, page.Subpage);
        }
        if (pageLabel != null) pageLabel.text = $"Trang {currentPage} / {PageCount}";
        if (previousButton != null) previousButton.interactable = currentPage > 1;
        if (nextButton != null) nextButton.interactable = currentPage < PageCount;
    }
    private void AddPages(string vocabulary, string grammar)
    {
        int count = Mathf.Max(CountColumnPages(entries, vocabulary), CountColumnPages(grammarEntries, grammar));
        for (int i = 1; i <= count; i++)
            pages.Add(new JournalPage { Vocabulary = vocabulary, Grammar = grammar, Subpage = i });
    }
    private static int CountColumnPages(TMP_Text text, string content)
    {
        if (text == null) return 1;
        SetColumn(text, content, 1);
        return Mathf.Max(1, text.textInfo.pageCount);
    }
    private static void SetColumn(TMP_Text text, string content, int page)
    {
        if (text == null) return;
        text.richText = false;
        text.fontStyle |= FontStyles.Bold;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Page;
        text.text = content;
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();
        // TMP clamps out-of-range pages; explicitly blank a shorter column instead of repeating it.
        if (page > Mathf.Max(1, text.textInfo.pageCount)) text.text = "";
        text.pageToDisplay = page;
    }
    private void LateUpdate()
    {
        if (notebook == null || !notebook.activeInHierarchy || entries == null) return;
        Vector2 size = entries.rectTransform.rect.size;
        Vector2 grammarSize = grammarEntries != null ? grammarEntries.rectTransform.rect.size : Vector2.zero;
        if (size == lastPageSize && grammarSize == lastGrammarSize) return;
        lastPageSize = size;
        lastGrammarSize = grammarSize;
        Refresh();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeJournal = null;
}
