using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FoodMenuUI : MonoBehaviour
{
    [Header("UI Objects")]
    public GameObject foodMenuPanel;
    public Button btnApple;
    public Button btnHam;
    public Button btnChicken;
    public Button btnConfirm;

    [Header("References")]
    public SteakThrower steakThrower;

    private int selectedIndex = -1;

    void Start()
    {
        // Hide menu initially
        foodMenuPanel.SetActive(false);

        // Assign button actions
        btnApple.onClick.AddListener(() => SelectFood(0));
        btnHam.onClick.AddListener(() => SelectFood(1));
        btnChicken.onClick.AddListener(() => SelectFood(2));
        btnConfirm.onClick.AddListener(ConfirmSelection);
    }

    public void OpenMenu()
    {
        foodMenuPanel.SetActive(true);
        selectedIndex = -1;
        ClearButtonHighlights();
    }

    public void CloseMenu()
    {
        foodMenuPanel.SetActive(false);
    }

    private void SelectFood(int index)
    {
        selectedIndex = index;
        HighlightButton(index);
    }

    private void ConfirmSelection()
    {
        if (selectedIndex == -1)
        {
            Debug.LogWarning("No food selected!");
            return;
        }

        steakThrower.SelectFood(selectedIndex);
        CloseMenu();

        Debug.Log($"Confirmed Food: {steakThrower.foodItems[selectedIndex].name}");
    }

    private void HighlightButton(int index)
    {
        ClearButtonHighlights();

        Color highlight = new Color(1f, 1f, 0.5f);

        if (index == 0) btnApple.image.color = highlight;
        if (index == 1) btnHam.image.color = highlight;
        if (index == 2) btnChicken.image.color = highlight;
    }

    private void ClearButtonHighlights()
    {
        btnApple.image.color = Color.white;
        btnHam.image.color = Color.white;
        btnChicken.image.color = Color.white;
    }
}
