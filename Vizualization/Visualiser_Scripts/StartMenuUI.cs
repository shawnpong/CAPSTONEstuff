using UnityEngine;

public class StartMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject startMenuCanvas;
    [SerializeField] private GameObject gameElementsRoot;

    void Start()
    {
        if (startMenuCanvas) startMenuCanvas.SetActive(true);
        if (gameElementsRoot) gameElementsRoot.SetActive(false);
    }

    public void OnPlayButtonClicked()
    {
        if (startMenuCanvas) startMenuCanvas.SetActive(false);
        if (gameElementsRoot) gameElementsRoot.SetActive(true);
    }
}
