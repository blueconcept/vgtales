// The Npc class is rather simple. It contains state Update functions that do
// nothing at the moment, because Npcs are supposed to stand around all day.
//
// Npcs first show the welcome text and then have options for item trading and
// quests.
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;

public class Npc : Entity {
    [Header("Health")]
    [SerializeField] int _hpMax = 1;
    public override int hpMax { get { return _hpMax; } }

    [Header("Mana")]
    [SerializeField] int _mpMax = 1;
    public override int mpMax { get { return _mpMax; } }

    [Header("Welcome Text")]
    [TextArea(1, 30)] public string welcome;

    [Header("Items for Sale")]
    public ItemTemplate[] saleItems;

    [Header("Quests")]
    public QuestTemplate[] quests;

    [Header("Teleportation")]
    public Transform teleportTo;

    // other properties
    public override int damage { get { return 0; } }
    public override int defense { get { return 0; } }
    
    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartServer() {
        base.OnStartServer();

        // all npcs should spawn with full health and mana
        hp = hpMax;
        mp = mpMax;
    }

    // finite state machine states /////////////////////////////////////////////
    [Server] protected override string UpdateServer() { return state; }
    [Client] protected override void UpdateClient() {}

    // skills //////////////////////////////////////////////////////////////////
    public override bool HasCastWeapon() { return true; }
    public override bool CanAttackType(System.Type t) { return false; }

    // quests //////////////////////////////////////////////////////////////////
    // helper function to filter the quests that are shown for a player
    // -> all quests that:
    //    - can be started by the player
    //    - or were already started but aren't completed yet
    public List<QuestTemplate> QuestsVisibleFor(Player player) {
        return quests.Where(q => player.CanStartQuest(q) ||
                                 player.HasActiveQuest(q.name)).ToList();
    }
}
