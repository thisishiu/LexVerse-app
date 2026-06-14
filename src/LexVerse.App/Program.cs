using System;
using System.Threading;

namespace LexVerse.App
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using var controller = new LexVerse.Overlay.Services.OverlayController();
            controller.StatusChanged += (s, st) => Console.WriteLine($"Overlay status: {st}");
            controller.Start();

            // demo: push 3 items, then accept console commands
            controller.UpdateItems(new[] {
                new LexVerse.Overlay.Models.OverlayTextItem { Text = "Xin chào thế giới", X = 100, Y = 100, FontSize = 28 },
                new LexVerse.Overlay.Models.OverlayTextItem { Text = "Đây là một quyển sách", X = 300, Y = 160, FontSize = 22 },
                new LexVerse.Overlay.Models.OverlayTextItem { Text = "LexVerse Overlay Demo", X = 200, Y = 240, FontSize = 26 }
            });

            Console.WriteLine("LexVerse.App started. Type 'help' for commands.");
            while (true)
            {
                Console.Write("> ");
                var cmd = Console.ReadLine();
                if (string.IsNullOrEmpty(cmd)) continue;
                var parts = cmd.Split(' ', 2);
                var c = parts[0].ToLowerInvariant();
                if (c == "exit" || c == "quit") break;
                if (c == "hide") controller.Hide();
                else if (c == "show") controller.Show();
                else if (c == "clear") controller.Clear();
                else if (c == "clickthrough") controller.ToggleClickThrough(true);
                else if (c == "noclick") controller.ToggleClickThrough(false);
                else if (c == "update")
                {
                    controller.UpdateItems(new[] {
                        new LexVerse.Overlay.Models.OverlayTextItem { Text = "Updated @ " + DateTime.Now.ToString("HH:mm:ss"), X = 150, Y = 200, FontSize = 24 }
                    });
                }
                else if (c == "help") Console.WriteLine("commands: show, hide, clear, update, clickthrough, noclick, exit");
            }

            controller.Stop();
        }
    }
}
