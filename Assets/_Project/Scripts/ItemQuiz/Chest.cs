using UnityEngine;

public class Chest : MonoBehaviour
{
    [Header("Chest state")]
    public GameObject openChestPrefab; // prefab chest đã mở, kéo vào Inspector
    private bool isOpened = false;

    public void Open()
    {
        if (isOpened) return; 
        isOpened = true;

        ChangeToOpenVisual();

        Debug.Log("Hiện câu hỏi"); // Sau naỳ đổi thành code hiện bảng.
    }

    private void ChangeToOpenVisual()
    {
        if (openChestPrefab != null)
        {
            Instantiate(openChestPrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }
    }
}