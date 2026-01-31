using Godot;
using System;
using System.IO;

namespace AceManager.Core
{
    public static class SaveManager
    {
        private const string SaveFolder = "user://saves/";

        public static void SaveGame(SaveData data, string slotName)
        {
            if (!DirAccess.DirExistsAbsolute(SaveFolder))
            {
                DirAccess.MakeDirRecursiveAbsolute(SaveFolder);
            }

            string path = $"{SaveFolder}{slotName}.trehs";
            Error err = ResourceSaver.Save(data, path);

            if (err == Error.Ok)
            {
                GD.Print($"Game saved successfully to {path}");
            }
            else
            {
                GD.PrintErr($"Failed to save game: {err}");
            }
        }

        public static SaveData LoadGame(string slotName)
        {
            string path = $"{SaveFolder}{slotName}.trehs";
            if (!Godot.FileAccess.FileExists(path))
            {
                GD.PrintErr($"Save file not found: {path}");
                return null;
            }

            var data = ResourceLoader.Load<SaveData>(path);
            if (data != null)
            {
                GD.Print($"Game loaded successfully from {path}");
            }
            return data;
        }

        public static string[] GetSaveSlots()
        {
            if (!DirAccess.DirExistsAbsolute(SaveFolder)) return Array.Empty<string>();
            return DirAccess.GetFilesAt(SaveFolder);
        }
    }
}
