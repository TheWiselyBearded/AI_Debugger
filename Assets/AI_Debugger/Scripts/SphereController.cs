using System.Collections;
using UnityEngine;

public class SphereController : MonoBehaviour
{
    public enum SphereMode
    {
        Idle,
        Talking,
        Listening
    }

    [System.Serializable]
    public class ModeSettings
    {
        public Color color;
        public float colorTransitionDuration;
        public float scaleChangeDuration;
        public float minScale;
        public float maxScale;
        public float oscillationSpeed;
    }

    public SphereMode mode = SphereMode.Idle;
    private SphereMode currentMode;
    public ModeSettings idleSettings;
    public ModeSettings talkingSettings;
    public ModeSettings listeningSettings;

    private Renderer rend;
    private Color targetColor;
    private ModeSettings currentSettings;

    private Vector3 initialScale;
    private Vector3 targetScale;
    private Coroutine colorTransitionCoroutine;
    private Coroutine scaleChangeCoroutine;
    private bool isScaling = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        initialScale = transform.localScale;
        SetMode(mode);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetMode(mode);
        }

        switch (currentMode)
        {
            case SphereMode.Idle:
                break;
            case SphereMode.Talking:
                if (!isScaling)
                    UpdateTalkingState();
                break;
            case SphereMode.Listening:
                if (!isScaling)
                    UpdateListeningState();
                break;
        }
    }

    void UpdateTalkingState()
    {
        // Execute scaling operation
        ScaleOscillation(talkingSettings);
    }

    void UpdateListeningState()
    {
        // Execute scaling operation
        ScaleOscillation(listeningSettings);
    }

    void ScaleOscillation(ModeSettings settings)
    {
        Debug.Log($"Attempting to scale oscillation, state {isScaling}");
        float lerpParameter = Mathf.PingPong(Time.time * settings.oscillationSpeed, 1f);
        float scaleFactor = Mathf.Lerp(settings.minScale, settings.maxScale, lerpParameter);
        transform.localScale = initialScale * scaleFactor;
    }

    public void SetMode(SphereMode newMode)
    {
        currentMode = mode = newMode;
        switch (mode)
        {
            case SphereMode.Idle:
                ApplyModeSettings(idleSettings);
                break;
            case SphereMode.Talking:
                ApplyModeSettings(talkingSettings);
                break;
            case SphereMode.Listening:
                ApplyModeSettings(listeningSettings);
                break;
        }
    }

    void ApplyModeSettings(ModeSettings settings)
    {
        currentSettings = settings;
        targetColor = settings.color;

        // Start color transition coroutine
        if (colorTransitionCoroutine != null)
            StopCoroutine(colorTransitionCoroutine);
        colorTransitionCoroutine = StartCoroutine(ColorTransitionCoroutine(targetColor, settings.colorTransitionDuration));

        StartScaling();
    }

    IEnumerator ColorTransitionCoroutine(Color targetColor, float duration)
    {
        Color initialColor = rend.material.color;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float lerpValue = timer / duration;
            rend.material.color = Color.Lerp(initialColor, targetColor, lerpValue);
            yield return null;
        }

        rend.material.color = targetColor;
    }

    void StartScaling()
    {
        // Set target scale
        targetScale = Vector3.one * currentSettings.minScale;
        // Start scaling coroutine
        if (scaleChangeCoroutine != null)
            StopCoroutine(scaleChangeCoroutine);
        isScaling = true;
        scaleChangeCoroutine = StartCoroutine(ScaleChangeCoroutine(currentSettings.minScale, currentSettings.scaleChangeDuration));
    }

    IEnumerator ScaleChangeCoroutine(float targetScale, float duration)
    {
        Vector3 initialScale = transform.localScale;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float lerpValue = timer / duration;
            transform.localScale = Vector3.Lerp(initialScale, this.targetScale, lerpValue);
            yield return null;
        }

        transform.localScale = this.targetScale;
        isScaling = false;
    }
}
