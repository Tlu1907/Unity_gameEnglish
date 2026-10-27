# Hướng dẫn sử dụng sau refactor

## 1. Thiết kế component

- **GameplayState:** dùng chung cho trạng thái Playing, Quiz, Paused, GameOver. BeginQuiz có chủ sở hữu; EndQuiz chỉ chủ phiên được kết thúc. Quiz khôi phục trạng thái trước đó, không hồi sinh GameOver.
- **GridMovement:** duy nhất một motor trên mỗi nhân vật. Cấu hình Tilemap nền, mask vật cản, gridSize; nhận yêu cầu bước đi và phát StepCompleted. Coroutine giữ vị trí/đích khi gameplay dừng, tiếp tục khi Playing. Disable nhân vật hủy coroutine, xóa cờ và đưa về tâm ô.
- **CharacterAnimation:** cập nhật IsRunning hoặc tên tham số tùy cấu hình và flipX. Không điều khiển quyết định AI.
- **Health:** giữ máu riêng trên GameObject, nhận sát thương qua IDamageable, hồi máu và phát Changed/Died. Không lưu trạng thái nhân vật trong ScriptableObject.
- **PlayerInputReader:** dùng input cũ, gửi hướng/tốc độ cho PlayerInteractor.
- **PlayerInteractor:** tìm QuizTrigger ở ô tới, mở quiz khi hoàn tất bước; gặp rương thì gọi IInteractable, quyền mở do Chest quyết định.
- **PlayerInventory:** trạng thái None/Weapon; dùng SetItem để thay đổi runtime.
- **PlayerCollision:** đầu vào va chạm duy nhất, chặn trong quiz/paused/gameover, dùng inventory để quyết định giết quái hoặc nhận sát thương.
- **EnemyBrain:** tuần tra/truy đuổi theo quy tắc cũ; yêu cầu motor di chuyển, không tự chạy coroutine.
- **EnemyAttack:** gọi IDamageable khi gameplay đang Playing. Sát thương vẫn lấy từ monsterDamage trên PlayerMovement để giữ cấu hình hiện có.
- **QuizController:** giữ câu hỏi/phiên, kiểm tra chỉ số đáp án, trả kết quả một lần; xử lý hủy và disable.
- **QuizPanel:** hiển thị, quản lý callback của chính nó, nhận lựa chọn và gửi về controller. Không điều khiển quái hoặc mở rương.
- **QuizTrigger:** giữ QuestionData, rương và controller, nhận kết quả riêng của phiên để hoàn thành item.
- **Chest:** tự giữ khóa/mở, yêu cầu đúng questionId để mở khóa; Opened phát một lần, kể cả Open gọi lặp. Không thêm vật phẩm thưởng mới trong lần refactor này.

Không thêm lớp Character cơ sở vì chưa có hành vi chung cần kế thừa. Những wrapper kế thừa còn lại phục vụ tương thích với asset hiện có.

## 2. Player prefab

Prefab hiện có: `Gameplay/Characters/Player/Prefabs/PLayer.prefab`.

Giữ SpriteRenderer, Animator, collider, Rigidbody2D, tag Player và thiết lập vật lý hiện tại. Các component gameplay:

| Component | Cấu hình |
|---|---|
| PlayerMovement | Adapter bảo toàn dữ liệu cũ, đăng ký sự kiện chết và chuyển GameOver |
| GridMovement | Ground Tilemap, Obstacle Mask, Grid Size |
| PlayerInputReader | Speed |
| PlayerInteractor | Tương tác sau bước đi |
| PlayerInventory | Current Item |
| Health | Maximum, Current |
| CharacterAnimation | Use Animator, Run Parameter Name |
| PlayerCollision | Giữ trên những object đã dùng xử lý va chạm |

Prefab độc lập có thể không tham chiếu được Tilemap của scene. Gán Tilemap trên instance trong scene; adapter có thể chuyển tham chiếu legacy sang motor khi motor còn thiếu. Không thay collider/animation để thực hiện refactor.

Sau migration, cấu hình runtime mới ở các component đích; dữ liệu public cũ trên PlayerMovement được giữ vì serialized override và các callback cũ. Không gắn thêm một script di chuyển khác.

## 3. Enemy prefab và thêm loại quái

Prefab hiện có: `Gameplay/Characters/Enemies/Prefabs/Monster (1).prefab`.

- Giữ tag Monster, collider trigger, renderer, animation hiện tại.
- Giữ MonsterGridAI nếu prefab đã có; đây là wrapper của EnemyBrain. Với prefab mới chỉ gắn EnemyBrain, không gắn cả hai.
- Có GridMovement, CharacterAnimation, EnemyAttack. Adapter/brain bổ sung component thiếu trước khi chạy; nên lưu chúng bằng migration để có thể chỉnh trực tiếp trong Inspector.
- Gán player, groundTilemap, obstacleMask, tốc độ tuần tra/đuổi, bán kính tuần tra và phạm vi đuổi trong EnemyBrain.
- Nếu chỉnh mask/gridSize/Tilemap sau migration, đồng bộ cấu hình GridMovement; motor chịu trách nhiệm di chuyển còn brain dùng cấu hình bản đồ cho quyết định.
- Thêm loại quái bằng prefab variant, đổi visual và tham số tốc độ/phạm vi. Không cần A*, cấu hình asset mới hay kế thừa nhiều tầng.
- EnemyAttack chưa có cooldown hoặc sát thương riêng: giữ quy tắc sát thương một lần mỗi OnTriggerEnter2D của source cũ.

## 4. Câu hỏi, item và rương

### QuestionData

Tạo từ menu Assets/Create/EarthDefender/Question Data, lưu ở `Learning/Quiz/Data/Questions/`.

Điền id ổn định, skillTag, content, answers, correctIndex (bắt đầu từ 0) và explanation. freezeDuration giữ để đọc asset cũ, không ảnh hưởng thời gian dừng gameplay. Công cụ tạo dữ liệu test đã đổi sang đường dẫn mới.

### QuizController và giao diện

Trong scene hiện có vẫn giữ QuestionManager; đó là wrapper của QuizController với GUID cũ. Gán panel và contentText. Panel giữ PanelController, wrapper của QuizPanel; không gắn thêm QuizPanel thứ hai.

QuizPanel có answerButtons, contentText và feedbackText tùy chọn. Gán đủ nút theo số đáp án; thứ tự mảng là thứ tự chỉ số câu trả lời. Với layout cũ chưa gán, panel duyệt các nút con, bỏ qua nút Cancel. Menu migration lưu danh sách này vào Inspector.

Callback Cancel cũ vẫn gọi PanelController.OnCancelClicked. OnSubmitClicked được giữ để UnityEvent không mất method; với nút đáp án, callback runtime có chỉ số riêng chịu trách nhiệm gửi đáp án, tránh callback Submit cũ gửi nhầm đáp án 0 hoặc gửi hai lần. Listener do code thêm được gỡ riêng, không RemoveAllListeners và không xóa UnityEvent đã lưu.

Controller dùng reference hoặc tìm QuizPanel theo kiểu một lần khi khởi tạo/migration; không còn tìm object tên Panel/Content trong luồng mở câu hỏi.

**Giới hạn scene có sẵn:** hiện chỉ có hai nút Submit, còn asset câu hỏi có bốn lựa chọn. Refactor giữ nguyên layout. Muốn chơi đủ mọi lựa chọn cần bổ sung/gán đủ nút trong Unity. Nếu thiếu, Console cảnh báo rõ. feedbackText không gán thì phản hồi được ghi Console như trước; panel vẫn đóng ngay sau khi trả lời.

### QuizTrigger và Chest

Giữ MirrorTreasureItem trên object cũ; prefab mới có thể dùng QuizTrigger. Gán QuestionData, relatedChest và controller. Rương cùng object/con được tự nhận diện khi thiếu tham chiếu.

- Chest có questionId hoặc requiresQuiz thì bị khóa. Trigger có câu hỏi cũng đặt khóa cho rương liên kết, không phụ thuộc thứ tự Awake.
- Rương thường để requiresQuiz=false và questionId rỗng.
- Sai/hủy giữ khóa và không hoàn thành item.
- Đúng chỉ mở rương liên kết với trigger đã tạo phiên; các item khác dùng chung questionId không tự động nhận kết quả toàn cục.
- Open không bỏ qua khóa. Opened là event C# để phần thưởng sau này đăng ký; trạng thái đã mở được đặt trước khi gọi event để chống gọi lặp.
- Giữ openChestPrefab và tham số animation cũ; không thay tài nguyên rương.
- QuizEvents vẫn phát sự kiện để các thành phần ngoài có thể quan sát; không dùng để mở tất cả rương theo ID.

## 5. Migration scene/prefab

### Đã làm trong repository

Di chuyển script, prefab, animation và dữ liệu câu hỏi theo cặp asset + meta, giữ GUID. Sửa tên file QuizManager.cs thành QuestionManager.cs để trùng class. Các wrapper cũ giữ scene, prefab và UnityEvent liên kết.

Scene/prefab chưa được save lại bằng Editor trong phiên thực hiện. Khi chạy trước migration, PlayerMovement/EnemyBrain tạo component thiếu và chuyển cấu hình trong Awake; chỉ hệ thống component mới xử lý gameplay.

### Thao tác trong Unity

1. Mở bằng Unity 6000.5.5f1, chờ import/compile hoàn tất.
2. Lưu scene đang chỉnh, thoát Play Mode.
3. Chọn **EarthDefender → Refactor → Migrate project scenes and prefabs**.
4. Kiểm tra diff scene/prefab và Inspector sau migration.
5. Chạy lại menu: khi không thay đổi cấu hình liên kết, không có asset nào cần chuyển tiếp.
6. Chạy Test Runner và Play Mode theo checklist bên dưới.

Công cụ chỉ quét `Assets/_Project`, xử lý prefab base trước variant rồi scene. Prefab có version migration theo độ sâu, scene dùng version riêng 1000 để ghi đúng override đã cấu hình trên instance. Dữ liệu nhân vật chỉ sao chép lần đầu mỗi cấp; chạy lại không ghi đè Health/Inventory/Speed đã chỉnh trên component mới. Liên kết trigger/controller và mảng nút có thể được bổ sung khi còn thiếu.

Công cụ từ chối chạy nếu scene đang dirty, không tự lưu hoặc bỏ thay đổi chưa lưu. Khôi phục danh sách scene mở sau khi kết thúc; không xóa component cũ, không đổi UnityEvent và không thêm component trùng. Nếu có lỗi giữa chừng, kiểm tra Console; các asset đã chuyển được giữ và có thể chạy lại. Không cần chỉnh YAML tay.

Các adapter đang được giữ để tránh làm mất prefab override, nên **không xóa thủ công** PlayerMovement, MonsterGridAI, QuestionManager, PanelController hoặc MirrorTreasureItem ở các object hiện có.

## 6. Kiểm tra

### Đã thực hiện ngoài Editor

- Compile runtime, Editor migration/seeder và mã test bằng Roslyn đi kèm Unity 6000.5.5f1.
- Dùng references/defines lấy từ response file của project; output compile nằm trong thư mục tạm, không ghi vào Library/Temp/Logs của project.
- Kiểm tra 26 file meta đã chuyển: giữ nguyên nội dung/GUID; không có GUID trùng trong Assets. Đối chiếu hash 12 asset câu hỏi, prefab và animation đã chuyển: không thay đổi nội dung. Không có file dự án cần import bị thiếu meta hoặc tên file MonoBehaviour lệch class.
- Kiểm tra source, scene/prefab, question asset và callback Cancel/Submit.
- Không sửa font TMP hoặc EditorBuildSettings đang có thay đổi của người dùng.

Đây là compile C# với reference Unity, **chưa xác nhận Unity import/serialization/runtime**. Chưa thực thi migration hoặc NUnit Test Runner trong Unity Editor đang mở.

### Test Runner

Mở Window → General → Test Runner → EditMode, chạy `GameplayRefactorTests`:

1. Sát thương bị chặn trong quiz, hồi máu giới hạn, chết chỉ phát một lần.
2. Quiz khôi phục Paused, chủ khác không đóng được phiên và GameOver không trở về Playing.
3. Mở rương trực tiếp bị chặn; trả lời sai/hủy vẫn khóa; đúng chỉ phát Opened một lần dù gửi/mở lại.

4. Migration sao chép thông số cũ một lần, chạy lại giữ cấu hình component mới và không thêm motor trùng.

Tests dùng reflection để truy cập Assembly-CSharp từ test assembly riêng, tránh thêm asmdef runtime hoặc đổi cấu trúc assembly của game. Chưa có test tự động Play Mode cho physics/coroutine.

### Checklist Play Mode

- [ ] Console không có lỗi compile, missing script/reference.
- [ ] Chỉ một motor và một brain/input xử lý trên mỗi nhân vật.
- [ ] Giữ nguyên tốc độ, máu, item, mask và Tilemap đã cấu hình trước migration.
- [ ] WASD/mũi tên di chuyển đúng ô, vật cản vẫn chặn.
- [ ] Quái tuần tra/đuổi như cũ.
- [ ] Mở quiz khi quái đang đi: quái đứng đúng vị trí đó, tiếp tục cùng bước khi đóng.
- [ ] Trong quiz không bắt đầu bước mới, không mất máu và không tiêu diệt quái qua trigger.
- [ ] Cancel/sai không mở rương, có thể tương tác lại.
- [ ] Đúng mở một lần, prefab mở xuất hiện đúng vị trí, item hoàn thành.
- [ ] Rương thường vẫn mở, rương quiz không mở bằng gọi Open trực tiếp.
- [ ] Đóng quiz sau GameOver không đưa gameplay về Playing.
- [ ] Các nút không bị nhân đôi listener qua nhiều lần mở/đóng.
- [ ] Cấu hình đủ bốn nút nếu tiếp tục dùng bốn đáp án; kiểm tra riêng đáp án chỉ số 2/3.
- [ ] Camera, map, animation và giao diện giữ nguyên.
