using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour {
    public AudioSource AudioSource;

    // Define a static event
    public static event System.Action onFinishedTalking;

    private Coroutine checkAudioCoroutine;

    public void TogglePlayPause() {
        if (AudioSource.isPlaying) {
            AudioSource.Pause();
            StopCheckingAudio();
            onFinishedTalking?.Invoke(); // Invoke event when paused
        } else if (AudioSource.clip != null && !AudioSource.isPlaying) {
            AudioSource.UnPause();
            StartCheckingAudio();
        }
    }

    public void StopAudio() {
        if (AudioSource.isPlaying || AudioSource.time > 0) {
            AudioSource.Stop();
            StopCheckingAudio();
            onFinishedTalking?.Invoke(); // Invoke event when stopped
        }
    }

    private void StartCheckingAudio() {
        if (checkAudioCoroutine != null) {
            StopCoroutine(checkAudioCoroutine);
        }
        checkAudioCoroutine = StartCoroutine(CheckIfAudioFinished());
    }

    private void StopCheckingAudio() {
        if (checkAudioCoroutine != null) {
            StopCoroutine(checkAudioCoroutine);
            checkAudioCoroutine = null;
        }
    }

    private IEnumerator CheckIfAudioFinished() {
        while (AudioSource.isPlaying) {
            yield return null; // Wait until the next frame
        }

        // Audio has finished playing
        onFinishedTalking?.Invoke();
        checkAudioCoroutine = null;
    }
}
