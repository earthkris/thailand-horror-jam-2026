using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueScriptableObject", menuName = "Dialogue/DialogueScriptableObject")]
public class DialogueScriptableObject : ScriptableObject
{
    public List<Line> lines = new();

    [Serializable]
    public struct Line
    {
        public Sprite spriteImage;
        public string name;

        [TextArea(3, 10)]
        public string text;
    }
}
