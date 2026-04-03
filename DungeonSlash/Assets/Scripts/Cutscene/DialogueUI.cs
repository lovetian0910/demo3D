using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns the dialogue panel: speaker name, body text, and blinking click-prompt.
/// Wire references in Inspector after creating the UI hierarchy in the scene.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI clickPromptText;

    private Coroutine blinkCoroutine;

    public void Show(string speakerName, string text)
    {
        panel.SetActive(true);
        speakerNameText.text = speakerName;
        dialogueText.text = text;

        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkPrompt());
    }

    public void Hide()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        panel.SetActive(false);
    }

    private IEnumerator BlinkPrompt()
    {
        clickPromptText.text = "[ Click to continue ]";
        while (true)
        {
            float alpha = Mathf.PingPong(Time.time * 2f, 1f);
            Color c = clickPromptText.color;
            c.a = alpha;
            clickPromptText.color = c;
            yield return null;
        }
    }
}
