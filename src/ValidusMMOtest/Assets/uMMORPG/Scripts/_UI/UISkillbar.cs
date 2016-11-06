using UnityEngine;
using UnityEngine.UI;

public class UISkillbar : MonoBehaviour {
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Transform content;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // instantiate/destroy enough slots
        UIUtils.BalancePrefabs(slotPrefab, player.skillbar.Length, content);

        // refresh all
        for (int i = 0; i < player.skillbar.Length; ++i) {
            var entry = content.GetChild(i).GetChild(0); // slot entry
            entry.name = i.ToString(); // for drag and drop

            // overlay hotkey (without 'Alpha' etc.)
            var pretty = player.skillbarHotkeys[i].ToString().Replace("Alpha", "");
            entry.GetChild(1).GetComponentInChildren<Text>().text = pretty;

            // skill, inventory item or equipment item?
            var skillIndex = player.GetSkillIndexByName(player.skillbar[i]);
            var invIndex = player.GetInventoryIndexByName(player.skillbar[i]);
            var equipIndex = player.GetEquipmentIndexByName(player.skillbar[i]);
            if (skillIndex != -1) {
                var skill = player.skills[skillIndex];

                // click event (done more than once but w/e)
                entry.GetComponent<Button>().onClick.SetListener(() => {
                    player.CmdUseSkill(skillIndex);
                });
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = true;
                entry.GetComponent<UIDragAndDropable>().dragable = true;
                // note: entries should be dropable at all times

                // image
                entry.GetComponent<Image>().color = Color.white;
                entry.GetComponent<Image>().sprite = player.skills[skillIndex].image;
                entry.GetComponent<UIShowToolTip>().text = player.skills[skillIndex].Tooltip();

                // overlay cooldown
                var cd = player.skills[skillIndex].CooldownRemaining();
                entry.GetChild(0).gameObject.SetActive(cd > 0);
                if (cd > 1) entry.GetChild(0).GetComponentInChildren<Text>().text = cd.ToString("F0");

                // hotkey pressed and not typing in any input right now?
                if (skill.learned && skill.IsReady() &&
                    Input.GetKeyDown(player.skillbarHotkeys[i]) &&
                    !UIUtils.AnyInputActive()) {
                    player.CmdUseSkill(skillIndex);
                }
            } else if (invIndex != -1) {

                // click event (done more than once but w/e)
                entry.GetComponent<Button>().onClick.SetListener(() => {
                    player.CmdUseInventoryItem(invIndex);
                });
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = true;
                entry.GetComponent<UIDragAndDropable>().dragable = true;
                // note: entries should be dropable at all times

                // image
                entry.GetComponent<Image>().color = Color.white;
                entry.GetComponent<Image>().sprite = player.inventory[invIndex].image;
                entry.GetComponent<UIShowToolTip>().text = player.inventory[invIndex].Tooltip();

                // overlay amount
                var amount = player.inventory[invIndex].amount;
                entry.GetChild(0).gameObject.SetActive(amount > 1);
                if (amount > 1) entry.GetChild(0).GetComponentInChildren<Text>().text = amount.ToString();
                
                // hotkey pressed and not typing in any input right now?
                if (Input.GetKeyDown(player.skillbarHotkeys[i]) && !UIUtils.AnyInputActive())
                    player.CmdUseInventoryItem(invIndex);
            } else if (equipIndex != -1) {

                // click event (done more than once but w/e)
                entry.GetComponent<Button>().onClick.RemoveAllListeners();
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = true;
                entry.GetComponent<UIDragAndDropable>().dragable = true;
                // note: entries should be dropable at all times

                // image
                entry.GetComponent<Image>().color = Color.white;
                entry.GetComponent<Image>().sprite = player.equipment[equipIndex].image;
                entry.GetComponent<UIShowToolTip>().text = player.equipment[equipIndex].Tooltip();

                // overlay
                entry.GetChild(0).gameObject.SetActive(false);
            } else {
                // outdated reference. clear it.
                player.skillbar[i] = "";

                // remove listeners                    
                entry.GetComponent<Button>().onClick.RemoveAllListeners();

                // set state
                entry.GetComponent<UIShowToolTip>().enabled = false;
                entry.GetComponent<UIDragAndDropable>().dragable = false;

                // image
                entry.GetComponent<Image>().color = Color.clear;
                entry.GetComponent<Image>().sprite = null;

                // overlay
                entry.GetChild(0).gameObject.SetActive(false);
            }       
        }
    }
}
