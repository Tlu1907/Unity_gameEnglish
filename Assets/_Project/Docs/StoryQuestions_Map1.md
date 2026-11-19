# Luồng câu hỏi cốt truyện Màn 1

Luồng hiện tại chạy hoàn toàn bằng dữ liệu có sẵn, không cần Flask.

1. Chạm vùng trigger của `Tower` trong Scene1 để tự mở hội thoại linh hồn → nhấn **C** → chọn đồng ý → hoàn thành sáu câu từ `Data/Json/map1_spirit_questions_en.json`: sword, shield, am, is, are, The gate is closed. Chọn sai được thử lại. Đọc giải thích bằng C giữa các câu; sau câu cuối đóng phần kết để nhận trang bị và nhiệm vụ cứu nhà khảo cổ. Tháp giữ nguyên trên map, không có cơ chế mở gương/rương. Xem `JsonNpcConversations.md` để chỉnh JSON và `TowerInteraction.md` để thêm hình tháp.
2. Sau khi nhận trang bị, hạ Monster (4) và Monster (5), rồi quay gần tháp để nghe Linh nhận tín hiệu cầu cứu. Đọc hết thông báo, đến hạ quái canh giữ để xuất hiện `ScientistNPC`; gặp nhà khảo cổ → nhấn **C** → trắc nghiệm `bridge` (nhiễu: `school`, `bank`, `hospital`). Đúng thì biết đường đến phòng nghiên cứu. Xem `LinhRescueSignal.md` để chỉnh nhóm quái và thông báo.

Chọn bằng chuột hoặc phím **1–4**, **C** tiếp tục, **Esc** đóng. Nếu chưa trả lời đúng, đóng hội thoại không hoàn thành nhiệm vụ; rời điểm tương tác rồi quay lại để thử lại. Sau hai lần chọn “Tôi chưa sẵn sàng”, nút đồng ý đổi màu nổi bật.

Nhấn **J** mở sổ manh mối sau lần gặp linh hồn đầu tiên. Tiến độ và manh mối chỉ giữ trong phiên Play hiện tại.

Khung truyện là prefab tĩnh `Assets/_Project/Resources/StoryDialogue.prefab`, đã đặt trong Scene1 với tên `TreasureMirrorDialogueCanvas`. Nền và font lấy từ giao diện NPC lúc tạo prefab; lúc chơi không tạo lại bố cục. Chân dung nhà khảo cổ lấy từ `ScientistDialogue`. Có thể gán ảnh linh hồn trực tiếp vào Image `avatar` của khung tĩnh.

## Chỉnh khung và thêm nút

Trong Hierarchy, mở `TreasureMirrorDialogueCanvas → DialoguePanel`. Bật tạm `DialoguePanel` để chỉnh ở Edit Mode; khi Play, script sẽ ẩn khung cho tới lúc tương tác với gương.

- Chỉnh kích thước, vị trí, font, ảnh nền ngay trên RectTransform/Image/TMP trong Hierarchy hoặc mở prefab `StoryDialogue` để chỉnh mẫu dùng chung.
- `ChoiceButton1`–`ChoiceButton4`: bốn vị trí lựa chọn. Script cập nhật chữ và chỉ hiện hai nút ở bước đồng ý.
- Không có nút tiếp tục/đóng. Ghi chú nhỏ hiện “C to continue · Esc to close”; khi chọn đáp án hiện “Click or press 1–4 to choose · Esc to close”.
- `CustomActions`: vùng trống dành cho các nút mới. Tạo `UI → Button - TextMeshPro` bên trong và nối OnClick theo ý muốn. Script không tự ẩn hoặc bố trí lại các nút trong vùng này.
- Component `StoryQuizPanel` trên DialoguePanel cho phép gán các text, ảnh, nút và thêm tương tác qua `On Opened`, `On Correct`, `On Wrong`, `On Closed`. Các nút có sẵn vẫn giữ những OnClick bổ sung do người thiết kế gán.

Không sửa `Configure()` để đổi bố cục khung đã tạo: đây chỉ là hàm khởi tạo mẫu cho công cụ Editor và test. Menu `Tools → Story → Add Static Treasure Dialogue` thêm prefab vào scene nếu còn thiếu, không xây lại mẫu đã chỉnh. Scene khác thiếu khung sẽ dùng prefab đã lưu làm dự phòng.

Dữ liệu Tòa tháp chỉnh trực tiếp trong file JSON được tham chiếu qua Conversation Json của `q1_spirit.asset`; `q2_archaeologist.asset` vẫn chứa câu bridge của nhà khảo cổ. JSON kiểm tra đủ trường, câu multiple_choice và 1–3 lựa chọn nhiễu không trùng. Nội dung fallback cũ trong StoryBeatData vẫn có kiểm tra đáp án cố định. Vị trí lựa chọn được xáo trộn Fisher–Yates cho từng câu.

`ChestQuizInteraction.beatId` trên prefab Key là `q1_spirit`; `NPCDialogueTrigger.beatId` trên TalkRange của ScientistNPC là `q2_archaeologist`. MirrorTreasureItem cũng dùng `q1_spirit` mặc định. Các sự kiện QuizEvents vẫn phát khi bắt đầu câu hỏi, chọn đúng/sai và đóng câu hỏi; StoryProgress.BeatCompleted báo hoàn thành chặng sau khi đóng phần kết.

Phòng nghiên cứu, câu `key` và cổng cuối chưa triển khai theo phạm vi hiện tại. Các script API vẫn giữ để dùng lại về sau, nhưng hai câu truyện này không gọi chúng.
