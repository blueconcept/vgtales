﻿using UnityEngine;
using UnityEngine.UI;

public class UIExperienceBar : MonoBehaviour {
    [SerializeField] Slider bar;
    [SerializeField] Text status;

    void Update() {
        var player = Utils.ClientLocalPlayer();
        if (!player) return;

        bar.value = player.ExpPercent();
        status.text = "Lv." + player.level + " (" + (player.ExpPercent() * 100f).ToString("F2") + "%)";
    }
}
