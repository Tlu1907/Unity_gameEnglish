using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterAnimation : MonoBehaviour
{
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string runParameterName = "IsRunning";
    [SerializeField] private Animator animator;
    private SpriteRenderer sprite;
    private bool running;
    private Vector2 facing = Vector2.down;
    private bool directional;
    private bool hasRunParameter;
    private static readonly int FaceX = Animator.StringToHash("FaceX");
    private static readonly int FaceY = Animator.StringToHash("FaceY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public Animator Animator => animator;
    public Vector2 Facing => facing;
    private void Awake() => RefreshController();
    private void Start() => RefreshController();
    private void OnEnable() => GameplayState.Changed += OnStateChanged;
    private void OnDisable() => GameplayState.Changed -= OnStateChanged;
    public void Configure(bool enabledAnimation, string parameter)
    { useAnimator = enabledAnimation; runParameterName = parameter; RefreshController(); }
    public void RefreshController()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        sprite = animator != null ? animator.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
        directional = false;
        hasRunParameter = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            bool x = false, y = false, moving = false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                x |= parameter.nameHash == FaceX && parameter.type == AnimatorControllerParameterType.Float;
                y |= parameter.nameHash == FaceY && parameter.type == AnimatorControllerParameterType.Float;
                moving |= parameter.nameHash == IsMoving && parameter.type == AnimatorControllerParameterType.Bool;
                hasRunParameter |= parameter.name == runParameterName && parameter.type == AnimatorControllerParameterType.Bool;
            }
            directional = x && y && moving;
            animator.applyRootMotion = false;
        }
        if (directional && sprite != null) sprite.flipX = false;
        Apply();
    }
    public void Face(Vector3 direction)
    {
        if (direction == Vector3.zero) return;
        facing = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));
        if (!directional && sprite != null && direction.x != 0f) sprite.flipX = direction.x < 0f;
        Apply();
    }
    public void SetMoving(bool value) { running = value; Apply(); }
    private void OnStateChanged(GameplayMode mode) => Apply();
    private void Apply()
    {
        if (!useAnimator || animator == null || animator.runtimeAnimatorController == null) return;
        if (directional)
        {
            animator.SetFloat(FaceX, facing.x);
            animator.SetFloat(FaceY, facing.y);
            animator.SetBool(IsMoving, running && GameplayState.CanAct);
        }
        else if (hasRunParameter) animator.SetBool(runParameterName, running && GameplayState.CanAct);
    }
}
