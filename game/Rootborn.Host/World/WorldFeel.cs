using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Core.World;
using Rootborn.Host.Input;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 월드의 조작감·연출(game/world/presentation.json): 부드러운 카메라, 마우스 왼클릭 행동, 문 열림 덮개와 이동 구역 페이드.
    /// 계산은 Rootborn.World(SmoothCamera, CharacterActionTimer, PointerFacing, WorldTransition)에 있고, 여기서는 WorldScreen의 한 프레임에 잇고 그린다.
    /// WorldScreen·HostGame은 이 파일을 부르는 줄만 갖는다. 매 프레임 새 객체를 만들지 않는다.
    /// </summary>
    public sealed class WorldFeel : IDisposable
    {
        private readonly GraphicsDevice _device;
        private Texture2D _pixel;
        private string _cameraMap;
        private float _cameraDt;
        private bool _wasDown;

        public readonly WorldPresentation Presentation;
        public readonly SmoothCamera Camera;
        public readonly CharacterActionTimer Action;
        public readonly WorldTransition Transition;

        /// <summary>월드가 받은 클릭 수(UI 위나 막힌 때의 클릭은 세지 않는다 — 시험용).</summary>
        public int WorldClicks { get; private set; }
        /// <summary>마지막 월드 클릭이 가리킨 맵 픽셀.</summary>
        public Vector2 LastClickPoint { get; private set; }

        public WorldFeel(GraphicsDevice device, WorldPresentation presentation)
        {
            _device = device;
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            Camera = new SmoothCamera(presentation.CameraSmoothSeconds);
            Action = new CharacterActionTimer(presentation.ActionSeconds, presentation.ActionFirstColumn, presentation.ActionColumnCount);
            Transition = new WorldTransition(presentation);
        }

        /// <summary>연출 동안 걷기를 막는가(데이터. Unity 판은 막지 않았다).</summary>
        public bool BlocksInput => Transition.BlocksInput;

        /// <summary>걷기를 한 프레임 돌린 직후(그림 칸을 정하기 전)에 부른다: 행동 중이면 누른 쪽을 계속 보고, 문 연출(진행도는 Core가 센다)과 카메라 시간을 맞춘다.</summary>
        public void AfterWalk(WorldSession session, MapEntry entry, float dt)
        {
            Action.Advance(dt);
            if (Action.Active) session.Walker.Face(Action.Facing);
            DoorArea opening = session.OpeningDoor;
            Transition.Show(opening, opening != null && IsArea(entry, opening), session.OpeningProgress);
            _cameraDt += Math.Max(0f, dt);
        }

        /// <summary>맵 목록의 이동 구역(areas)에서 온 문인가 — 맵 성질이다(맵 id로 가르지 않는다).</summary>
        private static bool IsArea(MapEntry entry, DoorArea door)
        {
            for (int i = 0; i < entry.Areas.Count; i++)
            {
                MapArea a = entry.Areas[i];
                if (a.Name == door.Target && a.Rect.X == door.Area.X && a.Rect.Y == door.Area.Y && a.Rect.W == door.Area.W && a.Rect.H == door.Area.H) return true;
            }
            return false;
        }

        /// <summary>그릴 때: 캐릭터를 따라가는 보이는 사각형. 맵이 바뀌었으면 바로 맞춘다.</summary>
        public AreaRect Follow(string mapId, float x, float y, AreaRect bounds, float viewW, float viewH)
        {
            if (mapId != _cameraMap) { _cameraMap = mapId; Camera.Snap(); }
            float dt = _cameraDt;
            _cameraDt = 0f;
            return Camera.Follow(x, y, bounds, viewW, viewH, dt);
        }

        /// <summary>다른 카메라(가구 배치)가 비추는 동안: 돌아오면 바로 맞춘다.</summary>
        public void CameraReleased()
        {
            Camera.Snap();
            _cameraDt = 0f;
        }

        /// <summary>
        /// 마우스 왼클릭(Unity PlayerController.BeginAttack): 막 누른 프레임이고, UI 위가 아니고(모달이 떠 있으면 막는 판이 UI다), 막히지 않았으면
        /// 누른 쪽을 바라보고 행동을 시작한다. true면 Host가 E와 같은 상호작용을 한다. 포인터는 창 픽셀, 월드는 같은 크기의 장면 버퍼에 그린다.
        /// </summary>
        public bool Click(PointerState pointer, bool overUi, bool blocked, WorldScreen world)
        {
            bool pressed = pointer.LeftDown && !_wasDown;
            _wasDown = pointer.LeftDown;
            if (!pressed || overUi || blocked || world.Scale <= 0f) return false;
            float x = world.CameraOffset.X + pointer.X / world.Scale, y = world.CameraOffset.Y + pointer.Y / world.Scale;
            Walker walker = world.Session.Walker;
            Facing facing = PointerFacing.From(x - walker.X, y - walker.Y, walker.Facing);
            walker.Face(facing);
            Action.Play(facing);
            LastClickPoint = new Vector2(x, y);
            WorldClicks++;
            return true;
        }

        /// <summary>월드를 다 그린 뒤(빛을 곱한 뒤): 열리는 문 위의 사각형(맵 좌표), 이동 구역의 화면 덮개(화면 좌표).</summary>
        public void DrawOverlays(SpriteBatch batch, Matrix view, int screenWidth, int screenHeight, int cellSize)
        {
            if (Transition.Kind == WorldTransitionKind.None) return;
            if (_pixel == null)
            {
                _pixel = new Texture2D(_device, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
            WorldPresentation p = Presentation;
            if (Transition.Kind == WorldTransitionKind.Door)
            {
                AreaRect door = Transition.Door.Area;
                float w = p.DoorWidthCells * cellSize * Transition.DoorWidthRatio, h = p.DoorHeightCells * cellSize;
                float bottom = door.CenterY - p.DoorLiftCells * cellSize;
                var rect = new Rectangle((int)MathF.Round(door.CenterX - w / 2f), (int)MathF.Round(bottom - h), Math.Max(1, (int)MathF.Round(w)), Math.Max(1, (int)MathF.Round(h)));
                int edge = Math.Max(1, (int)MathF.Round(p.DoorEdgeCells * cellSize));
                float alpha = Transition.DoorAlpha;
                batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, view);
                batch.Draw(_pixel, rect, ToColor(p.DoorEdge, alpha));
                if (rect.Width > edge * 2 && rect.Height > edge * 2)
                    batch.Draw(_pixel, new Rectangle(rect.X + edge, rect.Y + edge, rect.Width - edge * 2, rect.Height - edge * 2), ToColor(p.DoorFill, alpha));
                batch.End();
            }
            else
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, null);
                batch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), ToColor(p.AreaColor, Transition.FadeAlpha));
                batch.End();
            }
        }

        private static Color ToColor(LightColor c, float alpha) =>
            new Color(Math.Clamp(c.R, 0f, 1f), Math.Clamp(c.G, 0f, 1f), Math.Clamp(c.B, 0f, 1f), Math.Clamp(alpha, 0f, 1f));

        public void Dispose()
        {
            _pixel?.Dispose();
            _pixel = null;
        }
    }
}
