using UnityEngine;
using TMPro;

public class FloatUpText : MonoBehaviour
{
    public float floatSpeed = 50f;
    public float fadeDuration = 1.5f;
    private TextMeshProUGUI tmpText;
    private Color originalColor;
    private float timer;

    void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        originalColor = tmpText.color;
    }

    void Update()
    {
        // Move upward in local space (UI coordinates)
        transform.Translate(Vector3.up * floatSpeed * Time.deltaTime);

        // Fade out
        timer += Time.deltaTime;
        float alpha = Mathf.Lerp(originalColor.a, 0, timer / fadeDuration);
        tmpText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

        if (timer >= fadeDuration)
        {
            gameObject.SetActive(false); // return to pool
        }
    }

    public void Show(string message, Transform parentCanvas)
    {
        transform.SetParent(parentCanvas, false); // attach to canvas
        transform.localPosition = Vector3.zero;   // center of canvas
        tmpText.text = message;
        tmpText.color = originalColor;
        timer = 0f;
        gameObject.SetActive(true);
    }
}
