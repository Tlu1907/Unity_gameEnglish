using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Map-specific inputs for the reusable level prefab. Keeps UI authored in the prefab.</summary>
[DefaultExecutionOrder(-200)]
public sealed class ReusableLevelConfiguration : MonoBehaviour
{
    public string mapId = "map2";
    public string mapTitle = "Map 2";
    [TextArea] public string objective = "";
    public SpriteRenderer mapBackground;
    public Tilemap ground, stairs, bridges;
    public CityTerrain terrain;
    public Transform playerSpawn;
    public PlayerMovement player;
    public PlayerCameraFollow2D cameraFollow;
    public SchematicMinimap minimap;

    private void Awake()
    {
        LearningJournal.LoadEarlierMaps(mapId);
        ApplyConfiguration();
    }

    [ContextMenu("Apply Map Configuration")]
    public void ApplyConfiguration()
    {
        if (mapBackground != null)
        {
            // Sorting layers take precedence over order: Default would cover MapGround bridges.
            mapBackground.sortingLayerName = "MapBackground";
            mapBackground.sortingOrder = -100;
            var backgroundGroup = mapBackground.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (backgroundGroup != null)
            {
                backgroundGroup.sortingLayerName = "MapBackground";
                backgroundGroup.sortingOrder = -100;
            }
        }
        if (terrain != null)
        {
            terrain.stairs = stairs;
            terrain.bridges = bridges;
            terrain.surfaceLayers = new[] { ground, stairs, bridges };
        }
        if (player != null)
        {
            if (playerSpawn != null) player.transform.position = playerSpawn.position;
            player.groundTilemap = ground;
            var motor = player.GetComponent<GridMovement>();
            if (motor != null)
            {
                motor.Configure(ground, motor.Obstacles, motor.GridSize);
                motor.SetWalkableBackground(mapBackground);
            }
        }
        foreach (var panel in GetComponentsInChildren<StoryQuizPanel>(true)) panel.SetJournalMap(mapId, mapTitle);
        foreach (var panel in GetComponentsInChildren<NpcJsonDialoguePanel>(true)) panel.SetJournalMap(mapId, mapTitle);
        if (cameraFollow != null && player != null) cameraFollow.SetTarget(player.transform);
        if (minimap != null) minimap.SetObjectiveMode(false, objective);
        if (mapBackground != null && mapBackground.sprite != null)
        {
            var bounds = mapBackground.bounds;
            if (cameraFollow != null) cameraFollow.SetMapBounds(bounds);
            if (minimap != null) minimap.SetMapSprite(mapBackground.sprite, bounds);
        }
        else if (minimap != null) minimap.SetMap(null, new Bounds(Vector3.zero, new Vector3(40, 30, 0)));
    }
}
