using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 조합 그리기의 엔진 쪽: 조각 시트 PNG → 픽셀(<see cref="PieceImage"/>, <c>Texture2D.GetData</c> 한 번, 시트마다 캐시)과
    /// 조합한 방의 픽셀 → 텍스처 한 장. 픽셀 꼴이 <c>Color.PackedValue</c>와 같아 그대로 올린다. 조합 자체는 World(RoomTileCompositor)가 한다.
    /// </summary>
    public sealed class ComposedRoomTextures : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly string _repoRoot;
        private readonly Dictionary<string, PieceImage> _sheets = new Dictionary<string, PieceImage>(StringComparer.Ordinal);

        /// <param name="repoRoot">시트 경로(정의의 sheet, 저장소 뿌리 기준)의 뿌리.</param>
        public ComposedRoomTextures(GraphicsDevice device, string repoRoot)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
        }

        /// <summary>읽어 둔 시트 수(시험·진단용).</summary>
        public int SheetCount => _sheets.Count;

        /// <summary>시트 PNG의 픽셀. 처음 부를 때 PNG를 읽어 GPU에 올리지 않고 픽셀만 남긴다(텍스처는 바로 버린다). 없으면 null(조합이 문제로 알린다).</summary>
        public PieceImage Sheet(string sheet)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            if (_sheets.TryGetValue(sheet, out PieceImage image)) return image;
            string path = Path.GetFullPath(Path.Combine(_repoRoot, sheet));
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                using Texture2D texture = Texture2D.FromStream(_device, stream);
                var pixels = new uint[texture.Width * texture.Height];
                texture.GetData(pixels);
                image = new PieceImage(texture.Width, texture.Height, pixels);
            }
            _sheets[sheet] = image;
            return image;
        }

        /// <summary>조합한 방의 묶음을 텍스처로 올린다(맵에 처음 들어설 때 한 번). 픽셀이 없으면(상한 초과) null.</summary>
        public static Texture2D Upload(GraphicsDevice device, ComposedRoom room)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (room.TooLarge || room.Width == 0 || room.Height == 0) return null;
            var texture = new Texture2D(device, room.Width, room.Height, false, SurfaceFormat.Color);
            texture.SetData(room.Pixels);
            return texture;
        }

        /// <summary>읽어 둔 시트 픽셀을 버린다(텍스처는 이미 버렸다).</summary>
        public void Dispose() => _sheets.Clear();
    }
}
