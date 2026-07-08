using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class DialogueBubble : MonoBehaviour
{
    public TextMeshProUGUI dialogueTextUI; // Assign a TMP text in your Canvas
                                           // (e.g. a panel or HUD text element)

    public void LoadRandomDialogue(string locationName)
    {
        string fileName = $"dialogues_{locationName.ToLower().Replace(" ", "")}";
        TextAsset jsonFile = Resources.Load<TextAsset>(fileName);

        if (jsonFile != null)
        {
            DialogueData data = JsonUtility.FromJson<DialogueData>(jsonFile.text);
            if (data != null && data.lines.Count > 0)
            {
                string randomLine = data.lines[Random.Range(0, data.lines.Count)];

                // Instead of instantiating a prefab, just set the text
                dialogueTextUI.text = randomLine;
            }
        }
    }
}

[System.Serializable]
public class DialogueData
{
    public List<string> lines;
}
