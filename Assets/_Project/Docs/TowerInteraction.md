# Tòa tháp trên map

Scene1 đã thay gương/rương Key bằng `Gameplay/Items/Prefabs/Tower.prefab`, giữ nguyên vị trí và tỉ lệ.

Hierarchy: **Tower → Visual**. Visual đã có SpriteRenderer để gán hình tháp vào **SpriteRenderer → Sprite**. Nếu ảnh đang import ở chế độ Multiple, mở mũi tên cạnh ảnh trong Project và kéo sprite con phù hợp vào trường Sprite. Có thể thêm các object con nếu tháp gồm nhiều mảnh. Chỉnh Position/Scale của Visual để đặt chân tháp đúng vị trí; script không ghi đè hình hoặc transform này khi chơi.

Root Tower có BoxCollider2D trigger, TowerDialogueTrigger và TowerCollisionBounds. `Tower/Collision` có BoxCollider2D solid trên layer Obstacle để chặn player/quái. Không có Chest, ChestQuizInteraction, MirrorTreasureItem hoặc hoạt ảnh mở vật phẩm.

Collider được chỉnh thủ công và giữ nguyên khi Play. Chỉnh BoxCollider2D trên Tower cho vùng tương tác, và BoxCollider2D trên Tower/Collision cho vùng chặn; vùng trigger nên lớn hơn vùng solid. TowerCollisionBounds chỉ căn lại khi bạn chủ động chọn context menu **Fit Tower Colliders To Visuals** trên component; không tự chạy trong Awake, OnEnable hoặc LateUpdate.

Player chạm vùng trigger sẽ tự mở hội thoại JSON q1_spirit, không cần bấm phím tương tác hoặc giữ phím di chuyển. Đóng giữa chừng không nhận thưởng; rời hẳn vùng rồi quay lại để thử lại. Nhiều collider trên player không gây mở lặp khi vẫn còn chạm tháp.

Hoàn thành đủ sáu câu và đóng phần kết sẽ trao trang bị cho chính player đã chạm tháp. Tiến độ và quái chờ nhận trang bị hoạt động như trước. Tòa tháp không biến mất, đổi sprite hoặc phát hoạt ảnh mở sau khi hoàn thành; không mở lại câu hỏi đã hoàn thành trong cùng phiên Play.

Trường Dialogue Panel trên TowerDialogueTrigger là tùy chọn: có thể gán khung hội thoại tĩnh của scene, hoặc để trống để tự tìm StoryQuizPanel hiện có.
