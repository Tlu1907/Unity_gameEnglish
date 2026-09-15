# He Thong Quiz - Guong Kho Bau (Quiz System Flow)

> Tai lieu nay mo ta toan bo kien truc va luong xu ly cua he thong cau hoi khi player cham vao Guong kho bau.
> AI doc file nay truoc khi them tinh nang moi de tranh pha vo logic hien tai.

---

## 1. Cau Truc Scene (Hierarchy)

```
SampleScene
|-- Grid
|   └── Tilemap                      <- Ban do di chuyen theo o (grid-based)
|-- Player                           <- PlayerMovement.cs
|-- Monsters
|   └── [Monster 1..N]               <- MonsterGridAI.cs
|-- Item
|   └── PF Props - Chest 01 [x4]     <- MirrorTreasureItem.cs (guong kho bau)
|       |-- Shadow
|       └── Canvas
|           └── Question1            <- TMP_Text chua NOI DUNG cau hoi cua guong nay
|-- Question
|   └── Canvas
|       └── Panel                    <- Bat dau INACTIVE, mo/dong boi QuestionManager
|           |-- Cancel               <- Button -> PanelController.OnCancelClicked()
|           |-- Submit               <- Button -> PanelController.OnSubmitClicked()
|           |   └── Text (TMP)
|           |-- Submit (1)
|           |   └── Text (TMP)
|           └── Content              <- TMP_Text nhan NOI DUNG tu guong khi panel mo
└── QuestionManager                  <- QuestionManager.cs (tu tao runtime neu chua co)
```

---

## 2. Cac Script va Vai Tro

| Script | File | Gan tren | Vai tro |
|--------|------|----------|---------|
| PlayerMovement | Player/PlayerMovement.cs | Player | Di chuyen theo grid, phat hien guong o ke tiep |
| MirrorTreasureItem | ItemQuiz/MirrorTreasureItem.cs | Chest 01 | Doc cau hoi, kich hoat panel |
| QuestionManager | ItemQuiz/QuizManager.cs | QuestionManager | Singleton: mo/dong panel, freeze gameplay |
| PanelController | ItemQuiz/PanelController.cs | Panel | Xu ly nut Cancel/Submit |
| MonsterFreezeController | ItemQuiz/MonsterFreezeController.cs | (static class) | Dong bang quai vat |
| MonsterGridAI | Monster/MonsterGridAI.cs | Monster | AI grid, kiem tra freeze |

---

## 3. Luong Mo Panel (Khi Player Cham Guong)

```
[Player nhan phim di chuyen]
        |
        v
PlayerMovement.Update()
  -> Kiem tra QuestionManager.IsQuestionOpen
     -> Neu true: DUNG, khong xu ly input
  -> FindMirrorAtPosition(targetPosition)
     -> Phat hien MirrorTreasureItem o o ke tiep
  -> StartCoroutine(MoveOneTile(direction, mirror))
        |
        v
MoveOneTile() hoan thanh di chuyen
  -> Goi mirror.TryActivate()
        |
        v
MirrorTreasureItem.TryActivate()
  [1] Kiem tra QuestionManager.IsQuestionOpen -> return neu dang mo
  [2] Doc text tu questionText (TMP_Text "Question1" trong children)
  [3] Dam bao QuestionManager ton tai (tao moi neu chua co)
  [4] Goi QuestionManager.Instance.ShowQuestion("mirror_treasure", null)
      -> QuestionManager dung FindGameObjectIncludingInactive("Panel")
         !!! QUAN TRONG: Khong dung GameObject.Find() vi Panel la INACTIVE
      -> Panel.SetActive(true)
      -> IsQuestionOpen = true
      -> FreezeGameplay(): playerMovement.enabled = false, monsterAI.enabled = false
  [5] Tim Panel (da active) -> tim child "Content" -> set TMP_Text.text = content
  [6] MonsterFreezeController.TriggerFreeze(freezeDuration)
```

---

## 4. Luong Dong Panel (Cancel / Submit)

```
[Nguoi choi nhan Cancel hoac Submit]
        |
        v
PanelController.OnCancelClicked() hoac OnSubmitClicked()
  -> Submit: Debug.Log (TODO: them logic kiem tra dap an)
  -> QuestionManager.Instance.CloseQuestion()
        |
        v
QuestionManager.CloseQuestion()
  -> IsQuestionOpen = false
  -> Panel.SetActive(false)
  -> ResumeGameplay(): playerMovement.enabled = true, monsterAI.enabled = true
```

---

## 5. Co Che Freeze

MonsterFreezeController la static class - khong gan len GameObject:

```csharp
MonsterFreezeController.TriggerFreeze(float duration); // Dong bang N giay
MonsterFreezeController.IsFrozen; // true neu dang frozen
```

MonsterGridAI.Update() dung khi:
1. QuestionManager.IsQuestionOpen == true
2. MonsterFreezeController.IsFrozen == true

---

## 6. Co Che Re-Activation

- Khong dung isActivated flag -> guong mo lai duoc sau khi Cancel
- Guard duy nhat: QuestionManager.IsQuestionOpen (chong spam khi panel dang mo)

---

## 7. Inspector Setup

### Moi Guong (PF Props - Chest 01):
- Question Text: keo "Question1" TMP_Text vao (neu khong keo thi tu tim bang GetComponentInChildren)
- Question Panel: de trong (tu tim runtime sau khi ShowQuestion active panel)
- Freeze Duration: 3 giay (mac dinh)

### Button Cancel:
- On Click() -> keo "Panel" -> chon PanelController.OnCancelClicked()

### Button Submit:
- On Click() -> keo "Panel" -> chon PanelController.OnSubmitClicked()

### QuestionManager (neu add thu cong vao scene):
- Question Panel: keo Panel vao (optional)
- Player Movement: keo Player vao
- Monster AIs: keo tat ca Monster vao (array)

---

## 8. Loi Thuong Gap va Cach Tranh

### LOI: Panel khong hien khi cham guong
- Nguyen nhan: Dung GameObject.Find("Panel") khi Panel dang inactive -> tra null
- Cach tranh: Luon dung FindGameObjectIncludingInactive() trong QuestionManager
  Su dung FindObjectsByType voi FindObjectsInactive.Include

### LOI: Guong chi mo duoc 1 lan
- Nguyen nhan: isActivated = true va khong bao gio reset
- Cach tranh: Khong dung isActivated flag. Dung QuestionManager.IsQuestionOpen lam guard

### LOI: Guong bi mat (invisible)
- Nguyen nhan: Doi sortingLayerName = "Default" trong code -> sprite render sau Tilemap
- Cach tranh: Khong doi sortingLayerName trong code. Dung Inspector de chinh

### LOI: Guong de len Player
- Nguyen nhan: Guong dung Sorting Layer "Chest" Order 1, Player dung layer khac
- Cach tranh: Dat Player SpriteRenderer Sorting Layer = "Chest", Order = 5 (cao hon guong)

---

## 9. TODO / Diem Mo Rong

- QuestionManager.OnSubmitButtonClicked(): Them logic kiem tra dap an dung/sai
- MirrorTreasureItem: Them isCompleted flag de danh dau guong da hoan thanh -> khong mo lai
- Neu them loai trigger moi: tao script rieng, goi QuestionManager.Instance.ShowQuestion() roi set Content
