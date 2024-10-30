﻿using HarmonyLib;
using Kingmaker.UI;
using Kingmaker.UI.InputSystems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.EventSystems;

namespace CinematicUnityExplorer.Owlcat;
public static class HotkeySupression
{
    private static bool _isSuppressed = false;

    public static bool IsSuppressed => _isSuppressed;
    public static void Enable() => _isSuppressed = true;
    public static void Disable() => _isSuppressed = false;

    [HarmonyPatch]
    private static class KeyboardAccessPatch
    {
        [HarmonyPatch(typeof(KeyboardAccess), nameof(KeyboardAccess.IsInputFieldSelected))]
        [HarmonyPostfix]
        static void CheckFieldType(ref bool __result)
        {
            if (__result == true || _isSuppressed) return;

            __result = EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.gameObject.GetComponent<InputField>() != null;
        }
    }
}
