using System;
using System.IO;
using System.IO.Compression;
using Rootborn.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 시험용 PNG 읽기(엔진 없음): 8비트 RGBA·RGB, 비월, 줄 필터 0~4. 키트(Pillow)가 쓰는 꼴만 받고 나머지는 분명히 거절한다.
    /// 픽셀은 <see cref="PieceImage"/>와 같은 꼴(가장 낮은 바이트 R, 그다음 G, B, A — MonoGame Color.PackedValue 순서).
    /// </summary>
    internal static class PngPixels
    {
        public static PieceImage Read(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++)
                if (file[i] != signature[i]) throw new InvalidDataException(path + ": PNG가 아니다");
            int width = 0, height = 0, channels = 0;
            using var idat = new MemoryStream();
            for (int at = 8; at < file.Length;)
            {
                int length = BigEndian(file, at);
                string type = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);
                int data = at + 8;
                if (type == "IHDR")
                {
                    width = BigEndian(file, data);
                    height = BigEndian(file, data + 4);
                    byte depth = file[data + 8], color = file[data + 9], interlace = file[data + 12];
                    if (depth != 8 || interlace != 0 || (color != 6 && color != 2))
                        throw new InvalidDataException($"{path}: 이 시험 읽기는 8비트 RGBA·RGB 비월만 받는다(깊이 {depth}, 색 {color}, 비월 {interlace})");
                    channels = color == 6 ? 4 : 3;
                }
                else if (type == "IDAT") idat.Write(file, data, length);
                else if (type == "IEND") break;
                at = data + length + 4;
            }
            idat.Position = 0;
            int stride = width * channels;
            var raw = new byte[(stride + 1) * height];
            using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < raw.Length)
                {
                    int n = z.Read(raw, read, raw.Length - read);
                    if (n == 0) throw new InvalidDataException(path + ": 그림 자료가 모자란다");
                    read += n;
                }
            }
            var current = new byte[stride];
            var previous = new byte[stride];
            var pixels = new uint[width * height];
            for (int y = 0; y < height; y++)
            {
                int row = y * (stride + 1);
                byte filter = raw[row];
                for (int x = 0; x < stride; x++)
                {
                    int a = x >= channels ? current[x - channels] : 0, b = previous[x], c = x >= channels ? previous[x - channels] : 0;
                    int v = raw[row + 1 + x];
                    current[x] = (byte)(filter switch
                    {
                        0 => v,
                        1 => v + a,
                        2 => v + b,
                        3 => v + (a + b) / 2,
                        4 => v + Paeth(a, b, c),
                        _ => throw new InvalidDataException(path + ": 모르는 줄 필터 " + filter),
                    });
                }
                for (int x = 0; x < width; x++)
                {
                    int p = x * channels;
                    uint alpha = channels == 4 ? current[p + 3] : 255u;
                    pixels[y * width + x] = current[p] | ((uint)current[p + 1] << 8) | ((uint)current[p + 2] << 16) | (alpha << 24);
                }
                (previous, current) = (current, previous);
            }
            return new PieceImage(width, height, pixels);
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
    }
}
