using System;
using UnityEngine;

/// <summary>Local identity shared across scenes. PlayerId is independent of the editable name.</summary>
[Serializable]
public sealed class PlayerProfile
{
    public string playerId;
    public string playerName;
}

public static class PlayerProfileStore
{
    private const string ProfileKey = "EarthDefender.PlayerProfile.v1";
    private const string LegacyNameKey = "EarthDefender.PlayerName";
    private static PlayerProfile current;
    public static string PlayerId { get { EnsureLoaded(); return current.playerId; } }
    public static string PlayerName { get { EnsureLoaded(); return current.playerName; } }

    public static string NormalizeName(string value)
    {
        string clean = string.Join(" ", (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        return clean.Length > 20 ? clean.Substring(0, 20).TrimEnd() : clean;
    }

    public static bool SetName(string value)
    {
        string clean = NormalizeName(value);
        if (clean.Length == 0) return false;
        EnsureLoaded();
        current.playerName = clean;
        Save();
        return true;
    }

    private static void EnsureLoaded()
    {
        if (current != null) return;
        try { current = JsonUtility.FromJson<PlayerProfile>(PlayerPrefs.GetString(ProfileKey, "")); }
        catch (ArgumentException) { current = null; }
        if (current == null) current = new PlayerProfile();
        if (string.IsNullOrWhiteSpace(current.playerId)) current.playerId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(current.playerName))
            current.playerName = NormalizeName(PlayerPrefs.GetString(LegacyNameKey, ""));
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(current));
        PlayerPrefs.SetString(LegacyNameKey, current.playerName ?? "");
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => current = null;
}
