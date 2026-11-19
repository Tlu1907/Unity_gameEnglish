# Ba cơ chế quái theo cốt truyện

Chọn **Story encounter → Encounter Mode** trên `EnemyBrain` / `MonsterGridAI` của từng quái trước khi Play.

| Chế độ | Hành vi |
|---|---|
| Tuần tra thông thường | Giữ hành vi tuần tra và đuổi theo trong phạm vi cũ. |
| 1. Gương | Đứng chờ, chưa tấn công và chưa nhận đòn của player. Sau khi hoàn tất `q1_spirit` **và thực sự nhận trang bị**, bắt đầu đuổi player kể cả ngoài phạm vi phát hiện cũ. Kích hoạt một lần cho phiên chơi. |
| 2. Canh giữ | Đánh như quái thường. NPC gán trong **Archaeologist To Reveal** ẩn lúc bắt đầu. Hạ quái sẽ hiện NPC đó hoặc tạo NPC từ prefab, đúng một lần. |
| 3. Phòng nghiên cứu | Ẩn hình ảnh và collider, không di chuyển hoặc tấn công. Chỉ hiện và đuổi player sau khi đã vào phòng nghiên cứu và giải mã thành công tài liệu. |

Scene1 đã gán `Monster (4)` cho cơ chế gương và `Monster (3)` cho cơ chế cứu `ScientistNPC` (nhà khảo cổ). Những quái khác giữ chế độ tuần tra. Các vị trí quái vẫn như trước.

Với quái canh giữ, có thể kéo một Transform vào **Archaeologist Spawn Point** để chỉ định vị trí xuất hiện. Nếu để trống, NPC xuất hiện theo **Archaeologist Offset** gần nơi quái chết; mã tìm hướng khác khi vị trí đó bị chặn. Nếu không tìm được ô phù hợp, NPC giữ vị trí đã đặt trong scene. NPC vẫn dùng hội thoại `q2_archaeologist`; hạ quái không tự trả lời câu `bridge`.

## Chuẩn bị cho phòng nghiên cứu

Phòng nghiên cứu và câu hỏi tài liệu chưa được xây theo phạm vi trước đó. Đã có hai prefab để ghép khi làm phần này:

- `Gameplay/Characters/Enemies/Prefabs/Monster_Laboratory.prefab`: quái chế độ 3, giữ GameObject **active** để nó chờ tiến độ; hình ảnh/collider tự ẩn.
- `Gameplay/Items/Prefabs/ResearchRoomStoryTrigger.prefab`: đặt vùng trigger ở phòng, chỉnh BoxCollider2D phủ khu vực vào phòng.

Khi player vào vùng trigger sau câu `bridge`, game ghi nhận đã tới phòng. Chỉ nối sự kiện **giải mã đúng / thành công** của câu đố tài liệu tới `ResearchRoomStoryTrigger.OnDocumentsDecoded()`. Không gọi phương thức này khi mới vào phòng hoặc khi trả lời sai.

Có thể tích hợp bằng code qua `StoryProgress.TryEnterResearchRoom()` và `StoryProgress.TryDecodeResearchDocuments()`. Cả hai đều kiểm tra tiến độ; giải mã không có hiệu lực trước khi vào phòng, trước câu `bridge`, hoặc sau GameOver. Bắt đầu phiên Play mới sẽ đặt lại hai tín hiệu này.
