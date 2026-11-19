using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[InitializeOnLoad]
public static class EquipmentAnimationValidation
{
    private const string Request = "Library/EquipmentAnimationValidation.request";
    private const string Root = "Assets/_Project/Gameplay/Characters/Player/";
    static EquipmentAnimationValidation() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Player/Validate Equipment Animations")]
    public static void Run()
    {
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "Frame/Player_NoItem/Animation/Visual.controller");
            var equipped = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Root + "Frame/Player_Item/Animation/Player_Equipped.overrideController");
            if (controller == null || equipped == null) throw new InvalidOperationException("Missing Player animation controllers.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Player.prefab");
            if (prefab == null) throw new InvalidOperationException("Missing Player prefab.");
            var copy = UnityEngine.Object.Instantiate(prefab);
            copy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var appearance = copy.GetComponent<PlayerAppearance>();
                var inventory = copy.GetComponent<PlayerInventory>();
                var visuals = copy.GetComponent<CharacterAnimation>();
                if (appearance == null || inventory == null || visuals == null) throw new InvalidOperationException("Player prefab lacks appearance components.");
                var type = typeof(PlayerAppearance);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                type.GetMethod("OnEnable", flags).Invoke(appearance, null);
                inventory.SetEquipped(false);
                type.GetMethod("Start", flags).Invoke(appearance, null);
                if (visuals.Animator.runtimeAnimatorController != controller) throw new InvalidOperationException("Unequipped Player uses the wrong controller.");
                inventory.SetEquipped(true);
                if (!appearance.UsesEquippedAnimations || visuals.Animator.runtimeAnimatorController != equipped)
                    throw new InvalidOperationException("Equipping Player did not select equipped controller.");
                inventory.SetEquipped(false);
                if (appearance.UsesEquippedAnimations || visuals.Animator.runtimeAnimatorController != controller)
                    throw new InvalidOperationException("Unequipping Player did not restore base controller.");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }

            var directions = new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
            var names = new[] { "Down", "Up", "Left", "Right" };
            int cases = 0;
            foreach (bool wearing in new[] { false, true })
            for (int direction = 0; direction < directions.Length; direction++)
            for (int moving = 0; moving < 2; moving++)
            {
                string expected = (wearing ? "E_" : "U_") + (moving == 1 ? "Walk_" : "Idle_") + names[direction];
                Playback(wearing ? equipped : controller, directions[direction], moving == 1, false, expected);
                cases++;
            }
            for (int direction = 0; direction < directions.Length; direction++)
            {
                Playback(equipped, directions[direction], false, true, "Attack_" + names[direction]);
                cases++;
            }
            string result = "SUCCESS\nEquip/unequip selects the correct controller. " + cases +
                " playback cases passed: 8 unarmed, 8 equipped Idle/Walk and 4 equipped attacks; sprite keys are valid.";
            File.WriteAllText("Library/EquipmentAnimationValidation.result", result);
            Debug.Log(result);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/EquipmentAnimationValidation.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
    }

    private static void Playback(RuntimeAnimatorController controller, Vector2 direction, bool moving, bool attack, string expected)
    {
        var go = new GameObject("Equipment animation validation") { hideFlags = HideFlags.HideAndDontSave };
        var sprite = go.AddComponent<SpriteRenderer>();
        var animator = go.AddComponent<Animator>();
        var graph = PlayableGraph.Create("Equipment animation validation");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimatorControllerPlayable.Create(graph, controller);
            AnimationPlayableOutput.Create(graph, "Visual", animator).SetSourcePlayable(playable);
            graph.Play();
            graph.Evaluate(.01f);
            playable.SetFloat("FaceX", direction.x);
            playable.SetFloat("FaceY", direction.y);
            playable.SetBool("IsMoving", moving);
            if (attack) playable.SetTrigger("Attack");
            int steps = attack ? 4 : 30;
            for (int step = 0; step < steps; step++) graph.Evaluate(.01f);
            var active = playable.GetCurrentAnimatorClipInfo(0);
            var clip = active.FirstOrDefault(item => string.Equals(item.clip.name, expected, StringComparison.OrdinalIgnoreCase) && item.weight > .99f).clip;
            if (clip == null) throw new InvalidOperationException("Expected " + expected + "; got " + string.Join(", ", active.Select(item => item.clip.name + ":" + item.weight)));
            if (sprite.sprite == null) throw new InvalidOperationException("Invisible sprite in " + expected);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                if (key.value == null) throw new InvalidOperationException("Missing animation sprite in " + expected + " at " + key.time);
        }
        finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(go); }
    }
}
