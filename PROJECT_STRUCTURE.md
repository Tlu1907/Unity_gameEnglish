# Cấu trúc dự án DoAn_CK

## Tổng quan

Player hiện di chuyển liên tục bằng Rigidbody2D Dynamic trong FixedUpdate. GridMovement giữ nhánh bước ô cho quái; các mô tả Player đi từng ô bên dưới thuộc thiết lập cũ. Tham khảo [PLAYER_PHYSICS_MOVEMENT.md](Assets/_Project/Docs/PLAYER_PHYSICS_MOVEMENT.md) cho luồng hiện hành.

Game Unity 2D top-down, di chuyển từng ô Tilemap, tương tác câu hỏi tiếng Anh để mở rương và chạm quái để nhận sát thương hoặc tiêu diệt quái khi có vũ khí.

- Unity: **6000.5.5f1**.
- Đồ họa: URP và Tilemap; giao diện: Unity UI, TextMesh Pro.
- Scene bật trong Build Settings: `Assets/_Project/Scenes/Scene_Test/SampleScene.unity`.
- Input vẫn dùng `UnityEngine.Input`, WASD/phím mũi tên; giữ phím đi liên tục từng ô, ưu tiên Up > Down > Left > Right, không đi chéo.
- Không thêm package, framework hoặc thay asset bên thứ ba.

## Cấu trúc chính

```text
Assets/_Project/
├── Core/Scripts/
│   ├── GameplayState.cs
│   └── MonsterFreezeController.cs        # API tương thích cũ
├── Gameplay/
│   ├── Characters/
│   │   ├── Common/Scripts/CharacterAnimation.cs
│   │   ├── Player/
│   │   │   ├── Scripts/
│   │   │   │   ├── PlayerMovement.cs     # Adapter giữ dữ liệu Inspector cũ
│   │   │   │   ├── PlayerInputReader.cs
│   │   │   │   ├── PlayerInteractor.cs
│   │   │   │   ├── PlayerInventory.cs
│   │   │   │   └── PlayerCollision.cs
│   │   │   ├── Prefabs/PLayer.prefab
│   │   │   └── Animations/
│   │   └── Enemies/
│   │       ├── Scripts/
│   │       │   ├── EnemyBrain.cs
│   │       │   ├── EnemyAttack.cs
│   │       │   └── MonsterGridAI.cs      # Tên/GUID tương thích
│   │       └── Prefabs/Monster (1).prefab
│   ├── Grid/Scripts/
│   │   ├── GridMovement.cs
│   │   └── SnapToGrid.cs
│   ├── Combat/Scripts/
│   │   ├── Health.cs
│   │   └── IDamageable.cs
│   ├── Interaction/Scripts/
│   │   ├── IInteractable.cs
│   │   ├── QuizTrigger.cs
│   │   └── MirrorTreasureItem.cs         # Tên/GUID tương thích
│   └── Items/
│       ├── Scripts/Chest.cs
│       └── Prefabs/                      # Rương đóng/mở hiện có
├── Learning/Quiz/
│   ├── Scripts/
│   │   ├── QuestionData.cs
│   │   ├── QuizController.cs
│   │   ├── QuizEvents.cs
│   │   └── QuestionManager.cs            # Wrapper, sửa lệch tên file/class
│   └── Data/Questions/Test/              # Năm asset câu hỏi đã bảo toàn GUID
├── UI/Quiz/
│   ├── QuizPanel.cs
│   └── PanelController.cs               # Giữ callback Button cũ
├── Editor/
│   ├── QuestionDataSeeder.cs
│   └── GameplayRefactorMigration.cs
├── Tests/EditMode/
│   ├── DoAn.Refactor.EditModeTests.asmdef
│   └── GameplayRefactorTests.cs
├── Scenes/Scene_Test/SampleScene.unity
├── Art/                                 # Sprite, font, material...
├── Audio/
├── Docs/
│   └── REFACTOR_GUIDE.md
└── ...                                  # Scaffold và tài nguyên cũ còn được giữ
```

Các thư mục scaffold cũ trong `Scripts/`, `Data/`, `Prefabs/` có thể còn file giữ chỗ; không còn chứa các script gameplay, câu hỏi và prefab đã chuyển ở trên. Không tạo các hệ thống Save, Audio manager, HUD, Menus hoặc cấu hình nhân vật chưa được sử dụng.

`Assets/Cainos`, `Jungle_UI`, `Fantasy Wooden GUI  Free`, `TextMesh Pro` giữ vị trí cũ. Camera vẫn dùng `Cainos.PixelArtTopDown_Basic.CameraFollow` trong bộ Cainos, giữ cách bám target bằng Lerp và khoảng cách ban đầu.

## Phân chia trách nhiệm

| Thành phần | Trách nhiệm |
|---|---|
| GameplayState | Nguồn trạng thái Playing, Quiz, Paused, GameOver; lưu trạng thái trước quiz |
| Health / IDamageable | Máu, nhận sát thương, hồi máu và sự kiện chết một lần |
| GridMovement | Tilemap, vật cản, bước di chuyển, coroutine và trạng thái đang di chuyển dùng chung |
| CharacterAnimation | FaceX/FaceY/IsMoving trên Visual; giữ IsRunning/flipX cho controller cũ |
| PlayerInputReader | Đọc phím và gửi yêu cầu |
| PlayerInteractor / IInteractable | Tìm item/rương ở ô đích, tương tác sau bước đi hoặc khi gặp rương |
| PlayerInventory | Vật phẩm/vũ khí riêng của từng người chơi |
| PlayerAppearance | Đổi controller theo PlayerInventory, giữ hướng và trạng thái bước |
| PlayerCollision | Một đầu vào trigger duy nhất cho va chạm người chơi với quái |
| EnemyBrain | Tuần tra và truy đuổi theo khoảng cách Manhattan; giữ thuật toán đuổi cũ |
| EnemyAttack | Gây sát thương qua IDamageable; giữ giá trị monsterDamage trên cấu hình người chơi cũ |
| QuizController | Phiên câu hỏi, kiểm tra đáp án, trả kết quả và đóng phiên |
| QuizPanel | Nội dung, các nút lựa chọn, callback UI và phản hồi |
| QuizTrigger | Câu hỏi trên map, phiên tương tác và rương liên kết |
| Chest | Kiểm tra khóa, mở một lần, đổi visual và thông báo Opened để nối phần thưởng |
| QuestionData | Dữ liệu câu hỏi dùng chung; không chứa trạng thái phiên |
| QuestionDataSeeder | Tạo/cập nhật dữ liệu thử nghiệm ở đường dẫn mới |
| GameplayRefactorMigration | Chuyển cấu hình cũ sang component mới trong scene/prefab một cách lặp lại an toàn |

Không có logic nhặt vũ khí, lưu game hoặc phần thưởng inventory mới. Sự kiện `Chest.Opened` là điểm nối phần thưởng; visual mở rương hiện có vẫn giữ nguyên.

## Luồng chính

1. PlayerInputReader nhận phím, PlayerInteractor kiểm tra ô đích.
2. GridMovement thực hiện bước đi; đến item thì QuizTrigger yêu cầu QuizController mở câu hỏi.
3. GameplayState chuyển sang Quiz. Motor không bắt đầu bước mới, bước đang chạy giữ nguyên tiến độ; sát thương/va chạm chiến đấu bị chặn. UI vẫn chạy, không đổi Time.timeScale.
4. QuizPanel gửi chỉ số đáp án. QuizController kiểm tra và trả kết quả riêng cho trigger đã mở phiên.
5. Đúng: trigger hoàn thành, mở khóa rương đúng ID; Chest chỉ mở/trao sự kiện một lần. Sai hoặc hủy: rương vẫn khóa, item có thể thử lại.
6. Đóng panel khôi phục trạng thái trước quiz. Nếu đã GameOver thì không chuyển lại Playing.

`QuestionData.freezeDuration` được giữ để tương thích serialized data, không còn điều khiển đóng băng có thời hạn. `MonsterFreezeController` chỉ là API tương thích, không đăng ký event hoặc chạy hệ thống freeze thứ hai.

## Tương thích dữ liệu và migration

- Asset được chuyển cùng `.meta`, giữ GUID.
- Scene và callback Button chưa được viết lại bằng thao tác sửa YAML.
- Các class PlayerMovement, PlayerCollision, MonsterGridAI, QuestionManager, PanelController, MirrorTreasureItem giữ GUID/tên cần thiết.
- Các field cũ vẫn tồn tại trong adapter hoặc lớp cơ sở serialized để giữ override và reference.
- Khi chưa migration, adapter bổ sung component thiếu lúc Awake và sao chép cấu hình. Không tồn tại hai coroutine di chuyển song song.
- Menu migration lưu component mới vào asset, giữ adapter cũ; chỉ sao chép dữ liệu nhân vật một lần theo cấp prefab/scene.
- Sau migration, chỉnh tốc độ người chơi ở PlayerInputReader, máu ở Health, inventory ở PlayerInventory, quy tắc bước đi ở GridMovement. Các field legacy trên PlayerMovement là dữ liệu tương thích; không dùng làm cấu hình mới.

Xem [hướng dẫn refactor và cấu hình](Assets/_Project/Docs/REFACTOR_GUIDE.md) trước khi migration.

## Kiểm tra và giới hạn

Đã compile mã runtime, Editor và test bằng Roslyn của Unity 6000.5.5f1 với references/defines từ response file của project; kết quả không có lỗi C#. Đây chưa phải xác nhận import, serialization hoặc Play Mode trong Unity.

Đã kiểm tra GUID asset di chuyển không bị mất, không có GUID trùng trong Assets. Scene/map, prefab và animation được di chuyển nhưng giữ nội dung, GUID và thông số cũ. Các thay đổi có sẵn ở font TMP và EditorBuildSettings được giữ nguyên.

Các test EditMode bao phủ sát thương/chết, khóa rương khi sai/hủy, mở một lần và khôi phục trạng thái quiz; chưa chạy bằng Unity Test Runner. Migration chưa được thực thi trong Editor.

Scene hiện chỉ có **hai nút đáp án**, trong khi các asset có **bốn lựa chọn**. Giữ nguyên giao diện theo phạm vi refactor; QuizPanel cảnh báo nếu thiếu nút. Cần cấu hình đủ nút để chọn được mọi đáp án. Nếu không gán feedbackText, giải thích vẫn ghi Console như trước.
