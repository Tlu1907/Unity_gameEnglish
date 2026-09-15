using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    private PlayerMovement playerMovement;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Monster"))
            return;

        if (playerMovement != null && playerMovement.CanDefeatMonster())
        {
            Destroy(collision.gameObject);
            Debug.Log("Player dùng vật phẩm để giết quái.");
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.TakeDamage();
        }
        else
        {
            Debug.Log("Player bị quái đánh vì chưa có vật phẩm.");
        }
    }
}