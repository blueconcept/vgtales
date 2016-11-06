// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;

public class UIPlayerTrading : MonoBehaviour {
    [SerializeField] GameObject panel;

    [SerializeField] GameObject otherSlotPrefab;
    [SerializeField] Transform otherContent;
    [SerializeField] Text otherStatus;
    [SerializeField] InputField otherGold;

    [SerializeField] GameObject mySlotPrefab;
    [SerializeField] Transform myContent;
    [SerializeField] Text myStatus;
    [SerializeField] InputField myGold;

    [SerializeField] Button lockButton;
    [SerializeField] Button acceptButton;
    [SerializeField] Button cancelButton;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // only if trading, otherwise set inactive
        if (player.state == "TRADING" && player.target != null && player.target is Player) {
            var other = (Player)player.target;

            panel.SetActive(true);

            // OTHER
            // status text
            if (other.tradeOfferAccepted) otherStatus.text = "[ACCEPTED]";
            else if (other.tradeOfferLocked) otherStatus.text = "[LOCKED]";
            else otherStatus.text = "";
            // gold input
            otherGold.text = other.tradeOfferGold.ToString();
            // items
            UIUtils.BalancePrefabs(otherSlotPrefab, other.tradeOfferItems.Count, otherContent);
            for (int i = 0; i < other.tradeOfferItems.Count; ++i) {
                var entry = otherContent.GetChild(i).GetChild(0); // slot entry
                entry.name = i.ToString(); // for drag and drop
                var idx = other.tradeOfferItems[i]; // inventory index

                if (0 <= idx && idx < other.inventory.Count && other.inventory[idx].valid) {
                    var item = other.inventory[idx];

                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = true;

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = item.image;
                    entry.GetComponent<UIShowToolTip>().text = item.Tooltip();

                    // amount overlay
                    entry.transform.GetChild(0).gameObject.SetActive(item.amount > 1);
                    if (item.amount > 1) entry.GetComponentInChildren<Text>().text = item.amount.ToString();
                } else {
                    // remove listeners
                    entry.GetComponent<Button>().onClick.RemoveAllListeners();

                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = false;

                    // image
                    entry.GetComponent<Image>().color = Color.clear;
                    entry.GetComponent<Image>().sprite = null;

                    // amount overlay
                    entry.transform.GetChild(0).gameObject.SetActive(false);
                }
            }

            // SELF
            // status text
            if (player.tradeOfferAccepted) myStatus.text = "[ACCEPTED]";
            else if (player.tradeOfferLocked) myStatus.text = "[LOCKED]";
            else myStatus.text = "";
            // gold input
            if (player.tradeOfferLocked) {
                myGold.interactable = false;
                myGold.text = player.tradeOfferGold.ToString();
            } else {
                myGold.interactable = true;
                myGold.onValueChanged.SetListener((val) => {
                    var n = Utils.ClampLong(val.ToLong(), 0, player.gold);
                    myGold.text = n.ToString();
                    player.CmdTradeOfferGold(n);
                });
            }
            // items
            UIUtils.BalancePrefabs(mySlotPrefab, player.tradeOfferItems.Count, myContent);
            for (int i = 0; i < player.tradeOfferItems.Count; ++i) {
                var entry = myContent.GetChild(i).GetChild(0); // slot entry
                entry.name = i.ToString(); // for drag and drop
                var idx = player.tradeOfferItems[i]; // inventory index

                if (0 <= idx && idx < player.inventory.Count && player.inventory[idx].valid) {
                    var item = player.inventory[idx];

                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = true;
                    entry.GetComponent<UIDragAndDropable>().dragable = !player.tradeOfferLocked;
                    // note: entries should be dropable at all times

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = item.image;
                    entry.GetComponent<UIShowToolTip>().text = item.Tooltip();

                    // amount overlay
                    entry.transform.GetChild(0).gameObject.SetActive(item.amount > 1);
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
                    entry.transform.GetChild(0).gameObject.SetActive(false);
                }
            }

            // button lock
            lockButton.interactable = !player.tradeOfferLocked;
            lockButton.onClick.SetListener(() => {
                player.CmdTradeOfferLock();
            });
            
            // button accept (only if both have locked the trade and if not
            // accepted yet)
            acceptButton.interactable = player.tradeOfferLocked && other.tradeOfferLocked && !player.tradeOfferAccepted;
            acceptButton.onClick.SetListener(() => {
                player.CmdTradeOfferAccept();
            });

            // button cancel
            cancelButton.onClick.SetListener(() => {
                player.CmdTradeCancel();
            });
        } else {
            panel.SetActive(false);
            myGold.text = "0"; // reset
        }        
    }
}
