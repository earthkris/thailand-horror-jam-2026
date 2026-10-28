using System;
using UnityEngine;
using UnityEngine.Video;

public class VidPlayer : MonoBehaviour
{
    [SerializeField]
    private String videoFileName;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayVideo();   
    }

    private void PlayVideo(){
        VideoPlayer videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer)
        {
            videoPlayer.url = System.IO.Path.Combine (Application.streamingAssetsPath, videoFileName); 
            videoPlayer.Play();
        }
    }
}
