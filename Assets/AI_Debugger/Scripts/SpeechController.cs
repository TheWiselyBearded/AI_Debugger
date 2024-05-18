using OpenAI;
using OpenAI.Audio;
using OpenAI.Models;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine.UI;
using Utilities.Audio;
using Utilities.Encoding.Wav;
using Utilities.Extensions;

public class SpeechController 
{
    public AudioSource audioSource;
    public GameObject audioPanel;

    public delegate void OnSpeechToText(string text);
    public static OnSpeechToText onSTT;

    public SpeechController(AudioSource audioSource, GameObject audioPanel)
    {
        this.audioSource = audioSource;
        this.audioPanel = audioPanel;
        Init();
    }

    public void Init()
    {
        OnValidate();
        GPTInterfacer.onGPTMessageReceived += GenerateSpeech;
    }

    public void OnValidate()
    {
        audioSource.Validate();
    }

    public void Destroy()
    {
        GPTInterfacer.onGPTMessageReceived -= GenerateSpeech;
    }

    /// <summary>
    /// invoked via settings toggle menu
    /// </summary>    
    public void EnableAudioInterface(bool status)
    {
        audioPanel.SetActive(status);
        //recordButton.SetActive(status);
        audioSource.enabled = status;
    }

    public async void GenerateSpeech(string text, MessageColorMode.MessageType messageType)
    {
        if (!DopeCoderController.Instance.Settings.tts) return;
        //text = text.Replace("![Image](output.jpg)", string.Empty);
        var request = new SpeechRequest(text, Model.TTS_1);        
        var (clipPath, clip) = await DopeCoderController.Instance.gptInterfacer.openAI.AudioEndpoint.CreateSpeechAsync(request, DopeCoderController.Instance.gptInterfacer.lifetimeCancellationTokenSource.Token);
        Debug.Log($"Speech request fired {clip.name}");
        audioSource.clip = clip;
        audioSource.Play();

        if (DopeCoderController.Instance.Settings.debugMode) Debug.Log(clipPath);

    }

    public void ToggleRecording()
    {
        RecordingManager.EnableDebug = DopeCoderController.Instance.Settings.debugMode;

        if (RecordingManager.IsRecording)
        {
            RecordingManager.EndRecording();
        }
        else
        {
            //inputField.interactable = false;
            RecordingManager.StartRecording<WavEncoder>(callback: ProcessRecording);
        }
    }


    private async void ProcessRecording(Tuple<string, AudioClip> recording)
    {
        var (path, clip) = recording;

        if (DopeCoderController.Instance.Settings.debugMode) Debug.Log(path);
        Debug.Log("Processing recording");

        try
        {
            DopeCoderController.Instance.uiController.recordButton.interactable = false;
            var request = new AudioTranscriptionRequest(clip, temperature: 0.1f, language: "en");
            var userInput = await DopeCoderController.Instance.gptInterfacer.openAI.AudioEndpoint.CreateTranscriptionAsync(request, DopeCoderController.Instance.gptInterfacer.lifetimeCancellationTokenSource.Token);

            if (DopeCoderController.Instance.Settings.debugMode) Debug.Log($"voice input {userInput}");
        
            DopeCoderController.Instance.uiController.inputField.text = userInput;            
            onSTT?.Invoke(userInput);
            DopeCoderController.Instance.uiController.inputField.interactable = true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            DopeCoderController.Instance.uiController.inputField.interactable = true;
        }
        finally
        {
            DopeCoderController.Instance.uiController.recordButton.interactable = true;
        }
    }

}
