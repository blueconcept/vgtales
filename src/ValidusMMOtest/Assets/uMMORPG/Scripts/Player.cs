// All player logic was put into this class. We could also split it into several
// smaller components, but this would result in many GetComponent calls and a
// more complex syntax.
//
// The default Player class takes care of the basic player logic like the state
// machine and some properties like damage and defense.
//
// The Player class stores the maximum experience for each level in a simple
// array. So the maximum experience for level 1 can be found in expMax[0] and
// the maximum experience for level 2 can be found in expMax[1] and so on. The
// player's health and mana are also level dependent in most MMORPGs, hence why
// there are hpMax and mpMax arrays too. We can find out a players's max health
// in level 1 by using hpMax[0] and so on.
//
// The class also takes care of selection handling, which detects 3D world
// clicks and then targets/navigates somewhere/interacts with someone.
//
// Animations are not handled by the NetworkAnimator because it's still very
// buggy and because it can't really react to movement stops fast enough, which
// results in moonwalking. Not synchronizing animations over the network will
// also save us bandwidth. 
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerChat))]
[RequireComponent(typeof(NetworkName))]
public class Player : Entity {
    // some properties have to be stored for saving
    [HideInInspector] public string account = "";
    [HideInInspector] public string className = "";

    // level based stats
    [System.Serializable]
    public struct PlayerLevel {
        public int hpMax;
        public int mpMax;
        public long expMax;
        public int baseDamage;
        public int baseDefense;
        public PlayerLevel(int _hpMax, int _mpMax, long _expMax, int _baseDamage, int _baseDefense) {
            hpMax = _hpMax;
            mpMax = _mpMax;
            expMax = _expMax;
            baseDamage = _baseDamage;
            baseDefense = _baseDefense;
        }
    }
    [Header("Level based Stats")]
    public PlayerLevel[] levels = new PlayerLevel[]{new PlayerLevel(100, 100, 10, 1, 1)}; // default

    // health
    public override int hpMax {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipHpBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsHpMax).Sum();

            // calculate strength bonus (1 strength means 1% of hpMax bonus)
            var attrBonus = Convert.ToInt32(levels[level-1].hpMax * (strength * 0.01f));

            // return base + attribute + equip + buffs
            return levels[level-1].hpMax + equipBonus + buffBonus + attrBonus;
        }
    }
    
    // mana
    public override int mpMax {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipMpBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsMpMax).Sum();

            // calculate intelligence bonus (1 intelligence means 1% of hpMax bonus)
            var attrBonus = Convert.ToInt32(levels[level-1].mpMax * (intelligence * 0.01f));
            
            // return base + attribute + equip + buffs
            return levels[level-1].mpMax + equipBonus + buffBonus + attrBonus;
        }
    }

    // damage
    public int baseDamage { get { return levels[level-1].baseDamage; } }
    public override int damage {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipDamageBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsDamage).Sum();
            
            // return base + equip + buffs
            return baseDamage + equipBonus + buffBonus;
        }
    }

    // defense
    public int baseDefense { get { return levels[level-1].baseDefense; } }
    public override int defense {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipDefenseBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsDefense).Sum();
            
            // return base + equip + buffs
            return baseDefense + equipBonus + buffBonus;
        }
    }

    [Header("Attributes")]
    [SyncVar, SerializeField] public int strength = 0;
    [SyncVar, SerializeField] public int intelligence = 0;

    [Header("Experience")] // note: int is not enough (can have > 2 mil. easily)
    [SyncVar, SerializeField] long _exp = 0;
    public long exp {
        get { return _exp; }
        set {
            if (value <= exp) {
                // decrease
                _exp = Utils.MaxLong(value, 0);
            } else {
                // increase with level ups
                // set the new value (which might be more than expMax)
                _exp = value;

                // now see if we leveled up (possibly more than once too)
                // (can't level up if already max level)
                while (_exp >= expMax && level < levels.Length) {
                    // subtract current level's required exp, then level up
                    _exp -= expMax;
                    ++level;
                }

                // set to expMax if there is still too much exp remaining
                if (_exp > expMax) _exp = expMax;
            }
        }
    }
    public long expMax { get { return levels[level-1].expMax; } }

    [Header("Skill Experience")]
    [SyncVar] public long skillExp = 0;
    
    [Header("Indicator")]
    [SerializeField] GameObject indicatorPrefab;
    GameObject indicator;

    [Header("Inventory")]
    public int inventorySize = 30;
    public SyncListItem inventory = new SyncListItem();
    public ItemTemplate[] defaultItems;

    [Header("Trash")]
    [SyncVar] public Item trash = new Item();

    [Header("Gold")] // note: int is not enough (can have > 2 mil. easily)
    [SerializeField, SyncVar] long _gold = 0;
    public long gold { get { return _gold; } set { _gold = Utils.MaxLong(value, 0); } }

    [Header("Equipment")]
    public string[] equipmentTypes = new string[]{"EquipmentWeapon", "EquipmentHead", "EquipmentChest", "EquipmentLegs", "EquipmentShield", "EquipmentShoulders", "EquipmentHands", "EquipmentFeet"};
    public SyncListItem equipment = new SyncListItem();
    public List<ItemTemplate> defaultEquipment;

    [Header("Skillbar")]
    public KeyCode[] skillbarHotkeys = new KeyCode[] {KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0};
    public string[] skillbar = new string[] {"", "", "", "", "", "", "", "", "", ""};

    [Header("Loot")]
    public float lootRange = 4f;

    
    [Header("Quests")] // contains active and completed quests (=all)
    public int questLimit = 10;
    public SyncListQuest quests = new SyncListQuest();

    [Header("Interaction")]
    public float talkRange = 4f;

    [Header("Trading")]
    [SyncVar, HideInInspector] public string tradeRequestFrom = "";
    [SyncVar, HideInInspector] public bool tradeOfferLocked = false;
    [SyncVar, HideInInspector] public bool tradeOfferAccepted = false;
    [SyncVar, HideInInspector] public long tradeOfferGold = 0;
    public SyncListInt tradeOfferItems = new SyncListInt(); // inventory indices

    [Header("Death")]
    [SerializeField] float deathExpLossPercent = 0.05f;

    // the next skill to be set if we try to set it while casting
    int skillNext = -1;

    // the next target to be set if we try to set it while casting
    Entity targetNext = null;

    // networkbehaviour ////////////////////////////////////////////////////////
    protected override void Awake() {
        // cache base components
        base.Awake();
    }

    public override void OnStartLocalPlayer() {
        // setup camera targets
        Camera.main.GetComponent<CameraMMO>().target = transform;
        GameObject.FindWithTag("MinimapCamera").GetComponent<CopyPosition>().target = transform;

        // load skillbar after player data was loaded
        print("loading skillbar for " + name);
        if (isLocalPlayer) LoadSkillbar();
    }

    public override void OnStartServer() {
        base.OnStartServer();
        // initialize trade item indices
        for (int i = 0; i < 6; ++i) tradeOfferItems.Add(-1);
    }

    void Start() {
        // setup synclist callbacks
        // note: buggy in 5.3.4. make sure to use Unity 5.3.5!
        // http://forum.unity3d.com/threads/bug-old-synclist-callback-bug-was-reintroduced-to-5-3-3f1.388637/
        equipment.Callback += OnEquipmentChanged;

        // refresh all locations once (on synclist changed won't be called for
        // initial lists)
        for (int i = 0; i < equipment.Count; ++i)
            RefreshLocations(equipmentTypes[i], equipment[i]);
    }

    [ClientCallback]
    void LateUpdate() {
        // pass parameters to animation state machine
        // note: 'Speed' is the ONLY reliable parameter for movement animations.
        //       'RemainingDistance' has nothing to do with animations, since it
        //       is often tiny like 0.1 when following a target with full speed.
        GetComponent<Animator>().SetInteger("Hp", hp);
        GetComponent<Animator>().SetFloat("Speed", agent.velocity.magnitude);
        GetComponent<Animator>().SetInteger("skillCur", skillCur);

#if UNITY_5_3_2 || UNITY_5_4_0
        // we have to update 3D equipment models all the time because of bug
        // #774970 that was introduced for the third time again by UNET devs.
        // (https://issuetracker.unity3d.com/issues/synclist-callback-called-by-synclist-value-change-still-has-old-value-inside-callback-function)
        // 5.2.4: fine
        // 5.3.2: bug
        // 5.4.0: bug
        // 5.4.1p1: aparrently fixed
        // 5.5.0: fine
        // we will use a callback again as soon as the bug was fixed by Unity.
        // note: this has to happen on server too, to know the equipped weapon
        for (int i = 0; i < equipment.Count; ++i)
            RefreshLocations(equipmentTypes[i], equipment[i]);
#endif
    }

    void OnDestroy() {
        // note: this function isn't called if it has a [ClientCallback] tag,
        // so let's use isLocalPlayer etc.
        // note: trying to do this in OnNetworkDestroy doesn't work well
        if (isLocalPlayer) {
            Destroy(indicator);
            SaveSkillbar();
        }
    }

    // finite state machine events - status based //////////////////////////////
    // status based events
    bool EventDied() {
        return hp == 0;
    }

    bool EventTargetDisappeared() {
        return target == null;
    }

    bool EventTargetDied() {
        return target != null && target.hp == 0;
    }
    
    bool EventSkillRequest() {
        return 0 <= skillCur && skillCur < skills.Count;        
    }
    
    bool EventSkillFinished() {
        return 0 <= skillCur && skillCur < skills.Count &&
               skills[skillCur].CastTimeRemaining() == 0f;        
    }

    bool EventMoveEnd() {
        return state == "MOVING" && !IsMoving();
    }

    bool EventTradeStarted() {
        // did someone request a trade? and did we request a trade with him too?
        var p = FindPlayerFromTradeInvitation();
        return p != null && p.tradeRequestFrom == name;
    }

    bool EventTradeDone() {
        // trade canceled or finished?
        return state == "TRADING" && tradeRequestFrom == "";
    }

    // finite state machine events - command based /////////////////////////////
    // client calls command, command sets a flag, event reads and resets it
    // => we use a set so that we don't get ultra long queues etc.
    // => we use set.Return to read and clear values
    HashSet<string> cmdEvents = new HashSet<String>();

    [Command] public void CmdRespawn() { cmdEvents.Add("Respawn"); }
    bool EventRespawn() { return cmdEvents.Remove("Respawn"); }
    
    [Command] public void CmdCancelAction() { cmdEvents.Add("CancelAction"); }
    bool EventCancelAction() { return cmdEvents.Remove("CancelAction"); }

    Vector3 navigatePos = Vector3.zero;
    float navigateStop = 0;
    [Command] void CmdNavigateTo(Vector3 pos, float stoppingDistance) {
        navigatePos = pos; navigateStop = stoppingDistance;
        cmdEvents.Add("NavigateTo");
    }
    bool EventNavigateTo() { return cmdEvents.Remove("NavigateTo"); }

    // finite state machine - server ///////////////////////////////////////////
    [Server]
    string UpdateServer_IDLE() {        
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died.
            OnDeath();
            skillCur = skillNext = -1; // in case we died while trying to cast
            return "DEAD";
        }
        if (EventCancelAction()) {
            // the only thing that we can cancel is the target
            target = null;
            return "IDLE";
        }
        if (EventTradeStarted()) {
            // cancel casting (if any), set target, go to trading
            skillCur = skillNext = -1; // just in case
            target = FindPlayerFromTradeInvitation();
            return "TRADING";
        }
        if (EventNavigateTo()) {
            // cancel casting (if any) and start moving
            skillCur = skillNext = -1;
            // move
            agent.stoppingDistance = navigateStop;
            agent.destination = navigatePos;
            return "MOVING";
        }
        if (EventSkillRequest()) {
            // user wants to cast a skill.            
            // check self (alive, mana, weapon etc.) and target
            var skill = skills[skillCur];
            targetNext = target; // return to this one after any corrections by CastCheckTarget
            if (CastCheckSelf(skill) && CastCheckTarget(skill)) {
                // check distance between self and target
                if (CastCheckDistance(skill)) {
                    // start casting and set the casting end time
                    skill.castTimeEnd = Time.time + skill.castTime;
                    skills[skillCur] = skill;
                    return "CASTING";
                } else {
                    // move to the target first
                    // (use collider point(s) to also work with big entities)
                    agent.stoppingDistance = skill.castRange;
                    agent.destination = target.collider.ClosestPointOnBounds(transform.position);
                    return "MOVING";
                }
            } else {
                // checks failed. stop trying to cast.
                skillCur = skillNext = -1;
                return "IDLE";
            }
        }
        if (EventSkillFinished()) {} // don't care
        if (EventMoveEnd()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care

        return "IDLE"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_MOVING() {        
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died.
            OnDeath();
            skillCur = skillNext = -1; // in case we died while trying to cast
            return "DEAD";
        }
        if (EventMoveEnd()) {
            // finished moving. do whatever we did before.
            return "IDLE";
        }
        if (EventCancelAction()) {
            // cancel casting (if any) and stop moving
            skillCur = skillNext = -1;
            agent.ResetPath();
            return "IDLE";
        }
        if (EventTradeStarted()) {
            // cancel casting (if any), stop moving, set target, go to trading
            skillCur = skillNext = -1;
            agent.ResetPath();
            target = FindPlayerFromTradeInvitation();
            return "TRADING";
        }
        if (EventNavigateTo()) {
            // cancel casting (if any) and start moving
            skillCur = skillNext = -1;
            agent.stoppingDistance = navigateStop;
            agent.destination = navigatePos;
            return "MOVING";
        }
        if (EventSkillRequest()) {
            // if and where we keep moving depends on the skill and the target
            // check self (alive, mana, weapon etc.) and target
            var skill = skills[skillCur];
            targetNext = target; // return to this one after any corrections by CastCheckTarget
            if (CastCheckSelf(skill) && CastCheckTarget(skill)) {
                // check distance between self and target
                if (CastCheckDistance(skill)) {
                    // stop moving, start casting and set the casting end time
                    agent.ResetPath();
                    skill.castTimeEnd = Time.time + skill.castTime;
                    skills[skillCur] = skill;
                    return "CASTING";
                } else {
                    // keep moving towards the target
                    // (use collider point(s) to also work with big entities)
                    agent.stoppingDistance = skill.castRange;
                    agent.destination = target.collider.ClosestPointOnBounds(transform.position);
                    return "MOVING";
                }
            } else {
                // invalid target. stop trying to cast, but keep moving.
                skillCur = skillNext = -1;
                return "IDLE";
            }
        }
        if (EventSkillFinished()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care

        return "MOVING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_CASTING() { 
        // keep looking at the target for server & clients (only Y rotation)
        if (target) LookAtY(target.transform.position);

        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died.
            OnDeath();
            skillCur = skillNext = -1; // in case we died while trying to cast
            return "DEAD";
        }
        if (EventNavigateTo()) {
            // cancel casting and start moving
            skillCur = skillNext = -1;
            agent.stoppingDistance = navigateStop;
            agent.destination = navigatePos;
            return "MOVING";
        }
        if (EventCancelAction()) {
            // cancel casting
            skillCur = skillNext = -1;
            return "IDLE";
        }
        if (EventTradeStarted()) {
            // cancel casting (if any), stop moving, set target, go to trading
            skillCur = skillNext = -1;
            agent.ResetPath();
            target = FindPlayerFromTradeInvitation();
            return "TRADING";
        }
        if (EventTargetDisappeared()) {
            // cancel if we were trying to cast an attacks kill
            if (skills[skillCur].category == "Attack") {
                skillCur = skillNext = -1;
                return "IDLE";
            }
        }
        if (EventTargetDied()) {
            // cancel if we were trying to cast an attack skill
            if (skills[skillCur].category == "Attack") {
                skillCur = skillNext = -1;
                return "IDLE";
            }
        }
        if (EventSkillFinished()) {
            // apply the skill after casting is finished
            // note: we don't check the distance again. it's more fun if players
            //       still cast the skill if the target ran a few steps away
            var skill = skills[skillCur];

            // apply the skill on the target
            CastSkill(skill);

            // casting finished for now. user pressed another skill button?
            if (skillNext != -1) {
                skillCur = skillNext;
                skillNext = -1;
            // skill should be followed with default attack? otherwise clear
            } else skillCur = skill.followupDefaultAttack ? 0 : -1;

            // user tried to target something while casting? or we saved the
            // target before correcting it in CastCheckTarget?
            // (we have to wait until the skill is finished, otherwise people
            //  may start to cast and then switch to a far away target while
            //  casting, etc.)
            if (targetNext != null) {
                target = targetNext;
                targetNext = null;
            }

            // go back to IDLE
            return "IDLE";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "CASTING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_TRADING() {        
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died, stop trading. other guy will receive targetdied event.
            OnDeath();
            skillCur = skillNext = -1; // in case we died while trying to cast
            TradeCleanup();
            return "DEAD";
        }
        if (EventCancelAction()) {
            // stop trading
            TradeCleanup();
            return "IDLE";
        }
        if (EventTargetDisappeared()) {
            // target disconnected, stop trading
            TradeCleanup();
            return "IDLE";
        }
        if (EventTargetDied()) {
            // target died, stop trading
            TradeCleanup();
            return "IDLE";
        }
        if (EventTradeDone()) {
            // someone canceled or we finished the trade. stop trading
            TradeCleanup();
            return "IDLE";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTradeStarted()) {} // don't care
        if (EventNavigateTo()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "TRADING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_DEAD() {        
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventRespawn()) {
            // revive to closest spawn, with 50% health, then go to idle
            var start = NetworkManager.singleton.GetStartPosition();
            agent.Warp(start.position); // recommended over transform.position
            Revive(0.5f);
            return "IDLE";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventDied()) {} // don't care
        if (EventCancelAction()) {} // don't care
        if (EventTradeStarted()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventNavigateTo()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "DEAD"; // nothing interesting happened
    }

    [Server]
    protected override string UpdateServer() {
        if (state == "IDLE")    return UpdateServer_IDLE();
        if (state == "MOVING")  return UpdateServer_MOVING();
        if (state == "CASTING") return UpdateServer_CASTING();
        if (state == "TRADING") return UpdateServer_TRADING();
        if (state == "DEAD")    return UpdateServer_DEAD();
        Debug.LogError("invalid state:" + state);
        return "IDLE";
    }

    // finite state machine - client ///////////////////////////////////////////
    [Client]
    protected override void UpdateClient() {
        if (state == "IDLE" || state == "MOVING") {
            if (isLocalPlayer) {
                // simply accept input
                SelectionHandling();
                WSADHandling();

                // canel action if escape key was pressed
                if (Input.GetKeyDown(KeyCode.Escape)) CmdCancelAction();
            }
        } else if (state == "CASTING") {            
            // keep looking at the target for server & clients (only Y rotation)
            if (target) LookAtY(target.transform.position);

            if (isLocalPlayer) {            
                // simply accept input
                SelectionHandling();
                WSADHandling();

                // canel action if escape key was pressed
                if (Input.GetKeyDown(KeyCode.Escape)) CmdCancelAction();
            }   
        } else if (state == "TRADING") {

        } else if (state == "DEAD") {

        } else Debug.LogError("invalid state:" + state);
    }

    // recover /////////////////////////////////////////////////////////////////
    public override void Recover() {
        // base recovery
        base.Recover();

        // additional buff recovery
        if (enabled && hp > 0) {
            // health percent    
            var buffHpPercent = (from skill in skills
                                 where skill.BuffTimeRemaining() > 0
                                 select skill.buffsHpPercentPerSecond).Sum();
            hp += Convert.ToInt32(buffHpPercent * 0.01f * hpMax);

            // mana percent    
            var buffMpPercent = (from skill in skills
                                 where skill.BuffTimeRemaining() > 0
                                 select skill.buffsMpPercentPerSecond).Sum();
            mp += Convert.ToInt32(buffMpPercent * 0.01f * mpMax);
        }
    }

    // attributes //////////////////////////////////////////////////////////////
    public int AttributesSpendable() {
        // calculate the amount of attribute points that can still be spent
        // -> one point per level
        // -> we don't need to store the points in an extra variable, we can
        //    simply decrease the attribute points spent from the level
        return level - (strength + intelligence);
    }

    [Command]
    public void CmdIncreaseStrength() {
        // validate
        if (hp > 0 && AttributesSpendable() > 0) ++strength;
    }

    [Command]
    public void CmdIncreaseIntelligence() {
        // validate
        if (hp > 0 && AttributesSpendable() > 0) ++intelligence;
    }

    // combat //////////////////////////////////////////////////////////////////
    // custom DealDamageAt function that also rewards experience if we killed
    // the monster
    [Server]
    public override HashSet<Entity> DealDamageAt(Entity entity, int n, float aoeRadius=0f) {
        // deal damage with the default function. get all entities that were hit
        // in the AoE radius
        var entities = base.DealDamageAt(entity, n, aoeRadius);
        foreach (var e in entities) {
            // a monster?
            if (e is Monster) {
                // did we kill it?
                if (e.hp == 0) {
                    // gain experience reward
                    var rewardExp = ((Monster)e).rewardExp;
                    var balancedExp = BalanceExpReward(rewardExp, level, e.level);
                    exp += balancedExp;

                    // gain skill experience reward
                    var rewardSkillExp = ((Monster)e).rewardSkillExp;
                    skillExp += BalanceExpReward(rewardSkillExp, level, e.level);
                    
                    // increase quest kill counters
                    IncreaseQuestKillCounterFor(e.name);
                }
            // a player?
            // (see murder code section comments to understand the system)
            } else if (e is Player) {
                // was he innocent?
                if (!((Player)e).IsOffender() && !((Player)e).IsMurderer()) {
                    // did we kill him? then start/reset murder status
                    // did we just attack him? then start/reset offender status
                    // (unless we are already a murderer)
                    if (e.hp == 0) StartMurderer();
                    else if (!IsMurderer()) StartOffender();
                }
            }
        }
        return entities; // not really needed anywhere
    }

    // experience //////////////////////////////////////////////////////////////
    public float ExpPercent() {
        return (exp != 0 && expMax != 0) ? (float)exp / (float)expMax : 0.0f;
    }
    
    // players gain exp depending on their level. if a player has a lower level
    // than the monster, then he gains more exp (up to 100% more) and if he has
    // a higher level, then he gains less exp (up to 100% less)
    // -> test with monster level 20 and expreward of 100:
    //   BalanceExpReward( 1, 20, 100)); => 200
    //   BalanceExpReward( 9, 20, 100)); => 200
    //   BalanceExpReward(10, 20, 100)); => 200
    //   BalanceExpReward(11, 20, 100)); => 190
    //   BalanceExpReward(12, 20, 100)); => 180
    //   BalanceExpReward(13, 20, 100)); => 170
    //   BalanceExpReward(14, 20, 100)); => 160
    //   BalanceExpReward(15, 20, 100)); => 150
    //   BalanceExpReward(16, 20, 100)); => 140
    //   BalanceExpReward(17, 20, 100)); => 130
    //   BalanceExpReward(18, 20, 100)); => 120
    //   BalanceExpReward(19, 20, 100)); => 110
    //   BalanceExpReward(20, 20, 100)); => 100
    //   BalanceExpReward(21, 20, 100)); =>  90
    //   BalanceExpReward(22, 20, 100)); =>  80
    //   BalanceExpReward(23, 20, 100)); =>  70
    //   BalanceExpReward(24, 20, 100)); =>  60
    //   BalanceExpReward(25, 20, 100)); =>  50
    //   BalanceExpReward(26, 20, 100)); =>  40
    //   BalanceExpReward(27, 20, 100)); =>  30
    //   BalanceExpReward(28, 20, 100)); =>  20
    //   BalanceExpReward(29, 20, 100)); =>  10
    //   BalanceExpReward(30, 20, 100)); =>   0
    //   BalanceExpReward(31, 20, 100)); =>   0
    public static long BalanceExpReward(long reward, int attackerLevel, int victimLevel) {
        var levelDiff = Mathf.Clamp(victimLevel - attackerLevel, -10, 10);
        var multiplier = 1f + levelDiff*0.1f;
        return Convert.ToInt64(reward * multiplier);
    }

    // death ///////////////////////////////////////////////////////////////////
    [Server]
    void OnDeath() {
        // stop any movement and buffs, clear target
        agent.ResetPath();
        StopBuffs();
        target = null;
        
        // lose experience
        var loss = Convert.ToInt64(expMax * deathExpLossPercent);
        exp -= loss;

        // send an info chat message
        var msg = new ChatInfoMsg();
        msg.text = "You died and lost " + loss + " experience.";
        netIdentity.connectionToClient.Send(ChatInfoMsg.MsgId, msg);
    }

    // loot ////////////////////////////////////////////////////////////////////
    [Command]
    public void CmdTakeLootGold() {
        // validate: dead monster and close enough?
        // use collider point(s) to also work with big entities
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            target != null && target is Monster && target.hp == 0 &&
            Utils.ClosestDistance(collider, target.collider) <= lootRange)
        {
            // take it
            gold += ((Monster)target).lootGold;
            ((Monster)target).lootGold = 0;
        }
    }

    [Command]
    public void CmdTakeLootItem(int index) {
        // validate: dead monster and close enough and valid loot index?
        // use collider point(s) to also work with big entities
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            target != null && target is Monster && target.hp == 0 &&
            Utils.ClosestDistance(collider, target.collider) <= lootRange &&
            0 <= index && index < ((Monster)target).lootItems.Count)
        {
            // find a free inventory slot
            var monster = (Monster)target;
            var freeIdx = inventory.FindIndex(item => !item.valid);
            if (freeIdx != -1) {
                // take it
                var item = monster.lootItems[index];
                inventory[freeIdx] = item;

                // clear it
                // note: Item has a .valid property that can be used to 'delete' an
                //       item. it's better than .RemoveAt() because we won't run into index-
                //       out-of-range issues
                item.valid = false;
                monster.lootItems[index] = item;
            }
        }
    }

    // inventory ///////////////////////////////////////////////////////////////
    [Command]
    public void CmdSwapInventoryTrash(int inventoryIndex) {
        // dragging an inventory item to the trash always overwrites the trash
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count) {
            // inventory slot has to be valid and destroyable
            if (inventory[inventoryIndex].valid && inventory[inventoryIndex].destroyable) {
                // overwrite trash
                trash = inventory[inventoryIndex];
                // clear inventory slot
                var temp = inventory[inventoryIndex];
                temp.valid = false;
                inventory[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdSwapTrashInventory(int inventoryIndex) {
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count) {
            // inventory slot has to be empty or destroyable
            if (!inventory[inventoryIndex].valid || inventory[inventoryIndex].destroyable) {
                // swap them
                var temp = inventory[inventoryIndex];
                inventory[inventoryIndex] = trash;
                trash = temp;
            }
        }
    }

    public int GetInventoryIndexByName(string itemName) {
        return inventory.FindIndex(item => item.valid && item.name == itemName);
    }

    public int InventorySlotsFree() {
        return inventory.Where(item => !item.valid).Count();
    }

    // count total amount of item type in inventory
    public int InventoryCountAmount(string itemName) {
        return (from item in inventory
                where item.valid && item.name == itemName
                select item.amount).Sum();
    }

    // helper function to remove 'n' items from the inventory
    public bool InventoryRemoveAmount(string itemName, int amount) {
        for (int i = 0; i < inventory.Count; ++i) {
            if (inventory[i].valid && inventory[i].name == itemName) {
                var item = inventory[i];

                // take as many as possible
                var take = Mathf.Min(amount, item.amount);
                item.amount -= take;
                amount -= take;

                // make slot invalid if amount is 0 now
                if (item.amount == 0) item.valid = false;

                // save all changes
                inventory[i] = item;

                // are we done?
                if (amount == 0) return true;
            }
        }

        // if we got here, then we didn't remove enough items
        return false;
    }

    [Command]
    public void CmdSwapInventoryInventory(int fromIndex, int toIndex) {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // swap them
            var temp = inventory[fromIndex];
            inventory[fromIndex] = inventory[toIndex];
            inventory[toIndex] = temp;
        }
    }

    [Command]
    public void CmdInventorySplit(int fromIndex, int toIndex) {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // slotFrom has to have an entry, slotTo has to be empty
            if (inventory[fromIndex].valid && !inventory[toIndex].valid) {
                // from entry needs at least amount of 2
                if (inventory[fromIndex].amount >= 2) {
                    // split them serversided (has to work for even and odd)
                    var itemFrom = inventory[fromIndex];
                    var itemTo = inventory[fromIndex]; // copy the value
                    //inventory[toIndex] = inventory[fromIndex]; // copy value type
                    itemTo.amount = itemFrom.amount / 2;
                    itemFrom.amount -= itemTo.amount; // works for odd too

                    // put back into the list
                    inventory[fromIndex] = itemFrom;
                    inventory[toIndex] = itemTo;
                }
            }
        }
    }

    [Command]
    public void CmdInventoryMerge(int fromIndex, int toIndex) {
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // both items have to be valid
            if (inventory[fromIndex].valid && inventory[toIndex].valid) {
                // make sure that items are the same type
                if (inventory[fromIndex].name == inventory[toIndex].name) {
                    // merge from -> to
                    var itemFrom = inventory[fromIndex];
                    var itemTo = inventory[toIndex];
                    var stack = Mathf.Min(itemFrom.amount + itemTo.amount, itemTo.maxStack);
                    var put = stack - itemFrom.amount;
                    itemFrom.amount = itemTo.amount - put;
                    itemTo.amount = stack;
                    // 'from' empty now? then clear it
                    if (itemFrom.amount == 0) itemFrom.valid = false;
                    // put back into the list
                    inventory[fromIndex] = itemFrom;
                    inventory[toIndex] = itemTo;
                }
            }
        }
    }

    [Command]
    public void CmdUseInventoryItem(int index) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= index && index < inventory.Count && inventory[index].valid) {
            // what we have to do depends on the category
            //print("use item:" + index);
            var item = inventory[index];
            if (item.category.StartsWith("Potion")) {
                // use
                hp += item.usageHp;
                mp += item.usageMp;
                exp += item.usageExp;

                // decrease amount or destroy
                if (item.usageDestroy) {
                    --item.amount;
                    if (item.amount == 0) item.valid = false;
                    inventory[index] = item; // put new values in there
                }
            } else if (item.category.StartsWith("Equipment")) {
                // for each slot: find out if equipable and then do so
                for (int i = 0; i < equipment.Count; ++i)
                    if (CanEquip(equipmentTypes[i], item))
                        SwapInventoryEquip(index, i);
            }
        }
    }

    // equipment ///////////////////////////////////////////////////////////////
    public int GetEquipmentIndexByName(string itemName) {
        return equipment.FindIndex(item => item.valid && item.name == itemName);
    }

    [Server]
    public bool CanEquip(string slotType, Item item) {
        // note: we use StartsWith because a sword could also have the type
        //       EquipmentWeaponSpecial or whatever, which is fine too
        // note: empty slot types shouldn't be able to equip anything
        return slotType != "" && item.category.StartsWith(slotType) && level >= item.minLevel;
    }

    void OnEquipmentChanged(SyncListItem.Operation op, int index) {
        // update the model for server and clients
        RefreshLocations(equipmentTypes[index], equipment[index]);
    }

    void RefreshLocation(Transform loc, Item item) {
        // clear previous one in any case (when overwriting or clearing)
        if (loc.childCount > 0) Destroy(loc.GetChild(0).gameObject);

        // valid item? and has a model? then set it
        if (item.valid && item.model != null) {                    
            // load the resource
            var g = (GameObject)Instantiate(item.model);
            g.transform.SetParent(loc, false);
        }
    }

    void RefreshLocations(string category, Item item) {
        // find the locations with that category and refresh them
        var locations = GetComponentsInChildren<PlayerEquipmentLocation>().Where(loc => loc.acceptedCategory == category).ToList();
        locations.ForEach(loc => RefreshLocation(loc.transform, item));
    }

    public void SwapInventoryEquip(int inventoryIndex, int equipIndex) {
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (hp > 0 &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count &&
            0 <= equipIndex && equipIndex < equipment.Count) {
            // slotInv has to be empty or equipable
            if (!inventory[inventoryIndex].valid || CanEquip(equipmentTypes[equipIndex], inventory[inventoryIndex])) {
                // swap them
                var temp = equipment[equipIndex];
                equipment[equipIndex] = inventory[inventoryIndex];
                inventory[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdSwapInventoryEquip(int inventoryIndex, int equipIndex) {
        // SwapInventoryEquip sometimes needs to be called by the server and
        // sometimes as a Command by clients, but calling a Command from the
        // Server causes a UNET error, so we need it once as a normal function
        // and once as a Command.
        SwapInventoryEquip(inventoryIndex, equipIndex);
    }

    // skills //////////////////////////////////////////////////////////////////
    public override bool HasCastWeapon() {
        // equipped any 'EquipmentWeapon...' item?
        return equipment.FindIndex(item => item.valid && item.category.StartsWith("EquipmentWeapon")) != -1;
    }

    public override bool CanAttackType(System.Type t) {
        // players can attack players and monsters
        return t == typeof(Monster) || t == typeof(Player);
    }

    public int GetSkillIndexByName(string skillName) {
        return skills.FindIndex(skill => skill.learned && skill.name == skillName);
    }

    [Command]
    public void CmdUseSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            // can the skill be casted?
            if (skills[skillIndex].learned && skills[skillIndex].IsReady()) {
                // add as current or next skill, unless casting same one already
                // (some players might hammer the key multiple times, which
                //  doesn't mean that they want to cast it afterwards again)
                // => also: always set skillCur when moving or idle or whatever
                //  so that the last skill that the player tried to cast while
                //  moving is the first skill that will be casted when attacking
                //  the enemy.
                if (skillCur == -1 || state != "CASTING")
                    skillCur = skillIndex;
                else if (skillCur != skillIndex)
                    skillNext = skillIndex;
            }
        }
    }

    [Command]
    public void CmdLearnSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            var skill = skills[skillIndex];

            // not learned already? enough skill exp, required level?
            // note: status effects aren't learnable
            if (!skill.category.StartsWith("Status") &&
                !skill.learned &&
                level >= skill.requiredLevel &&
                skillExp >= skill.requiredSkillExp) {
                // decrease skill experience
                skillExp -= skill.requiredSkillExp;

                // learn skill
                skill.learned = true;
                skills[skillIndex] = skill;
            }
        }
    }

    [Command]
    public void CmdUpgradeSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            var skill = skills[skillIndex];

            // already learned? enough skill exp and required level for upgrade?
            // and can be upgraded?
            // note: status effects aren't upgradeable
            if (!skill.category.StartsWith("Status") &&
                skill.learned &&
                skill.level < skill.maxLevel &&
                level >= skill.upgradeRequiredLevel &&
                skillExp >= skill.upgradeRequiredSkillExp) {
                // decrease skill experience
                skillExp -= skill.upgradeRequiredSkillExp;

                // upgrade
                ++skill.level;
                skills[skillIndex] = skill;
            }
        }
    }
    
    // skillbar ////////////////////////////////////////////////////////////////
    [Client]
    void SaveSkillbar() {
        // save skillbar to player prefs (based on player name, so that
        // each character can have a different skillbar)
        for (int i = 0; i < skillbar.Length; ++i)
            PlayerPrefs.SetString(name + "_skillbar_" + i, skillbar[i]);

        // force saving playerprefs, otherwise they aren't saved for some reason
        PlayerPrefs.Save();
    }

    [Client]
    void LoadSkillbar() {
        // we simply load the entries. if a user modified it to something invalid
        // then it will be sorted out in the OnGUI check automatically
        for (int i = 0; i < skillbar.Length; ++i)
            skillbar[i] = PlayerPrefs.GetString(name + "_skillbar_" + i, "");
    }

    // quests //////////////////////////////////////////////////////////////////
    public int GetQuestIndexByName(string questName) {
        return quests.FindIndex(quest => quest.name == questName);
    }

    [Server]
    public void IncreaseQuestKillCounterFor(string monsterName) {
        for (int i = 0; i < quests.Count; ++i) {
            // active quest and not completed yet?
            if (!quests[i].completed && quests[i].killName == monsterName) {
                var quest = quests[i];
                quest.killed = Mathf.Min(quest.killed + 1, quest.killAmount);
                quests[i] = quest;
            }
        }
    }

    // helper function to check if the player has completed a quest before
    public bool HasCompletedQuest(string questName) {
        return quests.Any(q => q.name == questName && q.completed);
    }

    // helper function to check if a player has an active (not completed) quest
    public bool HasActiveQuest(string questName) {
        return quests.Any(q => q.name == questName && !q.completed);
    }

    // helper function to check if the player can accept a new quest
    // note: no quest.completed check needed because we have a'not accepted yet'
    //       check
    public bool CanStartQuest(QuestTemplate quest) {
        // not too many quests yet?
        // has required level?
        // not accepted yet?
        // has finished predecessor quest (if any)?
        return quests.Count < questLimit &&
               level >= quest.level &&                  // has required level?
               GetQuestIndexByName(quest.name) == -1 && // not accepted yet?
               (quest.predecessor == null || HasCompletedQuest(quest.predecessor.name));
    }

    // helper function to check if the player can complete a quest
    public bool CanCompleteQuest(string questName) {
        // has the quest and not completed yet?
        var idx = GetQuestIndexByName(questName);
        if (idx != -1 && !quests[idx].completed) {
            // fulfilled?
            var quest = quests[idx];
            var gathered = quest.gatherName != "" ? InventoryCountAmount(quest.gatherName) : 0;
            if (quest.IsFulfilled(gathered)) return true;
        }
        return false;
    }

    [Command]
    public void CmdAcceptQuest(int npcQuestIndex) {
        // validate
        // use collider point(s) to also work with big entities
        if (state == "IDLE" &&
            target != null &&
            target.hp > 0 &&
            target is Npc &&
            0 <= npcQuestIndex && npcQuestIndex < ((Npc)target).quests.Length &&
            Utils.ClosestDistance(collider, target.collider) <= talkRange &&
            CanStartQuest(((Npc)target).quests[npcQuestIndex]))
        {
            var npcQuest = ((Npc)target).quests[npcQuestIndex];
            quests.Add(new Quest(npcQuest));
        }
    }

    [Command]
    public void CmdCompleteQuest(int npcQuestIndex) {
        // validate
        // use collider point(s) to also work with big entities
        if (state == "IDLE" &&
            target != null &&
            target.hp > 0 &&
            target is Npc &&
            0 <= npcQuestIndex && npcQuestIndex < ((Npc)target).quests.Length &&
            Utils.ClosestDistance(collider, target.collider) <= talkRange)
        {
            var npc = (Npc)target;
            var npcQuest = npc.quests[npcQuestIndex];

            // does the player have that quest?
            var idx = GetQuestIndexByName(npcQuest.name);
            if (idx != -1) {
                var quest = quests[idx];

                // not completed before?
                if (!quest.completed) {
                    // is it fulfilled (are all requirements met)?
                    var gathered = quest.gatherName != "" ? InventoryCountAmount(quest.gatherName) : 0;
                    if (quest.IsFulfilled(gathered)) {
                        // remove gathered items from player's inventory
                        if (quest.gatherName != "")
                            InventoryRemoveAmount(quest.gatherName, quest.gatherAmount);

                        // gain rewards
                        gold += quest.rewardGold;
                        exp += quest.rewardExp;

                        // complete quest
                        quest.completed = true;
                        quests[idx] = quest;
                    }
                }
            }
        }
    }

    // npc trading /////////////////////////////////////////////////////////////
    [Command]
    public void CmdNpcBuyItem(int index, int amount) {
        // validate: close enough, npc alive and valid index?
        // use collider point(s) to also work with big entities
        if (state == "IDLE" &&
            target != null &&
            target.hp > 0 &&
            target is Npc &&
            Utils.ClosestDistance(collider, target.collider) <= talkRange &&
            0 <= index && index < ((Npc)target).saleItems.Length)
        {
            var npc = (Npc)target;

            // valid amount?
            if (1 <= amount && amount <= npc.saleItems[index].maxStack) {
                var price = npc.saleItems[index].buyPrice * amount;

                // enough gold?
                if (gold >= price) {
                    // find free inventory slot
                    var freeIdx = inventory.FindIndex(item => !item.valid);
                    if (freeIdx != -1) {
                        // buy it
                        gold -= price;
                        var item = new Item(npc.saleItems[index], amount);
                        inventory[freeIdx] = item;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdNpcSellItem(int index, int amount) {
        // validate: close enough, npc alive and valid index and valid item?
        // use collider point(s) to also work with big entities
        if (state == "IDLE" &&
            target != null && 
            target.hp > 0 &&
            target is Npc &&
            Utils.ClosestDistance(collider, target.collider) <= talkRange &&
            0 <= index && index < inventory.Count &&
            inventory[index].valid &&
            inventory[index].sellable)
        {
            var item = inventory[index];

            // valid amount?
            if (1 <= amount && amount <= item.amount) {
                // sell the amount
                var price = item.sellPrice * amount;
                gold += price;
                item.amount -= amount;
                if (item.amount == 0) item.valid = false;
                inventory[index] = item;
            }
        }
    }

    // npc teleport ////////////////////////////////////////////////////////////
    [Command]
    public void CmdNpcTeleport() {
        // validate
        if (state == "IDLE" &&
            target != null && 
            target.hp > 0 &&
            target is Npc &&
            Utils.ClosestDistance(collider, target.collider) <= talkRange &&
            ((Npc)target).teleportTo != null)
        {
            // using agent.Warp is recommended over transform.position
            // (the latter can cause weird bugs when using it with an agent)
            agent.Warp(((Npc)target).teleportTo.position);
        }

    }

    // player to player trading ////////////////////////////////////////////////
    // how trading works:
    // 1. A invites his target with CmdTradeRequest()
    //    -> sets B.tradeInvitationFrom = A;
    // 2. B sees a UI window and accepts (= invites A too)
    //    -> sets A.tradeInvitationFrom = B;
    // 3. the TradeStart event is fired, both go to 'TRADING' state
    // 4. they lock the trades
    // 5. they accept, then items and gold are swapped

    public bool CanStartTrade() {
        // a player can only trade if he is not trading already and alive
        return hp > 0 && state != "TRADING";
    }

    public bool CanStartTradeWith(Entity e) {
        // can we trade? can the target trade? are we close enough?
        return e != null && e is Player && e != this &&
               CanStartTrade() && ((Player)e).CanStartTrade() &&
               Utils.ClosestDistance(collider, e.collider) <= talkRange;
    }

    // request a trade with the target player.
    [Command]
    public void CmdTradeRequestSend() {
        // validate
        if (CanStartTradeWith(target)) {
            // send a trade request to target
            ((Player)target).tradeRequestFrom = name;
            print(name + " invited " + target.name + " to trade");
        }
    }

    // helper function to find the guy who sent us a trade invitation
    Player FindPlayerFromTradeInvitation() {
        if (tradeRequestFrom != "") {
            var go = netIdentity.FindObserver(tradeRequestFrom);
            if (go != null) return go.GetComponent<Player>();
        }
        return null;
    }

    // accept a trade invitation by simply setting 'requestFrom' for the other
    // person to self
    [Command]
    public void CmdTradeRequestAccept() {
        var sender = FindPlayerFromTradeInvitation();
        if (sender != null) {
            if (CanStartTradeWith(sender)) {
                // also send a trade request to the person that invited us
                sender.tradeRequestFrom = name;
                print(name + " accepted " + sender.name + "'s trade request");
            }
        }
    }

    // decline a trade invitation
    [Command]
    public void CmdTradeRequestDecline() {
        tradeRequestFrom = "";
    }

    [Server]
    void TradeCleanup() {
        // clear all trade related properties
        tradeOfferGold = 0;
        for (int i = 0; i < tradeOfferItems.Count; ++i) tradeOfferItems[i] = -1;
        tradeOfferLocked = false;        
        tradeOfferAccepted = false;
        tradeRequestFrom = "";
    }

    [Command]
    public void CmdTradeCancel() {
        // validate
        if (state == "TRADING") {
            // clear trade request for both guys. the FSM event will do the rest
            var p = FindPlayerFromTradeInvitation();
            if (p != null) p.tradeRequestFrom = "";
            tradeRequestFrom = "";
        }
    }

    [Command]
    public void CmdTradeOfferLock() {
        // validate
        if (state == "TRADING")
            tradeOfferLocked = true;
    }

    [Command]
    public void CmdTradeOfferGold(long n) {
        // validate
        if (state == "TRADING" && !tradeOfferLocked &&
            0 <= n && n <= gold)
            tradeOfferGold = n;
    }

    [Command]
    public void CmdTradeOfferItem(int inventoryIndex, int offerIndex) {
        // validate
        if (state == "TRADING" && !tradeOfferLocked &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count &&
            inventory[inventoryIndex].valid &&
            0 <= offerIndex && offerIndex < tradeOfferItems.Count &&
            !tradeOfferItems.Contains(inventoryIndex)) // only one reference
            tradeOfferItems[offerIndex] = inventoryIndex;
    }

    [Command]
    public void CmdTradeOfferItemClear(int offerIndex) {
        // validate
        if (state == "TRADING" && !tradeOfferLocked &&
            0 <= offerIndex && offerIndex < tradeOfferItems.Count)
            tradeOfferItems[offerIndex] = -1;
    }

    [Server]
    bool IsTradeOfferStillValid() {
        // enough gold and all offered items are -1 or valid?
        return gold >= tradeOfferGold &&
               tradeOfferItems.All(idx => idx == -1 ||
                                          (0 <= idx && idx < inventory.Count && inventory[idx].valid));
    }

    [Server]
    int TradeOfferItemSlotAmount() {
        return tradeOfferItems.Where(i => i != -1).Count();
    }

    [Server]
    int InventorySlotsNeededForTrade() {
        // if other guy offers 2 items and we offer 1 item then we only need
        // 2-1 = 1 slots. and the other guy would need 1-2 slots and at least 0.
        if (target != null && target is Player) {
            var other = (Player)target;
            var amountMy = TradeOfferItemSlotAmount();
            var amountOther = other.TradeOfferItemSlotAmount();
            return Mathf.Max(amountOther - amountMy, 0);
        }
        return 0;
    }

    [Command]
    public void CmdTradeOfferAccept() {
        // validate
        // note: distance check already done when starting the trade
        if (state == "TRADING" && tradeOfferLocked &&
            target != null && target is Player) {
            // other guy locked the offer too?
            var other = (Player)target;
            if (other.tradeOfferLocked) {
                // are we the first one to accept?
                if (!other.tradeOfferAccepted) {
                    // then simply accept and wait for the other guy
                    tradeOfferAccepted = true;
                    print("first accept by " + name);
                // otherwise both have accepted now, so start the trade
                } else {                        
                    // accept
                    tradeOfferAccepted = true;
                    print("second accept by " + name);

                    // both offers still valid?
                    if (IsTradeOfferStillValid() && other.IsTradeOfferStillValid()) {
                        // both have enough inventory slots?
                        if (InventorySlotsFree() >= InventorySlotsNeededForTrade() &&
                            other.InventorySlotsFree() >= other.InventorySlotsNeededForTrade()) {
                            // exchange the items by first taking them out
                            // into a temporary list and then putting them
                            // in. this guarantees that exchanging even
                            // works with full inventories
                            
                            // take them out
                            var tempMy = new Queue<Item>();
                            for (int i = 0; i < tradeOfferItems.Count; ++i) {
                                var idx = tradeOfferItems[i];
                                if (idx != -1) {
                                    tempMy.Enqueue(inventory[idx]);
                                    var item = inventory[idx];
                                    item.valid = false;
                                    inventory[idx] = item;
                                }
                            }

                            var tempOther = new Queue<Item>();
                            for (int i = 0; i < other.tradeOfferItems.Count; ++i) {
                                var idx = other.tradeOfferItems[i];
                                if (idx != -1) {
                                    tempOther.Enqueue(other.inventory[idx]);
                                    var item = other.inventory[idx];
                                    item.valid = false;
                                    other.inventory[idx] = item;
                                }
                            }

                            // put them into the free slots
                            for (int i = 0; i < inventory.Count; ++i)
                                if (!inventory[i].valid && tempOther.Count > 0)
                                    inventory[i] = tempOther.Dequeue();
                            
                            for (int i = 0; i < other.inventory.Count; ++i)
                                if (!other.inventory[i].valid && tempMy.Count > 0)
                                    other.inventory[i] = tempMy.Dequeue();

                            // did it all work?
                            if (tempMy.Count > 0 || tempOther.Count > 0)
                                Debug.LogWarning("item trade problem");

                            // exchange the gold
                            gold -= tradeOfferGold;
                            other.gold -= other.tradeOfferGold;

                            gold += other.tradeOfferGold;
                            other.gold += tradeOfferGold;
                        }
                    } else {
                        print("trade canceled because offer is not valid anymore");
                    }

                    // clear trade request for both guys. the FSM event will do
                    // the rest
                    tradeRequestFrom = "";
                    other.tradeRequestFrom = "";
                }
            }
        }
    }

    // pvp murder system ///////////////////////////////////////////////////////
    // attacking someone innocent results in Offender status
    //   (can be attacked without penalty for a short time)
    // killing someone innocent results in Murderer status
    //   (can be attacked without penalty for a long time + negative buffs)
    // attacking/killing a Offender/Murderer has no penalty
    //
    // we use buffs for the offender/status because buffs have all the features
    // that we need here.
    public bool IsOffender() {
        return skills.Any(s => s.category == "StatusOffender" && s.BuffTimeRemaining() > 0);
    }

    public bool IsMurderer() {
        return skills.Any(s => s.category == "StatusMurderer" && s.BuffTimeRemaining() > 0);
    }

    void StartOffender() {
        // start or reset the murderer buff
        for (int i = 0; i < skills.Count; ++i)
            if (skills[i].category == "StatusOffender") {
                var skill = skills[i];
                skill.buffTimeEnd = Time.time + skill.buffTime;
                skills[i] = skill;
            }
    }

    void StartMurderer() {
        // start or reset the murderer buff
        for (int i = 0; i < skills.Count; ++i)
            if (skills[i].category == "StatusMurderer") {
                var skill = skills[i];
                skill.buffTimeEnd = Time.time + skill.buffTime;
                skills[i] = skill;
            }
    }

    // selection handling //////////////////////////////////////////////////////
    void SetIndicatorViaParent(Transform parent) {
        if (!indicator) indicator = Instantiate(indicatorPrefab);
        indicator.transform.SetParent(parent, true);
        indicator.transform.position = parent.position + Vector3.up * 0.01f;
        indicator.transform.up = Vector3.up;
    }

    void SetIndicatorViaPosition(Vector3 pos, Vector3 normal) {
        if (!indicator) indicator = Instantiate(indicatorPrefab);
        indicator.transform.parent = null;
        indicator.transform.position = pos + Vector3.up * 0.01f;
        indicator.transform.up = normal; // adjust to terrain normal
    }

    [Command]
    void CmdSetTarget(NetworkIdentity ni) {
        // validate
        if (ni != null) {
            // can directly change it, or change it after casting?
            if (state == "IDLE" || state == "MOVING")
                target = ni.GetComponent<Entity>();
            else if (state == "CASTING")
                targetNext = ni.GetComponent<Entity>();
        }
    }

    [Client]
    void SelectionHandling() {
        // click raycasting only if not over a UI element
        // note: this only works if the UI's CanvasGroup blocks Raycasts
        if (Input.GetMouseButtonDown(0) && !Utils.IsCursorOverUserInterface()) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) {
                // valid target?
                var entity = hit.transform.GetComponent<Entity>();
                if (entity) {
                    // set indicator
                    SetIndicatorViaParent(hit.transform);

                    // clicked last target again? and is not self?
                    if (entity != this && entity == target) {
                        // what is it?
                        if (entity is Monster) {
                            // dead or alive?
                            if (entity.hp > 0) {
                                // cast the first skill (if any, and if ready)
                                if (skills.Count > 0 && skills[0].IsReady())
                                    CmdUseSkill(0);
                                // otherwise walk there if still on cooldown etc
                                // use collider point(s) to also work with big entities
                                else
                                    CmdNavigateTo(entity.collider.ClosestPointOnBounds(transform.position), skills.Count > 0 ? skills[0].castRange : 0f);
                            } else {
                                // has loot? and close enough?
                                // use collider point(s) to also work with big entities
                                if (((Monster)entity).HasLoot() &&
                                    Utils.ClosestDistance(collider, entity.collider) <= lootRange)
                                    FindObjectOfType<UILoot>().Show();
                                // otherwise walk there
                                // use collider point(s) to also work with big entities
                                else
                                    CmdNavigateTo(entity.collider.ClosestPointOnBounds(transform.position), lootRange);
                            }
                        } else if (entity is Player) {
                            // cast the first skill (if any)
                            if (skills.Count > 0) CmdUseSkill(0);
                        } else if (entity is Npc) {
                            // close enough to talk?
                            // use collider point(s) to also work with big entities
                            if (Utils.ClosestDistance(collider, entity.collider) <= talkRange)
                                FindObjectOfType<UINpcDialogue>().Show();                               
                            // otherwise walk there
                            // use collider point(s) to also work with big entities
                            else
                                CmdNavigateTo(entity.collider.ClosestPointOnBounds(transform.position), talkRange);
                        }
                    // clicked a new target
                    } else {
                        // target it
                        CmdSetTarget(entity.netIdentity);
                    }
                // otherwise it's a movement target
                } else {
                    // set indicator and move
                    SetIndicatorViaPosition(hit.point, hit.normal);
                    CmdNavigateTo(hit.point, 0f);
                }
            }
        }
    }

    // simple WSAD movement without prediction
    [Client]
    void WSADHandling() {
        // get horizontal and vertical input
        var h = Input.GetAxis("Horizontal");
        var v = Input.GetAxis("Vertical");
        if (h != 0 || v != 0) {
            // don't move if currently typing in an input
            // we check this after checking h and v to save computations
            if (!UIUtils.AnyInputActive()) {
                // calculate the direction relative to the camera, based on
                // input and make it a bit longer so that we don't have to
                // resend the packet all the time
                var dir = Camera.main.transform.rotation * new Vector3(h, 0, v) * 2;

                // avoid spamming the command by only sending it if we changed
                // the direction or if we almost reached the current goal (which
                // is about 1 unity away, or 2 units if scaled)
                if (dir.normalized != agent.velocity.normalized ||
                    agent.remainingDistance < 0.7f) {
                    CmdNavigateTo(transform.position + dir, 0f);
                }

                // clear indicator if there is one, and if it's not on a target
                // (simply looks better)
                if (indicator != null && indicator.transform.parent == null)
                    Destroy(indicator);
            }
        }
    }
}
