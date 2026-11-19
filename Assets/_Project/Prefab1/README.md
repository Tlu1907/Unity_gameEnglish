# Prefab1 — xuất object và ghép scene

Menu Unity: **Tools > Prefab1 > Export Both Scenes and Build Merged Scene**.

Mỗi lần chạy tạo thư mục `Export_<thời gian>` mới, không ghi đè prefab hay scene nguồn.

- `Scene1/`: prefab riêng cho object cấp gốc và các prefab instance được đặt trong bản đồ Scene1.
- `Sence1_final/`: prefab riêng của scene nhiệm vụ.
- `Merged/`: prefab của bản ghép.
- `Scene1_Layout.unity`, `Sence1_final_Layout.unity`: bản layout giữ vị trí và tham chiếu scene, dùng prefab vừa xuất.
- `Scene1_Merged.unity`: bản đồ Grid, CityExpansion, LayeredCityTerrain, HidingAndTraps từ Scene1; các object nhiệm vụ, Player, Camera, UI và quái từ Sence1_final. Camera lấy cấu hình bản đồ mở rộng và gán lại tham chiếu.
- `Scene1_MapInput.unity`: bản sao bản đồ dùng làm đầu vào ghép, không phải scene gameplay cuối.
- `EXPORT_REPORT.txt`: danh sách object/prefab, kiểm tra tham chiếu hoặc nguyên nhân thất bại.

Các cây Visual/UI/Tilemap được giữ nguyên nhóm để không phá animation, bố cục UI và hệ tọa độ Grid. Tên prefab có số thứ tự và chia thư mục theo scene để tránh trùng.

Prefab riêng không thể lưu tham chiếu tới object nằm ngoài nó. Layout đã xuất lưu lại các liên kết đó dưới dạng override trên instance. Khi dựng lại, mở layout hoặc scene ghép; kéo riêng một prefab sang scene khác vẫn cần gán các tham chiếu scene.

Công cụ đọc bản scene đã lưu trên ổ đĩa. Không tự lưu các chỉnh sửa chưa Save trong Editor, không đổi Build Settings và không sửa logic gameplay. Kiểm thử bản ghép trước khi thêm vào build; đặc biệt logic encounter của EnemyBrain đang thiếu so với các test nhiệm vụ.
