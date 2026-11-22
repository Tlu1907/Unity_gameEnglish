# Quản lý dữ liệu câu hỏi

## Khung màn chơi tái sử dụng

Prefab: `Assets/_Project/Scenes/Sence_1/Prefabs/ReusableLevelTemplate.prefab`.
Prefab gồm Player, Main Camera, Lighting, Terrain trống, PlayerSpawn, UI hội thoại mở đầu,
hội thoại JSON, máu/giáp, minimap, quiz, sổ ghi chú và EventSystem.

Để dựng Map 2:

1. Kéo prefab vào scene trống; bỏ Camera/EventSystem có sẵn nếu scene đã có chúng.
2. Gán sprite nền tại `Terrain/MapBackground`, chỉnh vị trí/kích thước của nền.
3. Tô các Tilemap `Terrain_Ground`, `Terrain_Stairs`, `Terrain_Bridges`; tô vật cản ở `Terrain_Obstacles`.
4. Đặt `PlayerSpawn` trên vùng đi được.
5. Trên object gốc, component `ReusableLevelConfiguration` có `mapId`, `mapTitle`, `objective`.
   Mặc định là `map2` / `Map 2`. Dùng context menu **Apply Map Configuration** để cập nhật trước khi Play.
   Khi Play, cấu hình tự nối Camera, minimap và sổ với nền/vùng mới.
6. Hội thoại mở đầu nằm trong `UI/DialogueCanvas` (component `NPCIntroDialogue`);
   sao chép DialogueData trước khi chỉnh nội dung riêng cho màn mới.

Các công cụ `Tools/Levels/Export Reusable Level Template from Map 1` và
`Tools/Levels/Verify Reusable Level Template` giúp xuất lại/kiểm tra prefab.
Xuất lại lấy bản snapshot của Map 1, giữ bố cục chưa lưu nếu Map 1 đang mở, không lưu đè scene gốc.
Khung không chứa địa hình, quái, tháp, cổng hoặc tiến trình nhiệm vụ riêng của Map 1;
nhiệm vụ/NPC của màn mới cần được bổ sung riêng.

## Nhóm ngữ pháp trong sổ

Mỗi đoạn có `grammar` (nội dung), `grammarId` (ID công thức), `groupId` (nhóm).
Cùng một công thức phải dùng chung `grammarId` dù xuất hiện ở nhiều đoạn hoặc có cách diễn đạt khác nhau.
Ví dụ `be_adjective` dùng cho cả mô tả trạng thái và đặc điểm; `present_continuous` dùng chung ở tháp và phòng nghiên cứu.

Các `groupId` hỗ trợ:

| groupId | Tiêu đề trong sổ |
| --- | --- |
| `to_be` | To be & Descriptions |
| `present_continuous` | Present Continuous |
| `possession_relationships` | Possession & Relationships |
| `ability_future` | Ability & Future |
| `commands_suggestions` | Commands & Suggestions |
| `purpose_time` | Purpose & Time |
| `continuing_actions` | Continuing Actions |

Tài liệu nghiên cứu dùng thêm `introductionGrammarId`/`introductionGroupId` và
`destinationGrammarId`/`destinationGroupId` cho phần đầu/cuối.
Nhóm chỉ xuất hiện khi đã ghi ít nhất một kiến thức. Sổ giữ công thức và nghĩa, bỏ cách dùng/ví dụ.
Ngữ pháp được ghi sau khi trả lời câu hỏi hoặc hoàn tất hội thoại; Preview không ghi.
Đoạn cũ chưa có nhóm vẫn được hỗ trợ trong `Other Grammar`.

## Từ vựng chọn lọc cho sổ ghi chú

Chỉnh `Questions/Map1/Resources/Learning/map1_journal_vocabulary.json` để chọn từ được lưu trong sổ.
Danh sách `vocabulary` trong JSON hội thoại vẫn dùng để hiển thị và tô màu trong khung hội thoại;
sổ chỉ lấy từ trong file chọn lọc khi người chơi xem đúng đoạn. Không tự nạp hết từ khi bắt đầu game.

Mỗi phần tử trong `sections` có `conversationId`, `pageId`, và `vocabulary` gồm các cặp `word`/`meaning`.

- `conversationId`: `q1_spirit`, `q2_archaeologist`, hoặc `research_document`.
- `pageId`: `introduction`, `consent`, `reply_yes`, `reply_no`, `completion`.
- Câu hỏi tháp dùng ID câu hỏi trong JSON, ví dụ `q1_sword`, `q2_shield`.
- Câu hỏi tài liệu dùng `question_1` đến `question_5`.
- Đặt `vocabulary: []` hoặc bỏ mục tương ứng nếu không muốn lưu từ của đoạn đó.

Ví dụ:

```json
{
  "conversationId": "q1_spirit",
  "pageId": "introduction",
  "vocabulary": [{ "word": "tower", "meaning": "tòa tháp (n)" }]
}
```

Màn mới dùng `Resources/Learning/map2_journal_vocabulary.json` và đặt `journalMapId` của panel thành `map2`.
Thiếu file chọn lọc sẽ không tự ghi toàn bộ từ trong hội thoại làm phương án dự phòng.
Danh sách được nạp một lần mỗi phiên Play; sau khi sửa JSON, vào lại Play để kiểm tra.

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
