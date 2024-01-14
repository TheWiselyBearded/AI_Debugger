using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource AudioSource;

    public void TogglePlayPause() {
        if (AudioSource.isPlaying) AudioSource.Pause();
        else if (AudioSource.clip != null && !AudioSource.isPlaying) AudioSource.UnPause();
    }
}
