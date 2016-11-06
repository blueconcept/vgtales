using UnityEngine;
using System.Linq;

public class NpcQuestIndicator : MonoBehaviour {
    [SerializeField] Npc owner;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // does the player have a quest that can be completed at this npc?
        bool canComplete = owner.quests.Any(q => player.CanCompleteQuest(q.name));

        // does the npc have a quest that can be started by the player?
        bool canStart = owner.quests.Any(q => player.CanStartQuest(q));

        // set text (! > ? > "")
        if (canComplete)   GetComponent<TextMesh>().text = "!";
        else if (canStart) GetComponent<TextMesh>().text = "?";
        else               GetComponent<TextMesh>().text = "";
    }
}
