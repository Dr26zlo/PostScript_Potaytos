using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Dialogue", menuName = "Scriptables/Dialogues", order = 52)]
public class DialogueObject : ScriptableObject
{
    [Header("Speaker Settings")]
    public string speakerName;

    [TextArea(3, 10)]
    public string dialogueText;

    [Header("Choices (Optional)")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    public DialogueObject nextNode;

}

[System.Serializable]
public class DialogueChoice
{
    [System.NonSerialized] public System.Action action;
    public string choiceText; 
    public DialogueObject nextNode;

    public string choiceID;
}
