using Horde;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HordeSplitManager : MonoBehaviour
    {
        public TMP_Text selectedAmountText;
        public TMP_Text maxAmountText;
        public Slider slider;
        public Button button;
        private float _splitPercentage;

        private int SplitAmount =>
            (int)(_splitPercentage * InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats);
        private int splitAmount;
        private int maxPop;

        private void Update()
        {
            if (!InputHandler.Instance.LocalPlayer || !InputHandler.Instance.LocalPlayer!.selectedHorde) return;

            maxPop = InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;

            if (maxPop < 2 * PopulationController.INITIAL_POPULATION)
            {
                selectedAmountText.text = "Too small to split";
                maxAmountText.text = "";
                button.interactable = false;
                return;
            }

            button.interactable = true;

            selectedAmountText.text = "Horde 1: " + $"{splitAmount}";
            maxAmountText.text = "Horde 2: " + $"{maxPop - splitAmount}";

            // Both hordes must stay above initial population!
            slider.minValue = PopulationController.INITIAL_POPULATION;
            slider.maxValue = (maxPop - PopulationController.INITIAL_POPULATION);
        }

        private void OnEnable()
        {
            splitAmount = maxPop / 2;
            slider.value = splitAmount;
        }

        public void SplitHorde()
        {
            var horde = InputHandler.Instance.LocalPlayer?.selectedHorde;
            horde?.Player.SplitHorde(horde, _splitPercentage);
            gameObject.SetActive(false);
        }

        public void SetHordeSplitNumber()
        {
            splitAmount = (int)slider.value;
        }
    }
}