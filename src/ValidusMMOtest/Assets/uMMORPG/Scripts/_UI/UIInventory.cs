// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;

public class UIInventory : MonoBehaviour {
    [SerializeField] KeyCode hotKey = KeyCode.I;
    [SerializeField] GameObject panel;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Transform content;
    [SerializeField] Text goldText;
    [SerializeField] UIDragAndDropable trash;
    [SerializeField] GameObject trashOverlay;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // hotkey (not while typing in chat, etc.)
        if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
            panel.SetActive(!panel.activeSelf);

        // only update the panel if it's active
        if (panel.activeSelf) {
            // instantiate/destroy enough slots
            UIUtils.BalancePrefabs(slotPrefab, player.inventory.Count, content);

            // refresh all
            for (int i = 0; i < player.inventory.Count; ++i) {
                var entry = content.GetChild(i).GetChild(0); // slot entry
                entry.name = i.ToString(); // for drag and drop
                var item = player.inventory[i];

                if (item.valid) {
                    // click event
                    int icopy = i; // needed for lambdas, otherwise i is Count
                    entry.GetComponent<Button>().onClick.SetListener(() => {
                        player.CmdUseInventoryItem(icopy);
                    });
                    
                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = true;
                    entry.GetComponent<UIDragAndDropable>().dragable = true;
                    // note: entries should be dropable at all times

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = item.image;
                    entry.GetComponent<UIShowToolTip>().text = item.Tooltip();

                    // amount overlay
                    entry.GetChild(0).gameObject.SetActive(item.amount > 1);
                    if (item.amount > 1) entry.GetComponentInChildren<Text>().text = item.amount.ToString();
                } else {
                    // remove listeners
                    entry.GetComponent<Button>().onClick.RemoveAllListeners();

                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = false;
                    entry.GetComponent<UIDragAndDropable>().dragable = false;

                    // image
                    entry.GetComponent<Image>().color = Color.clear;
                    entry.GetComponent<Image>().sprite = null;

                    // amount overlay
                    entry.GetChild(0).gameObject.SetActive(false);
                }
            }

            // gold
            goldText.text = player.gold.ToString();

            // trash (tooltip always enabled, dropable always true)
            trash.dragable = player.trash.valid;

            // set other properties
            if (player.trash.valid) {
                // image
                trash.GetComponent<Image>().color = Color.white;
                trash.GetComponent<Image>().sprite = player.trash.image;

                // amount overlay
                var amount = player.trash.amount;
                trashOverlay.gameObject.SetActive(amount > 1);
                if (amount > 1) trashOverlay.GetComponentInChildren<Text>().text = amount.ToString();
            } else {
                // image
                trash.GetComponent<Image>().color = Color.clear;
                trash.GetComponent<Image>().sprite = null;

                // amount overlay
                trashOverlay.gameObject.SetActive(false);
            }
        }
    }
}
