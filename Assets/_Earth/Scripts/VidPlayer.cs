using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class VidPlayer : MonoBehaviour
{

    [SerializeField] GameObject videoCanvas;
    [SerializeField] string videoFileName;

    void Start()
    {
        GameState.Current = GameMode.Cutscene;

        VideoPlayer videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.Play();
        videoPlayer.loopPointReached += _ => Finish();
    }

    // The cutscene is over (it played to its end, or CutsceneSkip skipped it):
    // back to the game, and the first dialogue starts.
    public void Finish()
    {
        GameState.Current = GameMode.Exploring;
        if (videoCanvas != null) videoCanvas.SetActive(false);
        if (dialogueManager.Instance != null) dialogueManager.Instance.TriggerDialogue(0);
        gameObject.SetActive(false);
    }
}
