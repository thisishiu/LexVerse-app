using System;
using System.IO;

namespace LexVerse.Overlay.Services
{
    internal static class Logger
    {
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "LexVerseOverlay.log");

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
