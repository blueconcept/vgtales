// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class UISkills : MonoBehaviour {
    [SerializeField] KeyCode hotKey = KeyCode.R;
    [SerializeField] GameObject panel;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Transform content;
    [SerializeField] Text skillExpAvailable;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // hotkey (not while typing in chat, etc.)
        if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
            panel.SetActive(!panel.activeSelf);

        // only update the panel if it's active
        if (panel.activeSelf) {
            // instantiate/destroy enough slots
            // (we only care about non status skills)
            var skills = player.skills.Where(s => !s.category.StartsWith("Status")).ToList();
            UIUtils.BalancePrefabs(slotPrefab, skills.Count, content);

            // refresh all
            for (int i = 0; i < skills.Count; ++i) {
                var entry = content.GetChild(i).GetChild(0).GetChild(0); // slot entry
                
                // drag and drop name has to be the index in the real skill list,
                // not in the filtered list, otherwise drag and drop may fail
                var skillIdx = player.skills.FindIndex(s => s.name == skills[i].name); // real index in player.skills
                entry.name = skillIdx.ToString();

                // click event (done more than once but w/e)
                entry.GetComponent<Button>().onClick.RemoveAllListeners();
                if (skills[i].learned && skills[i].IsReady()) {
                    entry.GetComponent<Button>().onClick.SetListener(() => {
                        player.CmdUseSkill(skillIdx);
                    });
                }
                entry.GetComponent<Button>().interactable = skills[i].learned;
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = false; // in description
                entry.GetComponent<UIDragAndDropable>().dragable = skills[i].learned;
                // note: entries should be dropable at all times

                // image
                if (skills[i].learned) {
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = skills[i].image;
                }

                // description
                var descPanel = entry.transform.parent.parent.GetChild(1);
                descPanel.GetChild(0).GetComponent<Text>().text = skills[i].Tooltip(showRequirements:!skills[i].learned);

                // learn / upgrade button
                var btnLearn = descPanel.GetChild(1).GetComponent<Button>();
                // -> learnable?
                if (!skills[i].learned) {
                    btnLearn.gameObject.SetActive(true);
                    btnLearn.GetComponentInChildren<Text>().text = "Learn";
                    btnLearn.interactable = player.level >= skills[i].requiredLevel &&
                                            player.skillExp >= skills[i].requiredSkillExp;
                    btnLearn.onClick.SetListener(() => { player.CmdLearnSkill(skillIdx); });
                // -> upgradeable?
                } else if (skills[i].level < skills[i].maxLevel) {
                    btnLearn.gameObject.SetActive(true);
                    btnLearn.GetComponentInChildren<Text>().text = "Upgrade";
                    btnLearn.interactable = player.level >= skills[i].upgradeRequiredLevel &&
                                            player.skillExp >= skills[i].upgradeRequiredSkillExp;
                    btnLearn.onClick.SetListener(() => { player.CmdUpgradeSkill(skillIdx); });
                // -> otherwise no button needed
                } else btnLearn.gameObject.SetActive(false);

                // cooldown overlay
                var cd = skills[i].CooldownRemaining();
                if (skills[i].learned && cd > 0) {
                    entry.transform.GetChild(0).gameObject.SetActive(true);
                    entry.GetComponentInChildren<Text>().text = cd.ToString("F0");
                } else entry.transform.GetChild(0).gameObject.SetActive(false);
            }

            // skill exp available
            skillExpAvailable.text = player.skillExp.ToString();
        }
    }
}
