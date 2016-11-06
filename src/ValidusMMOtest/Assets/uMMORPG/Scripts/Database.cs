// Saves Character Data in XML files. The design is very simple, we store chars
// in a "Database/Account/Character" file. This way we can get all characters
// for a certain account very easily without parsing ALL the characters.
//
// benchmarks:
//  saving   10 chars: 0.03s
//  saving  100 chars: 0.45s
//  saving 1000 chars: 1.7s
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Linq;

public class Database {
    // database path: Application.dataPath is always relative to the project,
    // but we don't want it inside the Assets folder in the Editor (git etc.),
    // instead we put it above that.
    // we also use Path.Combine for platform independent paths
    // and we need persistentDataPath on android
#if UNITY_EDITOR
    static string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Database");
#elif UNITY_ANDROID
    static string path = Path.Combine(Application.persistentDataPath, "Database");
#elif UNITY_IOS
    static string path = Path.Combine(Application.persistentDataPath, "Database");
#else
    static string path = Path.Combine(Application.dataPath, "Database");
#endif

    // helper functions ////////////////////////////////////////////////////////
    static string AccPath(string account) {
        return Path.Combine(path, account);
    }

    static string CharPath(string account, string charName) {
        return Path.Combine(AccPath(account), charName);
    }

    // character saving ////////////////////////////////////////////////////////
    public static bool CharacterExists(string charName) {
        // GetFiles throws an exception if the path doesn't exist, so check it
        if (Directory.Exists(path))
            return Directory.GetFiles(path, charName, SearchOption.AllDirectories).FirstOrDefault() != null;
        return false;
    }

    public static void CharacterDelete(string account, string charName) {
        File.Delete(CharPath(account, charName));
    }

    public static List<string> CharactersForAccount(string account) {
        if (Directory.Exists(AccPath(account)))
            return (from f in Directory.GetFiles(AccPath(account))
                    select Path.GetFileName(f)).ToList();
        return new List<string>();
    }

    public static void CharacterSave(Player player) {
        Directory.CreateDirectory(AccPath(player.account)); // force directory

        var settings = new XmlWriterSettings();
        settings.Encoding = Encoding.UTF8;
        settings.Indent = true;
        
        using (var writer = XmlWriter.Create(CharPath(player.account, player.name), settings)) {
            writer.WriteStartDocument();
            writer.WriteStartElement("character");

            writer.WriteElementValue("class", player.className);
            writer.WriteElementString("name", player.name);
            writer.WriteElementObject("position", player.transform.position);
            writer.WriteElementValue("level", player.level);
            writer.WriteElementValue("hp", player.hp);
            writer.WriteElementValue("mp", player.mp);
            writer.WriteElementValue("strength", player.strength);
            writer.WriteElementValue("intelligence", player.intelligence);
            writer.WriteElementValue("exp", player.exp);
            writer.WriteElementValue("skillExp", player.skillExp);
            writer.WriteElementValue("gold", player.gold);
            writer.WriteElementObject("inventory", player.inventory.ToList());
            writer.WriteElementObject("equipment", player.equipment.ToList());
            // castTimeEnd and cooldownEnd are based on Time.time, which will be
            // different when restarting the server, so let's convert them to
            // the remaining time for easier save & load
            // note: this does NOT work when trying to save character data
            //       before closing the editor or game because Time.time is 0.
            var skillsAdjusted = new List<Skill>();
            foreach (var skill in player.skills) {
                var adjusted = skill;
                adjusted.castTimeEnd = skill.CastTimeRemaining();
                adjusted.cooldownEnd = skill.CooldownRemaining();
                adjusted.buffTimeEnd = skill.BuffTimeRemaining();
                skillsAdjusted.Add(adjusted);
            }
            writer.WriteElementObject("skills", skillsAdjusted);
            writer.WriteElementObject("quests", player.quests.ToList());

            writer.WriteEndDocument();
        }
    }

    public static GameObject CharacterLoad(string account, string charName, List<Player> prefabs) {
        var fpath = CharPath(account, charName);
        if (File.Exists(fpath)) {
            var settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            using (XmlReader reader = XmlReader.Create(fpath, settings)) {
                reader.ReadStartElement("character");

                // instantiate based on the class name
                var className                 = reader.ReadElementContentAsString();
                var prefab = prefabs.Find(p => p.name == className);
                if (prefab != null) {
                    var go = (GameObject)GameObject.Instantiate(prefab.gameObject);
                    var player = go.GetComponent<Player>();

                    player.name               = reader.ReadElementContentAsString();
                    player.account            = account;
                    player.className          = className;
                    // NEVER use player.transform.position = ...; because it
                    // places the player at weird positions. for example,
                    // (200, 0, -200) becomes (76, 0, -76)
                    // using agent.Warp is also recommended in the Unity docs.
                    player.agent.Warp          (reader.ReadElementObject<Vector3>());
                    player.level              = reader.ReadElementContentAsInt();
                    player.hp                 = reader.ReadElementContentAsInt();
                    player.mp                 = reader.ReadElementContentAsInt();
                    player.strength           = reader.ReadElementContentAsInt();
                    player.intelligence       = reader.ReadElementContentAsInt();
                    player.exp                = reader.ReadElementContentAsLong();
                    player.skillExp           = reader.ReadElementContentAsLong();
                    player.gold               = reader.ReadElementContentAsLong();

                    // load inventory
                    foreach (var item in reader.ReadElementObject< List<Item> >())
                        player.inventory.Add(item.valid && item.TemplateExists() ? item : new Item());
                    
                    // load equipment
                    foreach (var item in reader.ReadElementObject< List<Item> >())
                        player.equipment.Add(item.valid && item.TemplateExists() ? item : new Item());

                    // load skills based on skill templates
                    var skillsLoaded = reader.ReadElementObject< List<Skill> >();
                    foreach (var t in player.skillTemplates) {
                        // add the saved skill data for that template, otherwise new
                        var idx = skillsLoaded.FindIndex(skill => skill.name == t.name);
                        if (idx != -1) {
                            // get the skill
                            var skill = skillsLoaded[idx];
                            // make sure that 1 <= level <= maxlevel (in case we
                            // removed a skill level etc.
                            skill.level = Mathf.Clamp(skill.level, 1, skill.maxLevel);
                            // castTimeEnd and cooldownEnd are based on Time.time,
                            // which will be different when restarting a server,
                            // hence why we saved them as just the remaining times.
                            // so let's convert them back again.
                            skill.castTimeEnd += Time.time;
                            skill.cooldownEnd += Time.time;
                            skill.buffTimeEnd += Time.time;
                            // add it
                            player.skills.Add(skill);
                        } else {
                            // add the template skill
                            player.skills.Add(new Skill(t));
                        }
                    }
                    
                    // load quests
                    foreach (var quest in reader.ReadElementObject< List<Quest> >())
                        if (quest.TemplateExists()) player.quests.Add(quest);

                    reader.ReadEndElement();

                    return go;
                }
            }
        }
        Debug.LogWarning("couldnt load character data:" + fpath);
        return null;
    }

    // adds a character to the database
    public static void CharacterCreate(string account, string charName, Player playerPrefab, Vector3 startPosition) {
        // we instantiate a temporary player, set the default values and then
        // save it
        var player = GameObject.Instantiate(playerPrefab).GetComponent<Player>();
        player.name = charName;
        player.account = account;
        player.className = playerPrefab.name;
        player.transform.position = startPosition;

        // default inventory slots + items (if any)
        for (int i = 0; i < player.inventorySize; ++i)
            if (i < player.defaultItems.Length)
                player.inventory.Add(new Item(player.defaultItems[i]));
            else
                player.inventory.Add(new Item());

        // default equipment slots
        foreach (var equipType in player.equipmentTypes) {
            // any default item for that slot?
            var idx = player.defaultEquipment.FindIndex(
                dbItem => player.CanEquip(equipType, new Item(dbItem))
            );

            if (idx != -1)
                player.equipment.Add(new Item(player.defaultEquipment[idx]));
            else
                player.equipment.Add(new Item());
        }

        // full health and mana (after equipment so that hpmax is correct)
        player.hp = player.hpMax;
        player.mp = player.mpMax;

        // skills will be loaded from templates and there are no default quests
        CharacterSave(player);

        // destroy temporary player object again
        GameObject.Destroy(player.gameObject);
    }
}