using UnityEngine;

public class ColorChangeScript : MonoBehaviour
{
    public GameObject targetObject; // Reference to the object to change color
    public float changeInterval = 30.0f; // Time interval for color change
    private float timeSinceLastChange = 0.0f; // Time elapsed since the last color change
    private Renderer objectRenderer; // Reference to the target object's renderer

    void Start()
    {
        // Get the Renderer component of the target object
        objectRenderer = targetObject.GetComponent<Renderer>();

        // Initialize the time since last change to a random value within the interval
        timeSinceLastChange = Random.Range(0.0f, changeInterval);
    }

    void Update()
    {
        // Update the time elapsed
        timeSinceLastChange += Time.deltaTime;

        // Check if it's time to change the color
        if (timeSinceLastChange >= changeInterval)
        {
            // Generate a random color
            ChangeColor();

            // Reset the time since last change
            timeSinceLastChange = 0.0f;
        }
    }

    public void ChangeColor()
    {
        Color randomColor = new Color(Random.value, Random.value, Random.value);

        // Change the target object's material color to the random color
        objectRenderer.material.color = randomColor;
    }
}
