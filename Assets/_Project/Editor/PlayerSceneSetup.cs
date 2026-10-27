using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Sets up only the Player in the active scene, without editing source prefab/assets.</summary>
public static class PlayerSceneSetup
{
    private const string Menu = "Tools/Player/Setup Player in Current Scene";
    private const string BasePath = "Assets/_Project/Art/Sprites/Player/Player_NoItem/Visual.controller";
    private const string EquippedPath = "Assets/_Project/Art/Sprites/Player/Player_Item/Animation/Player_Equipped.overrideController";

    [MenuItem(Menu, true)]
    private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null;

    [MenuItem(Menu)]
    public static void Run()
    {
        if (!CanRun()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        GameObject[] objects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        GameObject[] candidates = objects.Where(go => go.CompareTag("Player")
            || go.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase)
            || go.GetComponent<PlayerMovement>() != null).ToArray();
        GameObject player = candidates.FirstOrDefault(go => go == Selection.activeGameObject);
        if (player == null && candidates.Length == 1) player = candidates[0];
        if (player == null)
        {
            Debug.LogWarning("Player setup: select the Player in the active scene. It must be named Player, tagged Player, or have PlayerMovement. Multiple candidates require selecting one.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Player in Current Scene");
        try
        {
            Undo.RecordObject(player, "Set Player tag");
            player.tag = "Player";
            PlayerMovement legacy = Ensure<PlayerMovement>(player);
            Rigidbody2D body = Ensure<Rigidbody2D>(player);
            Undo.RecordObject(body, "Configure top-down physics");
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearDamping = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Existing collider shapes are retained; motion now uses Physics2D.
            if (player.GetComponent<Collider2D>() == null)
            {
                Ensure<BoxCollider2D>(player);
                Debug.LogWarning("Player setup: added BoxCollider2D; adjust its size/offset around the feet.", player);
            }

            CharacterAnimation visuals = Ensure<CharacterAnimation>(player);
            bool newMotor = player.GetComponent<GridMovement>() == null;
            GridMovement motor = Ensure<GridMovement>(player);
            if (newMotor) motor.Configure(legacy.groundTilemap, legacy.obstacleMask, legacy.gridSize);
            if (player.GetComponent<Health>() == null)
                Ensure<Health>(player).Configure(legacy.maxHealth, legacy.currentHealth);
            if (player.GetComponent<PlayerInventory>() == null)
                Ensure<PlayerInventory>(player).SetItem(legacy.currentItem);
            PlayerAppearance appearance = Ensure<PlayerAppearance>(player);
            Ensure<PlayerInteractor>(player);
            if (player.GetComponent<PlayerInputReader>() == null)
                Ensure<PlayerInputReader>(player).Configure(legacy.speed);
            Ensure<PlayerCollision>(player);

            Animator animator = null;
            Transform visual = player.transform.Find("Visual");
            if (visual != null) animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                Animator[] animators = player.GetComponentsInChildren<Animator>(true);
                if (animators.Length == 1) animator = animators[0];
            }
            FillReference(visuals, "animator", animator);
            // An explicitly assigned Animator remains authoritative on repeated setup.
            animator = new SerializedObject(visuals).FindProperty("animator").objectReferenceValue as Animator;
            RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BasePath);
            if (animator != null)
            {
                Undo.RecordObject(animator, "Configure Visual Animator");
                animator.applyRootMotion = false;
                if (animator.runtimeAnimatorController == null) animator.runtimeAnimatorController = baseController;
                baseController = animator.runtimeAnimatorController;
                if (baseController is AnimatorOverrideController overrides) baseController = overrides.runtimeAnimatorController;
            }
            else Debug.LogWarning("Player setup: no unique Animator found. Assign Visual Animator on CharacterAnimation.", player);
            FillReference(appearance, "baseController", baseController);
            var equipped = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(EquippedPath);
            FillReference(appearance, "equippedController", equipped);
            if (equipped == null || equipped.runtimeAnimatorController == null)
                Debug.LogWarning("Player setup: Player_Equipped is missing or has no base controller. Configure its eight U_ to E_ overrides in Inspector.", player);

            if (motor.Ground == null)
            {
                Tilemap[] ground = objects.Select(go => go.GetComponent<Tilemap>())
                    .Where(tilemap => tilemap != null && tilemap.name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase)).ToArray();
                if (ground.Length == 1) FillReference(motor, "groundTilemap", ground[0]);
                else Debug.LogWarning("Player setup: cannot identify a unique Ground Tilemap. Assign GridMovement.Ground Tilemap manually.", player);
            }
            if (motor.Obstacles.value == 0)
                Debug.LogWarning("Player setup: Obstacle Mask is empty. Select obstacle layers on GridMovement; the tool does not guess environment layers.", player);

            if (motor.Ground == null || motor.Ground.GetUsedTilesCount() == 0)
            {
                SpriteRenderer[] backgrounds = objects.Where(go => go.name == "MapBackground")
                    .Select(go => go.GetComponent<SpriteRenderer>()).Where(sprite => sprite != null && sprite.sprite != null).ToArray();
                if (backgrounds.Length == 1) FillReference(motor, "walkableBackground", backgrounds[0]);
                else Debug.LogWarning("Player setup: Ground is empty. Assign GridMovement.Walkable Background or paint Ground tiles.", player);
            }

            Camera[] cameras = objects.Select(go => go.GetComponent<Camera>())
                .Where(camera => camera != null && camera.CompareTag("MainCamera")).ToArray();
            if (cameras.Length == 1)
            {
                Camera camera = cameras[0];
                var oldFollow = camera.GetComponent<Cainos.PixelArtTopDown_Basic.CameraFollow>();
                if (oldFollow != null && oldFollow.enabled)
                {
                    Undo.RecordObject(oldFollow, "Disable duplicate camera follow");
                    oldFollow.enabled = false;
                    Record(oldFollow);
                }
                PlayerCameraFollow2D follow = Ensure<PlayerCameraFollow2D>(camera.gameObject);
                var followSettings = new SerializedObject(follow);
                followSettings.FindProperty("target").objectReferenceValue = player.transform;
                followSettings.ApplyModifiedProperties();
                Undo.RecordObject(camera.transform, "Focus camera on Player");
                follow.Focus();
                Record(follow);
                Record(camera.transform);
            }
            else Debug.LogWarning("Player setup: tag exactly one camera MainCamera in the active scene to enable Player follow.", player);

            foreach (Component component in player.GetComponents<Component>()) Record(component);
            if (animator != null) Record(animator);
            Record(player);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = player;
            Debug.Log("Player setup completed in the active scene. Existing component values were preserved. Review warnings, then save the scene. Undo is available.", player);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }
    private static void FillReference(Object target, string field, Object value)
    {
        if (value == null) return;
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property.objectReferenceValue != null) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }
    private static void Record(Object target)
    {
        if (target == null) return;
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
