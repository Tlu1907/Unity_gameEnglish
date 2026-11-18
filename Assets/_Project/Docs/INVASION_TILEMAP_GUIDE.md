# Màn 1: thành phố bị xâm lược và Tilemap nhiều lớp

Thiết kế ngày 19/11/2026 thay cho cách rải 6 NPC dân thường của bản Lexi trước đó. Scene làm việc vẫn là Assets/_Project/Scenes/Sence_1/Scene1.unity. Bản dự phòng trước đổi bố cục: Assets/_Project/Scenes/Backups/Scene1_BeforeInvasionLayout.unity (không thêm vào Build Settings).

## Bố cục
- 7 nhà được gom vào các lô dân cư phía tây, chuẩn hóa chiều cao và collider chân. Nhà thừa tắt, không xóa asset.
- Hải điều phối sơ tán, Ba phụ trách kỹ thuật và Scientist gốc tập trung ở điểm hỗ trợ cạnh ga. Tí, Mận, Lan và người áo xám inactive; dữ liệu cũ vẫn giữ để dùng lại. Không còn tuyến thoại yêu cầu tìm người áo xám sau khi hoàn thành.
- 12 rương là các kho tiếp tế: điểm hỗ trợ/ga, lô dân cư, công viên, bờ đông và quảng trường. HUD ghi Tiếp tế x/12. Đúng câu hỏi rơi HP/kiếm, sai kích động quái như trước.
- 18 quái chia vào khu bị chiếm và tuyến tiếp cận, không đặt trong điểm hỗ trợ. 10 bụi ẩn/6 bẫy giữ lại.
- Sáu DialogueData mới trong Data/Dialogues/Invasion cho Hải/Ba, mốc 0/2/12. Giữ nguyên nội dung Linh/Scientist gốc và UI hội thoại.

## Cấu trúc map trong Hierarchy
LayeredCityTerrain chứa Grid có 6 Tilemap hiển thị:
- Terrain_Ground: cỏ, nền và chi tiết nền.
- Terrain_Roads: đường phố/vỉa hè.
- Terrain_Water: hồ/kênh và các tile bờ.
- Terrain_Bridges: cầu qua kênh và lối nối bờ hồ.
- Terrain_Stairs: bậc thang/lối lên xuống quảng trường, công viên.
- Terrain_Station: nền sân ga/đường ray.

Grid/Ground cũ là bản đồ điều hướng cho quái, renderer tắt. Player kiểm tra biên bằng các lớp Terrain_* và va chạm bằng collider nước/vật thể, nên ô navigation thô không chặn mép vỉa hè nữa. MapBackground renderer cũng tắt: hình ảnh đang được dựng bằng tile, không chồng ảnh nền.

LayeredCityTerrain/CollisionGrid_11px/Collision_Water_Walls là Tilemap va chạm nước, renderer tắt, TilemapCollider2D solid ở layer Water. Tên Water_Walls được giữ cho tương thích nhưng các dải chặn nền/vỉa hè đã bỏ. Ô va chạm 11px (1/3 đơn vị) giúp bám bờ sông/tường sát hơn ô art 33px. Lối cầu và bậc thang chừa mở. Polygon nước cũ được tắt sau khi chuyển.

CityTerrain đọc các Tilemap cầu/bậc thang; GridMovement dùng chân collider để xét đất, nhân tốc độ .72 trên bậc thang. Đây vẫn là Rigidbody2D Dynamic/FixedUpdate gốc, không có controller di chuyển thứ hai hoặc hệ nhảy/độ cao 3D.

## Chỉnh trong Tile Palette
1. Window > 2D > Tile Palette, chọn **City Map 33px**.
2. Palette đã có sẵn 1.444 tile; nguồn Assets/_Project/Art/Tilemaps/CityMap/CityMapAtlas.png. Tái sử dụng sprite/Tile và GUID cũ, không cắt lại hoặc thay ảnh nguồn.
3. Trong Active Tilemap, chọn đúng Terrain_Ground/Roads/Water/Bridges/Stairs/Station rồi vẽ/xóa tile. Các tile mép chứa cả nền và bờ được giữ nguyên pixel, không phải bộ RuleTile tự nối mép.
4. Khi mở lối mới, cập nhật cả Grid/Ground (ô đi được) và xóa ô cản tương ứng ở Collision_Water_Walls. Khi đóng lối, làm ngược lại. Không chỉ vẽ cầu lên lớp hình ảnh rồi giữ collider nước bên dưới.
5. Để sửa va chạm bằng cọ, chọn palette **City Collision**, Active Tilemap **Collision_Water_Walls**, dùng Paint/Erase của Tile Palette. Asset tô ô cản: Art/Tilemaps/InvasionCity/BlockedWaterWall.asset. Chọn Collision_Water_Walls để sửa collider; bật renderer tạm nếu cần nhìn vị trí các ô cản, tắt lại trước chơi.
6. Nhà/cây/ghế vẫn là object riêng phía trên Tilemap, có collider chân và SortingGroup theo Y, để chỉnh vị trí và che khuất độc lập.

## Kiểm tra
- Tools > Level 1 > Validate Layered Invasion Terrain: số layer, ảnh nền tắt, 3 cầu, nước solid, số NPC và đường tới 12 rương.
- Play mới > Tools > Level 1 > Test Invasion Movement and Story: thử đi vào sông, qua cầu bằng Rigidbody, tốc độ bậc thang, intro, thoại hỗ trợ và quiz theo mốc 0/2/12.
- Hai bài trên đã đạt. CityRouteValidation xác nhận 12 rương tiếp cận được và 18 quái không nằm trong vật cản (845 ô tới được ở lần kiểm tra cuối).
- Các bài kiểm tra Lexi cũ đòi 6 NPC/người áo xám chỉ dành cho scene backup; không dùng để đánh giá bố cục xâm lược mới.

Không chạy lại menu Convert To Layered Invasion Terrain trên scene đã chuyển. Chỉnh Tilemap trực tiếp; các script setup là công cụ dựng ban đầu, không phải logic runtime.

Kiểm tra mới nhất: CITY_WALKWAY_PLAY_PASS (26 lượt qua 13 tuyến cả hai chiều) và INVASION_PLAY_PASS đã đạt sau khi xóa 870 ô chặn đất sai và dời đồ trang trí khỏi lối thang. Các collider nước vẫn giữ. Menu chạy lại bài kiểm tra: Tools > Level 1 > Test Every City Walkway (Play Mode mới).


## Luật rương cập nhật
Mỗi rương chỉ nhận một câu trả lời trong một lượt chơi. Đúng: mở và rơi item. Sai: cũng mở và tính tiến độ, không rơi item, vẫn kích động quái. Không được trả lời lại rương đã sai. Đóng câu hỏi trước khi gửi đáp án không tiêu hao rương; Chơi lại màn reset trạng thái. Đã kiểm tra SINGLE_ATTEMPT_CHEST_PASS.
