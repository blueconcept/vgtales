using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class UIShortcuts : MonoBehaviour {
    [SerializeField] Button inventoryButton;
    [SerializeField] GameObject inventoryPanel;

    [SerializeField] Button equipmentButton;
    [SerializeField] GameObject equipmentPanel;

    [SerializeField] Button skillsButton;
    [SerializeField] GameObject skillsPanel;

    [SerializeField] Button characterInfoButton;
    [SerializeField] GameObject characterInfoPanel;

    [SerializeField] Button questsButton;
    [SerializeField] GameObject questsPanel;
    
    [SerializeField] Button quitButton;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        inventoryButton.onClick.SetListener(() => {
            inventoryPanel.SetActive(!inventoryPanel.activeSelf);
        });

        equipmentButton.onClick.SetListener(() => {
            equipmentPanel.SetActive(!equipmentPanel.activeSelf);
        });

        skillsButton.onClick.SetListener(() => {
            skillsPanel.SetActive(!skillsPanel.activeSelf);
        });

        characterInfoButton.onClick.SetListener(() => {
            characterInfoPanel.SetActive(!characterInfoPanel.activeSelf);
        });

        questsButton.onClick.SetListener(() => {
            questsPanel.SetActive(!questsPanel.activeSelf);
        });

        quitButton.onClick.SetListener(() => {
            Application.Quit();
        });
    }
}
