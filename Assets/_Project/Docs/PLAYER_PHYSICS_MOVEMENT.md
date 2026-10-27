# Player di chuyển bằng vật lý 2D

Player hiện di chuyển liên tục bằng Rigidbody2D thay vì nhảy từng ô. Giữ bốn hướng và ưu tiên Up > Down > Left > Right. Thả phím thì dừng ngay. Quái vẫn sử dụng coroutine bước ô trước đây.

## Luồng code

```text
PlayerInputReader.Update đọc phím
    → GridMovement.SetMovementInput
    → GridMovement.FixedUpdate đặt Rigidbody2D.linearVelocity
    → Physics2D xử lý va chạm với collider vật cản
    → GridMovement.LateUpdate cập nhật IsMoving sau solver
    → CharacterAnimation cập nhật FaceX/FaceY/IsMoving
    → camera theo Player trong LateUpdate
```

Giữ tên/component GridMovement để không mất tham chiếu và dữ liệu scene hiện có. Khi object có PlayerMovement hoặc PlayerInputReader, motor tự bật nhánh vật lý lúc chạy. Không có hai motor điều khiển Player. Trong nhánh này không snap tâm ô, không dùng OverlapBox 0.8 × 0.8 để chặn vật cản, không chạy coroutine bước đi hoặc phát StepCompleted. Grid Size không còn quyết định bước của Player.

HasGround vẫn kiểm tra vị trí dự kiến trong từng bước vật lý để giữ giới hạn nền: Tilemap có tile thì theo tile; Tilemap rỗng thì theo Walkable Background đã gán. Phần kiểm tra nền và biên map vẫn độc lập với collider tường.

## Thiết lập Player

Không cần gắn script mới hoặc chạy menu setup để bật hành vi runtime. Thoát Play Mode, Refresh đợi compile, rồi Play lại. Motor tự đặt Rigidbody2D Dynamic, Gravity Scale 0, Freeze Rotation Z, Linear Damping 0, Interpolate và Continuous collision detection. Menu Player setup cũng đã cập nhật cùng cấu hình này nếu cần lưu trong Edit Mode.

- Collider2D của Player phải enabled, Is Trigger tắt. Đặt vùng va chạm quanh chân nếu muốn đi phía sau mái nhà/tán cây.
- Vật cản tĩnh chỉ cần Collider2D enabled, Is Trigger tắt. Không cần Rigidbody2D.
- Các layer của Player và vật cản phải được phép va chạm trong Project Settings → Physics 2D. Khi Player dùng nhánh vật lý, layer collision matrix quyết định chặn collider; Obstacle Mask cũ chỉ còn phục vụ nhánh grid/tương thích.
- Không thay đổi kích thước collider hay sprite của bạn tự động. Collider Player lớn vẫn khiến nhân vật dừng cách tường theo đúng kích thước đó.
- Speed chỉnh tại PlayerInputReader. Tránh gắn thêm script tự đổi Transform/vận tốc Player.

## Rương và câu hỏi

PlayerInteractor nhận OnCollisionEnter2D/OnCollisionStay2D đối với rương đặc, hoặc OnTriggerEnter2D/OnTriggerStay2D đối với điểm câu hỏi trigger. Tìm Chest/QuizTrigger ở object va chạm hoặc cha của nó rồi gọi Interact khi Player đang có hướng input và GameplayState cho hành động.

Key vẫn chứa collider + Chest + ChestQuizInteraction; visual chứa SpriteRenderer. Khi chạm rương và đẩy về phía rương, quiz mở; đúng thì đổi Items_5. Sai/hủy vẫn có thể thử lại sau Retry Delay. Không dùng kiểm tra ô đích để mở rương từ xa.

GameplayState chuyển sang Quiz/Paused/GameOver sẽ xóa vận tốc ngay, không trượt tiếp trong lúc trả lời. Khi trở lại Playing, input đang giữ có thể tiếp tục cho chuyển động. Inventory/ngoại hình tiếp tục dùng clip của bạn như trước.

## Kiểm tra trong Editor

1. Giữ hướng vào tường: Player tiến sát rồi dừng; Idle khi bị chặn hoàn toàn, vẫn quay mặt theo hướng input.
2. Thả phím: dừng ngay, không cố hoàn tất một ô.
3. Đổi sang hướng dọc tường: đi tiếp được theo hướng đó.
4. Đi vào Key: quiz mở, không tiếp tục di chuyển trong quiz; thử sai/hủy/đúng.
5. Đi quanh cây/nhà: vùng chặn theo cả collider Player và collider vật thể. Thu collider ở chân Player nếu hình ảnh đầu/thân làm bạn cảm thấy khoảng cách còn lớn.
6. Thử mặc/tháo trang bị khi đi, camera và hướng nhìn phải giữ đúng.

Test EditMode kiểm tra cấu hình Dynamic, vận tốc bốn hướng, thả phím/Pause xóa vận tốc, chặn ngoài giới hạn nền và không chạy nhánh bước ô trên Player. Va chạm thật với tường/rương cần kiểm tra thêm trong Play Mode.
