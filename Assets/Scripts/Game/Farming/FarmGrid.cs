using System.Collections.Generic;
using Rootborn.Game.Crops;
using Rootborn.Game.Time;
using Rootborn.Game.Tools;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Farming
{
    /// <summary>
    /// 농장 셀 단위 토양/작물 상태 관리자. 단일 Tilemap 위에 tile swap + per-cell state dictionary.
    /// FarmAutoFiller 가 [FarmGrid] GameObject 에 부착하고 Ground Tilemap 참조 주입.
    /// 일일 자정 (GameClock.OnDayRolled) 에 물/비료 만료 처리.
    /// </summary>
    public sealed class FarmGrid : MonoBehaviour
    {
        [SerializeField] private Tilemap _ground;
        [SerializeField] private TileBase _tilledTile;
        [SerializeField] private TileBase _grassTile; // 사용자 결정 #2 — 수확 후 Tilled 유지이므로 현재 미사용. 후속 PR 에서 grass 복귀 옵션 시 참조.
        [SerializeField] private CropPlot _plotPrefab;
        [SerializeField] private Transform _cropsParent;

        private readonly Dictionary<Vector3Int, TileSoilState> _cells = new Dictionary<Vector3Int, TileSoilState>(64);
        private GameClock _clock;
        private bool _subscribed;

        public static FarmGrid Instance { get; private set; }
        public Tilemap GroundTilemap => _ground;
        public TileBase TilledTile => _tilledTile;

        // 테스트/외부 주입용 setter (Inspector 와이어링 우회).
        public void Configure(Tilemap ground, TileBase tilledTile, CropPlot plotPrefab, Transform cropsParent)
        {
            _ground = ground;
            _tilledTile = tilledTile;
            _plotPrefab = plotPrefab;
            _cropsParent = cropsParent;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (_subscribed && _clock != null)
            {
                _clock.OnDayRolled -= OnDayRolled;
                _subscribed = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _clock = GameClock.Instance;
            if (_clock == null) return;
            _clock.OnDayRolled += OnDayRolled;
            _subscribed = true;
        }

        private void Update()
        {
            // GameClock 가 늦게 깨어나는 경우 대비 — 첫 Update 에서도 구독 시도.
            if (!_subscribed) TrySubscribe();

            float dt = UnityEngine.Time.deltaTime;
            int day = _clock != null ? _clock.Day : 1;
            // dictionary 를 수정하지 않으므로 직접 enumerate 가능.
            foreach (var kv in _cells)
            {
                var st = kv.Value;
                if (st.Plot == null) continue;
                float fert = GetFertilizerMultiplierFromState(in st, day);
                st.Plot.Tick(dt, st.WaterLevel01, false, fert);
            }
        }

        public Vector3Int WorldToCell(Vector3 world)
        {
            return _ground != null ? _ground.WorldToCell(world) : Vector3Int.zero;
        }

        public Vector3 CellCenterWorld(Vector3Int cell)
        {
            return _ground != null ? _ground.GetCellCenterWorld(cell) : (Vector3)cell;
        }

        public bool IsTilled(Vector3Int cell)
        {
            return _cells.TryGetValue(cell, out var st) && st.IsTilled;
        }

        public bool HasPlot(Vector3Int cell)
        {
            return _cells.TryGetValue(cell, out var st) && st.Plot != null;
        }

        public CropPlot GetPlot(Vector3Int cell)
        {
            return _cells.TryGetValue(cell, out var st) ? st.Plot : null;
        }

        public float GetWaterLevel01(Vector3Int cell)
        {
            return _cells.TryGetValue(cell, out var st) ? st.WaterLevel01 : 0f;
        }

        public float GetFertilizerMultiplier(Vector3Int cell, int currentDay)
        {
            if (!_cells.TryGetValue(cell, out var st)) return 1f;
            return GetFertilizerMultiplierFromState(in st, currentDay);
        }

        private static float GetFertilizerMultiplierFromState(in TileSoilState st, int currentDay)
        {
            if (st.FertilizerExpiresOnDay > 0 && currentDay > st.FertilizerExpiresOnDay) return 1f;
            return st.FertilizerMultiplier <= 0f ? 1f : st.FertilizerMultiplier;
        }

        public void Till(Vector3Int cell)
        {
            if (!_cells.TryGetValue(cell, out var st))
            {
                st = TileSoilState.CreateUntilled();
            }
            if (st.IsTilled)
            {
                _cells[cell] = st; // idempotent
                return;
            }
            st.IsTilled = true;
            _cells[cell] = st;
            if (_ground != null && _tilledTile != null)
            {
                _ground.SetTile(cell, _tilledTile);
            }
            else if (_ground != null)
            {
                // TODO(asset): 전용 tilled-soil 타일 sprite 필요. fallback — 기존 타일에 어두운 tint.
                _ground.SetColor(cell, new Color(0.45f, 0.30f, 0.18f, 1f));
            }
        }

        public bool TryPlant(Vector3Int cell, CropDefinition crop)
        {
            if (crop == null) return false;
            if (!_cells.TryGetValue(cell, out var st) || !st.IsTilled) return false;
            if (st.Plot != null) return false;

            CropPlot plot;
            if (_plotPrefab != null)
            {
                plot = Instantiate(_plotPrefab, _cropsParent);
            }
            else
            {
                var go = new GameObject($"CropPlot_{cell.x}_{cell.y}");
                if (_cropsParent != null) go.transform.SetParent(_cropsParent, false);
                plot = go.AddComponent<CropPlot>();
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -5;
                // CropPlot 의 _renderer 는 [SerializeField] private — 외부 주입 위해 GetComponent 후 reflection 회피용
                // Plant() 호출 시 UpdateSprite 가 _renderer 를 사용하므로, prefab 없는 경우는 reflection 없이 프리뷰만 안 됨.
                // 대신 CropPlot 자체에 GetComponent<SpriteRenderer>() fallback 을 추가하는 게 cleaner — 하지만 현재 PR 스코프를 늘리지 않기 위해
                // 여기서 SerializedObject 없이 동적 주입은 생략 (테스트는 prefab 으로 대체 가능, MVP 는 fallback 색).
            }
            plot.transform.position = CellCenterWorld(cell);
            plot.Cell = cell;
            plot.Plant(crop);

            st.Plot = plot;
            _cells[cell] = st;
            return true;
        }

        public void Water(Vector3Int cell, int currentDay, float level01)
        {
            if (!_cells.TryGetValue(cell, out var st) || !st.IsTilled) return;
            st.LastWateredDay = currentDay;
            st.WaterLevel01 = Mathf.Clamp01(level01);
            _cells[cell] = st;
            if (_ground != null)
            {
                // TODO(asset): wet-soil sprite. fallback — tint 어둡게.
                _ground.SetColor(cell, new Color(0.6f, 0.5f, 0.4f, 1f));
            }
        }

        public void Fertilize(Vector3Int cell, int currentDay, float multiplier, int durationDays)
        {
            if (!_cells.TryGetValue(cell, out var st) || !st.IsTilled) return;
            st.LastFertilizedDay = currentDay;
            st.FertilizerMultiplier = Mathf.Max(1f, multiplier);
            st.FertilizerExpiresOnDay = currentDay + Mathf.Max(1, durationDays);
            _cells[cell] = st;
            // TODO(asset): fertilized-soil overlay sprite. fallback — 추가 시각화 없음 (현재).
        }

        public bool TryHarvest(Vector3Int cell, ToolDefinition tool, out CropDefinition harvested, out int yield)
        {
            harvested = null;
            yield = 0;
            if (!_cells.TryGetValue(cell, out var st)) return false;
            var plot = st.Plot;
            if (plot == null || !plot.IsHarvestable) return false;
            if (!plot.Crop.CanHarvestWith(tool)) return false;

            harvested = plot.Crop;
            yield = harvested.RollYield(_harvestRng);

            // 사용자 결정 #2 — 수확 후 Tilled 유지. Edit mode 호환을 위해 분기.
            if (Application.isPlaying)
                Destroy(plot.gameObject);
            else
                DestroyImmediate(plot.gameObject);
            st.Plot = null;
            _cells[cell] = st;
            return true;
        }

        private static readonly System.Random _harvestRng = new System.Random();

        private void OnDayRolled(int newDay)
        {
            // dictionary key 만 enumerate 하고 값을 다시 대입하므로 collection 변경 안 함.
            var keys = new List<Vector3Int>(_cells.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                var st = _cells[k];
                // 어제(=newDay-1) 물을 안 줬으면 오늘 물 0.
                if (st.LastWateredDay < newDay - 1)
                {
                    st.WaterLevel01 = 0f;
                    if (_ground != null && st.IsTilled && _tilledTile != null)
                    {
                        _ground.SetColor(k, Color.white); // wet tint 제거
                    }
                }
                if (st.FertilizerExpiresOnDay > 0 && newDay > st.FertilizerExpiresOnDay)
                {
                    st.FertilizerMultiplier = 1f;
                }
                _cells[k] = st;
            }
        }

        // 테스트 헬퍼 — 외부에서 day 를 강제 주입할 때 사용 (GameClock 인스턴스 없이 단위 테스트).
        public void TestForceDayRoll(int newDay)
        {
            OnDayRolled(newDay);
        }
    }
}
