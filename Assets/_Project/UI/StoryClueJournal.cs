using TMPro;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Session notebook containing notes from encountered pages.</summary>
public static class LearningJournal
{
    public sealed class GrammarNote
    {
        public string Id, GroupId, Text;
    }
    private static readonly string[] groupIds = { "to_be", "present_continuous", "possession_relationships", "ability_future", "commands_suggestions", "purpose_time", "continuing_actions", "general" };
    private static readonly string[] groupTitles = { "To be & Descriptions", "Present Continuous", "Possession & Relationships", "Ability & Future", "Commands & Suggestions", "Purpose & Time", "Continuing Actions", "Other Grammar" };
    public sealed class MapNotes
    {
        public readonly string Id;
        public string Title { get; internal set; }
        private readonly List<string> grammar = new List<string>();
        private readonly List<GrammarNote> grammarNotes = new List<GrammarNote>();
        private readonly HashSet<string> grammarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> vocabulary = new List<string>();
        private readonly HashSet<string> grammarKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> wordKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string> Grammar => grammar.AsReadOnly();
        public IReadOnlyList<string> Vocabulary => vocabulary.AsReadOnly();
        internal MapNotes(string id, string title) { Id = id; Title = title; }
        internal bool Add(ResearchDocument.Vocabulary[] words, string formula, string grammarId = null, string groupId = null)
        {
            bool changed = false;
            formula = SummarizeGrammar(formula);
            if (!string.IsNullOrWhiteSpace(formula))
            {
                string key = NoteKey(formula, true);
                string id = string.IsNullOrWhiteSpace(grammarId) ? key : grammarId.Trim();
                if (!grammarIds.Contains(id) && !grammarKeys.Contains(key))
                {
                    grammarIds.Add(id); grammarKeys.Add(key);
                    grammar.Add(formula.Trim());
                    grammarNotes.Add(new GrammarNote { Id = id, GroupId = Array.IndexOf(groupIds, groupId) >= 0 ? groupId : "general", Text = formula.Trim() });
                    changed = true;
                }
                else { grammarIds.Add(id); grammarKeys.Add(key); }
            }
            if (words != null)
                foreach (var word in words)
                {
                    if (word == null || string.IsNullOrWhiteSpace(word.word)) continue;
                    string line = FormatVocabulary(word.word, word.meaning);
                    if (wordKeys.Add(NoteKey(line, false))) { vocabulary.Add(line); changed = true; }
                }
            return changed;
        }
        public string BuildGrammarColumn()
        {
            var result = new StringBuilder("GRAMMAR\n\n");
            for (int group = 0; group < groupIds.Length; group++)
            {
                bool heading = false;
                foreach (var note in grammarNotes)
                {
                    if (note.GroupId != groupIds[group]) continue;
                    if (!heading)
                    {
                        if (result.Length > "GRAMMAR\n\n".Length) result.Append('\n');
                        result.Append("<b>").Append(Plain(groupTitles[group])).Append("</b>\n");
                        heading = true;
                    }
                    result.Append("• ").Append(Plain(note.Text)).Append('\n');
                }
            }
            return result.ToString();
        }
    }
    // A note's identity is its term/formula, independent of the wording of its translation.
    private static string NoteKey(string entry, bool isGrammar)
    {
        int separator = entry.IndexOf(':');
        string key = (separator < 0 ? entry : entry.Substring(0, separator))
            .Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
        key = System.Text.RegularExpressions.Regex.Replace(key, @"\s+", " ");
        key = key.Replace("’", "'").Replace("…", "...");
        if (isGrammar)
            key = System.Text.RegularExpressions.Regex.Replace(key, @"\s*([+/])\s*", "$1");
        return key.TrimEnd(' ', '.', ',', ';', '!');
    }
    public static string SummarizeGrammar(string grammar)
    {
        if (string.IsNullOrWhiteSpace(grammar)) return "";
        string text = grammar.Replace("\r\n", "\n").Replace('\r', '\n');
        var options = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        var formula = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:^|\n)\s*(?:Công thức(?: trong lời thoại)?|Formula|Structure)\s*:\s*([^\n]+)", options);
        var meaning = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:^|\n)\s*(?:Nghĩa(?: trong lời thoại)?|Meaning)\s*:\s*([^\n]+)", options);
        if (formula.Success)
            return formula.Groups[1].Value.Trim() + (meaning.Success ? ": " + meaning.Groups[1].Value.Trim() : "");
        // Compact JSON entries already use "formula: meaning". Discard usage/examples and their continuation.
        text = System.Text.RegularExpressions.Regex.Split(text,
            @"(?:Ví dụ|Vd\.?|Examples?|E\.g\.|Cách dùng|Usage)\s*:", options)[0];
        return text.Trim();
    }
    // Normalize authored notes as well as notes collected from dialogue JSON.
    public static string FormatVocabulary(string word, string meaning)
    {
        string SingleLine(string value) => string.Join(" ", (value ?? "")
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        string term = SingleLine(word).Replace('=', ':');
        string definition = SingleLine(meaning).Replace('=', ':');
        int separator = term.IndexOf(':');
        if (separator >= 0)
        {
            if (definition.Length == 0) definition = term.Substring(separator + 1).Trim();
            term = term.Substring(0, separator).Trim();
        }
        return definition.Length == 0 ? term : term + ": " + definition;
    }
    private static readonly List<MapNotes> maps = new List<MapNotes>();
    public static IReadOnlyList<MapNotes> Maps => maps.AsReadOnly();
    public static event Action Changed;
    public static void Record(string mapId, string title, ResearchDocument.Vocabulary[] words, string grammar)
    {
        if (string.IsNullOrWhiteSpace(mapId)) return;
        if (string.IsNullOrWhiteSpace(grammar) && (words == null || Array.TrueForAll(words,
            word => word == null || string.IsNullOrWhiteSpace(word.word)))) return;
        mapId = mapId.Trim();
        MapNotes map = maps.Find(item => string.Equals(item.Id, mapId, StringComparison.OrdinalIgnoreCase));
        if (map == null) { map = new MapNotes(mapId, string.IsNullOrWhiteSpace(title) ? mapId : title); maps.Add(map); }
        bool changed = map.Add(words, grammar);
        if (!string.IsNullOrWhiteSpace(title) && map.Title != title) { map.Title = title; changed = true; }
        if (changed) Changed?.Invoke();
    }
    private static string Plain(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
    public static void RecordGrammar(string mapId, string title, GrammarNote note)
    {
        if (note == null || string.IsNullOrWhiteSpace(note.Text) || string.IsNullOrWhiteSpace(mapId)) return;
        mapId = mapId.Trim();
        var map = maps.Find(item => string.Equals(item.Id, mapId, StringComparison.OrdinalIgnoreCase));
        if (map == null) { map = new MapNotes(mapId, string.IsNullOrWhiteSpace(title) ? mapId : title); maps.Add(map); }
        if (map.Add(null, note.Text, note.Id, note.GroupId)) Changed?.Invoke();
    }
    public static string BuildBook()
    {
        var book = new StringBuilder("SỔ TAY HỌC TẬP");
        if (maps.Count == 0) book.Append("\n\nChưa có ghi chú học tập. Trò chuyện và đọc tài liệu để ghi lại kiến thức.");
        foreach (var map in maps)
        {
            if (map.Grammar.Count > 0)
            {
                book.Append("<page>").Append(Plain(map.Title)).Append("\n\nNGỮ PHÁP\n\n").Append(map.BuildGrammarColumn());
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
    [SerializeField] private TMP_Text mapTitle;
    private int currentPage = 1;
    private bool pausedGameplay;
    private Vector2 lastPageSize;
    private Vector2 lastGrammarSize;
    private readonly List<JournalPage> pages = new List<JournalPage>();
    private sealed class JournalPage
    {
        public string Title, Vocabulary, Grammar;
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
        previousButton = Navigation("Previous Page", "← Previous", .05f, .27f, font);
        nextButton = Navigation("Next Page", "Next →", .73f, .95f, font);
        closeButton = Navigation("Close Book", "Close · J", .73f, .95f, font);
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
        LearningJournal.Changed += Refresh;
        Unbind(); Bind();
        Refresh();
    }
    private void OnDisable()
    {
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
        buttonLabel.text = "J · Learning Journal";
        if (notebook == null || !notebook.activeInHierarchy) return;
        EnsureHeader();
        Canvas.ForceUpdateCanvases();
        pages.Clear();
        if (LearningJournal.Maps.Count == 0)
            AddPages("VOCABULARY\n\nNo vocabulary recorded yet.",
                "GRAMMAR\n\nNo grammar recorded yet.");
        foreach (var map in LearningJournal.Maps)
        {
            var words = new StringBuilder("VOCABULARY\n\n");
            // Explicit TMP line breaks keep every vocabulary entry on its own line.
            words.Append(string.Join("\n", map.Vocabulary)).Append('\n');
            AddPages(words.ToString(), map.BuildGrammarColumn(), map.Id == "map1" ? "Map 1 · City" : map.Title);
        }
        PageCount = Mathf.Max(1, pages.Count);
        currentPage = Mathf.Clamp(currentPage, 1, PageCount);
        UpdatePage();
    }
    private void EnsureHeader()
    {
        if (mapTitle == null)
        {
            var existing = notebook.transform.Find("Map Title");
            mapTitle = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (mapTitle == null) mapTitle = Text(notebook.transform, entries.font);
            mapTitle.name = "Map Title";
        }
        mapTitle.color = entries.color;
        mapTitle.fontStyle = FontStyles.Bold;
        mapTitle.alignment = TextAlignmentOptions.MidlineLeft;
        mapTitle.enableWordWrapping = false;
        var headerMin = new Vector2(.05f, .9f);
        var headerMax = new Vector2(.70f, .97f);
        if (closeButton != null)
        {
            var closeRect = (RectTransform)closeButton.transform;
            headerMin.y = closeRect.anchorMin.y;
            headerMax.y = closeRect.anchorMax.y;
        }
        Place(mapTitle.rectTransform, headerMin, headerMax);
        SetButtonCaption(previousButton, "← Previous");
        SetButtonCaption(nextButton, "Next →");
        SetButtonCaption(closeButton, "Close · J");
    }
    private static void SetButtonCaption(Button button, string caption)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = caption;
    }
    private void UpdatePage()
    {
        if (pages.Count > 0)
        {
            var page = pages[Mathf.Clamp(currentPage - 1, 0, pages.Count - 1)];
            if (mapTitle != null) mapTitle.text = page.Title;
            SetColumn(entries, page.Vocabulary, page.Subpage);
            SetColumn(grammarEntries, page.Grammar, page.Subpage, true);
        }
        if (pageLabel != null) pageLabel.text = $"Page {currentPage} / {PageCount}";
        if (previousButton != null) previousButton.interactable = currentPage > 1;
        if (nextButton != null) nextButton.interactable = currentPage < PageCount;
    }
    private void AddPages(string vocabulary, string grammar, string title = "Learning Journal")
    {
        int count = Mathf.Max(CountColumnPages(entries, vocabulary), CountColumnPages(grammarEntries, grammar, true));
        for (int i = 1; i <= count; i++)
            pages.Add(new JournalPage { Title = title, Vocabulary = vocabulary, Grammar = grammar, Subpage = i });
    }
    private static int CountColumnPages(TMP_Text text, string content, bool richText = false)
    {
        if (text == null) return 1;
        SetColumn(text, content, 1, richText);
        return Mathf.Max(1, text.textInfo.pageCount);
    }
    private static void SetColumn(TMP_Text text, string content, int page, bool richText = false)
    {
        if (text == null) return;
        text.richText = richText;
        text.parseCtrlCharacters = true;
        if (richText) text.fontStyle &= ~FontStyles.Bold;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Page;
        text.text = content.Replace("\r\n", "\n").Replace('\r', '\n');
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
