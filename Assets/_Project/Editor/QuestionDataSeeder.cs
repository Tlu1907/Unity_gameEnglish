#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class QuestionDataSeeder
{
    private const string FolderPath = "Assets/_Project/Data/Questions/Test";

    [MenuItem("EarthDefender/Tạo dữ liệu câu hỏi test")]
    public static void CreateQuestionTestData()
    {
        EnsureFolderExists();

        QuestionData[] questions = new QuestionData[]
        {
            CreateQuestion(
                "test_vocab_01",
                "Vocabulary",
                "What is the meaning of 'happy'?",
                new[] { "Sad", "Feeling good", "Tired", "Angry" },
                1,
                "'Happy' means feeling pleased or glad.",
                3f),

            CreateQuestion(
                "test_vocab_02",
                "Vocabulary",
                "Which word means 'a place to buy food'?",
                new[] { "School", "Market", "Hospital", "Library" },
                1,
                "'Market' is a place where people buy and sell food and goods.",
                3f),

            CreateQuestion(
                "test_grammar_01",
                "Grammar",
                "Choose the correct sentence:",
                new[] { "She go to school.", "She goes to school.", "She going to school.", "She gone to school." },
                1,
                "With 'she/he/it', the verb takes an -s ending in present simple: 'goes'.",
                3f),

            CreateQuestion(
                "test_grammar_02",
                "Grammar",
                "Fill in the blank: 'I ___ a student.'",
                new[] { "is", "am", "are", "be" },
                1,
                "'I' always pairs with 'am' in the verb 'to be'.",
                3f),

            CreateQuestion(
                "test_reading_01",
                "Reading",
                "Tom has a red bike. He rides it to school every day. What color is Tom's bike?",
                new[] { "Blue", "Green", "Red", "Black" },
                2,
                "The passage says 'Tom has a red bike.'",
                3f)
        };

        for (int i = 0; i < questions.Length; i++)
        {
            string assetPath = $"{FolderPath}/Q_{i + 1:00}_{questions[i].id}.asset";
            QuestionData existingAsset = AssetDatabase.LoadAssetAtPath<QuestionData>(assetPath);

            if (existingAsset == null)
            {
                AssetDatabase.CreateAsset(questions[i], assetPath);
            }
            else
            {
                existingAsset.id = questions[i].id;
                existingAsset.skillTag = questions[i].skillTag;
                existingAsset.content = questions[i].content;
                existingAsset.answers = questions[i].answers;
                existingAsset.correctIndex = questions[i].correctIndex;
                existingAsset.explanation = questions[i].explanation;
                existingAsset.freezeDuration = questions[i].freezeDuration;
                EditorUtility.SetDirty(existingAsset);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Đã tạo 5 câu hỏi test tại Assets/_Project/Data/Questions/Test/");
    }

    private static void EnsureFolderExists()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
        {
            string parentFolder = "Assets/_Project/Data/Questions";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                string[] parts = parentFolder.Split('/');
                string current = "Assets/_Project/Data";

                if (!AssetDatabase.IsValidFolder(current))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Data");
                    current = "Assets/_Project/Data";
                }

                if (!AssetDatabase.IsValidFolder(current + "/Questions"))
                {
                    AssetDatabase.CreateFolder(current, "Questions");
                }
            }

            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data/Questions", "Test");
            }
        }
    }

    private static QuestionData CreateQuestion(
        string id,
        string skillTag,
        string content,
        string[] answers,
        int correctIndex,
        string explanation,
        float freezeDuration)
    {
        QuestionData question = ScriptableObject.CreateInstance<QuestionData>();
        question.id = id;
        question.skillTag = skillTag;
        question.content = content;
        question.answers = answers;
        question.correctIndex = correctIndex;
        question.explanation = explanation;
        question.freezeDuration = freezeDuration;
        return question;
    }
}
#endif
