using System.IO;
using UnityEngine;

namespace Rootborn.Game.Save
{
    public sealed class SaveService
    {
        private readonly string _slot;
        private readonly string _dir;

        public SaveService(string slot)
        {
            _slot = string.IsNullOrEmpty(slot) ? "default" : slot;
            _dir = Path.Combine(Application.persistentDataPath, "saves", _slot);
            if (!System.IO.Directory.Exists(_dir))
            {
                System.IO.Directory.CreateDirectory(_dir);
            }
        }

        public string Slot => _slot;
        public string DirectoryPath => _dir;

        public void WriteJson(string fileName, string json)
        {
            var path = Path.Combine(_dir, fileName);
            File.WriteAllText(path, json);
        }

        public string ReadJson(string fileName)
        {
            var path = Path.Combine(_dir, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
