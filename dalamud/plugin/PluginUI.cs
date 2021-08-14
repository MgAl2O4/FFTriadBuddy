using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace TriadBuddyPlugin
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private GameUI gameUI;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        public PluginUI(GameUI gameUI)
        {
            this.gameUI = gameUI;
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            // TODO: replace with simple label placed on minigame board when solver reading data correctly

            ImGui.SetNextWindowSize(new Vector2(375, 100), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 100), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Debug me", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (gameUI.currentState != null)
                {
                    ImGui.Text($"Move: {gameUI.currentState.move}");
                    ImGui.Text($"Rules: {string.Join(", ", gameUI.currentState.rules)}, red:{string.Join(",", gameUI.currentState.redPlayerDesc)}");

                    ImGui.Separator();
                    DrawCardArray(gameUI.currentState.blueDeck, "Blue");

                    ImGui.Separator();
                    DrawCardArray(gameUI.currentState.redDeck, "Red");

                    ImGui.Separator();
                    DrawCardArray(gameUI.currentState.board, "Board");
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.9f, 0.2f, 0.2f, 1), "Game is not active");
                }

                if (ImGui.Button("Memory snapshot"))
                {
                    DebugSnapshot();
                }
            }
            ImGui.End();
        }

        private string GetCardDesc(GameUI.State.Card card)
        {
            if (card.numU == 0)
                return "hidden";

            string lockDesc = card.isLocked ? " [LOCKED] " : "";
            return $"[{card.numU:X}-{card.numL:X}-{card.numD:X}-{card.numR:X}]{lockDesc}, o:{card.owner}, t:{card.type}, r:{card.rarity}, tex:{card.texturePath}";
        }

        private void DrawCardArray(GameUI.State.Card[] cards, string prefix)
        {
            for (int idx = 0; idx < cards.Length; idx++)
            {
                if (cards[idx].isPresent)
                {
                    ImGui.Text($"{prefix}[{idx}]: {GetCardDesc(cards[idx])}");
                }
            }
        }

        private void DebugSnapshot()
        {
            // temporary, to be removed once all screen data is loaded correctly

            try
            {
                string fname = string.Format(@"D:\temp\snap\{0:00}{1:00}{2:00}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
                string fnameLog = fname + ".log";
                string fnameJpg = fname + ".jpg";

                if (File.Exists(fnameLog)) { File.Delete(fnameLog); }
                if (File.Exists(fnameJpg)) { File.Delete(fnameJpg); }

                if (gameUI.addonPtr != IntPtr.Zero)
                {
                    using (var file = File.Create(fnameLog))
                    {
                        int size = Marshal.SizeOf(typeof(AddonTripleTriad));
                        byte[] memoryBlock = new byte[size];
                        Marshal.Copy(gameUI.addonPtr, memoryBlock, 0, memoryBlock.Length);
                        file.Write(memoryBlock, 0, memoryBlock.Length);
                        file.Close();
                    }

                    PluginLog.Log("saved: {0}", fname);
                }

                var screen1Size = new Size(2560, 1440);
                var screen2Size = new Size(1920, 1080);
                var bitmap = new Bitmap(screen2Size.Width, screen2Size.Height);

                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(screen1Size.Width, screen1Size.Height - screen2Size.Height, 0, 0, screen2Size);
                }

                var resizedBitmap = new Bitmap(bitmap, new Size(screen2Size.Width / 2, screen2Size.Height / 2));
                resizedBitmap.Save(fnameJpg);

                bitmap.Dispose();
                resizedBitmap.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.LogError(ex, "oops!");
            }
        }
    }
}
