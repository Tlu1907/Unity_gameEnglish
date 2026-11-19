using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DustMonsterLogicSetup
{
    private const string Enemies = "Assets/_Project/Gameplay/Characters/Enemies/Prefabs/";
    private const string Request = "Library/DustMonsterLogicSetup.request";
    private static readonly string[] Fields = { "patrolSpeed", "chaseSpeed", "chaseRangeTiles", "behaviour", "patrolOffsets", "patrolRadius", "loseSightSeconds", "personalSpace", "useAnimator", "runParameterName", "gridSize" };
    static DustMonsterLogicSetup() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Enemies/Use Dust Visuals with Monster Logic")]
    public static void Run()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var monster = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "Monsters/Monster (1).prefab");
            var source = monster != null ? monster.GetComponent<EnemyBrain>() : null;
            if (source == null) throw new InvalidOperationException("Monster logic template is missing.");
            string backup = "Library/DustMonsterLogicBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            int prefabs = 0, sceneActors = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Enemies + "Dust" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string copy = backup + "/" + path;
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.Copy(path, copy, true);
                    File.Copy(path + ".meta", copy + ".meta", true);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    var brain = root.GetComponent<EnemyBrain>();
                    if (brain == null || brain.GetType() != source.GetType()) throw new InvalidOperationException("Unexpected Dust AI: " + path);
                    var animator = root.GetComponent<CharacterAnimation>()?.Animator;
                    var controller = animator != null ? animator.runtimeAnimatorController : null;
                    if (controller == null) throw new InvalidOperationException("Dust animator is missing: " + path);
                    SyncLogic(source, brain, false);
                    Validate(root, source);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success || AssetDatabase.AssetPathToGUID(path) != guid) throw new IOException("Could not save Dust prefab with its original GUID.");
                    if (animator.runtimeAnimatorController != controller) throw new InvalidOperationException("Dust visuals changed unexpectedly.");
                    prefabs++;
                    report.AppendLine("Updated: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            Scene scene = SceneManager.GetActiveScene();
            var actors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyBrain>(true)).ToArray();
            foreach (var actor in actors)
            {
                var visual = actor.GetComponent<CharacterAnimation>()?.Animator;
                bool alreadyDust = visual != null && AssetDatabase.GetAssetPath(visual.runtimeAnimatorController).Contains("/CityCreatures/");
                if (alreadyDust)
                {
                    // Dust instances may still have old AI values saved as scene overrides.
                    if (!actor.name.StartsWith("Monster", StringComparison.OrdinalIgnoreCase))
                    {
                        SyncLogic(source, actor, true);
                        Record(actor);
                        if (actor.GetComponent<EnemyAttack>() != null) Record(actor.GetComponent<EnemyAttack>());
                    }
                    sceneActors++;
                    continue;
                }
                if (!actor.name.StartsWith("Monster", StringComparison.OrdinalIgnoreCase)) continue;
                string dustName = actor.name.Contains("(3)") || actor.name.Contains("Laboratory") ? "DustGuard" :
                    actor.name.Contains("(2)") || actor.name.Contains("(5)") ? "DustPatrol" : "DustChaser";
                var dust = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "Dust/" + dustName + ".prefab");
                var templateAnimator = dust != null ? dust.GetComponent<CharacterAnimation>()?.Animator : null;
                if (templateAnimator == null) throw new InvalidOperationException("Dust appearance missing: " + dustName);
                Undo.RegisterFullObjectHierarchyUndo(actor.gameObject, "Use Dust appearance");
                // Add the same runtime components the legacy Monster creates in Awake, without replacing its brain.
                actor.EnsureComponents();
                foreach (var renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Undo.RecordObject(renderer, "Hide old Monster body");
                    renderer.enabled = false;
                    Record(renderer);
                }
                var body = Object.Instantiate(templateAnimator.gameObject);
                body.name = "DustVisual";
                body.transform.SetParent(actor.transform, false);
                body.transform.localPosition = templateAnimator.transform.localPosition;
                body.transform.localRotation = templateAnimator.transform.localRotation;
                Undo.RegisterCreatedObjectUndo(body, "Add Dust visual");
                var animator = body.GetComponent<Animator>();
                var sprite = body.GetComponent<SpriteRenderer>();
                sprite.enabled = true;
                var foot = actor.GetComponent<Collider2D>();
                var templateSprite = templateAnimator.GetComponent<SpriteRenderer>();
                float height = templateSprite.sprite.bounds.size.y * Mathf.Abs(templateSprite.transform.lossyScale.y);
                float scale = height / Mathf.Max(.001f, sprite.sprite.bounds.size.y * Mathf.Abs(actor.transform.lossyScale.y));
                body.transform.localScale = Vector3.one * scale;
                if (foot != null)
                {
                    Physics2D.SyncTransforms();
                    body.transform.position += Vector3.up * (foot.bounds.min.y - sprite.bounds.min.y);
                }
                var visuals = actor.GetComponent<CharacterAnimation>();
                var settings = new SerializedObject(visuals);
                settings.FindProperty("animator").objectReferenceValue = animator;
                settings.FindProperty("useAnimator").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                visuals.RefreshController();
                Record(visuals);
                foreach (Component component in actor.GetComponents<Component>()) if (component != null) Record(component);
                // Brain, Health and mission references stay on the same scene GameObject.
                sceneActors++;
                report.AppendLine("Scene visual: " + actor.name + " -> " + dustName + "; original brain and mission links retained");
            }
            if (sceneActors > 0) EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            string result = "SUCCESS\n" + prefabs + " Dust prefabs updated; " + sceneActors + " scene actors configured.\n" +
                "Dust art/controllers retained. Monster AI fields matched; attack damage=30, interval=1.5, range=1.5. Validated attack state, all directional attack events and sprite keys.\nBackup: " + backup + "\n" + report;
            File.WriteAllText("Library/DustMonsterLogicSetup.result", result);
            File.WriteAllText("Assets/_Project/Docs/DUST_MONSTER_LOGIC.txt", result);
            Debug.Log("Dust visuals now use Monster logic: " + prefabs + " prefabs and " + sceneActors + " scene actors.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/DustMonsterLogicSetup.result", "FAILED\n" + report + "\n" + error);
            Debug.LogException(error);
        }
    }

    private static void SyncLogic(EnemyBrain source, EnemyBrain target, bool undo)
    {
        if (undo) Undo.RecordObject(target, "Use Monster AI settings");
        var original = new SerializedObject(source);
        var destination = new SerializedObject(target);
        foreach (string field in Fields) destination.CopyFromSerializedProperty(original.FindProperty(field));
        destination.ApplyModifiedPropertiesWithoutUndo();
        var attack = target.GetComponent<EnemyAttack>();
        if (attack == null) attack = target.gameObject.AddComponent<EnemyAttack>();
        if (undo) Undo.RecordObject(attack, "Use Monster attack settings");
        var settings = new SerializedObject(attack);
        settings.FindProperty("damage").intValue = 30;
        settings.FindProperty("attackInterval").floatValue = 1.5f;
        settings.FindProperty("attackRange").floatValue = 1.5f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Validate(GameObject root, EnemyBrain source)
    {
        var brain = root.GetComponent<EnemyBrain>();
        var actual = new SerializedObject(brain);
        var expected = new SerializedObject(source);
        foreach (string field in Fields.Where(field => field != "patrolOffsets"))
        {
            var a = actual.FindProperty(field); var b = expected.FindProperty(field);
            if (a.propertyType == SerializedPropertyType.Float && !Mathf.Approximately(a.floatValue, b.floatValue) ||
                a.propertyType == SerializedPropertyType.Enum && a.enumValueIndex != b.enumValueIndex ||
                a.propertyType == SerializedPropertyType.Integer && a.intValue != b.intValue)
                throw new InvalidOperationException("Dust AI differs at " + field);
        }
        var animator = root.GetComponent<CharacterAnimation>()?.Animator;
        if (animator == null || animator.GetComponent<EnemyAttackAnimationEvents>() == null) throw new InvalidOperationException("Dust lacks attack animation receiver.");
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null || !controller.layers[0].stateMachine.states.Any(item => item.state.name == "Attack")) throw new InvalidOperationException("Attack state is missing.");
        var attacks = controller.animationClips.Where(clip => clip.name.StartsWith("Attack_")).Distinct().ToArray();
        if (attacks.Length != 4) throw new InvalidOperationException("Expected 4 directional Dust attack clips.");
        foreach (var clip in attacks)
        {
            var events = AnimationUtility.GetAnimationEvents(clip);
            if (!events.Any(item => item.functionName == "Strike") || !events.Any(item => item.functionName == "FinishAttack")) throw new InvalidOperationException("Missing attack events: " + clip.name);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                if (key.value == null) throw new InvalidOperationException("Missing Dust sprite: " + clip.name);
        }
    }
    private static void Record(Object item)
    {
        EditorUtility.SetDirty(item);
        if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
    }
}
