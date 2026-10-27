> Tài liệu lịch sử trước refactor. Cấu trúc và luồng hiện tại được mô tả trong [REFACTOR_GUIDE.md](REFACTOR_GUIDE.md) và [PROJECT_STRUCTURE.md](../../../PROJECT_STRUCTURE.md). Các đường dẫn và cơ chế freeze bên dưới không còn là thiết kế hiện hành.

# Hướng Dẫn Refactor Hệ Thống Quiz — Gương Kho Báu

> File này dùng cho 2 mục đích:
> 1. **Đưa cho GitHub Copilot / Copilot Chat** để nó sửa code đúng ý, đúng thứ tự, không phá vỡ logic cũ.
> 2. **Hướng dẫn người đọc (bạn)** các bước thao tác tay trong Unity Editor mà Copilot không làm thay được (kéo thả Inspector, tạo Asset...).

Đọc kèm với `QUIZ_SYSTEM_FLOW.md` gốc trước khi bắt đầu — file đó mô tả luồng hiện tại, file này mô tả luồng sau khi sửa.

**Nguyên tắc bắt buộc khi refactor:** không đổi hành vi người chơi thấy được (Player vẫn đi tới rương, panel vẫn mở/đóng, Cancel/Submit vẫn hoạt động y hệt). Chỉ đổi *cách các script nói chuyện với nhau bên trong*.

---

## Mục lục các bước

| Bước | Nội dung | Loại việc |
|---|---|---|
| 1 | Tạo `QuestionData` — tách câu hỏi ra khỏi Scene | Code + Unity Editor |
| 2 | Tạo `QuizEvents` — bỏ điều khiển trực tiếp Player/Monster | Code + Unity Editor |
| 3 | Refactor `MirrorTreasureItem` dùng `QuestionData` + bắn event | Code + Unity Editor |
| 4 | Refactor `QuestionManager` — bỏ Find theo tên, dùng event | Code + Unity Editor |
| 5 | Refactor `PanelController` — kiểm tra đúng/sai qua `QuestionData` | Code + Unity Editor |
| 6 | Gộp `MonsterFreezeController` vào 1 nguồn sự thật duy nhất | Code + Unity Editor |
| 7 | Thêm cờ hoàn thành cho rương (`isCompleted`) | Code + Unity Editor |
| 8 | Kiểm thử toàn bộ luồng | Unity Editor |

---

## BƯỚC 1 — Tạo `QuestionData`

### 1a. Prompt đưa cho Copilot

```
Tạo file mới Assets/_Project/Scripts/ItemQuiz/QuestionData.cs.
Yêu cầu:
- Là ScriptableObject, có [CreateAssetMenu(fileName = "QuestionData", menuName = "EarthDefender/Question Data")]
- Các field: id (string), skillTag (string), content (string, TextArea),
  answers (string[4]), correctIndex (int), explanation (string, TextArea)
- Không thêm logic gì khác, chỉ là data container thuần.
- Namespace giống các script khác trong ItemQuiz nếu project đang dùng namespace.
```

### 1b. Code mẫu (Copilot nên tạo ra tương tự)

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionData", menuName = "EarthDefender/Question Data")]
public class QuestionData : ScriptableObject
{
    public string id;
    public string skillTag; // "Vocabulary" | "Grammar" | "Reading"

    [TextArea(2, 4)]
    public string content;

    public string[] answers = new string[4];
    public int correctIndex;

    [TextArea(2, 4)]
    public string explanation;
}
```

### 1c. Thao tác trong Unity Editor

1. Sau khi Copilot tạo file, quay lại Unity, đợi compile xong (icon xoay ở góc dưới phải biến mất).
2. Chuột phải trong cửa sổ **Project** → `Assets/_Project/Data/Questions` (tạo folder này nếu chưa có) → **Create → EarthDefender → Question Data**.
3. Đặt tên file ví dụ `Q_Chest01_Vocab01`.
4. Click vào file vừa tạo, điền vào Inspector:
   - `Id`: `mirror_treasure_01`
   - `Skill Tag`: `Vocabulary`
   - `Content`: nội dung câu hỏi (thay cho text đang gõ cứng trong TMP "Question1")
   - `Answers`: 4 đáp án
   - `Correct Index`: số thứ tự đáp án đúng (0 = đáp án đầu tiên)
   - `Explanation`: giải thích đáp án đúng
5. Lặp lại cho mỗi rương (4 rương trong scene hiện tại → tạo 4 asset `QuestionData`, hoặc nhiều hơn nếu muốn 1 rương có nhiều câu).

> Từ bước này, bạn **không còn cần TMP_Text "Question1" chứa nội dung câu hỏi nữa** — nhưng đừng xoá nó vội, để dành làm gì thì xem Bước 3.

---

## BƯỚC 2 — Tạo `QuizEvents`

### 2a. Prompt đưa cho Copilot

```
Tạo file mới Assets/_Project/Scripts/ItemQuiz/QuizEvents.cs.
Yêu cầu: một static class chứa 3 C# event dùng để tách rời QuestionManager
khỏi PlayerMovement và MonsterGridAI:
- event Action<QuestionData> OnQuizOpened
- event Action<bool, QuestionData> OnAnswerSubmitted  // bool = trả lời đúng hay sai
- event Action OnQuizClosed
Mỗi event có kèm 1 method static để raise (gọi) event đó, đặt tên
RaiseOpened, RaiseAnswered, RaiseClosed.
Dùng ?.Invoke() để tránh lỗi null khi chưa có ai subscribe.
```

### 2b. Code mẫu

```csharp
using System;

public static class QuizEvents
{
    public static event Action<QuestionData> OnQuizOpened;
    public static event Action<bool, QuestionData> OnAnswerSubmitted;
    public static event Action OnQuizClosed;

    public static void RaiseOpened(QuestionData question) => OnQuizOpened?.Invoke(question);
    public static void RaiseAnswered(bool isCorrect, QuestionData question) => OnAnswerSubmitted?.Invoke(isCorrect, question);
    public static void RaiseClosed() => OnQuizClosed?.Invoke();
}
```

### 2c. Thao tác trong Unity Editor

Không cần thao tác gì trong Editor ở bước này — đây là script thuần logic, không có Inspector field. Chỉ cần đợi Unity compile xong không lỗi.

---

## BƯỚC 3 — Refactor `MirrorTreasureItem`

### 3a. Prompt đưa cho Copilot

```
Sửa file Assets/_Project/Scripts/ItemQuiz/MirrorTreasureItem.cs theo yêu cầu sau,
KHÔNG được đổi hành vi bên ngoài (Player vẫn chạm rương, panel vẫn mở y như cũ):

1. Thêm field [SerializeField] private QuestionData question; hiển thị trong Inspector
   để kéo asset QuestionData vào thay vì đọc TMP_Text.
2. Giữ nguyên field questionText (TMP_Text) NHƯNG đổi vai trò: nó chỉ dùng để
   hiển thị preview câu hỏi phía trên đầu rương trong world space (nếu game có tính năng này),
   không còn là nguồn dữ liệu chính.
3. Trong TryActivate(), thay vì đọc text từ questionText, gọi:
   QuestionManager.Instance.ShowQuestion(question);
   (ShowQuestion giờ nhận QuestionData thay vì 2 tham số string như cũ - sẽ sửa ở Bước 4)
4. Thêm field private bool isCompleted = false; kiểm tra ở đầu TryActivate(),
   return ngay nếu isCompleted == true.
5. Subscribe QuizEvents.OnAnswerSubmitted trong OnEnable(), unsubscribe trong OnDisable().
   Trong callback, nếu câu hỏi trả lời đúng LÀ CÂU HỎI CỦA RƯƠNG NÀY (so sánh question.id
   với id trong callback) thì set isCompleted = true.
6. Giữ nguyên toàn bộ comment tiếng Việt hiện có trong file, chỉ thêm/sửa phần liên quan.
```

### 3b. Thao tác trong Unity Editor

1. Sau khi Copilot sửa xong và Unity compile hết lỗi, chọn từng GameObject `PF Props - Chest 01` (và các bản copy khác) trong Hierarchy.
2. Ở Inspector, script `MirrorTreasureItem` giờ sẽ có thêm ô **Question** — kéo đúng asset `QuestionData` tương ứng (ví dụ `Q_Chest01_Vocab01`) vào ô đó.
3. Làm tương tự cho tất cả 4 rương, mỗi rương gán 1 `QuestionData` khác nhau.
4. Nếu Copilot báo lỗi "ShowQuestion không nhận tham số này" — đó là bình thường, vì `QuestionManager` chưa sửa (làm ở Bước 4 ngay sau đây). Cứ tiếp tục.

---

## BƯỚC 4 — Refactor `QuestionManager`

### 4a. Prompt đưa cho Copilot

```
Sửa file Assets/_Project/Scripts/ItemQuiz/QuizManager.cs (class QuestionManager) theo yêu cầu:

1. Đổi chữ ký hàm ShowQuestion để nhận QuestionData thay vì (string, string):
   public void ShowQuestion(QuestionData question)
   Lưu question vào 1 field private QuestionData currentQuestion; để PanelController đọc lại.

2. Thay toàn bộ việc dùng GameObject.Find("Panel") / FindGameObjectIncludingInactive("Panel")
   bằng 1 field [SerializeField] private GameObject panel; gán sẵn trong Inspector.
   Nếu panel null lúc Awake(), Debug.LogWarning("QuestionManager: chưa gán Panel trong Inspector,
   đang fallback sang tìm theo tên - hãy gán tay để tránh lỗi khi đổi tên object trong Hierarchy.")
   rồi mới dùng FindGameObjectIncludingInactive như phương án dự phòng.

3. Tương tự thêm [SerializeField] private TMP_Text contentText; gán sẵn Content bên trong Panel,
   thay vì tìm bằng tên "Content" lúc runtime.

4. XOÁ việc gọi trực tiếp playerMovement.enabled = false và monsterAI.enabled = false
   trong FreezeGameplay(). Thay bằng gọi QuizEvents.RaiseOpened(question) ngay sau khi
   panel.SetActive(true). Tương tự trong CloseQuestion(), XOÁ việc set enabled = true cho
   player/monster, thay bằng QuizEvents.RaiseClosed().
   => Sau bước này QuestionManager KHÔNG CÒN cần giữ reference tới PlayerMovement hay
      mảng MonsterGridAI nữa, có thể xoá các field đó luôn nếu không dùng ở đâu khác.

5. Thêm hàm public void SubmitAnswer(int selectedIndex) gọi từ PanelController khi bấm Submit:
   - So sánh selectedIndex với currentQuestion.correctIndex
   - Gọi QuizEvents.RaiseAnswered(isCorrect, currentQuestion)
   - Sau đó gọi CloseQuestion() như cũ

6. Vẫn giữ nguyên cơ chế "tự tạo QuestionManager runtime nếu chưa có trong scene",
   nhưng thêm Debug.LogWarning("QuestionManager không có sẵn trong scene, đang tự tạo runtime -
   kiểm tra lại Hierarchy nếu đây không phải điều bạn muốn.") ngay khi tự tạo.

Giữ nguyên biến IsQuestionOpen (public static hoặc property tuỳ code cũ) vì
MonsterGridAI đang dùng biến này để dừng AI.
```

### 4b. Thao tác trong Unity Editor

1. Chọn GameObject `QuestionManager` trong Hierarchy.
2. Ở Inspector, sẽ xuất hiện 2 ô mới: **Panel** và **Content Text**.
3. Kéo GameObject `Panel` (nằm trong `Question/Canvas/Panel`) vào ô **Panel**.
4. Mở rộng `Panel` trong Hierarchy, kéo GameObject `Content` (TMP_Text) vào ô **Content Text**.
5. Nếu Inspector còn hiển thị ô **Player Movement** hoặc **Monster AIs** (dữ liệu cũ) mà Copilot đã xoá field tương ứng trong code — Unity sẽ tự ẩn field đó đi, không cần làm gì thêm. Nếu Unity báo warning "field bị mất dữ liệu", có thể bỏ qua vì field đó không còn dùng.

---

## BƯỚC 5 — Refactor `PanelController`

### 5a. Prompt đưa cho Copilot

```
Sửa file Assets/_Project/Scripts/ItemQuiz/PanelController.cs:

1. Trong OnSubmitClicked(), XOÁ dòng Debug.Log (TODO) cũ.
   Thêm logic: xác định người chơi đã chọn đáp án nào (dựa theo 2 nút Submit và
   Submit (1) hiện có trong Hierarchy - nếu code cũ dùng 2 nút này làm 2 lựa chọn
   riêng thì giữ nguyên cấu trúc, chỉ thêm việc truyền index của nút được bấm).
   Gọi QuestionManager.Instance.SubmitAnswer(selectedIndex).

2. OnCancelClicked() giữ nguyên hành vi cũ (chỉ gọi CloseQuestion()), nhưng đảm bảo
   nó KHÔNG gọi QuizEvents.RaiseAnswered - Cancel nghĩa là bỏ qua câu hỏi,
   không tính là trả lời đúng hay sai.

3. Nếu file hiện tại có UI hiển thị đáp án đúng/sai hoặc explanation sau khi Submit,
   hãy thêm đoạn code hiển thị currentQuestion.explanation khi trả lời đúng
   (theo đúng tinh thần "Explanation-on-Escape" trong GDD - hiện giải thích như
   một phần thưởng sau khi vượt qua câu hỏi).
   Nếu UI cho phần này CHƯA CÓ, chỉ cần Debug.Log(currentQuestion.explanation)
   tạm thời và ghi comment TODO để làm UI sau.
```

### 5b. Thao tác trong Unity Editor

1. Kiểm tra lại 2 nút `Submit` và `Submit (1)` trong Hierarchy — xác nhận với Copilot xem code cũ đang coi 2 nút này là 2 đáp án khác nhau hay là 1 nút bị nhân đôi nhầm. Việc này Copilot không tự biết được, cần bạn xác nhận bằng cách xem trong Inspector của mỗi nút, phần **On Click()** đang trỏ tới hàm nào.
2. Sau khi sửa xong, không cần gán thêm gì mới trong Inspector cho bước này — chỉ cần test lại nút Submit ở Bước 8.

---

## BƯỚC 6 — Gộp `MonsterFreezeController`

### 6a. Prompt đưa cho Copilot

```
Sửa file Assets/_Project/Scripts/ItemQuiz/MonsterFreezeController.cs:

Thay vì để MirrorTreasureItem gọi trực tiếp MonsterFreezeController.TriggerFreeze(),
hãy làm cho MonsterFreezeController tự lắng nghe QuizEvents:
- Thêm 1 MonoBehaviour nhỏ (hoặc static constructor nếu class này không cần GameObject)
  subscribe QuizEvents.OnQuizOpened để tự gọi TriggerFreeze(freezeDuration mặc định).
- Nếu cần freezeDuration khác nhau tuỳ rương, thêm field duration vào QuestionData
  (ScriptableObject ở Bước 1) và truyền qua QuizEvents.OnQuizOpened(QuestionData question)
  - đọc question.freezeDuration thay vì hằng số cố định.
Xoá lời gọi MonsterFreezeController.TriggerFreeze() trực tiếp trong MirrorTreasureItem
vì giờ nó tự động xảy ra qua event.
```

### 6b. Thao tác trong Unity Editor

1. Nếu Copilot thêm field `freezeDuration` vào `QuestionData`, quay lại từng asset `QuestionData` đã tạo ở Bước 1, điền giá trị (ví dụ 3 giây) cho ô **Freeze Duration** mới xuất hiện.
2. Nếu `MonsterFreezeController` cần gắn lên 1 GameObject (không còn static thuần) — tạo 1 GameObject rỗng tên `SystemManagers` trong Hierarchy, gắn script này vào đó.

---

## BƯỚC 7 — Cờ hoàn thành cho rương

Đã gộp vào Bước 3 (field `isCompleted`). Không cần bước riêng, nhưng lưu ý:

### Thao tác kiểm tra trong Unity Editor

1. Chạy game (Play), trả lời đúng 1 rương.
2. Đi ra xa rồi quay lại chạm rương đó lần nữa — panel **không được mở lại**.
3. Nếu vẫn mở lại được, kiểm tra trong Inspector của rương đó xem field `question.id` có bị để trống hoặc trùng với rương khác không (Bước 3 so sánh theo `id`).

---

## BƯỚC 8 — Kiểm thử toàn bộ luồng (checklist tay, không cần Copilot)

Chạy lần lượt, tick từng dòng:

- [ ] Chạm rương 1 → Panel mở, đúng nội dung câu hỏi của rương 1 (không phải rương khác)
- [ ] Trong lúc Panel mở, Player không di chuyển được khi bấm phím
- [ ] Trong lúc Panel mở, tất cả Monster đứng yên
- [ ] Bấm Cancel → Panel đóng, Player và Monster hoạt động lại bình thường, rương **mở lại được** lần sau
- [ ] Bấm Submit với đáp án đúng → Panel đóng, rương này **không mở lại được** nữa
- [ ] Bấm Submit với đáp án sai → Panel đóng, rương **vẫn mở lại được** (vì chưa hoàn thành)
- [ ] Chạm rương thứ 2 khi rương 1 đang xử lý — không bị chồng 2 panel cùng lúc
- [ ] Tắt hẳn `QuestionManager` khỏi Hierarchy, chạy lại game, chạm rương → thấy Warning log "đang tự tạo runtime" xuất hiện trong Console (xác nhận cơ chế fallback vẫn hoạt động)
- [ ] Đổi tên GameObject `Panel` thành tên khác tạm thời → xác nhận game vẫn chạy đúng nhờ đã gán tay trong Inspector (Bước 4), rồi đổi tên lại như cũ

---

## Ghi chú khi giao việc cho Thành viên A / B (AI layer & UI layer)

Sau khi hoàn thành các bước trên, 2 thành viên còn lại có thể làm việc mà **không cần đợi bạn**:

- **Thành viên A (AI/Data)**: chỉ cần biết `QuestionData` có cấu trúc gì (Bước 1) để viết `AIQuestionService` sinh ra đúng định dạng này, rồi tạo `QuestionData` bằng code (`ScriptableObject.CreateInstance<QuestionData>()`) thay vì tạo tay trong Editor.
- **Thành viên B (UI)**: chỉ cần subscribe `QuizEvents.OnQuizOpened`, `OnAnswerSubmitted`, `OnQuizClosed` (Bước 2) để làm hiệu ứng UI riêng (rung màn hình khi sai, hiệu ứng confetti khi đúng...) mà không cần đụng vào `QuestionManager` hay `MirrorTreasureItem`.

Đây chính là lý do nên làm 3 bước đầu (`QuestionData`, `QuizEvents`, refactor `MirrorTreasureItem`) **trước tiên** — nó là điều kiện để 2 người kia bắt đầu việc của họ song song với bạn.
