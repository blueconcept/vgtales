// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;

public class UITarget : MonoBehaviour {
    [SerializeField] GameObject panel;
    [SerializeField] Slider hpBar;
    [SerializeField] Text textName;
    [SerializeField] Button buttonTrade;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        if (player.target != null && player.target != player) {
            // name and health
            panel.SetActive(true);
            hpBar.value = player.target.HpPercent();
            textName.text = player.target.name;

            // trade button
            if (player.target is Player) {
                buttonTrade.gameObject.SetActive(true);
                buttonTrade.interactable = player.CanStartTradeWith(player.target);
                buttonTrade.onClick.SetListener(() => {
                    player.CmdTradeRequestSend();
                });
            } else {
                buttonTrade.gameObject.SetActive(false);
            }
        } else panel.SetActive(false); // hide
    }
}
