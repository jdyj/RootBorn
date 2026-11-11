using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 월드 화면(--world 맵). 걷기·충돌·문은 Core의 WorldSession이 계산하고, 여기서는 입력 방향을 넘기고 결과를 그리기만 한다.
    /// 그리는 순서: 타일 층(모두 캐릭터 아래) → 발 위치로 정렬된 가구와 캐릭터. 배율은 맵이 정한 보이는 높이에 가장 가까운 정수,
    /// 카메라는 캐릭터를 따라가되 맵 밖을 비추지 않는다(맵이 화면보다 작으면 가운데).
    /// </summary>
    public sealed class WorldScreen : IDisposable
    {
        // Premade 캐릭터 시트: 32x64 칸, 1행 서 있기, 2행 걷기, 방향마다 6칸(오른쪽, 위, 왼쪽, 아래).
        private const int FrameW = 32, FrameH = 64, FramesPerDirection = 6, StandRow = 1, WalkRow = 2;
        private const float WalkFramesPerSecond = 10f;

        private readonly GraphicsDevice _device;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Texture2D _character;
        private readonly Color _background = new Color(24, 22, 30);

        public readonly WorldMapLoader Maps;
        public readonly WorldSession Session;

        public LoadedMap Current => Maps.LoadMap(Session.Map.Id);
        public int Zoom { get; private set; } = 1;
        /// <summary>마지막으로 그린 화면의 왼쪽 위가 맵의 어느 픽셀이었나(맵이 작으면 음수).</summary>
        public Vector2 CameraOffset { get; private set; }

        public WorldScreen(GraphicsDevice device, HostOptions options)
        {
            _device = device;
            string catalog = options.WorldMapsPath ?? Path.Combine(options.RepoRoot, WorldCatalog.DefaultRelativePath);
            Maps = new WorldMapLoader(options.RepoRoot, WorldCatalog.Load(catalog));
            MapEntry start = Maps.Catalog.Find(options.World) ?? throw new InvalidOperationException("맵 목록에 없는 맵: " + options.World);
            string spawn = options.WorldSpawn;
            if (string.IsNullOrEmpty(spawn)) foreach (string id in start.Spawns.Keys) { spawn = id; break; }
            Session = new WorldSession(Maps, Maps.Catalog.Walker);
            if (!Session.Enter(start.Id, spawn)) throw new InvalidOperationException(Session.LastProblem);
            foreach (string problem in Current.Problems) Console.Error.WriteLine("[월드] " + start.Id + ": " + problem);
            _character = Load(options.CharacterSheetPath);
        }

        private Texture2D Load(string path)
        {
            if (_textures.TryGetValue(path, out Texture2D texture)) return texture;
            using (FileStream stream = File.OpenRead(path)) texture = Texture2D.FromStream(_device, stream); // PNG를 그 자리에서 읽는다
            _textures[path] = texture;
            return texture;
        }

        /// <summary>한 프레임: 입력 방향과 지난 시간으로 걷고, 문을 지나면 맵이 바뀐다.</summary>
        public void Update(int dx, int dy, float dt)
        {
            string before = Session.Map.Id;
            Session.Update(dx, dy, dt);
            if (Session.Map.Id != before)
                foreach (string problem in Current.Problems) Console.Error.WriteLine("[월드] " + Session.Map.Id + ": " + problem);
        }

        public void Draw(SpriteBatch batch, int screenWidth, int screenHeight)
        {
            LoadedMap map = Current;
            MapGrid grid = map.World.Grid;
            Zoom = WorldCamera.Zoom(screenHeight, map.Entry.ViewCells, grid.CellSize);
            Walker walker = Session.Walker;
            float viewW = screenWidth / (float)Zoom, viewH = screenHeight / (float)Zoom;
            var offset = new Vector2(WorldCamera.Offset(walker.X, grid.PixelWidth, viewW), WorldCamera.Offset(walker.Y, grid.PixelHeight, viewH));
            CameraOffset = offset;

            _device.Clear(_background);
            Matrix view = Matrix.CreateTranslation(-offset.X, -offset.Y, 0f) * Matrix.CreateScale(Zoom, Zoom, 1f);
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, view);
            foreach (SpriteDraw s in map.Picture.Under) DrawSprite(batch, s);
            List<SpriteDraw> sorted = map.Picture.Sorted;
            int actorAt = map.Picture.ActorIndex(walker.Y);
            for (int i = 0; i < actorAt; i++) DrawSprite(batch, sorted[i]);
            DrawCharacter(batch, walker);
            for (int i = actorAt; i < sorted.Count; i++) DrawSprite(batch, sorted[i]);
            batch.End();
        }

        private void DrawSprite(SpriteBatch batch, SpriteDraw s)
        {
            Texture2D texture = Load(s.Texture);
            var effects = SpriteEffects.None;
            if ((s.Flip & 1) != 0) effects |= SpriteEffects.FlipHorizontally;
            if ((s.Flip & 2) != 0) effects |= SpriteEffects.FlipVertically;
            var dest = new Rectangle((int)MathF.Round(s.X), (int)MathF.Round(s.Y), (int)MathF.Round(s.W), (int)MathF.Round(s.H));
            batch.Draw(texture, dest, new Rectangle(s.SrcX, s.SrcY, s.SrcW, s.SrcH), Color.White * s.Alpha, 0f, Vector2.Zero, effects, 0f);
        }

        /// <summary>캐릭터 그림은 발(몸 상자 아래 가운데)에 맞춰 선다 — Unity 판 FeetOffsetY -0.5칸.</summary>
        private void DrawCharacter(SpriteBatch batch, Walker walker)
        {
            int direction = walker.Facing == Facing.Right ? 0 : walker.Facing == Facing.Up ? 1 : walker.Facing == Facing.Left ? 2 : 3;
            int frame = walker.Moving ? (int)(walker.WalkSeconds * WalkFramesPerSecond) % FramesPerDirection : 0;
            var source = new Rectangle((direction * FramesPerDirection + frame) * FrameW, (walker.Moving ? WalkRow : StandRow) * FrameH, FrameW, FrameH);
            AreaRect body = walker.Box;
            var position = new Vector2(MathF.Round(body.CenterX - FrameW / 2f), MathF.Round(body.Bottom - FrameH));
            batch.Draw(_character, position, source, Color.White);
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _textures.Values) texture.Dispose();
            _textures.Clear();
        }
    }
}
