# Quản lý dữ liệu câu hỏi

Tất cả câu hỏi tĩnh được quản lý trong Assets/_Project/Data:

- Questions/Map1/Json/map1_spirit_questions_en.json: hội thoại và chuỗi câu hỏi ở tháp. Chỉnh bằng trình soạn thảo JSON. Đây là nguồn được ưu tiên bởi q1_spirit.asset.
- Questions/Map1/Resources/Story/q1_spirit.asset: cấu hình nhiệm vụ tháp và liên kết tới JSON; các trường câu hỏi đơn dùng cho luồng tương thích khi không gán JSON.
- Questions/Map1/Resources/Story/q2_archaeologist.asset: hội thoại, câu hỏi, đáp án và giải thích của nhà khảo cổ. Chọn asset trong Unity và sửa Inspector.
- Questions/Shared/Test/*.asset: mỗi file là một câu hỏi trắc nghiệm độc lập. Chỉnh content, answers, correctIndex (bắt đầu từ 0), explanation trong Inspector.
- Dialogues/*.asset: hội thoại thuần, không có câu hỏi.

Với StoryBeatData, correct là đáp án đúng; distractors là 1–3 đáp án sai, không trùng nhau. Nội dung và đáp án không còn bị khóa vào các từ sword/bridge trong C#.

Giữ nguyên beatId của hai nhiệm vụ hiện tại vì tiến trình nhiệm vụ dùng các ID này. Giữ thư mục Resources/Story để Resources.Load tìm được asset khi build. Khi di chuyển asset, luôn chuyển cùng file .meta để giữ liên kết scene/prefab.

Menu EarthDefender > Questions > Open question data chỉ mở thư mục dữ liệu, không tạo lại hay ghi đè câu hỏi. Script runtime không chứa bộ câu hỏi dự phòng; thiếu dữ liệu hợp lệ sẽ báo lỗi để sửa asset.

FlaskEnglishQuizPanel vẫn nhận câu hỏi động từ server; các câu hỏi đó không phải ngân hàng câu hỏi tĩnh cục bộ.
Mỗi màn có thư mục riêng: Questions/Map1, Questions/Map2, ...; Shared chứa câu hỏi dùng chung. Với màn mới, dùng beatId riêng và đặt asset trong Resources/Story để bộ nạp hiện tại tìm được; tiến trình nhiệm vụ mới cần nối logic riêng.
