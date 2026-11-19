# Lab building interaction (current workflow)

No room or GUI generator is needed. Use your existing building sprite with its BoxCollider2D.

1. Select the lab building and Add Component > LabBuildingInteraction.
2. Keep BoxCollider2D enabled and Is Trigger OFF so the building remains solid. The script handles physical collision as well as optional triggers; it never changes your collider. Player must retain its existing Rigidbody2D Dynamic and non-trigger collider.
3. Question Data is assigned by Reset when the component is added in Unity Editor. If blank, drag Assets/_Project/Data/Questions/Map1/Json/research_document_questions_en.json into it.
4. Dialogue Panel may be left blank to find the existing StoryQuizPanel, or assign the existing TreasureMirrorDialogueCanvas panel explicitly.
5. Set each research monster's Encounter Mode to 3 (LaboratoryAmbush) and keep its GameObject enabled. Its existing script hides it until the research result is submitted.

After completing the tower and archaeologist missions, touch the building. Linh introduces the damaged report in your existing bilingual dialogue frame. Press C to proceed; choose by button or 1-4. Exactly five questions are asked. Each first choice is final; wrong answers also advance after displaying the explanation. C on the final result closes the dialogue, commits the score once, updates the destination in the journal and activates mode-3 monsters.

Escape/Close before reaching the final result cancels without activating monsters. Walk fully away and return to retry. All mode-3 monsters in the scene use the same submitted result.

0/1/2/3 wrong => 100/150/200/250 HP and 20/35/50/65 damage.
4-5 wrong => invulnerable and one-hit lethal, including armor.

Dialogue and five questions are in the Map1 JSON, including Vietnamese translations. The northern watchtower is an editable sample next destination; adjust both destination fields and question 5 to match your map.

The old Hierarchy generator menu was removed. Existing generated objects in your scene are not deleted automatically. Remove the obsolete LabRoom/document UI objects yourself if they overlap the building or screen.

Runtime and EditMode test sources compile. Tests cover all score values, repeated choice protection, cancel behavior and existing monster difficulty checks. Unity Test Runner / Play Mode have not been run for this change. Your screenshot shows unsaved scene edits; the script has not been attached by modifying the saved scene file.