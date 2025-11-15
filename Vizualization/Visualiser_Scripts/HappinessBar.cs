using UnityEngine;
using UnityEngine.UI;

public class HappinessBar : MonoBehaviour
{

    public Slider slider;

    public void SetMaxHappiness(int happiness)
    {
        slider.maxValue = happiness;
        slider.value = happiness;

    }

    public void SetHappiness(int happiness)
    {
        slider.value = happiness;
    }

}
