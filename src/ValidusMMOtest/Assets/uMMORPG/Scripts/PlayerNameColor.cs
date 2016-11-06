// Colors the name overlay in case of offender/murderer status.
using UnityEngine;

public class PlayerNameColor : MonoBehaviour {
    [SerializeField] Color defaultColor = Color.white;
    [SerializeField] Color offenderColor = Color.magenta;
    [SerializeField] Color murdererColor = Color.red;

    void Update() {
        // note: murderer has higher priority (a player can be a murderer and an
        // offender at the same time)
        var player = GetComponent<Player>();
        if (player.IsMurderer())
            GetComponentInChildren<TextMesh>().color = murdererColor;
        else if (player.IsOffender())
            GetComponentInChildren<TextMesh>().color = offenderColor;
        else
            GetComponentInChildren<TextMesh>().color = defaultColor;
    }
}
