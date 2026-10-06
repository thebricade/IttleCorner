using UnityEngine;
using System.Collections;

public class CollectionManager : MonoBehaviour
{
    public UnlockPopup unlockPopup;
    public string presetDrawingTag = "MyPreset";

    void Start()
    {
        Wallet.Instance.onThresholdReached += OnThresholdReached;
    }

    void OnDestroy()
    {
        if (Wallet.Instance != null)
            Wallet.Instance.onThresholdReached -= OnThresholdReached;
    }

    void OnThresholdReached()
    {
        unlockPopup.Show(
            "You collected them all!",
            "Now create something with what you found.",
            null
        );

        StartCoroutine(OpenDrawingAfterDelay(3.5f));
    }

    IEnumerator OpenDrawingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        GameModeManager.Instance.SetGameMode(GameMode.Drawing);

        yield return null; // wait one frame for drawing screen to activate

        DrawingPad pad = FindObjectOfType<DrawingPad>();
        Drawing preset = DrawingManager.Instance.GetGameDrawing(presetDrawingTag);

        if (preset != null && pad != null)
        {
            pad.LoadDrawing(preset.texture);
        }
        else
        {
            Debug.LogWarning("CollectionManager: preset '" + presetDrawingTag + "' not found or pad is null");
        }
    }
}