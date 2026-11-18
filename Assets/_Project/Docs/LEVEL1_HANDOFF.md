# Cập nhật mới nhất — 19/11/2026

Bố cục và truyện đã chuyển sang Trái Đất bị xâm lược: 2 NPC hỗ trợ + Scientist tại điểm sơ tán, 7 nhà khu dân cư, 12 rương tiếp tế; map dùng nhiều Tilemap và collider nước/tường. Xem [INVASION_TILEMAP_GUIDE.md](INVASION_TILEMAP_GUIDE.md) cho cấu trúc hiện tại và cách sửa palette. Phần bên dưới là bàn giao bản Lexi trước lần đổi thiết kế, chỉ để đối chiếu lịch sử.

# Màn 1 — bàn giao

Scene dùng: Assets/_Project/Scenes/Sence_1/Scene1.unity.

## Nội dung hiện tại
- 12 rương/câu hỏi, 18 quái (8 tuần tra, 6 đuổi, 4 canh), 10 bụi ẩn, 6 bẫy, 6 NPC mới.
- Đúng câu hỏi: rơi HP hoặc kiếm, phải nhặt mới nhận. Sai: kích động quái quanh rương. Player giữ model và animation gốc.
- NPC: đứng gần và nhấn E hoặc bấm Nói chuyện; nội dung theo 0/2/12 rương. Người áo xám mở sau 2 rương. Dùng UI hội thoại cũ.
- Quái có Idle/Walk/Attack bốn hướng, giữ khoảng cách và gây damage bằng Animation Event.

## F — hình ảnh
- Giữ art thành phố và góc nhìn 3/4, bổ sung bóng elip chung màu đen alpha .35, lệch xuống-phải 1.5 pixel map. Chuẩn hóa ở scene và prefab để vật thể sinh ra có cùng bóng.
- 150 vật thể hoạt động có bóng chân. Sprite quái/NPC dùng Point, không nén, không mipmap. Không thêm package hoặc sửa map nguồn trong bước F.
- SortingGroup đặt thứ tự theo chân; SpriteRenderer giữ layer riêng theo loại. Camera Custom Axis (0,1,0).
- Scene không có Light2D hoạt động, vì vậy giữ hệ sáng hiện tại; không bật hệ đèn mới.
- Dùng sprite hiện có và bóng bổ sung theo quy tắc asset 8(c). Tim/kiếm vẫn là icon sprite có bóng, không phải mô hình ba mặt mới. Các mặt sáng/tối được vẽ sẵn trong art, không phải chiếu sáng 3D thật.
- Không tạo placeholder bằng script trong F. Các sprite sinh bằng imagegen của các bước trước được ghi nguồn/prompt trong ASSET_CREDITS và CITY_CREATURE_PROMPTS/LEXI_NPC_ART.

## File chính của bước hoàn thiện
- Editor/LevelVisualPolish.cs: chuẩn hóa bóng, kiểm tra art/sorting/đường đi; menu Tools > Level 1 > Polish Ground Shadows and Pixel Art.
- Gameplay/Characters/Player/Scripts/NPCInteractor.cs: nút tương tác sử dụng Canvas HUD hiện có.
- Gameplay/Characters/Common/Scripts/CharacterAnimation.cs: không cập nhật Animator khi đã tắt/chưa khởi tạo lúc chuyển scene.
- Editor/LevelHudValidation.cs: bài kiểm tra HUD tách khỏi cửa sổ miễn sát thương; bài B+C vẫn kiểm tra miễn sát thương thật.
- Scene1.unity và các prefab dưới Gameplay: lưu bóng đã chuẩn hóa.
- Prefab tái sử dụng: Gameplay/Interaction/Prefabs/SharedGroundShadow, HidingBush, MonsterTrap; Gameplay/Items/Prefabs/Level1; Gameplay/Characters/Enemies/Prefabs/Level1; Gameplay/Characters/NPC/Prefabs/LexiResident.

## Giả định cốt truyện
Giữ nguyên Linh là người làm nhiệm vụ cứu hộ và thoại Scientist; điều chỉnh NPC mới nói về cứu hộ/khôi phục chữ. Chuyến tàu là nội dung thoại, không thêm cơ chế lái tàu hoặc nhánh quest/save. Mốc cuối tăng từ 4 lên 12 theo lần mở rộng map đã yêu cầu.

## Kiểm tra bằng tay
1. Play Scene1, đọc intro; WASD/phím mũi tên di chuyển. T tấn công khi có kiếm.
2. Đi cạnh NPC: chỉ hiện nút, không tự bật thoại; nhấn E hoặc bấm nút, C tiếp tục thoại.
3. Trả lời sai một rương để dụ quái; đúng để nhặt HP/kiếm. Quan sát quái dừng và đánh thay vì đè player.
4. Đi vào bụi: player mờ, HUD Đang ẩn; dụ quái vào bẫy cam, chờ giữ 4 giây và hồi 8 giây.
5. Đi trước/sau cây, rương, NPC để kiểm tra che khuất. Xem bóng ở độ zoom chơi thực tế.
6. Pause/tiếp tục, hết máu/GameOver, Chơi lại; tiến độ và máu phải reset.
7. Mở 2 rồi 12 rương để đọc thoại mới, xem người áo xám xuất hiện.

Nguồn art: ../Art/ASSET_CREDITS.md và ASSET_CREDITS.md. Ảnh toàn cảnh: Logs/Level1Checks/city-expanded.png.

## Kết quả kiểm tra cuối
HUD/Pause/GameOver/Restart, B+C, D+E, cận chiến và G đều đã chạy lại đạt. Kiểm tra sorting, đường đi và bóng đạt. Pivot 96 frame quái được căn theo chân thật; ghế Lan xếp sau NPC; bóng trùng đã tắt. Ảnh cận cảnh: Logs/Level1Checks/lan-grounding-fixed.png. Scene lưu ở Edit Mode.

