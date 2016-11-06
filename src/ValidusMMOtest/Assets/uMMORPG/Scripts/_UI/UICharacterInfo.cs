// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;

public class UICharacterInfo : MonoBehaviour {
    [SerializeField] KeyCode hotKey = KeyCode.C;
    [SerializeField] GameObject panel;
    [SerializeField] Text damage;
    [SerializeField] Text defense;
    [SerializeField] Text health;
    [SerializeField] Text mana;
    [SerializeField] Text speed;
    [SerializeField] Text level;
    [SerializeField] Text expCur;
    [SerializeField] Text expMax;
    [SerializeField] Text skillExp;
    [SerializeField] Text strength;
    [SerializeField] Button buttonStrength;
    [SerializeField] Text intelligence;
    [SerializeField] Button buttonIntelligence;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        // hotkey (not while typing in chat, etc.)
        if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
            panel.SetActive(!panel.activeSelf);

        // only update the panel if it's active
        if (panel.activeSelf) {
            // stats
            damage.text = player.damage.ToString();
            defense.text = player.defense.ToString();
            health.text = player.hpMax.ToString();
            mana.text = player.mpMax.ToString();
            speed.text = player.speed.ToString();
            level.text = player.level.ToString();
            expCur.text = player.exp.ToString();
            expMax.text = player.expMax.ToString();
            skillExp.text = player.skillExp.ToString();

            // attributes
            strength.text = player.strength.ToString();
            buttonStrength.interactable = player.AttributesSpendable() > 0;
            buttonStrength.onClick.SetListener(() => {
                player.CmdIncreaseStrength();
            });
            intelligence.text = player.intelligence.ToString();
            buttonIntelligence.interactable = player.AttributesSpendable() > 0;
            buttonIntelligence.onClick.SetListener(() => {
                player.CmdIncreaseIntelligence();
            });
        }
    }
}
