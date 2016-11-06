using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;

public class UINpcQuest : MonoBehaviour {
    [SerializeField] GameObject panel;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Transform content;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // npc quest
        // use collider point(s) to also work with big entities
        if (player.target != null && player.target is Npc &&
            Utils.ClosestDistance(player.collider, player.target.collider) <= player.talkRange) {
            var npc = (Npc)player.target;

            // instantiate/destroy enough slots
            var questsAvailable = npc.QuestsVisibleFor(player);
            UIUtils.BalancePrefabs(slotPrefab, questsAvailable.Count, content);

            // refresh all
            for (int i = 0; i < questsAvailable.Count; ++i) {
                var entry = content.GetChild(i).GetChild(1); // slot panel
                var description = entry.GetChild(0).GetComponent<Text>();
                var actionButton = entry.GetComponentInChildren<Button>();

                // find quest index in original npc quest list (unfiltered)
                var npcIdx = Array.FindIndex(npc.quests, q => q.name == questsAvailable[i].name);

                // find quest index in player quest list
                var idx = player.GetQuestIndexByName(npc.quests[npcIdx].name);
                if (idx != -1) {
                    // running quest: shows description with current progress
                    // instead of static one
                    var quest = player.quests[idx];
                    var gathered = quest.gatherName != "" ? player.InventoryCountAmount(quest.gatherName) : 0;
                    description.text = quest.Tooltip(gathered);                
                    actionButton.interactable = quest.IsFulfilled(gathered);
                    actionButton.GetComponentInChildren<Text>().text = "Complete";
                    actionButton.onClick.SetListener(() => {
                        player.CmdCompleteQuest(npcIdx);
                        panel.SetActive(false);
                    });
                } else {
                    // new quest
                    description.text = new Quest(npc.quests[npcIdx]).Tooltip();
                    actionButton.interactable = true;
                    actionButton.GetComponentInChildren<Text>().text = "Accept";
                    actionButton.onClick.SetListener(() => {
                        player.CmdAcceptQuest(npcIdx);
                    });
                }
            }
        } else panel.SetActive(false); // hide
    }
}
