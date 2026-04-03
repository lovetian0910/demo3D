using System;
using System.Collections.Generic;

/// <summary>
/// Mirrors the JSON schema for one line of opening dialogue.
/// speaker: "player" | "enemy_0" | "enemy_1" | "enemy_2"
/// animation: Animator State name (e.g. "Relax", "Nod Head", "Clapping")
/// </summary>
[Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;
    public string animation;
}

[Serializable]
public class DialogueData
{
    public List<DialogueLine> lines;
}
