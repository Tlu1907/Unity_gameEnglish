using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[InitializeOnLoad]
public static class PlayerAttackAnimatorSetup
{
    private const string Request = "Library/PlayerAttackAnimatorSetup.request";
    private const string ControllerPath = "Assets/_Project/Gameplay/Characters/Player/Frame/Player_NoItem/Animation/Visual.controller";
    private const string Clips = "Assets/_Project/Gameplay/Characters/Player/Frame/attack/Animation/Attack_";

    static PlayerAttackAnimatorSetup() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Player/Connect Attack Animations")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Player controller is missing.");
            var machine = controller.layers[0].stateMachine;
            var idle = machine.states.FirstOrDefault(item => item.state.name == "Idle").state;
            var walk = machine.states.FirstOrDefault(item => item.state.name == "Walk").state;
            if (idle == null || walk == null) throw new InvalidOperationException("Expected Idle and Walk states.");
            foreach (string parameter in new[] { "FaceX", "FaceY", "IsMoving" })
                if (!controller.parameters.Any(item => item.name == parameter)) throw new InvalidOperationException("Missing parameter " + parameter);
            var directions = new[] { "down", "up", "left", "right" };
            var positions = new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
            var clips = directions.Select(direction => AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + direction + ".anim")).ToArray();
            if (clips.Any(clip => clip == null)) throw new InvalidOperationException("An attack clip is missing.");
            string backup = "Assets/_Project/Prefab1/AnimationBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backup);
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(ControllerPath, backup + "/Visual.controller")) throw new IOException("Could not back up controller.");
            foreach (var clip in clips)
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(clip), backup + "/" + clip.name + ".anim")) throw new IOException("Could not back up " + clip.name);
            Undo.RegisterCompleteObjectUndo(controller, "Connect Player attack");
            Undo.RegisterCompleteObjectUndo(machine, "Connect Player attack");
            var parameterMatch = controller.parameters.FirstOrDefault(item => item.name == "Attack");
            if (parameterMatch != null && parameterMatch.type != AnimatorControllerParameterType.Trigger)
                throw new InvalidOperationException("Attack parameter already exists with a different type.");
            if (parameterMatch == null) controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var state = machine.states.FirstOrDefault(item => item.state.name == "Attack").state;
            if (state == null) state = machine.AddState("Attack", new Vector3(500, 200));
            Undo.RegisterCompleteObjectUndo(state, "Connect Player attack");
            var tree = new BlendTree { name = "AttackDirections", blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "FaceX", blendParameterY = "FaceY", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (int index = 0; index < clips.Length; index++)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clips[index]);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clips[index], settings);
                tree.AddChild(clips[index], positions[index]);
                EditorUtility.SetDirty(clips[index]);
            }
            state.motion = tree;
            state.speed = 1f;
            foreach (var transition in machine.anyStateTransitions.Where(item => item.destinationState == state).ToArray()) machine.RemoveAnyStateTransition(transition);
            var enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0;
            enter.canTransitionToSelf = false;
            enter.interruptionSource = TransitionInterruptionSource.None;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            foreach (var transition in state.transitions.ToArray()) state.RemoveTransition(transition);
            Exit(state, idle, AnimatorConditionMode.IfNot);
            Exit(state, walk, AnimatorConditionMode.If);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            foreach (var clip in clips) AssetDatabase.SaveAssetIfDirty(clip);
            string result = Validate(controller, clips, positions);
            File.WriteAllText("Library/PlayerAttackAnimatorSetup.result", "SUCCESS\n" + result + "\nBackup: " + backup);
            Debug.Log("Player attack animations connected. " + result);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/PlayerAttackAnimatorSetup.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
    }

    private static void Exit(AnimatorState attack, AnimatorState destination, AnimatorConditionMode condition)
    {
        var transition = attack.AddTransition(destination);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0;
        transition.AddCondition(condition, 0, "IsMoving");
    }

    private static string Validate(AnimatorController controller, AnimationClip[] clips, Vector2[] positions)
    {
        for (int direction = 0; direction < clips.Length; direction++)
        for (int moving = 0; moving < 2; moving++)
        {
            var go = new GameObject("Attack animation validation") { hideFlags = HideFlags.HideAndDontSave };
            var sprite = go.AddComponent<SpriteRenderer>();
            var animator = go.AddComponent<Animator>();
            var graph = PlayableGraph.Create("Player attack validation");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                var output = AnimationPlayableOutput.Create(graph, "Visual", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                graph.Evaluate(.01f);
                playable.SetFloat("FaceX", positions[direction].x);
                playable.SetFloat("FaceY", positions[direction].y);
                playable.SetBool("IsMoving", moving == 1);
                playable.SetTrigger("Attack");
                for (int step = 0; step < 4; step++) graph.Evaluate(.01f);
                if (!playable.GetCurrentAnimatorStateInfo(0).IsName("Attack")) throw new InvalidOperationException("Attack state was not entered.");
                var activeClips = playable.GetCurrentAnimatorClipInfo(0);
                if (!activeClips.Any(item => item.clip == clips[direction] && item.weight > .99f))
                    throw new InvalidOperationException("Wrong directional attack clip: " + clips[direction].name);
                if (sprite.sprite == null) throw new InvalidOperationException("Attack sprite is missing: " + clips[direction].name);
                for (int step = 0; step < 100; step++) graph.Evaluate(.01f);
                if (!playable.GetCurrentAnimatorStateInfo(0).IsName(moving == 1 ? "Walk" : "Idle"))
                    throw new InvalidOperationException("Attack did not return to locomotion.");
            }
            finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(go); }
        }
        return "Validated 4 attack directions and return to Idle/Walk (8 playback cases).";
    }
}
