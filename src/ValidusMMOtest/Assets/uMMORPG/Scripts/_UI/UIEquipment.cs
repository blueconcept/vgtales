// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;

public class UIEquipment : MonoBehaviour {
    [SerializeField] KeyCode hotKey = KeyCode.E;
    [SerializeField] GameObject panel;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Transform content;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // hotkey (not while typing in chat, etc.)
        if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
            panel.SetActive(!panel.activeSelf);

        // only update the panel if it's active
        if (panel.activeSelf) {
            // instantiate/destroy enough slots
            UIUtils.BalancePrefabs(slotPrefab, player.equipment.Count, content);

            // refresh all
            for (int i = 0; i < player.equipment.Count; ++i) {
                var entry = content.GetChild(i).GetChild(0); // slot entry
                entry.name = i.ToString(); // for drag and drop
                var item = player.equipment[i];

                // set category overlay in any case. we use the last noun in the
                // category string, for example:
                //   EquipmentWeaponBow => Bow
                //   EquipmentShield => Shield
                // (disable overlay if no category, e.g. for archer shield slot)
                if (player.equipmentTypes[i] != "") {
                    entry.GetChild(0).gameObject.SetActive(true);
                    var overlay = Utils.ParseLastNoun(player.equipmentTypes[i]);
                    entry.GetComponentInChildren<Text>().text = overlay != "" ? overlay : "?";
                } else entry.GetChild(0).gameObject.SetActive(false);

                if (item.valid) {
                    // click event (done more than once but w/e)
                    entry.GetComponent<Button>().onClick.RemoveAllListeners();
                    
                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = item.valid;
                    entry.GetComponent<UIDragAndDropable>().dragable = item.valid;
                    // note: entries should be dropable at all times

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = item.image;
                    entry.GetComponent<UIShowToolTip>().text = item.Tooltip();
                } else {
                    // remove listeners
                    entry.GetComponent<Button>().onClick.RemoveAllListeners();

                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = false;
                    entry.GetComponent<UIDragAndDropable>().dragable = false;
                    // note: entries should be dropable at all times
                    
                    // image
                    entry.GetComponent<Image>().color = Color.clear;
                    entry.GetComponent<Image>().sprite = null;
                }
            }
        }
    }
}
