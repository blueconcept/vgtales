// The Quest struct only contains the dynamic quest properties and a name, so
// that the static properties can be read from the scriptable object. The
// benefits are low bandwidth and easy Player database saving (saves always
// refer to the scriptable quest, so we can change that any time).
//
// Quests have to be structs in order to work with SyncLists.
//
// Note: the file can't be named "Quest.cs" because of the following UNET bug:
// http://forum.unity3d.com/threads/bug-syncliststruct-only-works-with-some-file-names.384582/
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct Quest {
    // name used to reference the database entry (cant save template directly
    // because synclist only support simple types)
    public string name;

    // dynamic stats
    public int killed;
    public bool completed; // after finishing it at the npc and getting rewards

    // constructors
    public Quest(QuestTemplate template) {
        name = template.name;
        killed = 0;
        completed = false;
    }

    // does the template still exist?
    public bool TemplateExists() {
        return QuestTemplate.dict.ContainsKey(name);
    }

    // database quest property access
    public int level {
       get { return QuestTemplate.dict[name].level; }
    }
    public string predecessor {
        get { return QuestTemplate.dict[name].predecessor != null ? QuestTemplate.dict[name].predecessor.name : ""; }
    }
    public int rewardGold {
       get { return QuestTemplate.dict[name].rewardGold; }
    }
    public int rewardExp {
       get { return QuestTemplate.dict[name].rewardExp; }
    }
    public string killName {
       get { return QuestTemplate.dict[name].killTarget != null ? QuestTemplate.dict[name].killTarget.name : ""; }
    }
    public int killAmount {
       get { return QuestTemplate.dict[name].killAmount; }
    }
    public string gatherName {
       get { return QuestTemplate.dict[name].gatherItem != null ? QuestTemplate.dict[name].gatherItem.name : ""; }
    }
    public int gatherAmount {
       get { return QuestTemplate.dict[name].gatherAmount; }
    }

    // fill in all variables into the tooltip
    // this saves us lots of ugly string concatenation code. we can't do it in
    // QuestTemplate because some variables can only be replaced here, hence we
    // would end up with some variables not replaced in the string when calling
    // Tooltip() from the template.
    // -> note: each tooltip can have any variables, or none if needed
    // -> example usage:
    /*
    <b>{NAME}</b>
    Description here...

    Tasks:
    * Kill {KILLNAME}: {KILLED}/{KILLAMOUNT}
    * Gather {GATHERNAME}: {GATHERED}/{GATHERAMOUNT}

    Rewards:
    * {REWARDGOLD} Gold
    * {REWARDEXP} Experience

    {STATUS}
    */
    public string Tooltip(int gathered = 0) {
        var tip = QuestTemplate.dict[name].tooltip;
        tip = tip.Replace("{NAME}", name);
        tip = tip.Replace("{KILLNAME}", killName);
        tip = tip.Replace("{KILLAMOUNT}", killAmount.ToString());
        tip = tip.Replace("{GATHERNAME}", gatherName);
        tip = tip.Replace("{GATHERAMOUNT}", gatherAmount.ToString());
        tip = tip.Replace("{REWARDGOLD}", rewardGold.ToString());
        tip = tip.Replace("{REWARDEXP}", rewardExp.ToString());
        tip = tip.Replace("{KILLED}", killed.ToString());
        tip = tip.Replace("{GATHERED}", gathered.ToString());
        tip = tip.Replace("{STATUS}", IsFulfilled(gathered) ? "<i>Completed!</i>" : "");
        return tip;
    }

    // a quest is fulfilled if all requirements were met and it can be completed
    // at the npc
    public bool IsFulfilled(int gathered) {
        return killed >= killAmount && gathered >= gatherAmount;
    }
}

public class SyncListQuest : SyncListStruct<Quest> { }
