using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Play-session progress; entering Play Mode starts a fresh mission.</summary>
public static class StoryProgress
{
    private static readonly HashSet<string> completed = new HashSet<string>();
    private static readonly HashSet<string> completedConversations = new HashSet<string>();
    public static bool HasMarkedTempleLocation { get; private set; }
    public static bool ShowTempleLocation => HasMarkedTempleLocation && !completedConversations.Contains("map2_ancient_temple");
    public static void MarkTempleLocation()
    {
        if (HasMarkedTempleLocation) return;
        HasMarkedTempleLocation = true;
        MissionChanged?.Invoke();
    }
    public static bool CanStartConversation(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && !completedConversations.Contains(id)
            && (id != "map2_ancient_temple" || completedConversations.Contains("map2_forest_ranger"));
    }
    public static bool CanTalkTo(DialogueData data)
    {
        if (data == null) return false;
        return data.conversationJson == null ||
            NpcJsonDialogue.TryParse(data.conversationJson.text, out var parsed) && CanStartConversation(parsed.id);
    }
    public static bool CompleteConversation(string id)
    {
        if (!CanStartConversation(id)) return false;
        completedConversations.Add(id);
        MissionChanged?.Invoke();
        return true;
    }
    private static readonly List<string> clues = new List<string>();
    public static IReadOnlyList<string> Clues => clues.AsReadOnly();
    public static string Objective => HasDecodedResearchDocuments && !string.IsNullOrEmpty(ResearchDestination)
        ? ResearchDestination : IsCompleted("q2_archaeologist")
        ? "Manh mối: qua cây cầu để đến phòng nghiên cứu."
        : HasReceivedArchaeologistSignal ? "Nhiệm vụ: hạ quái canh giữ gần nhà ga, gặp nhà khảo cổ để lấy thông tin."
        : HasClearedTowerEnemies ? "Nhiệm vụ: quay lại gần tháp để nghe thông báo từ Linh."
        : IsCompleted("q1_spirit") ? "Nhiệm vụ: hạ các quái quanh tháp để nhận thông báo từ Linh."
        : "Nhiệm vụ: tìm tín hiệu tại tòa tháp trong công viên.";
    public static event Action<string> BeatCompleted;
    public static event Action MissionChanged;
    public static bool HasClearedTowerEnemies { get; private set; }
    public static bool HasReceivedArchaeologistSignal { get; private set; }
    public static bool TryClearTowerEnemies()
    {
        if (!IsCompleted("q1_spirit") || HasClearedTowerEnemies || CurrentIsGameOver) return false;
        HasClearedTowerEnemies = true;
        MissionChanged?.Invoke();
        return true;
    }
    public static bool TryReceiveArchaeologistSignal()
    {
        if (!HasClearedTowerEnemies || HasReceivedArchaeologistSignal || CurrentIsGameOver) return false;
        HasReceivedArchaeologistSignal = true;
        clues.Add("Linh nhận tín hiệu cầu cứu của nhà khảo cổ gần nhà ga. Hạ quái canh giữ và nói chuyện với ông ấy.");
        MissionChanged?.Invoke();
        return true;
    }
    private static bool CurrentIsGameOver => GameplayState.Current == GameplayMode.GameOver;
    public static bool HasEnteredResearchRoom { get; private set; }
    public static bool HasDecodedResearchDocuments { get; private set; }
    public static int ResearchWrongAnswers { get; private set; }
    public static string ResearchDestination { get; private set; }
    public static bool TryCompleteResearchDocument(int correctAnswers, string destination)
    {
        if (correctAnswers < 0 || correctAnswers > 5 || HasDecodedResearchDocuments ||
            !HasEnteredResearchRoom || !IsCompleted("q2_archaeologist") || CurrentIsGameOver) return false;
        ResearchWrongAnswers = 5 - correctAnswers;
        ResearchDestination = destination;
        clues.Add(destination);
        if (!TryDecodeResearchDocuments()) return false;
        MissionChanged?.Invoke();
        return true;
    }
    public static event Action ResearchDocumentsDecoded;
    public static bool TryEnterResearchRoom()
    {
        if (!IsCompleted("q2_archaeologist") || GameplayState.Current == GameplayMode.GameOver) return false;
        HasEnteredResearchRoom = true;
        return true;
    }
    public static bool TryDecodeResearchDocuments()
    {
        if (!HasEnteredResearchRoom || !IsCompleted("q2_archaeologist") || HasDecodedResearchDocuments ||
            GameplayState.Current == GameplayMode.GameOver) return false;
        HasDecodedResearchDocuments = true;
        ResearchDocumentsDecoded?.Invoke();
        return true;
    }
    public static bool IsCompleted(string id) => completed.Contains(id);
    public static bool CanStart(string id) => !IsCompleted(id) &&
        (id == "q1_spirit" || id == "q2_archaeologist" && IsCompleted("q1_spirit") && HasReceivedArchaeologistSignal);
    public static bool Complete(string id)
    {
        if (!CanStart(id)) return false;
        completed.Add(id);
        clues.Add(id == "q1_spirit" ? "sword = thanh kiếm; shield = chiếc khiên. Dùng trang bị để hạ quái quanh tháp."
            : "bridge = cây cầu. Băng qua cây cầu để đến phòng nghiên cứu.");
        BeatCompleted?.Invoke(id);
        MissionChanged?.Invoke();
        return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        completed.Clear(); clues.Clear(); BeatCompleted = null;
        completedConversations.Clear();
        HasMarkedTempleLocation = false;
        HasEnteredResearchRoom = HasDecodedResearchDocuments = false;
        ResearchWrongAnswers = 0;
        ResearchDestination = null;
        ResearchDocumentsDecoded = null;
        HasClearedTowerEnemies = HasReceivedArchaeologistSignal = false;
        MissionChanged = null;
    }
}
