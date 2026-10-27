# Thiết lập vật cản thủ công

Công cụ MapObstacleSetup đã được gỡ theo yêu cầu. Không còn menu Map Obstacles hoặc logic tự thêm collider cho môi trường khi mở Unity.

## Cấu trúc khuyến nghị

- Object gốc: Transform scale (1,1,1), Collider2D và layer Obstacle.
- Object con Visual: SpriteRenderer; chỉnh kích thước hình ảnh tại đây.
- Tránh collider trùng giữa object gốc và Visual.

## Thiết lập

1. Thêm BoxCollider2D hoặc PolygonCollider2D lên object gốc.
2. Chỉnh Size/Offset hoặc Edit Collider quanh phần thực sự cần chặn.
3. Đặt layer của object chứa collider thành Obstacle; Is Trigger tắt với vật cản đặc.
4. Chọn Player → GridMovement → Obstacle Mask và tích Obstacle.
5. Lưu scene; lưu object thành prefab để tái sử dụng.

Các collider đã có trong scene/prefab vẫn được giữ. Nếu vùng chặn quá lớn, tự thu nhỏ hoặc xóa collider không cần thiết. Gỡ công cụ không tự xóa cấu hình đã lưu.

Player hiện di chuyển bằng Rigidbody2D Dynamic: collider thật và layer collision matrix quyết định vùng chặn, không dùng vùng kiểm tra ô 0.8 × 0.8. Nhánh bước ô cũ chỉ còn áp dụng cho quái/tương thích. Nhà/cây trong ảnh nền cần object collider riêng nếu muốn chặn. Xem [PLAYER_PHYSICS_MOVEMENT.md](PLAYER_PHYSICS_MOVEMENT.md).
