
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace UnityExplorer.UI {
    public class SecondaryDisplayHelper {
        public static Dictionary<int, int> DisplayTohWnd = new();
        public static HashSet<int> ActivatedDisplays = [0];
        public enum ShowStates {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
            ShowNoActivateRecentPosition = 4,
            Show = 5,
            MinimizeActivateNext = 6,
            MinimizeNoActivate = 7,
            ShowNoActivate = 8,
            Restore = 9,
            ShowDefault = 10,
            ForceMinimize = 11
        }

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        [DllImport("User32")]
        private static extern int ShowWindowAsync(int hwnd, int nCmdShow);

        [DllImport("user32.dll", EntryPoint = "FindWindowEx", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static void DeactivateAdditionalDisplay() {
            var hWnd = FindWindow(null, "Unity Secondary Display").ToInt32();
            DisplayTohWnd[DisplayManager.ActiveDisplayIndex] = hWnd;
            ShowWindowAsync(hWnd, SW_HIDE);
        }
        public static void ActivateAdditionalDisplay() {
            if (ActivatedDisplays.Contains(DisplayManager.ActiveDisplayIndex)) {
                if (DisplayTohWnd.TryGetValue(DisplayManager.ActiveDisplayIndex, out var hWnd)) {
                    ShowWindowAsync(hWnd, SW_SHOW);
                }
            } else {
                ActivatedDisplays.Add(DisplayManager.ActiveDisplayIndex);
                DisplayManager.ActiveDisplay.Activate();
            }
        }
    }
}
