using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyEncounterSetup
{
    private const string Request = "Library/EnemyEncounterSetup.v3.request";
    private const string EnemyPath = "Assets/_Project/Gameplay/Characters/Enemies/Prefabs/";
    private static bool running;
    static EnemyEncounterSetup() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }
    [MenuItem("Tools/Enemies/Restore Mission Encounters and Chase Speed 2")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        var report = new StringBuilder();
        try
        {
            VerifyMissionLogic();
            report.AppendLine("PASS: patrol, equipment gate, persistent pursuit, dormant damage, tower group deaths, guard rescue, research gate and original visibility restoration.");
            string backup = "Library/EnemyEncounterBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            int prefabs = 0, actors = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var brains = root.GetComponentsInChildren<EnemyBrain>(true);
                    var scientist = path.IndexOf("Scientist", StringComparison.OrdinalIgnoreCase) >= 0 ? root.GetComponentsInChildren<NPCDialogueTrigger>(true) : Array.Empty<NPCDialogueTrigger>();
                    if (brains.Length == 0 && scientist.Length == 0) continue;
                    string copy = backup + "/" + path;
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.Copy(path, copy, true); File.Copy(path + ".meta", copy + ".meta", true);
                    foreach (var brain in brains)
                    {
                        var settings = new SerializedObject(brain);
                        settings.FindProperty("chaseSpeed").floatValue = 2f;
                        settings.FindProperty("obstacleMask").intValue |= LayerMask.GetMask("Water", "Obstacle");
                        if (path.Contains("/Monsters/Variants/") || path.Contains("Monster_Laboratory"))
                            settings.FindProperty("encounterMode").enumValueIndex = (int)InferMode(brain.name, brain.EncounterMode);
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        SetMotorWaterMask(brain);
                    }
                    foreach (var npc in scientist) SetNpcBeat(npc);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success || AssetDatabase.AssetPathToGUID(path) != guid) throw new IOException("Prefab save failed: " + path);
                    prefabs++;
                    report.AppendLine("Speed 2: " + path + " (" + brains.Length + " enemies)");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                if (prefabs % 10 == 0) File.WriteAllText("Library/EnemyEncounterSetup.progress", prefabs + " prefab assets updated");
            }
            Preset("DustChaser", "Dust_TowerAmbush", EnemyEncounterMode.MirrorAmbush);
            Preset("DustGuard", "Dust_ArchaeologistGuard", EnemyEncounterMode.ArchaeologistGuard);
            Preset("DustPatrol", "Dust_LaboratoryAmbush", EnemyEncounterMode.LaboratoryAmbush);
            report.AppendLine("Created reusable Dust encounter presets under Dust/Encounters.");

            var scene = SceneManager.GetActiveScene();
            var enemies = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyBrain>(true)).ToArray();
            foreach (var enemy in enemies)
            {
                Undo.RecordObject(enemy, "Restore enemy mission role");
                var settings = new SerializedObject(enemy);
                settings.FindProperty("chaseSpeed").floatValue = 2f;
                settings.FindProperty("obstacleMask").intValue |= LayerMask.GetMask("Water", "Obstacle");
                settings.FindProperty("encounterMode").enumValueIndex = (int)InferMode(enemy.name, enemy.EncounterMode);
                settings.ApplyModifiedPropertiesWithoutUndo();
                SetMotorWaterMask(enemy);
                Record(enemy);
                actors++;
            }
            var mirrors = new HashSet<EnemyBrain>(enemies.Where(enemy => enemy.EncounterMode == EnemyEncounterMode.MirrorAmbush));
            foreach (var signal in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TowerRescueSignal>(true)))
            {
                var settings = new SerializedObject(signal);
                var group = settings.FindProperty("enemiesToDefeat");
                for (int i = 0; i < group.arraySize; i++)
                {
                    var enemy = group.GetArrayElementAtIndex(i).objectReferenceValue as EnemyBrain;
                    if (enemy == null) continue;
                    SetRole(enemy, EnemyEncounterMode.MirrorAmbush);
                    mirrors.Add(enemy);
                }
                if (mirrors.Count == 0)
                {
                    for (int spawnIndex = 0; spawnIndex < 2; spawnIndex++)
                    {
                        var enemy = SpawnRole(scene, "Dust_TowerAmbush", "TowerAmbush_" + (spawnIndex + 1), signal.transform.position + Vector3.right * (spawnIndex == 0 ? -1.5f : 1.5f));
                        mirrors.Add(enemy);
                        enemies = enemies.Concat(new[] { enemy }).ToArray();
                        actors++;
                    }
                    report.AppendLine("Restored missing tower ambush group.");
                }
                Undo.RecordObject(signal, "Bind tower enemy group");
                group.arraySize = mirrors.Count;
                int index = 0;
                foreach (var enemy in mirrors) group.GetArrayElementAtIndex(index++).objectReferenceValue = enemy;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Record(signal);
            }
            var npcRoot = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).FirstOrDefault(root => root.GetComponent<EnemyBrain>() == null &&
                root.GetComponentInChildren<NPCDialogueTrigger>(true) != null &&
                (root.name.IndexOf("Scientist", StringComparison.OrdinalIgnoreCase) >= 0 || root.name.IndexOf("Archaeologist", StringComparison.OrdinalIgnoreCase) >= 0));
            if (npcRoot == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Gameplay/Characters/NPC/Prefabs/Scientist/ScientistNPC.prefab");
                if (prefab == null) throw new InvalidOperationException("ScientistNPC prefab is missing.");
                npcRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                npcRoot.name = "ScientistNPC";
                Undo.RegisterCreatedObjectUndo(npcRoot, "Restore archaeologist NPC");
                report.AppendLine("Restored missing ScientistNPC.");
            }
            if (!enemies.Any(enemy => enemy.EncounterMode == EnemyEncounterMode.ArchaeologistGuard))
            {
                // The current scene may have had Monster (3) removed; restore the missing mission actor.
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath + "Dust/Encounters/Dust_ArchaeologistGuard.prefab");
                var position = GuardSpawn(scene, npcRoot.transform.position);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
                instance.name = "ArchaeologistGuard";
                instance.transform.position = position;
                Undo.RegisterCreatedObjectUndo(instance, "Restore missing archaeologist guard");
                var guard = instance.GetComponent<EnemyBrain>();
                enemies = enemies.Concat(new[] { guard }).ToArray();
                actors++;
                report.AppendLine("Restored missing archaeologist guard beside ScientistNPC.");
            }
            foreach (var guard in enemies.Where(enemy => enemy.EncounterMode == EnemyEncounterMode.ArchaeologistGuard))
            {
                if (npcRoot == null) throw new InvalidOperationException("ScientistNPC is missing from the active scene.");
                var settings = new SerializedObject(guard);
                settings.FindProperty("archaeologistToReveal").objectReferenceValue = npcRoot;
                if (settings.FindProperty("archaeologistSpawnPoint").objectReferenceValue == null)
                {
                    var point = new GameObject("ArchaeologistSpawnPoint");
                    point.transform.position = npcRoot.transform.position;
                    Undo.RegisterCreatedObjectUndo(point, "Add archaeologist spawn marker");
                    settings.FindProperty("archaeologistSpawnPoint").objectReferenceValue = point.transform;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                Record(guard);
                foreach (var npc in npcRoot.GetComponentsInChildren<NPCDialogueTrigger>(true)) { Undo.RecordObject(npc, "Restore archaeologist mission"); SetNpcBeat(npc); Record(npc); }
            }
            if (!enemies.Any(enemy => enemy.EncounterMode == EnemyEncounterMode.LaboratoryAmbush))
            {
                var lab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabBuildingInteraction>(true)).FirstOrDefault();
                if (lab == null) throw new InvalidOperationException("No research laboratory found for the missing lab enemy.");
                var collider = lab.GetComponent<Collider2D>();
                var center = collider != null ? new Vector3(collider.bounds.center.x, collider.bounds.min.y - 1f) : lab.transform.position + Vector3.down * 2;
                var enemy = SpawnRole(scene, "Dust_LaboratoryAmbush", "LaboratoryAmbush", center);
                enemies = enemies.Concat(new[] { enemy }).ToArray();
                actors++;
                report.AppendLine("Restored missing laboratory ambush enemy.");
            }
            foreach (var enemy in enemies)
                report.AppendLine("Scene: " + enemy.name + " -> " + enemy.EncounterMode + "; chase speed=" + new SerializedObject(enemy).FindProperty("chaseSpeed").floatValue);
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            VerifySavedSpeeds();
            string result = "SUCCESS\n" + prefabs + " prefab assets updated; " + actors + " scene enemies configured; 3 Dust mission presets created. All prefab enemies verified at speed 2.\nBackup: " + backup + "\n" + report;
            File.WriteAllText("Library/EnemyEncounterSetup.result", result);
            File.WriteAllText("Assets/_Project/Docs/ENEMY_ENCOUNTERS.txt", result);
            Debug.Log("Enemy encounters restored and all chase speeds set to 2.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/EnemyEncounterSetup.result", "FAILED\n" + report + "\n" + error);
            Debug.LogException(error);
        }
        finally { running = false; }
    }

    private static EnemyEncounterMode InferMode(string name, EnemyEncounterMode current)
    {
        if (current != EnemyEncounterMode.Normal) return current;
        if (name.IndexOf("Laboratory", StringComparison.OrdinalIgnoreCase) >= 0) return EnemyEncounterMode.LaboratoryAmbush;
        var match = Regex.Match(name, @"Monster \((\d+)\)");
        if (match.Success)
        {
            int id = int.Parse(match.Groups[1].Value);
            if (id == 2) return EnemyEncounterMode.LaboratoryAmbush;
            if (id == 3) return EnemyEncounterMode.ArchaeologistGuard;
            if (id == 4 || id == 5) return EnemyEncounterMode.MirrorAmbush;
        }
        return EnemyEncounterMode.Normal;
    }
    private static void SetNpcBeat(NPCDialogueTrigger npc)
    {
        var settings = new SerializedObject(npc);
        settings.FindProperty("beatId").stringValue = "q2_archaeologist";
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetMotorWaterMask(EnemyBrain brain)
    {
        var motor = brain.GetComponent<GridMovement>();
        if (motor == null) return;
        var settings = new SerializedObject(motor);
        settings.FindProperty("obstacleMask").intValue |= LayerMask.GetMask("Water", "Obstacle");
        settings.ApplyModifiedPropertiesWithoutUndo();
        Record(motor);
    }
    private static Vector3 GuardSpawn(Scene scene, Vector3 npc)
    {
        var terrain = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CityTerrain>(true)).FirstOrDefault();
        Physics2D.SyncTransforms();
        for (float radius = 1.5f; radius <= 4f; radius += .5f)
        foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.down, Vector3.up })
        {
            var point = npc + direction * radius;
            if (terrain != null && !terrain.ContainsSurface(point)) continue;
            if (Physics2D.OverlapBoxAll(point, new Vector2(.8f, .8f), 0).Any(hit => !hit.isTrigger)) continue;
            return point;
        }
        throw new InvalidOperationException("No clear spawn position near ScientistNPC; place a guard manually and run setup again.");
    }
    private static EnemyBrain SpawnRole(Scene scene, string prefabName, string name, Vector3 center)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath + "Dust/Encounters/" + prefabName + ".prefab");
        if (template == null) throw new InvalidOperationException("Encounter prefab missing: " + prefabName);
        var position = GuardSpawn(scene, center);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
        instance.name = name;
        instance.transform.position = position;
        Undo.RegisterCreatedObjectUndo(instance, "Restore missing mission enemy");
        return instance.GetComponent<EnemyBrain>();
    }
    private static void SetRole(EnemyBrain enemy, EnemyEncounterMode mode)
    {
        var settings = new SerializedObject(enemy);
        settings.FindProperty("encounterMode").enumValueIndex = (int)mode;
        settings.FindProperty("chaseSpeed").floatValue = 2f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Record(enemy);
    }
    private static void Preset(string source, string name, EnemyEncounterMode mode)
    {
        string folder = EnemyPath + "Dust/Encounters";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(EnemyPath + "Dust", "Encounters");
        string path = folder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(EnemyPath + "Dust/" + source + ".prefab", path)) throw new IOException("Cannot create " + path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try { SetRole(root.GetComponent<EnemyBrain>(), mode); PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved); if (!saved) throw new IOException(path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void VerifySavedSpeeds()
    {
        foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            foreach (var brain in prefab.GetComponentsInChildren<EnemyBrain>(true))
                if (!Mathf.Approximately(new SerializedObject(brain).FindProperty("chaseSpeed").floatValue, 2f)) throw new InvalidOperationException("Chase speed was not saved: " + path);
        }
    }
    private static void Record(Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }

    private static void VerifyMissionLogic()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var storyState = typeof(StoryProgress).GetFields(flags).Where(field => !field.IsInitOnly).ToDictionary(field => field, field => field.GetValue(null));
        var gameState = typeof(GameplayState).GetFields(flags).Where(field => !field.IsInitOnly).ToDictionary(field => field, field => field.GetValue(null));
        var completed = (HashSet<string>)typeof(StoryProgress).GetField("completed", flags).GetValue(null);
        var clues = (List<string>)typeof(StoryProgress).GetField("clues", flags).GetValue(null);
        var oldCompleted = completed.ToArray(); var oldClues = clues.ToArray();
        var root = new GameObject("Encounter verification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            GameplayState.Reset(); StoryProgress.Reset();
            GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
            var player = Child("Player"); var inventory = player.AddComponent<PlayerInventory>();
            EnemyBrain Enemy(EnemyEncounterMode mode, GameObject npc = null)
            {
                var go = Child(mode.ToString()); go.AddComponent<SpriteRenderer>(); go.AddComponent<BoxCollider2D>();
                var brain = go.AddComponent<EnemyBrain>(); brain.EnsureComponents();
                var settings = new SerializedObject(brain);
                settings.FindProperty("encounterMode").enumValueIndex = (int)mode;
                settings.FindProperty("player").objectReferenceValue = player;
                settings.FindProperty("archaeologistToReveal").objectReferenceValue = npc;
                settings.ApplyModifiedPropertiesWithoutUndo();
                typeof(EnemyBrain).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(brain, null);
                return brain;
            }
            void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
            var normal = Enemy(EnemyEncounterMode.Normal); normal.InitializeEncounter();
            Require(normal.IsEncounterActive, "Normal patrol is inactive.");
            var mirror = Enemy(EnemyEncounterMode.MirrorAmbush); mirror.InitializeEncounter();
            Require(!mirror.IsEncounterActive && !mirror.GetComponent<Collider2D>().enabled, "Tower enemy starts active.");
            mirror.GetComponent<Health>().TakeDamage(100);
            Require(!mirror.GetComponent<Health>().IsDead, "Dormant enemy took damage.");
            inventory.SetEquipped(true);
            Require(!mirror.IsEncounterActive, "Equipment skipped tower conversation.");
            inventory.SetEquipped(false); StoryProgress.Complete("q1_spirit");
            Require(!mirror.IsEncounterActive, "Tower activated without the actual reward.");
            inventory.SetEquipped(true);
            Require(mirror.IsEncounterActive && mirror.PursuesPlayerAfterTrigger, "Tower reward did not activate pursuit.");
            inventory.SetEquipped(false);
            Require(mirror.IsEncounterActive, "Unequipping reset the ambush.");
            var second = Enemy(EnemyEncounterMode.MirrorAmbush); inventory.SetEquipped(true); second.InitializeEncounter();
            var signal = Child("Tower signal").AddComponent<TowerRescueSignal>();
            var signalSettings = new SerializedObject(signal); var list = signalSettings.FindProperty("enemiesToDefeat");
            list.arraySize = 2; list.GetArrayElementAtIndex(0).objectReferenceValue = mirror; list.GetArrayElementAtIndex(1).objectReferenceValue = second;
            signalSettings.ApplyModifiedPropertiesWithoutUndo();
            typeof(TowerRescueSignal).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(signal, null);
            mirror.GetComponent<Health>().TakeDamage(100);
            Require(!StoryProgress.HasClearedTowerEnemies, "Tower cleared before all enemies died.");
            second.GetComponent<Health>().TakeDamage(100);
            Require(StoryProgress.HasClearedTowerEnemies, "Tower group death did not advance mission.");
            var npc = Child("Archaeologist"); var guard = Enemy(EnemyEncounterMode.ArchaeologistGuard, npc);
            var spawn = Child("NPC spawn"); spawn.transform.position = new Vector3(4, 3);
            var guardSettings = new SerializedObject(guard); guardSettings.FindProperty("archaeologistSpawnPoint").objectReferenceValue = spawn.transform; guardSettings.ApplyModifiedPropertiesWithoutUndo();
            guard.InitializeEncounter(); Require(!npc.activeSelf, "NPC visible before rescue.");
            guard.GetComponent<Health>().TakeDamage(100);
            Require(npc.activeSelf && npc.transform.position == spawn.transform.position, "Guard death did not reveal the NPC.");
            var lab = Enemy(EnemyEncounterMode.LaboratoryAmbush);
            var disabledChild = Child("Originally disabled collider"); disabledChild.transform.SetParent(lab.transform); disabledChild.AddComponent<BoxCollider2D>().enabled = false;
            lab.InitializeEncounter(); Require(!lab.IsEncounterActive, "Lab started active.");
            StoryProgress.TryReceiveArchaeologistSignal(); StoryProgress.Complete("q2_archaeologist"); StoryProgress.TryEnterResearchRoom(); lab.RefreshEncounter();
            Require(!lab.IsEncounterActive, "Room entry alone spawned lab enemy.");
            StoryProgress.TryCompleteResearchDocument(5, "Destination");
            Require(lab.IsEncounterActive && lab.GetComponent<Collider2D>().enabled && !disabledChild.GetComponent<Collider2D>().enabled, "Document completion did not restore original lab visibility.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            StoryProgress.Reset(); GameplayState.Reset();
            completed.UnionWith(oldCompleted); clues.AddRange(oldClues);
            foreach (var pair in storyState) pair.Key.SetValue(null, pair.Value);
            foreach (var pair in gameState) pair.Key.SetValue(null, pair.Value);
        }
    }
}
