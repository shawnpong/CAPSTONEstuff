using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LogScrollController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TextMeshProUGUI logText;

    [Header("Behavior")]
    [Tooltip("Auto-scrolls to bottom only when the user is already at bottom.")]
    [SerializeField] private bool autoScrollWhenAtBottom = true;

    private bool initialized = false;
    private const float BottomThreshold = 0.001f;

    private void Reset()
    {
        if (!scrollRect) scrollRect = GetComponent<ScrollRect>();
        if (!logText && scrollRect)
            logText = scrollRect.content.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Awake()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnUserScrolled);
    }

    private void Start()
    {
        if (scrollRect)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
        initialized = true;
    }

    public void NotifyLogsAppended()
    {
        if (!initialized || !scrollRect || !logText) return;

        Canvas.ForceUpdateCanvases();

        if (autoScrollWhenAtBottom)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void OnUserScrolled(Vector2 _)
    {
        if (!scrollRect) return;

        bool atBottom = scrollRect.verticalNormalizedPosition <= BottomThreshold;

        // If user scrolls up → disable auto-scroll
        if (!atBottom && autoScrollWhenAtBottom)
            autoScrollWhenAtBottom = false;
        // If user scrolls back to bottom → re-enable
        else if (atBottom && !autoScrollWhenAtBottom)
            autoScrollWhenAtBottom = true;
    }
}
