using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Curated notebook vocabulary, separate from the complete dialogue reference lists.</summary>
public static class JournalVocabularyCatalog
{
    [Serializable] private sealed class Catalog { public Section[] sections; }
    [Serializable] private sealed class Section
    {
        public string conversationId;
        public string pageId;
        public ResearchDocument.Vocabulary[] vocabulary;
    }
    private static readonly Dictionary<string, Catalog> catalogs = new Dictionary<string, Catalog>();

    public static ResearchDocument.Vocabulary[] Get(string mapId, string conversationId, string pageId)
    {
        if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(conversationId)) return null;
        string key = mapId.Trim().ToLowerInvariant();
        if (!catalogs.TryGetValue(key, out var catalog))
        {
            var source = Resources.Load<TextAsset>("Learning/" + key + "_journal_vocabulary");
            try { catalog = source != null ? JsonUtility.FromJson<Catalog>(source.text) : null; }
            catch (Exception exception) { Debug.LogWarning("Invalid journal vocabulary for " + key + ": " + exception.Message); }
            catalogs[key] = catalog;
            if (catalog?.sections == null)
                Debug.LogWarning("No curated journal vocabulary found for " + key + ". Add Resources/Learning/" + key + "_journal_vocabulary.json.");
        }
        if (catalog?.sections == null) return null;
        foreach (var section in catalog.sections)
            if (section != null && section.conversationId == conversationId && section.pageId == pageId)
                return section.vocabulary;
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() => catalogs.Clear();
}
