using FusionVRPlus.Networking;
using UnityEngine;

namespace FusionVRPlus.Misc
{
    public class FusionVRGUI : MonoBehaviour
    {
        private string RoomNameInput = "DefaultRoom";
        private string CosmeticNameInput = "TopHat";
        private string CosmeticSlotInput = "Head";

        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeTabStyle;

        public int PanelRadius = 10;
        public int ButtonRadius = 8;
        public int TextFieldRadius = 6;

        private int currentTab = 0; // 0 = Networking, 1 = Cosmetics
        private readonly string[] tabNames = { "Networking", "Cosmetics", "Player" };

        public Rect windowRect = new Rect(100, 100, 320, 450);

        void OnGUI()
        {
            if (windowStyle == null)
                InitStyles();

            windowRect = GUI.Window(0, windowRect, DrawWindow, "Fusion VR+", windowStyle);
        }

        void DrawWindow(int id)
        {
            // --- Tabs ---
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUIStyle styleToUse = (i == currentTab) ? activeTabStyle : buttonStyle;

                if (GUILayout.Button(tabNames[i], styleToUse, GUILayout.Height(30)))
                    currentTab = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- Tab Content ---
            switch (currentTab)
            {
                case 0:
                    DrawNetworkingTab();
                    break;
                case 1:
                    DrawCosmeticsTab();
                    break;
                case 2:
                    DrawPlayerTab();
                    break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void DrawNetworkingTab()
        {
            GUILayout.Label("Room Name", labelStyle);
            RoomNameInput = GUILayout.TextField(RoomNameInput, textFieldStyle, GUILayout.Height(30));
            GUILayout.Space(10);

            if (FusionVRPlusManager.Manager != null)
            {
                if (GUILayout.Button("Join Room", buttonStyle, GUILayout.Height(35)))
                    _ = FusionVRPlusManager.Manager.ConnectToServer(RoomNameInput, 10, false);

                GUI.enabled = FusionVRPlusNetworkRunner.IsInRoom();
                if (GUILayout.Button("Leave Room", buttonStyle, GUILayout.Height(35)))
                    FusionVRPlusManager.Manager.LeaveRoom();
                GUI.enabled = true;

                GUILayout.Space(10);
                if (GUILayout.Button("Toggle Debug Mode", buttonStyle, GUILayout.Height(35)))
                {
                    foreach (var plr in FusionVRPlusNetworkRunner.Players)
                        plr.DebugMode = !plr.DebugMode;
                }

                GUILayout.Space(10);
                GUILayout.Label("Is Shared Mode Master: " +
                    FusionVRPlusNetworkRunner.IsSharedModeMasterClient().ToString(), labelStyle);
            }
            else
            {
                GUILayout.Space(20);
                GUILayout.Label("⚠️ FusionVRPlusManager not initialized!", labelStyle);
            }
        }

        private Vector2 playerScroll;

        private void DrawPlayerTab()
        {
            GUILayout.Label("Players", labelStyle);
            GUILayout.Space(8);

            var players = FusionVRPlusNetworkRunner.GetPlayerList();

            if (players == null || players.Count == 0)
            {
                GUILayout.Label("No players found.", labelStyle);
                return;
            }

            playerScroll = GUILayout.BeginScrollView(playerScroll, false, true, GUILayout.Height(300));

            foreach (var plr in players)
            {
                // Slight background tint based on player color
                Color playerColor = plr.playerColor;
                Color bgColor = new Color(playerColor.r * 0.3f + 0.1f, playerColor.g * 0.3f + 0.1f, playerColor.b * 0.3f + 0.1f);

                // Begin "card"
                GUI.backgroundColor = bgColor;
                GUILayout.BeginVertical("box");
                GUI.backgroundColor = Color.white;

                GUILayout.BeginHorizontal();
                GUILayout.Label(plr.username, labelStyle, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                // Display RGB color values more neatly
                float r = Mathf.Clamp(plr.playerColor.r * 255f, 0f, 255f);
                float g = Mathf.Clamp(plr.playerColor.g * 255f, 0f, 255f);
                float b = Mathf.Clamp(plr.playerColor.b * 255f, 0f, 255f);

                GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(plr.playerColor)}>●</color>  ({r:0}, {g:0}, {b:0})",
                    new GUIStyle(labelStyle) { richText = true, alignment = TextAnchor.MiddleRight });

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                GUILayout.Space(6); // spacing between player entries
            }

            GUILayout.EndScrollView();
        }



        private void DrawCosmeticsTab()
        {
            GUILayout.Label("Cosmetic Name", labelStyle);
            CosmeticNameInput = GUILayout.TextField(CosmeticNameInput, textFieldStyle, GUILayout.Height(30));

            GUILayout.Label("Cosmetic Slot", labelStyle);
            CosmeticSlotInput = GUILayout.TextField(CosmeticSlotInput, textFieldStyle, GUILayout.Height(30));

            GUILayout.Space(10);

            if (FusionVRPlusManager.LocalPlayer != null)
            {
                if (GUILayout.Button("Toggle Cosmetic", buttonStyle, GUILayout.Height(35)))
                    FusionVRPlusManager.LocalPlayer.ToggleCosmetic(CosmeticNameInput, CosmeticSlotInput);
            }
            else
            {
                GUILayout.Label("Local Player not available yet.", labelStyle);
            }
        }

        private void InitStyles()
        {
            Texture2D panelTex = MakeRoundedTex(2, 2, new Color(0.12f, 0.12f, 0.12f), PanelRadius);
            Texture2D buttonTex = MakeRoundedTex(2, 2, new Color(0.25f, 0.25f, 0.25f), ButtonRadius);
            Texture2D buttonHoverTex = MakeRoundedTex(2, 2, new Color(0.35f, 0.35f, 0.35f), ButtonRadius);
            Texture2D buttonActiveTex = MakeRoundedTex(2, 2, new Color(0.18f, 0.18f, 0.18f), ButtonRadius);
            Texture2D activeTabTex = MakeRoundedTex(2, 2, new Color(0.15f, 0.35f, 0.45f), ButtonRadius);
            Texture2D textFieldTex = MakeRoundedTex(2, 2, new Color(0.20f, 0.20f, 0.20f), TextFieldRadius);

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white, background = panelTex },
                padding = new RectOffset(15, 15, 30, 15),
                border = new RectOffset(10, 10, 10, 10)
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = textFieldTex, textColor = Color.white },
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(8, 8, 6, 6)
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = buttonTex },
                hover = { textColor = Color.white, background = buttonHoverTex },
                active = { textColor = Color.white, background = buttonActiveTex },
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(8, 8, 6, 6)
            };

            activeTabStyle = new GUIStyle(buttonStyle)
            {
                normal = { textColor = Color.cyan, background = activeTabTex }
            };
        }

        private static Texture2D MakeRoundedTex(int width, int height, Color col, int radius)
        {
            int size = Mathf.Max(width, height) * 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    bool inside =
                        (x >= radius && x < tex.width - radius) ||
                        (y >= radius && y < tex.height - radius) ||
                        (Mathf.Pow(x - radius, 2) + Mathf.Pow(y - radius, 2) <= radius * radius) ||
                        (Mathf.Pow(x - (tex.width - radius - 1), 2) + Mathf.Pow(y - radius, 2) <= radius * radius) ||
                        (Mathf.Pow(x - radius, 2) + Mathf.Pow(y - (tex.height - radius - 1), 2) <= radius * radius) ||
                        (Mathf.Pow(x - (tex.width - radius - 1), 2) + Mathf.Pow(y - (tex.height - radius - 1), 2) <= radius * radius);

                    tex.SetPixel(x, y, inside ? col : transparent);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
