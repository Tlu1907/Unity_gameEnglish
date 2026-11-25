using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Moves prefab assets with their GUIDs, retaining exported variants and compound layouts.</summary>
[InitializeOnLoad]
public static class PrefabFolderOrganizer
{
    private const string Root = "Assets/_Project/";
    private const string Request = "Library/PrefabFolderOrganizer.request";
    private static bool running;
    [Serializable] private sealed class Move { public string from, to, guid; }
    [Serializable] private sealed class Manifest { public Move[] moves; }
    static PrefabFolderOrganizer() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Project/Organize Project Prefab Folders")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        var completed = new List<Move>();
        var report = new StringBuilder();
        string backup = "Library/PrefabOrganizationBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        try
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".prefab")).OrderBy(path => path).ToArray();
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var dependencies = prefabs.Concat(scenePaths).ToDictionary(AssetDatabase.AssetPathToGUID, DependencyGuids);
            var reserved = new HashSet<string>(AssetDatabase.GetAllAssetPaths(), StringComparer.OrdinalIgnoreCase);
            var plan = new List<Move>();
            foreach (string path in prefabs)
            {
                string destination = Destination(path);
                if (destination == null || string.Equals(destination, path, StringComparison.OrdinalIgnoreCase)) continue;
                if (!destination.StartsWith(Root, StringComparison.Ordinal) || destination.Contains("..")) throw new InvalidOperationException("Invalid destination: " + destination);
                string requested = destination;
                int suffix = 2;
                while (reserved.Contains(destination)) destination = requested.Substring(0, requested.Length - 7) + "_" + suffix++ + ".prefab";
                reserved.Add(destination);
                plan.Add(new Move { from = path, to = destination, guid = AssetDatabase.AssetPathToGUID(path) });
            }
            Directory.CreateDirectory(backup);
            File.WriteAllText(backup + "/manifest.json", JsonUtility.ToJson(new Manifest { moves = plan.ToArray() }, true));
            File.WriteAllText("Library/PrefabFolderOrganizer.progress", "Moving " + plan.Count + " prefabs...");
            foreach (var move in plan)
            {
                EnsureFolder(Path.GetDirectoryName(move.to).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(move.from, move.to);
                if (!string.IsNullOrEmpty(error)) throw new IOException(move.from + ": " + error);
                if (AssetDatabase.AssetPathToGUID(move.to) != move.guid) throw new InvalidOperationException("GUID changed: " + move.to);
                completed.Add(move);
                if (completed.Count % 25 == 0) File.WriteAllText("Library/PrefabFolderOrganizer.progress", completed.Count + "/" + plan.Count + " moved");
            }
            // GUID links survive moves; literal paths in authoring tools and tests also need updating.
            int sourceFiles = 0;
            foreach (string file in Directory.GetFiles("Assets/_Project", "*.cs", SearchOption.AllDirectories))
            {
                string before = File.ReadAllText(file), after = before;
                foreach (var move in completed) after = after.Replace(move.from, move.to);
                if (before == after) continue;
                string copy = backup + "/" + file.Replace('\\', '/');
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.WriteAllText(copy, before);
                File.WriteAllText(file, after, new UTF8Encoding(false));
                sourceFiles++;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int verified = 0;
            foreach (var pair in dependencies)
            {
                string path = AssetDatabase.GUIDToAssetPath(pair.Key);
                if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Asset GUID was lost: " + pair.Key);
                var after = DependencyGuids(path);
                if (!pair.Value.SetEquals(after)) throw new InvalidOperationException("Dependencies changed: " + path);
                verified++;
            }
            foreach (string key in new[] { "StoryDialogue" })
            {
                int count = AssetDatabase.GetAllAssetPaths().Count(path => path.EndsWith("/Resources/" + key + ".prefab", StringComparison.Ordinal));
                if (count != 1) throw new InvalidOperationException("Resources key must remain unique: " + key + " (" + count + ")");
            }
            foreach (string folder in completed.Select(move => Path.GetDirectoryName(move.from).Replace('\\', '/')).Distinct().OrderByDescending(path => path.Length))
                if (folder.StartsWith(Root, StringComparison.Ordinal) && AssetDatabase.IsValidFolder(folder) &&
                    Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                    AssetDatabase.DeleteAsset(folder);
            report.AppendLine("# Prefab folders");
            report.AppendLine();
            report.AppendLine("Organized " + completed.Count + " prefabs. Verified unchanged GUID dependencies for " + verified + " prefab/scene assets. Updated " + sourceFiles + " source files containing literal asset paths.");
            report.AppendLine();
            report.AppendLine("Canonical reusable prefabs sit directly in their category. Exported instances sit in Variants/<scene>. Compound object groups sit in Layouts/<scene>; their internal hierarchy is preserved.");
            report.AppendLine();
            report.AppendLine("StoryDialogue remains inside Resources folders with its original load key. No duplicate assets were deleted. Other imported art packages were not moved.");
            report.AppendLine();
            report.AppendLine("| Destination folder | Prefabs moved |\n|---|---:|");
            foreach (var category in completed.GroupBy(move => Path.GetDirectoryName(move.to).Replace('\\', '/')).OrderBy(group => group.Key))
                report.AppendLine("| " + category.Key + " | " + category.Count() + " |");
            report.AppendLine();
            report.AppendLine("## Asset moves\n\n| Previous path | Current path |\n|---|---|");
            foreach (var move in completed) report.AppendLine("| " + move.from + " | " + move.to + " |");
            File.WriteAllText(Root + "Docs/PREFAB_ORGANIZATION.md", report.ToString());
            File.WriteAllText("Library/PrefabFolderOrganizer.result", "SUCCESS\nMoved=" + completed.Count + "; verified=" + verified + "; source files=" + sourceFiles + "\nManifest: " + backup + "/manifest.json");
            File.WriteAllText("Library/PrefabFolderOrganizer.progress", "Finished successfully");
            Debug.Log("Prefab organization complete: " + completed.Count + " moved; " + verified + " asset dependency sets verified.");
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(backup);
            File.WriteAllText(backup + "/completed.json", JsonUtility.ToJson(new Manifest { moves = completed.ToArray() }, true));
            File.WriteAllText("Library/PrefabFolderOrganizer.result", "FAILED after " + completed.Count + " moves\n" + error);
            Debug.LogException(error);
        }
        finally { running = false; }
    }

    private static HashSet<string> DependencyGuids(string path) => new HashSet<string>(AssetDatabase.GetDependencies(path, true).Select(AssetDatabase.AssetPathToGUID).Where(guid => !string.IsNullOrEmpty(guid)));
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        if (string.IsNullOrEmpty(guid)) throw new IOException("Cannot create folder: " + path);
    }

    private static string Destination(string path)
    {
        bool export = path.StartsWith(Root + "Prefab1/", StringComparison.Ordinal);
        if (path.StartsWith(Root + "UI/Prefabs/HUD/", StringComparison.Ordinal)) return null;
        if (!export && (path.Contains("/Variants/") || path.Contains("/Layouts/") || path.Contains("/Encounters/"))) return null;
        string stem = Path.GetFileNameWithoutExtension(path);
        string number = Regex.Match(stem, @"^\d{4}(?=_)").Value;
        string clean = Regex.Replace(stem, @"^\d{4}_", "");
        string name = clean.ToLowerInvariant();
        string category;
        bool layout = false;
        switch (name)
        {
            case "new palette": return Root + "Gameplay/Grid/Palettes/" + clean + ".prefab";
            case "grid": case "cityexpansion": case "level1content": category = "Gameplay/Grid/Prefabs"; layout = true; break;
            case "layeredcityterrain": category = "Gameplay/Grid/Prefabs/Terrain"; break;
            case "hidingandtraps": category = "Gameplay/Interaction/Prefabs"; layout = true; break;
            case "lexistorynpcs": category = "Gameplay/Characters/NPC/Prefabs"; layout = true; break;
            case "level1progress": category = "Core/Prefabs/Objectives"; break;
            case "stationexitportal": category = "Core/Prefabs/Portals"; break;
            case "main camera": category = "Core/Prefabs/Cameras"; break;
            case "player": category = "Gameplay/Characters/Player/Prefabs"; break;
            case "groundshadow": case "sharedgroundshadow": category = "Gameplay/Characters/Common/Prefabs/Shadows"; break;
            case "scientistnpc": category = "Gameplay/Characters/NPC/Prefabs/Scientist"; break;
            case "lexiresident": category = "Gameplay/Characters/NPC/Prefabs/Residents"; break;
            case "chestquizsystem": category = "Learning/Quiz/Prefabs/Systems"; break;
            case "dialoguecanvas": case "treasuremirrordialoguecanvas": category = "UI/Prefabs/Dialogue"; break;
            case "flask english quiz test": category = "Learning/Quiz/Prefabs/Panels"; break;
            case "storydialogue": return Root + "UI/Prefabs/Resources/StoryDialogue.prefab";
            case "lab": category = "Gameplay/Items/Prefabs/Buildings/Laboratories"; break;
            case "tower": case "tower_story": category = "Gameplay/Items/Prefabs/Buildings/Towers"; if (!export && path.Contains("/Ground1/")) clean = "Tower_Story"; break;
            case "researchdocument": case "researchroomstorytrigger": category = "Gameplay/Interaction/Prefabs/Research"; break;
            case "bush": category = "Gameplay/Items/Prefabs/Vegetation/Bushes"; break;
            case "tree": category = "Gameplay/Items/Prefabs/Vegetation/Trees"; break;
            case "flowers_vertical2": category = "Gameplay/Items/Prefabs/Vegetation/Flowers"; break;
            case "planter_box": category = "Gameplay/Items/Prefabs/Vegetation/Planters"; break;
            case "car": category = "Gameplay/Items/Prefabs/Props/Vehicles"; break;
            case "fire_hydrant": case "recyclebin": category = "Gameplay/Items/Prefabs/Props/StreetFurniture"; break;
            case "street_lamp": case "lights_down": category = "Gameplay/Items/Prefabs/Props/Lighting"; break;
            case "item_47": category = "Gameplay/Items/Prefabs/Props/Decorations"; break;
            case "key": case "heartpickup": case "weaponpickup": category = "Gameplay/Items/Prefabs/Pickups"; break;
            default:
                if (name.StartsWith("house")) category = "Gameplay/Items/Prefabs/Houses";
                else if (name.StartsWith("monster") || name.StartsWith("dust")) category = "Gameplay/Characters/Enemies/Prefabs/" + (name.StartsWith("dust") ? "Dust" : "Monsters");
                else if (name.StartsWith("npc_")) category = "Gameplay/Characters/NPC/Prefabs/Residents";
                else if (name.StartsWith("hidingbush")) category = "Gameplay/Interaction/Prefabs/Hiding";
                else if (name.StartsWith("monstertrap")) category = "Gameplay/Interaction/Prefabs/Traps";
                else if (name.StartsWith("chest_") || name == "letterchest") category = "Gameplay/Items/Prefabs/Chests";
                else throw new InvalidOperationException("Unclassified prefab: " + path);
                break;
        }
        // MonsterTrap is an interaction prop, not an enemy.
        if (name.StartsWith("monstertrap")) category = "Gameplay/Interaction/Prefabs/Traps";
        if (export)
        {
            string scene = Path.GetFileName(Path.GetDirectoryName(path));
            category += (layout ? "/Layouts/" : "/Variants/") + scene;
            if (!layout && !string.IsNullOrEmpty(number)) clean += "_" + number;
        }
        else if (layout) category += "/Layouts";
        return Root + category + "/" + clean + ".prefab";
    }
}
