using System;
using System.IO;
using System.Text;

namespace Rootborn.DataExport.Tests
{
    /// <summary>손으로 쓴 작은 Unity 프로젝트 조각을 임시 폴더에 만든다.</summary>
    public sealed class TempRepo : IDisposable
    {
        public const string TileScriptGuid = "0000000000000000e000000000000000";

        public string Root { get; }
        public string Out => Path.Combine(Root, "data");

        public TempRepo()
        {
            Root = Path.Combine(Path.GetTempPath(), "rb-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "Assets", "Data"));
        }

        public static string G(int n) => n.ToString("x32");

        public void Write(string relPath, string text)
        {
            string full = Path.Combine(Root, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        public void Meta(string relPath, string guid, string rest = "")
            => Write(relPath + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n" + rest);

        public void Script(string className, string guid)
        {
            Write("Assets/Scripts/" + className + ".cs", "// " + className + "\n");
            Meta("Assets/Scripts/" + className + ".cs", guid);
        }

        /// <summary>MonoBehaviour 문서 하나. body는 두 칸 들여쓴 필드 줄들.</summary>
        public static string Mono(string scriptGuid, string name, string body, string fileId = "11400000")
        {
            string script = scriptGuid == TileScriptGuid
                ? "{fileID: 13312, guid: " + scriptGuid + ", type: 0}"
                : "{fileID: 11500000, guid: " + scriptGuid + ", type: 3}";
            return "--- !u!114 &" + fileId + "\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_Enabled: 1\n"
                + "  m_Script: " + script + "\n  m_Name: " + name + "\n  m_EditorClassIdentifier: \n" + body;
        }

        public void Asset(string relPath, string guid, params string[] docs)
        {
            Write(relPath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Concat(docs));
            Meta(relPath, guid, "NativeFormatImporter:\n  mainObjectFileID: 11400000\n");
        }

        /// <summary>헤더만 있는 PNG. 변환기는 IHDR의 크기만 읽는다.</summary>
        public void Png(string relPath, int width, int height, string guid, string importer)
        {
            byte[] b = new byte[33];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(b, 0);
            b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16); b[18] = (byte)(width >> 8); b[19] = (byte)width;
            b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16); b[22] = (byte)(height >> 8); b[23] = (byte)height;
            string full = Path.Combine(Root, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, b);
            Meta(relPath, guid, importer);
        }

        public string Read(string relOut) => File.ReadAllText(Path.Combine(Out, relOut));

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch (IOException) { }
        }
    }
}
