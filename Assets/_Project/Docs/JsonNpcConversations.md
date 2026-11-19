# Câu hỏi JSON và khung NPC dùng chung

## Sổ tay học tập có phân trang

Nhấn **J** hoặc nút **Clue Journal** để mở/đóng sổ. Dùng **Previous Page / Next Page** hoặc **Page Up / Page Down** để lật trang. Trang đầu ghi nhiệm vụ và manh mối; mỗi map có phần **Ngữ pháp** và **Từ vựng** riêng. TextMeshPro tự chia thêm trang khi nội dung vượt khung chữ, thay vì thu nhỏ chữ để nhét toàn bộ nội dung vào một trang.

Ghi chú được thu thập từ các trường `vocabulary` / `grammar` của trang hội thoại đang xem, bao gồm giới thiệu, đồng ý/từ chối, câu hỏi và phần kết. Tài liệu nghiên cứu dùng thêm `introductionVocabulary`, `introductionGrammar`, `destinationVocabulary`, `destinationGrammar`. Chưa đọc thì chưa ghi; xem lại cùng nội dung không tạo bản sao. Ghi chú giữ qua các scene trong cùng phiên chơi, đặt lại khi bắt đầu phiên Play mới; chưa lưu xuống đĩa.

Chỉnh giao diện tại **TreasureMirrorDialogueCanvas → Clue Entries** trong Hierarchy (bật tạm object khi chỉnh ngoài Play):

- **Vocabulary Text**: cột từ vựng bên trái; mỗi từ một dòng, dạng `word: nghĩa`.
- **Grammar Text**: cột ngữ pháp bên phải. Hai cột độc lập về font, cỡ chữ, khoảng cách dòng và lề; khi một cột dài hơn, sổ tự thêm trang cho map đó và để trống cột đã hết nội dung.
- **Previous Page / Next Page**: nút lật trang.
- **Page Number**: số trang hiện tại / tổng số trang.
- **Close Book**: nút đóng.
- **Clue Entries → Image / RectTransform**: màu giấy, vị trí và kích thước sổ.
- **TreasureMirrorDialogueCanvas → StoryClueJournal**: tham chiếu tới các thành phần trên.

Prefab gốc: `Assets/_Project/Resources/StoryDialogue.prefab`. Chỉnh prefab để dùng chung cho nhiều scene. Trên **DialoguePanel → StoryQuizPanel → Learning journal map**, đặt **Journal Map Id** và **Journal Map Title** theo từng màn; các hội thoại của cùng màn dùng chung ID. Mặc định là `map1`, `Màn 1 · Thành phố`. Dữ liệu nằm trong `LearningJournal`, phần hiển thị trong `StoryClueJournal.cs`.

Tòa tháp dùng trực tiếp `Assets/_Project/Data/Json/map1_spirit_questions_en.json`. Asset `Resources/Story/q1_spirit.asset` tham chiếu file qua **Conversation Json** nên file được đóng gói cùng bản build; không cần API hoặc sao chép sang Resources.

Luồng: chạm trigger Tower → giới thiệu → C → đồng ý/từ chối → C để xem câu hỏi → trả lời → C sau phần giải thích để sang câu tiếp. Sáu câu theo thứ tự JSON: sword, shield, am, is, are, The gate is closed. Mỗi câu xáo trộn lựa chọn bằng code; những câu có hai đáp án nhiễu chỉ hiện ba nút. Trả lời sai được chọn lại. Chỉ hoàn thành cả sáu câu và đóng phần kết mới nhận trang bị kiếm/khiên và kích hoạt quái gần tháp. Tháp không biến mất hoặc đổi sprite sau khi hoàn thành; phần hình ảnh do người thiết kế thêm vào Tower/Visual.

Nội dung phản hồi, gợi ý, nhãn đồng ý/từ chối dùng nguyên bản JSON. Tiếng Việt hiển thị ở các trường có dữ liệu; các câu chỉ có tiếng Anh không tự sinh bản dịch. Có thể tắt **Show Vietnamese** trên StoryQuizPanel để luyện hoàn toàn bằng tiếng Anh.

JSON được kiểm tra id, các trường bắt buộc, câu hỏi multiple_choice, 2–4 lựa chọn không trùng và hai lựa chọn consent yes/no. Nếu file lỗi, Tòa tháp dùng nội dung dự phòng trong StoryBeatData. Không thay đổi hoặc ghi đè file JSON nguồn.

## Chỉnh giao diện

Prefab `Resources/StoryDialogue.prefab` dùng cùng kích thước khung, vị trí tên/chân dung/chữ, font và nền với NPC Linh. `Choices` là vùng nút nằm phía trên khung để không đè lên lời thoại. Không có nút tiếp tục/đóng; dùng ghi chú C/Esc bằng tiếng Anh. Bố cục tĩnh chỉnh được trong Hierarchy hoặc Prefab Mode; lúc chơi script không bố trí lại các đối tượng.

`TreasureMirrorDialogueCanvas → DialoguePanel` trong Scene1 là instance của prefab này. Bật tạm DialoguePanel khi muốn chỉnh trong Edit Mode. `CustomActions` tiếp tục dành cho nút tương tác bổ sung.

Menu **Tools → Story → Apply Linh Layout To Story Dialogue** sao chép lại thiết kế từ NPC Linh khi chủ động chọn. Thao tác này ghi đè các vị trí/style khung và nút lựa chọn trên prefab; không tự chạy khi Play.

## Dùng cho NPC khác

Tạo file JSON cùng cấu trúc, có id riêng, speaker, introduction, questions và completion; consent có thể bỏ nếu không cần. Các trường tiếng Việt là tùy chọn. Mỗi câu hỏi có id riêng, type multiple_choice, skill, dialogue_en, correct, distractors (1–3 phần tử), explanation_en, wrong_hint_en và after_en.

Trên vùng tương tác NPC có `NPCDialogueTrigger`, kéo JSON vào **Question Conversation**. Có thể gán DialogueData để lấy ảnh chân dung, và nối **On Conversation Completed** với phần thưởng/nhiệm vụ riêng. Trường JSON được ưu tiên so với hội thoại thường và beatId của component đó.

NPC JSON dùng chung view, không tự ghi tiến độ q1/q2 của Màn 1. Nếu mở bằng code, gọi `StoryQuizPanel.TryOpenConversation(TextAsset, Action<bool>, Sprite)`; callback chỉ true sau khi hoàn thành toàn bộ câu hỏi. Các phần thưởng gameplay của NPC tùy chỉnh do callback hoặc sự kiện hoàn thành quyết định.
