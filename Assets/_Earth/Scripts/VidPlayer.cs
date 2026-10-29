using System;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class VidPlayer : MonoBehaviour
{

    [SerializeField] GameObject videoCanvas;
    [SerializeField] string videoFileName;
    VideoPlayer videoPlayer;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        PlayVideo();   
        videoPlayer.loopPointReached += OnCutsceneFinished;
    }

    private void PlayVideo(){

        GameState.Current = GameMode.Cutscene;

        VideoPlayer videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer)
        {
            videoPlayer.url = System.IO.Path.Combine (Application.streamingAssetsPath, videoFileName); 
            videoPlayer.Play();
        }
    }

    void OnCutsceneFinished(VideoPlayer vp)
    {
        GameState.Current = GameMode.Exploring;
        videoCanvas.SetActive(false);
        dialogueManager.Instance.TriggerDialogue(0);
        gameObject.SetActive(false); 
    }
}
