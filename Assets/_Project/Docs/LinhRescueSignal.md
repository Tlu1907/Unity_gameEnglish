# Tín hiệu cầu cứu sau trận chiến ở tháp

Luồng Màn 1: hoàn thành sáu câu và nhận trang bị → hạ nhóm quái quanh tháp → tự động nghe Linh qua bộ đàm ngay tại vị trí hiện tại → hạ quái canh giữ nhà khảo cổ → gặp ông ấy để nhận nhiệm vụ và thông tin tiếp theo.

Trên **Tower → TowerRescueSignal**, trường **Enemies To Defeat** đang gán **Monster (4)** và **Monster (5)**. Phải hạ hết danh sách này. Đối tượng chỉ bị tắt/ẩn không được tính là đã hạ. Có thể thêm/bớt quái bằng Inspector; nếu danh sách để trống, component lấy các quái có Encounter Mode 1 (Tòa tháp) trong cùng scene lúc bắt đầu Play.

Thông báo tự mở lần đầu khi hoàn thành q1_spirit và nhóm quái đã bị hạ, dù player đang ở đâu. Không cần quay lại tháp hoặc nhấn phím tương tác. Nếu đang có quiz/hội thoại khác hoặc tạm dừng, thông báo chờ đến khi gameplay tiếp tục.

Lời thoại và avatar chỉnh trong `Data/Dialogues/Linh_ArchaeologistSOS.asset`. Thông báo dùng khung NPC Linh hiện có, có tiếng Anh và bản dịch Việt. C hiện hết chữ/tiếp tục; Esc đóng. Chỉ sau khi đọc hết mới ghi nhận tín hiệu, cập nhật nhiệm vụ/sổ manh mối và mở bước q2_archaeologist. Đóng giữa chừng thì rời vùng tháp rồi quay lại để nghe lại; đọc hết rồi không phát lại trong phiên Play.

Quái canh giữ vẫn theo cơ chế cũ: hạ Monster (3) sẽ xuất hiện ScientistNPC (nhà khảo cổ) gần đó. Câu bridge chờ tín hiệu Linh được ghi nhận; việc mở tháp hoặc chỉ đến gần nhà khảo cổ không bỏ qua mốc cứu viện.

Tiến độ đặt lại khi bắt đầu phiên Play mới. TowerRescueSignal không đổi vị trí, hình ảnh hoặc kích thước collider do người thiết kế chỉnh.
