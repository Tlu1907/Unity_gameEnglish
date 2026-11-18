# Màn 1 — tiến độ thực hiện

## Bước A: HUD (18/11/2026)

Scene: Assets/_Project/Scenes/Sence_1/Scene1.unity.

- HUD nằm trong QuizCanvas có sẵn: Screen Space Overlay, 1920x1080, Match 0.5.
- Thanh máu dùng Health.Changed; quy đổi hiển thị 40 HP/tim, không thay đổi sát thương hiện có.
- Mảnh chữ đọc LevelObjectives. Bộ đếm đăng ký từng Chest.Opened và chống đếm trùng.
- Ô vũ khí đọc PlayerInventory.Changed; giữ combat bằng T và giáp hiện có.
- Dòng Đang ẩn đọc PlayerHidingState. Bụi cỏ chưa được triển khai trong bước A.
- Pause/GameOver đọc GameplayState. Escape chỉ Pause/Resume khi Playing/Paused.
- Nút Chơi lại tải lại scene hiện tại. Scene1 được thêm/bật trong Build Settings, giữ các entry cũ.
- Thanh giáp PlayerArmorBar dùng chung HUD khi có, không tạo Canvas thứ tư trong Scene1.
- Không sửa quiz, thoại NPC, map hoặc asset bên thứ ba. Không thêm package hay tải art.

## File mới

- Core/Scripts/LevelSessionActions.cs
- Gameplay/Items/Scripts/LevelObjectives.cs
- Gameplay/Characters/Player/Scripts/PlayerHidingState.cs
- UI/HealthBarUI.cs
- UI/ObjectiveUI.cs
- UI/WeaponSlotUI.cs
- UI/HideStatusUI.cs
- UI/GameStateUI.cs
- Editor/LevelHudSetup.cs
- Editor/LevelHudValidation.cs
- Docs/LEVEL1_PROGRESS.md
- Unity tạo các .meta tương ứng.

Các đường dẫn trên tính từ Assets/_Project/.

## File sửa

- Assets/_Project/Gameplay/Characters/Player/Scripts/PlayerArmorBar.cs
- Assets/_Project/Scenes/Sence_1/Scene1.unity
- ProjectSettings/EditorBuildSettings.asset

Các thay đổi đã tồn tại trước bước A không thuộc danh sách này.

## Art tái sử dụng

- Fantasy Wooden GUI Free: normal_ui_set A/UI board Medium  parchment.png (khung).
- Cainos Pixel Art Platformer - Village Props: TX Village Props - Sword (ô trang bị).
- Font TMP đang dùng cho VietNameseText của hệ thoại.
- Không có sprite placeholder mới, không có asset ngoài được tải.

## Kiểm tra đã chạy

Menu Tools/Level 1/Validate HUD in Play Mode:
- Nút Pause và Resume, không đổi timeScale; sát thương bị chặn khi Paused.
- Health.TakeDamage/Heal cập nhật fill; trang bị/bỏ trang bị cập nhật ô vũ khí.
- Hai nguồn ẩn chồng nhau không làm mất cờ ẩn sớm.
- Chest.Open gọi hai lần chỉ tăng bộ đếm một lần và cập nhật text.
- Player chết bật GameOver; nút Chơi lại được gọi qua UnityEvent thật.

Menu Tools/Level 1/Validate HUD Restart:
- Tải lại scene, máu đầy, số rương về 0, GameplayState trở về Playing sau đóng intro.
- Giữ đúng ba Canvas gốc; không tạo thêm Canvas giáp.

Kết quả cuối: HUD_VALIDATION_PASS và HUD_RESTART_PASS. Console không có error.
Đã thoát Play Mode, giữ scene ở Edit Mode. Kiểm tra này không thay thế kiểm thử toàn bộ gameplay.

## Kiểm tra tay

1. Play Scene1, dùng C đọc/đóng intro cũ. Kiểm tra chữ có dấu và HUD ở 16:9, 4:3, cửa sổ nhỏ.
2. Bấm Pause hoặc Escape, thử WASD/T; Tiếp tục phải khôi phục gameplay.
3. Kiểm tra panel Pause/GameOver và nút Thoát. Trong Editor, Thoát dừng Play Mode; trong bản build, đóng ứng dụng.
4. Sau bước B và D, thử rương thật và bụi thật để kiểm tra tiến độ/trạng thái ẩn từ gameplay.
5. Thử Chơi lại trong bản build. Chưa thực hiện build ứng dụng ở bước A.

## Bước B+C: Gameplay Màn 1 (18/11/2026)

### Script mới
- `Gameplay/Items/Scripts/WeaponPickup.cs` — trao vũ khí cho player khi mở rương liên kết.
- `Gameplay/Characters/Enemies/Scripts/GuardEnemy.cs` — quái canh cổng (đứng yên, chỉ tấn công khi đến gần).
- `Gameplay/Characters/Enemies/Scripts/BossEnemy.cs` — boss Cổng Bắc (HP 300, rớt key khi chết).

### Editor script mới
- `Editor/Map1QuestionSeeder.cs` — tạo 2 QuestionData (sword, shield). Chạy: **EarthDefender → Tạo câu hỏi Màn 1**.
- `Editor/HuyDialogueSeeder.cs` — tạo DialogueData cho NPC Huy. Chạy: **EarthDefender → Tạo thoại Huy**.
- `Editor/Map1GameplaySetup.cs` — đặt toàn bộ gameplay vào Scene1. Chạy: **Tools → Level 1 → Build Map1 Gameplay**.

### Quy trình chạy trong Unity Editor (theo thứ tự)
1. Mở Scene1 (không ở Play Mode).
2. Menu **EarthDefender → Tạo câu hỏi Màn 1** (tạo Q_map1_sword, Q_map1_shield).
3. Menu **EarthDefender → Tạo thoại Huy** (tạo Huy_Station.asset).
4. Menu **Tools → Level 1 → Build Map1 Gameplay** (đặt rương, quái, NPC Huy vào scene).
5. Điều chỉnh vị trí các vật thể nếu cần (xem hằng số vị trí trong Map1GameplaySetup.cs).

### Vị trí mặc định (đơn vị Unity, chỉnh trong Map1GameplaySetup.cs)
| Vật thể | Vị trí |
|---------|--------|
| Rương Sword (quiz) | (4, 9) |
| Rương Shield (quiz) | (6, 9) |
| Rương Cổng ga | (14, 4) |
| Rương Boss | (5, 18) |
| Quái tuần tra Park ×2 | (3,7) và (7,7) |
| Quái canh Cổng ga | (14, 2) |
| Boss Cổng Bắc | (5, 16) |
| NPC Huy | (14, 4) |

### Giới hạn bước B+C
- Rương và NPC Huy dùng **placeholder màu** (chưa gán sprite thật). Thay sprite sau ở bước F (Art).
- Câu hỏi sword/shield dùng text mô tả `(Hình: thanh kiếm)` thay cho ảnh thật — cần gán `portrait` hoặc tích hợp hình vào QuestionData sau.
- `ChestQuizInteraction.useFlaskQuiz = false` — dùng QuizController thông thường (4 nút đáp án trong scene phải đủ).
- D+E (ẩn/bẫy), G (NPC/cốt truyện đầy đủ), F (art/bóng) chưa thực hiện.
- Thoại cũ vẫn dùng timeScale theo hiện trạng và được giữ nguyên như phạm vi đã thống nhất.

## Cập nhật thực tế Scene1 — 2026-11-18
Phần này thay thế mô tả B+C và phần thưởng ở các mục cũ phía trên.
- Giữ nguyên model/controller nhân vật gốc; kiếm là sprite gắn riêng khi nhặt được vũ khí.
- Bốn rương trong Level1Content dùng câu hỏi hiện có. Đúng: mở rương, tung item ra map; item đáp xuống sau 0,6 giây rồi mới có thể nhặt. Không tự cộng HP/trang bị khi trả lời.
- Chest_Park rơi kiếm; ba rương còn lại rơi HP (40 HP/tim). Chưa dùng phần thưởng ngẫu nhiên.
- Không còn HP/kiếm đặt sẵn miễn phí. HP không bị tiêu thụ khi đầy máu. Mỗi rương chỉ rơi một item.
- Sai: không có phần thưởng; kích động quái trong bán kính 6 đơn vị quanh rương, truy đuổi 8 giây kể cả patrol/guard. Sau đó trở lại hành vi thường; chaser vẫn có thể tiếp tục nếu nhìn thấy player. Đóng câu hỏi không kích động quái.
- 9 quái: 4 patrol, 3 chaser, 2 guard. Điều hướng tránh hồ/kênh/NPC; player có 0,75 giây miễn nhiễm sau khi trúng đòn.
- Không sửa logic/UI quiz hoặc thoại cũ; dùng Chest.Opened và QuizEvents.OnAnswerSubmitted cho phần thưởng/báo động.
- Menu cấu hình: Tools/Level 1/Require Quiz For Health and Weapon (Edit Mode).
- Menu kiểm tra: Tools/Level 1/Validate Items and Enemies (Play Mode mới).
- D+E/G/F vẫn chưa hoàn tất trong bước này.
- Đã chạy Play Mode: LEVEL_BC_TEST_PASS với luồng rơi/nhặt HP và kiếm, sai kích động đúng phạm vi, hết báo động, hủy câu hỏi, giữ model, chống nhận lặp, đầy máu, pause, mục tiêu 4/4 và điều hướng.

## Mở rộng thành phố và animation quái — 2026-11-18
- Thay hình quái cũ bằng ba species: MossBeetle (tuần tra), DustGremlin (truy đuổi), ScrapGolem (canh gác).
- Ba sprite sheet mới, mỗi sheet 16 frame: bốn hướng × bốn pha bước chân; nền trong suốt, Point filtering.
- Dùng Animation Clip/Animator chuẩn Unity: 8 clip mỗi loại (Idle/Walk × Down/Left/Right/Up), tổng 24 clip và 3 controller; Walk 8 FPS, looping. CharacterAnimation điều khiển FaceX/FaceY/IsMoving. Không dùng script kéo hình/bob để giả bước đi.
- Tăng lên 18 quái: 8 patrol, 6 chaser, 4 guard; 12 câu hỏi/rương riêng biệt, HUD mục tiêu 12. Luồng rơi item và báo động khi sai giữ nguyên.
- Thêm 45 vật thể trong CityExpansion/CityScenery: nhà, cây, bụi cây, ghế, đèn, thùng rác và bồn hoa. Tái sử dụng Item.png của project, không vẽ đè map.
- Kiểm tra tĩnh: CITY_EXPANSION_VALIDATION_PASS (24 clip, frame khác nhau, đủ 12 câu hỏi) và CITY_ROUTES_PASS (12 rương tiếp cận được, 18 vị trí quái không bị chắn).
- Mở cửa sổ Animation để chỉnh: chọn CreatureVisual của một quái, Window > Animation > Animation; hoặc Tools > Level 1 > Open Creature Animation.
- Asset: Gameplay/Characters/Enemies/Animations/CityCreatures; Art/Sprites/Monsters/*Walk.png. Prompt tạo ảnh lưu trong CITY_CREATURE_PROMPTS.md.
- Play Mode đã đạt LEVEL_BC_TEST_PASS: quan sát được frame Walk đổi thật ở cả ba loại quái; phần thưởng rơi/nhặt, báo động/phục hồi, giữ model player và tiến độ 12/12 đều đạt.

## Cận chiến cho quái — 2026-11-18
- Chỉ thêm animation tấn công cho quái; không đổi animation/model player.
- 12 clip Attack mới (4 hướng cho mỗi loại), tổng 36 clip Idle/Walk/Attack. Clip Attack 0,8 giây: Strike ở 0,25 giây, FinishAttack ở 0,78 giây; không loop.
- EnemyAttack chỉ gây sát thương qua Animation Event, có wind-up, recovery và cooldown 1,5 giây. Kiểm tra lại tầm, hướng và vật cản lúc ra đòn; né khỏi tầm sẽ tránh được đòn. Pause dừng cả clip lẫn cooldown.
- EnemyBrain đuổi theo tâm collider chân player thay vì pivot ở trên người. Giữ khoảng trống tối thiểu 1,1 đơn vị, tầm đánh 1,5; không di chuyển trong lúc đánh. GridMovement kiểm tra khoảng cách từng bước nội suy của riêng quái để không lao xuyên player đang di chuyển.
- 18 quái có collider chân solid và Rigidbody2D Kinematic. Giữ nguyên nhánh Rigidbody2D Dynamic/FixedUpdate của player.
- Đã đạt ENEMY_MELEE_ASSETS_PASS và ENEMY_MELEE_PLAY_PASS: frame đánh có thay đổi, không damage trước wind-up, đúng một hit/swing, đứng yên khi đánh, pause, né đòn và tự đuổi rồi dừng đánh. Khoảng cách nhỏ nhất trong bài kiểm tra tự đuổi là 1,50 đơn vị, collider hai bên không đè nhau.
- Menu kiểm tra riêng: Tools/Level 1/Test Enemy Melee and Distance (Play Mode mới). Các thay đổi vị trí/HP phục vụ kiểm tra chỉ tồn tại trong Play Mode.

## D+E — Bụi ẩn và bẫy quái
- Scene1/HidingAndTraps: 10 HidingBush, 6 MonsterTrap. Hai bụi gần sân ga; phần còn lại ở khu dân cư/công viên. Nâng cấp các bụi trang trí do bước mở rộng tạo, giữ bản cũ inactive.
- Prefab ở Gameplay/Interaction/Prefabs: HidingBush, MonsterTrap, SharedGroundShadow.
- HidingSpot dùng trigger và theo dõi từng nguồn ẩn; hỗ trợ vùng chồng nhau, bỏ nguồn khi bụi tắt. PlayerHidingVisual giảm alpha thân/kiếm còn 50%, không đổi Animator hoặc chuyển động. HUD Đang ẩn dùng sự kiện có sẵn.
- EnemyBrain bỏ đuổi và ký ức báo động khi player ẩn ngoài 1 ô; trong 1 ô vẫn có thể phát hiện. Đòn đánh đã bắt đầu và logic sát thương không được miễn trừ chỉ vì vào bụi.
- EnemyBrain.Immobilize là API giữ chân duy nhất cho mỗi quái; GridMovement và EnemyAttack cùng đọc IsImmobilized. Không dùng MonsterFreezeController cũ (API tương thích quiz, hiện không có giữ chân riêng từng quái).
- Trap: chỉ quái kích hoạt, giữ 4 giây, sau đó cooldown 8 giây. Sẵn sàng màu cam, đang giữ nhấp nháy vàng, hồi bẫy màu tối. Player đi qua an toàn.
- Timer, chuyển động và animation bị giữ đều tuân theo GameplayState: pause/quiz/dialogue không tiêu hao thời gian bẫy. Không thêm Time.timeScale hay package.
- Kiểm tra tay: chạy vào bụi để thấy Đang ẩn và player mờ; dụ quái qua bẫy cam; thử pause khi quái bị giữ; chờ bẫy sáng lại để kích hoạt lần nữa.
- Menu kiểm tra Play Mode mới: Tools/Level 1/Validate Hiding and Traps.
- Đã đạt HIDING_TRAPS_PLAY_PASS và chạy lại LEVEL_BC_TEST_PASS sau khi thêm D+E; toàn bộ 12 rương và 18 quái vẫn hoạt động. Ảnh kiểm tra ẩn: Logs/Level1Checks/hiding-bush-tested.png.
- Chạy lại ENEMY_MELEE_PLAY_PASS sau D+E: animation tấn công, né đòn, khoảng cách player và pause vẫn đạt. Đã thoát Play Mode sau kiểm tra.

## G — NPC và tiến độ truyện
- Thêm Hải, Tí, Ba, Mận, Lan, người áo xám trong LexiStoryNPCs; 17 DialogueData song ngữ theo mốc 0/2/12 rương. Người áo xám mở sau 2 rương.
- Tái sử dụng NPCIntroDialogue và GameplayState hiện có; giữ thoại gốc Linh/Scientist. Dấu hội thoại biến mất sau khi đọc và xuất hiện khi có giai đoạn mới.
- Sáu Animator idle/blink sử dụng Art/NPC/LexiResidents.png; prompt và nguồn ở LEXI_NPC_ART.md.
- LEXI_STORY_PLAY_PASS: intro gốc, tiếp cận/rời/quay lại NPC, tránh mở lặp, độc quyền UI, chặn khi pause/quiz, mốc tiến độ, silhouette và phục hồi gameplay đều đạt.

## Sorting layer theo loại vật thể
- SpriteRenderer chia Player, Enemies, NPCs, Chests, Items, Vegetation, Buildings, Props, Shadows, Effects; nền ở Background.
- Mỗi vật thể có SortingGroup trên WorldDepth. WorldDepthSort lấy chân collider để tính thứ tự trước/sau, cho phép cây che nhân vật phía sau dù sprite thuộc các layer riêng.
- Áp dụng vào Scene1 và prefab gameplay, gồm cả HP/vũ khí sinh ra khi trả lời đúng. HUD giữ Canvas riêng.
- CATEGORY_SORTING_PASS: 226 renderer đã phân loại. FRONT_BACK_DEPTH_PASS: player đổi đúng thứ tự khi trước/sau cây; NPC và prefab phần thưởng đủ nhóm chiều sâu.
- Ảnh toàn cảnh: Logs/Level1Checks/city-expanded.png. Đã thoát Play Mode sau kiểm tra. F (polish tổng thể và bàn giao cuối) còn lại.

## Tương tác NPC chủ động
- Thay tự bật hội thoại khi tiếp xúc bằng NPCInteractor: nhấn E hoặc bấm nút Nói chuyện ở cuối màn hình. Chỉ hiện nút cho NPC gần nhất có thể nói chuyện.
- Khoảng cách tối đa .85 đơn vị tính giữa collider chân; kiểm tra vật cản trên đường nối. Collider chân NPC .42 x .28 đơn vị, tắt sensor tự kích hoạt cũ.
- Áp dụng cả sáu StoryNPC và Scientist/NPCDialogueTrigger; intro Linh đầu màn giữ nguyên. NPCDialogueTrigger chỉ được gọi chủ động, vẫn giữ quy tắc onlyOnce.
- Chạy lại LEXI_STORY_PLAY_PASS: đứng gần/quay lại không tự mở, tương tác gần thành công, ở xa bị từ chối; các mốc thoại và pause/quiz vẫn đạt. Scene đã lưu và thoát Play Mode.

## F và kiểm tra cuối — 19/11/2026
- Chuẩn hóa bóng cho scene/prefab, tắt bóng trùng do instance và prefab cùng có shadow. Căn mép bóng theo pixel có alpha thật trong sprite, bao gồm nhà/cây/ghế; không lấy đáy ô ảnh trong suốt.
- GroundedCreaturePolish cập nhật pivot 96 frame quái bằng Sprite Editor Data Provider, giữ spriteID/GUID và animation references. Ghế Lan dùng cùng mốc chiều sâu với NPC nhưng thứ tự thấp hơn.
- Nút nói chuyện chuyển về Canvas HUD sẵn có; sửa lỗi cập nhật Animator khi object đang bị tắt lúc reload scene.
- Đã chạy lại: HUD_VALIDATION_PASS, HUD_RESTART_PASS, LEVEL_BC_TEST_PASS, HIDING_TRAPS_PLAY_PASS, ENEMY_MELEE_PLAY_PASS (khoảng cách nhỏ nhất 1,50), LEXI_STORY_PLAY_PASS. Kiểm tra tĩnh VISUAL_POLISH_PASS, CATEGORY_SORTING_PASS, CITY_ROUTES_PASS.
- Bài kiểm tra HUD không áp dụng cửa sổ miễn sát thương trong riêng bài UI; bài cận chiến tắt trigger bẫy/bụi trong riêng lượt kiểm tra để tránh giữ chân/ẩn che khuất kết quả. Bài B+C và D+E kiểm tra độc lập các cơ chế đó ở cấu hình thật.
- Ảnh: Logs/Level1Checks/lan-grounding-fixed.png và city-expanded.png. Tài liệu tổng hợp file, prefab, giới hạn art và kiểm tra tay: LEVEL1_HANDOFF.md.

## Đổi thiết kế sang thành phố bị xâm lược — 19/11/2026
- Dừng đóng gói theo yêu cầu đổi bố cục. Tận dụng palette 1.444 tile 33px sẵn có, tách 6 Tilemap hiển thị, tắt ảnh MapBackground; thêm Collision_Water_Walls 11px với collider solid, chừa cầu/bậc thang.
- 7 nhà ở khu dân cư; 2 NPC hỗ trợ Hải/Ba cùng Scientist ở điểm sơ tán. NPC dân thường khác inactive, asset/thoại cũ giữ lại. 12 kho tiếp tế/18 quái phân vùng lại; HUD Tiếp tế x/12.
- CityTerrain + bổ sung GridMovement: xét chân collider, đọc lớp bậc thang để giảm tốc .72; giữ Rigidbody2D/FixedUpdate gốc.
- Backup: Scenes/Backups/Scene1_BeforeInvasionLayout.unity. Không đổi scene khởi động hoặc build trong bước này.
- INVASION_STATIC_PASS, CITY_ROUTES_PASS (845 ô), INVASION_PLAY_PASS đã đạt; lần Play cuối không có lỗi. Đã về Edit Mode và lưu Scene1.
- Hướng dẫn chỉnh tile/va chạm và thay đổi bố cục: INVASION_TILEMAP_GUIDE.md. Ảnh mới Logs/Level1Checks/city-invasion-layout.png.

## Sửa collider toàn map bằng Tile Palette — 19/11/2026
- CityPaletteCollisionRepair dùng GridPaintingState và GridBrush.Paint/Erase của Unity Tile Palette, không ghi YAML tilemap. Tạo palette City Collision và chọn Collision_Water_Walls làm Active Tilemap.
- Xóa 870 ô chặn sai trên đất, vỉa hè, đường ray và bậc thang; chỉ giữ ô va chạm nước theo đường bờ đã có, chừa cầu.
- Player dùng các surface Tilemap để kiểm tra biên map và collider nước 11px để chặn chuyển động. Không còn dùng ô rỗng của lưới điều hướng quái để chặn mép vỉa hè của player. Grid/Ground vẫn phục vụ quái.
- Dời cột đèn khỏi lối xuống phía đông công viên, trụ cứu hỏa khỏi bậc thang phía tây ga, bồn hoa khỏi bậc thang phía đông ga; thu collider trụ cứu hỏa/bồn hoa về chân.
- CITY_WALKWAY_PLAY_PASS: 26 lượt đi Rigidbody thật trên 13 tuyến, cả hai chiều, giữ collider đồ vật tĩnh. INVASION_PLAY_PASS: nước vẫn chặn, cầu đi được, bậc thang giảm tốc, thoại/quiz 0/2/12 vẫn đạt.
- File chính: Editor/CityPaletteCollisionRepair.cs, Editor/CityWalkwayValidation.cs, Gameplay/Grid/Scripts/CityTerrain.cs và GridMovement.cs; Scene1 được lưu ở Edit Mode.

## Rương chỉ trả lời một lần — 19/11/2026
- Sai: báo động quái theo phạm vi/thời gian hiện có, mở rương vĩnh viễn trong lượt chơi, đổi sprite/tắt collider như đúng, tính tiến độ một lần; không rơi HP/kiếm.
- Chest đánh dấu AllowsReward=false trước khi phát Opened; ChestWeaponReward không sinh loot cho rương thất bại. ChestQuizAlarm chỉ xử lý câu trả lời đã gửi nên đóng/hủy quiz không tiêu hao rương.
- Đúng: vẫn mở và rơi item như trước. Hủy khi chưa trả lời vẫn có thể thử lại. Chơi lại màn đặt lại toàn bộ trạng thái.
- SINGLE_ATTEMPT_CHEST_PASS: sai không được hỏi lại kể cả sau cooldown, không nhận thưởng qua sự kiện lặp, alarm đúng phạm vi, sprite/collider mở đúng, tiến độ chỉ tăng một lần; đúng vẫn có thưởng, hủy vẫn thử lại được, hỗn hợp đúng/sai đạt 12/12.
- Menu kiểm tra mới: Tools > Level 1 > Test Single Attempt Chests (Play Mode mới). Bài B+C cũ có bước trả lời lại rương sai thuộc luật chơi trước; dùng bài mới này để kiểm tra luật một lần.

## 2026-11-19 Camera limits and station exit
- PlayerCameraFollow2D constrains the full orthographic viewport to occupied city surface bounds (0.02-unit inset); adapts zoom for extreme aspect ratios.
- StationExitPortal appears in front of the station entrance only after all 18 active enemies die. Disabling enemies does not count. Cyan/violet rings pulse; a notification directs the player to the station.
- Walking into the unlocked trigger loads Assets/_Project/Scenes/Scene_Test/SampleScene.unity, the user-selected temporary destination already enabled in Build Settings.
- CITY_EXIT_PLAY_PASS: 45 camera positions/aspects, no early unlock with 1 enemy alive, last real death unlocks, physical trigger successfully loads SampleScene.
- Existing destination limitation: SampleScene reports missing Player and PF Props - Chest 01 prefab GUIDs on load. Destination scene was not rewritten.
- Setup / test / preview menus: Tools > Level 1 > Install Camera Bounds and Station Exit / Test Camera Bounds and Station Exit / Preview Station Exit. Run the test in fresh Play Mode; it consumes enemies and loads SampleScene only in that play session.

## Player melee reach and enemy HP feedback
- Fixed player attack origin and target geometry to use feet collider centers consistently with enemy combat; increased reach from 1.4 to 1.8 so enemies stopping around 1.5 can be hit.
- Solid obstacles still block swings; trigger volumes and the participants' own colliders do not. Facing, cooldown, unarmed 25 and weapon 50 damage preserved.
- EnemyBrain creates a small non-interactive world-space EnemyHealthBar at runtime. It follows the animated body, updates with Health.Changed, changes green/yellow/red and hides on death.
- Weapon HUD now explains T for both unarmed and equipped combat.
- Runtime C# assembly compiled successfully using Unity's cached response file and bundled Roslyn compiler (existing obsolete API warnings). Batch Play Mode regression could not execute: Unity exits with code 1 during project startup. In-editor gameplay/visual verification remains pending.
