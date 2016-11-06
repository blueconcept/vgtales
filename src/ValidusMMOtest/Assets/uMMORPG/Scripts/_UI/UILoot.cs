// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class UILoot : MonoBehaviour {
    [SerializeField] GameObject panel;
    [SerializeField] GameObject goldSlot;
    [SerializeField] Text goldText;
    [SerializeField] GameObject itemSlotPrefab;
    [SerializeField] Transform content;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // use collider point(s) to also work with big entities
        if (panel.activeSelf &&
            player.target != null &&
            player.target.hp == 0 &&
            Utils.ClosestDistance(player.collider, player.target.collider) <= player.lootRange &&
            player.target is Monster &&
            ((Monster)player.target).HasLoot()) {
            // cache monster
            var mob = (Monster)player.target;

            // gold slot
            if (mob.lootGold > 0) {
                goldSlot.SetActive(true);

                // button
                goldSlot.GetComponentInChildren<Button>().onClick.SetListener(() => { player.CmdTakeLootGold(); });

                // amount
                goldText.text = mob.lootGold.ToString();
            } else goldSlot.SetActive(false);


            // instantiate/destroy enough slots
            // (we only want to show the not-empty slots)
            var items = mob.lootItems.Where(item => item.valid).ToList();
            UIUtils.BalancePrefabs(itemSlotPrefab, items.Count, content);

            // refresh all item slots
            for (int i = 0; i < items.Count; ++i) {
                var entry = content.GetChild(i).GetChild(0).GetChild(0); // slot entry
                entry.name = i.ToString(); // for drag and drop

                if (items[i].valid) {
                    // click event (done more than once but w/e)
                    var itemIndex = mob.lootItems.FindIndex(item => item.name == items[i].name); // real item list index
                    entry.GetComponent<Button>().onClick.SetListener(() => {
                        player.CmdTakeLootItem(itemIndex);
                    });
                    
                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = items[i].valid;
                    entry.GetComponent<UIDragAndDropable>().dragable = items[i].valid;
                    // note: entries should be dropable at all times

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = items[i].image;
                    entry.GetComponent<UIShowToolTip>().text = items[i].Tooltip();

                    // name
                    entry.parent.parent.GetChild(1).GetComponent<Text>().text = items[i].name;

                    // amount overlay
                    entry.transform.GetChild(0).gameObject.SetActive(items[i].amount > 1);
                    if (items[i].amount > 1) entry.GetComponentInChildren<Text>().text = items[i].amount.ToString();
                }
            }
        } else panel.SetActive(false); // hide
    }

    public void Show() { panel.SetActive(true); }
}
