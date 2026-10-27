# Điều khiển Player và ngoại hình bốn hướng

> Cập nhật: Player hiện dùng di chuyển vật lý liên tục, Rigidbody2D Dynamic; không còn dừng ở tâm ô hay kiểm tra ô đích để chặn tường. Các hướng dẫn theo ô/Kinematic bên dưới thuộc thiết lập cũ. Tham khảo [PLAYER_PHYSICS_MOVEMENT.md](PLAYER_PHYSICS_MOVEMENT.md) cho luồng và cấu hình mới; các bước gán animation/inventory vẫn áp dụng.

## Kiến trúc và thay đổi

Giữ namespace toàn cục và Assembly-CSharp, không thêm package hoặc đổi input handling (hiện là Both). Giữ luồng PlayerInputReader → PlayerInteractor → GridMovement. GridMovement là nơi duy nhất thay đổi vị trí; tiếp tục dùng bước ô và quy tắc ground/obstacle/GameplayState cũ. Thả phím giữa bước vẫn hoàn thành bước; Quiz/Pause giữ tiến độ rồi tiếp tục như trước.

- PlayerInputReader: đọc phím giữ, ưu tiên cố định W/Up > S/Down > A/Left > D/Right. Hai phím vuông góc không đổi hướng qua lại. Không đổi hướng giữa bước.
- PlayerInteractor: quay mặt theo yêu cầu trước kiểm tra vật cản, giữ tương tác rương/câu hỏi.
- GridMovement: chặn hướng chéo ở đầu vào, cập nhật Walk từ thay đổi vị trí trong bước, giữ tâm ô và sự kiện StepCompleted.
- CharacterAnimation (Common): tham chiếu Animator trên Visual, hỗ trợ FaceX/FaceY/IsMoving khi đủ parameter; giữ IsRunning/flipX cho controller cũ của enemy. Ban đầu hướng xuống, dừng giữ hướng cuối. Không Animator.Play, không root motion.
- PlayerInventory: nguồn trạng thái trang bị duy nhất, phát Changed, API SetEquipped(bool), hai ContextMenu Play Mode. Weapon tương ứng ngoại hình đủ bộ trong yêu cầu này; không thêm hệ thống trang bị từng món.
- PlayerAppearance (cạnh các script Player): nghe inventory, đổi controller khi cần, áp lại hướng/trạng thái di chuyển. Override dùng cùng controller gốc để giữ state machine hiện tại. Thiếu override/clip thì giữ bộ gốc và cảnh báo một lần; inventory vẫn phản ánh trang bị đang có.
- PlayerMovement: adapter bổ sung PlayerAppearance khi chưa gắn; giữ logic máu/chết và dữ liệu cũ.

## Phát hiện trên asset hiện tại

- Visual.controller: cả hai trục Y đã là FaceY sau khi bạn sửa; giữ nguyên.
- Idle mặc định, điều kiện IsMoving, transition duration 0 và Has Exit Time tắt đã đúng.
- Player_Equipped.overrideController hiện chưa gán Controller và danh sách Clips rỗng. Cần cấu hình bên dưới.
- Đủ 16 clip. Clip trái Idle chưa trang bị tên thực tế là U_Idle_left (chữ l thường); code chấp nhận tên này, không cần đổi tên.
- Các clip chỉ animate m_Sprite, classID 212, path rỗng: đúng khi Animator và SpriteRenderer cùng trên Visual; không phát hiện binding Transform di chuyển Player.
- Scene1: Player có Rigidbody2D/BoxCollider2D và con Visual, chưa có script gameplay, tag đang Untagged. Gravity Scale 0 và khóa Z đã đúng.
- Prefab PLayer cũ khác Scene1: renderer/Animator trên root và controller cũ; không ghi đè prefab này.
- Không tìm thấy AGENTS.md trong project. Có nhiều thay đổi sẵn của người dùng; không hoàn tác.

## Gắn component và Inspector trong Scene1

Có thể tự gắn bằng menu **Tools → Player → Setup Player in Current Scene** (script Editor/PlayerSceneSetup.cs). Thoát Play Mode, mở scene cần dùng, chọn Player rồi chạy menu. Công cụ thêm component còn thiếu, gán Animator/Controller/Ground nếu nhận diện được, giữ tham chiếu và cấu hình đã có; không lưu scene hoặc sửa prefab gốc/animation asset. Ctrl+Z hoàn tác cả lần setup. Kiểm tra Console, đặt Obstacle Mask thủ công nếu còn trống và lưu scene sau khi xem lại. Nếu có nhiều Player, phải chọn đúng Player trong scene đang active. Không chạy trong Prefab Mode.

Thoát Play Mode trước khi cấu hình. Không cần chạy menu migration toàn project cho yêu cầu này.

1. Chọn Player, đặt Tag = Player. Giữ Rigidbody2D, Collider2D. Gravity Scale = 0, Freeze Rotation Z bật. Vì motor hiện điều khiển Transform theo ô, nên dùng Body Type = Kinematic để physics không đẩy lệch tâm ô; vật cản đi lại do mask/Tilemap của motor kiểm tra.
2. Gắn PlayerMovement nếu chưa có để giữ tích hợp Health/chết và tương thích hệ thống cũ. Gắn các component sau ngay trong Edit Mode để cấu hình được trước khi Play: CharacterAnimation, GridMovement, Health, PlayerInventory, PlayerAppearance, PlayerInteractor, PlayerInputReader; PlayerCollision nếu scene dùng va chạm quái hiện có. Adapter chỉ bổ sung component còn thiếu, không cần gắn trùng.
3. Player/CharacterAnimation: Animator kéo component Animator từ Player/Visual vào; Use Animator bật. Run Parameter Name giữ IsRunning (chỉ dành cho controller cũ; controller bốn hướng tự dùng IsMoving).
4. Player/GridMovement: Ground Tilemap kéo Tilemap Ground thực sự có tile đi được (không kéo Grid hoặc MapBackground); Obstacle Mask chọn layer các vật cản; Grid Size = 1 nếu mỗi ô vuông là 1 world unit. Với map khác, đặt đúng khoảng cách tâm ô; motor hiện dành cho lưới vuông trục XY. Gán collider vật cản vào layer đã chọn; tránh đưa collider Player vào mask.
5. Player/PlayerInputReader: Speed = 5 (world units/giây), hoặc tốc độ mong muốn. Đây là nơi chỉnh tốc độ sau khi đã gắn component tách riêng.
6. Player/PlayerInventory: Current Item = None để khởi đầu chưa trang bị; Weapon để khởi đầu có đủ bộ ngoại hình.
7. Player/PlayerAppearance: Base Controller kéo Assets/_Project/Art/Sprites/Player/Player_NoItem/Visual.controller; Equipped Controller kéo Assets/_Project/Art/Sprites/Player/Player_Item/Animation/Player_Equipped.overrideController.
8. Visual giữ SpriteRenderer + Animator, gán Animator.Controller = Visual.controller, Apply Root Motion tắt, SpriteRenderer Flip X tắt. Không gắn thêm CharacterAnimation trên Visual: component đó nằm trên Player để motor tìm thấy.
9. Health: Maximum/Current theo máu mong muốn (ví dụ 200/200). Các field speed/gridSize/health/currentItem cũ trên PlayerMovement chỉ phục vụ tương thích; component tách riêng đã gắn là nguồn cấu hình hiện hành. Ground legacy có thể để trống khi GridMovement đã được gán.

Giữ PlayerMovement vì nó là adapter và xử lý chết, không phải motor thứ hai. Nếu Player đang có Cainos TopDownCharacterController hoặc script tự viết khác trực tiếp đọc input/thay Transform/vận tốc, tắt/gỡ component đó trên Player vì sẽ tranh quyền với PlayerInputReader/GridMovement. Script NewMonoBehaviourScript hiện trống, không điều khiển chuyển động. Không cần gỡ các script tương tác/máu/quiz.

## Hoàn thiện Override Controller trong Editor

Chọn Player_Equipped, gán Controller = Visual.controller. Trong bảng override, gán:

| Original | Override |
|---|---|
| U_Idle_Down | E_Idle_Down |
| U_Idle_left | E_Idle_Left |
| U_Idle_Right | E_Idle_Right |
| U_Idle_Up | E_Idle_Up |
| U_Walk_Down | E_Walk_Down |
| U_Walk_Left | E_Walk_Left |
| U_Walk_Right | E_Walk_Right |
| U_Walk_Up | E_Walk_Up |

Các clip E_ đã xuất hiện trong state riêng của controller gốc có thể cũng hiện trong bảng; chỉ cần giữ chúng, không dùng chúng thay tám ô U_ phía trên. Các clip E_ nằm trong Player_Item/Animation, clip U_ trong Player_NoItem/Animation. Controller gốc vẫn dùng U_ trong hai Blend Tree.

## Kiểm tra trong Unity

### Map dùng ảnh nền và camera khám phá

Scene1 hiện dùng SpriteRenderer MapBackground và Ground rỗng. Chạy lại **Tools → Player → Setup Player in Current Scene** ngoài Play Mode: công cụ tự gán GridMovement.Walkable Background = MapBackground, đặt Rigidbody2D Kinematic để không bị physics đẩy lệch ô, thêm PlayerCameraFollow2D trên Main Camera và gán Target = Player. Giữ nguyên độ zoom và Z của camera; Smooth Time mặc định 0.1 giây, đặt 0 nếu muốn bám tức thời. Camera focus lúc bắt đầu và theo trong LateUpdate. Nếu có Cainos CameraFollow trên cùng camera, setup tắt component đó để tránh hai script điều khiển camera.

Khi Ground rỗng, các tâm ô trong giới hạn sprite MapBackground là vùng đi được. Khi Ground có tile, chỉ ô có tile mới được đi, không dùng ảnh nền để vượt qua lỗ/biên Tilemap. Obstacle Mask vẫn kiểm tra collider như cũ; ảnh nền tự nó không cung cấp vật cản cho nhà/cây/đường trong hình. Cần collider/layer vật cản để chặn các khu vực đó. Công cụ không đoán các vật cản từ ảnh.

Sau setup, lưu scene rồi Play. Camera bám Player để đi khám phá thay vì cố định tại tâm map. Nếu không có camera mang tag MainCamera hoặc không nhận diện được MapBackground, Console sẽ hướng dẫn gán thủ công.

1. Play: đứng yên nhìn xuống; nhấn/giữ WASD và mũi tên, kiểm tra Walk từng hướng. Giữ W+D: đi lên; thả W còn D: đi phải sau bước hiện tại.
2. Thả phím giữa bước: tới tâm ô rồi Idle theo hướng cuối. Đi vào ô thiếu ground hoặc vật cản: không đổi vị trí, Idle nhưng quay mặt theo hướng yêu cầu.
3. Trên menu component PlayerInventory chọn Equipment/Equip (Play Mode), rồi Unequip; thử khi đứng và giữa bước. Hướng, tiến độ bước và IsMoving phải giữ đúng. SetEquipped(bool) cũng dùng được từ gameplay.
4. Thử Quiz/Pause giữa bước và tiếp tục; kiểm tra tương tác rương, Health/chết vẫn hoạt động như cũ.
5. Thử bỏ override hoặc một clip E_: cảnh báo một lần, visual vẫn dùng bộ gốc, không NullReferenceException.

Không tự sửa scene, prefab, controller, clip hoặc sprite sheet trong lần triển khai này. Biên dịch C# riêng không thay thế kiểm tra import, Inspector, animation và Play Mode trong Unity.

Đã biên dịch thành công runtime Assembly-CSharp, Assembly-CSharp-Editor và DoAn.Refactor.EditModeTests bằng Roslyn đi kèm Unity 6000.5.5f1, sử dụng references/defines trong response file hiện có, output riêng trong Temp/PlayerControlsValidation. Không ghi đè DLL Unity đang dùng. Đã thêm hai test kiểm tra sự kiện inventory và hướng khi bước bị chặn; chưa chạy Unity Test Runner hoặc Play Mode. git diff --check không báo lỗi whitespace (chỉ có cảnh báo line ending ở các file có thay đổi sẵn).
