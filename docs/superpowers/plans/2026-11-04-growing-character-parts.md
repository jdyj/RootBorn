# 성장하는 캐릭터 파츠 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strix Halo의 ComfyUI로 3단계 × 3방향 베이스 파츠, 머리카락 3종, 단계 전용 옷 5벌을 만들어 조합 가능한 아트 팩으로 검수받는다.

**Architecture:** 규격을 `spec.json` 하나에 고정하고, 규격에서 가이드 시트를 렌더해 Qwen Image Edit에 입력한다. 생성 결과는 격자 기반 후처리와 자동 검사를 거쳐 아틀라스와 pivot JSON이 되고, 사용자 체크포인트를 통과한 그림만 다음 생성의 입력이 된다.

**Tech Stack:** Python 3 + Pillow + NumPy + pytest, ComfyUI 0.3.76 HTTP API, Qwen Image Edit 2511, SSH.

**Spec:** `docs/superpowers/specs/2026-11-04-growing-character-parts-design.md`

## Global Constraints

- 작업 폴더 `PACK = output/assetpacks/character-parts-v1-20261104/`, FEVM `/home/jdyj/farmer-art/character-parts-v1-20261104/`. `Assets/`, 씬, 세이브는 건드리지 않는다.
- 단계별 키: 놀이터 32, 학교 36, 졸업 후 40px. 머리 16px 공통. PPU 32. 알파는 0 또는 255만.
- 몸통 베이스는 모든 단계에서 민소매 속옷과 반바지를 입는다. 벗은 몸통을 생성하지 않는다. 어긋난 출력은 저장 전에 폐기하고 검수 시트에 넣지 않는다.
- SPUM 픽셀을 입력이나 참조로 쓰지 않는다.
- ComfyUI는 `http://127.0.0.1:8188`(FEVM 내부)를 쓴다. 제출 전 `/queue`가 비었는지 확인하고, 큐 조작이나 재시작을 하지 않는다. FEVM 접속은 `~/.codex/AGENTS.md`의 명령을 그대로 쓴다.
- 자동 보정은 조각당 불투명 픽셀의 5% 이하. 같은 조각 3회 실패 시 멈춘다.
- 로컬 Python에 pytest가 없다. `PACK/.venv`를 만들어 pytest, Pillow, NumPy를 설치하고 전역 환경은 바꾸지 않는다.
- 이 저장소 규칙상 사용자가 요청할 때만 커밋한다. 각 태스크 끝에 커밋하지 않는다.

## Review Focus

- 프롬프트 조립 중 금지 표현이 positive에 들어가는 경우: 제출 전에 예외로 막혀야 한다. Task 2 `test_positive_rejects_forbidden_terms`.
- Qwen이 가이드와 다른 크기·개수의 조각을 낸 경우: 조용히 잘리지 않고 실패로 기록돼야 한다. Task 3 `test_cut_fails_when_part_missing`.
- 큐가 다른 작업으로 차 있는 경우: 제출하지 않고 대기하거나 멈춰야 한다. Task 2 `test_submit_refuses_busy_queue`.
- 옆모습 반전 시 겹침 순서와 pivot이 어긋나는 경우: 조합 결과에 틈이 없어야 한다. Task 4 `test_flipped_side_has_no_gap`.
- 짧은 옷을 입었을 때 구간 밖 맨살까지 덮으라고 잘못 판정하는 경우: 평상복이 검사를 통과해야 한다. Task 3 `test_tshirt_passes_coverage_with_bare_forearm`.

---

### Task 1: 규격과 가이드 시트

**Files:**
- Create: `PACK/tools/spec.json`, `PACK/tools/guides.py`
- Test: `PACK/tools/tests/test_guides.py`

**Interfaces:**
- Produces: `load_spec(path) -> dict`, `part_rects(spec, stage, view) -> dict[str, tuple[x,y,w,h]]`, `render_guide(spec, stage, view, scale=16) -> PIL.Image`.
- `spec.json` 키: `stages`(playground/school/adult의 height·torso·legs), `views`(front/back/side), `parts`(조각 이름과 부위 색), `pivots`, `coverage`(옷별 구간 비율), `layer_order`(방향별).

- [ ] **Step 1: 실패 테스트 작성**

```python
def test_stage_heights(): assert [total_height(spec, s) for s in ("playground","school","adult")] == [32,36,40]
def test_head_same_across_stages(): assert {part_rects(spec,s,"front")["head"][2:] for s in stages} == {(16,16)}
def test_guide_parts_do_not_touch(): # 조각 간 최소 1셀 여백
def test_guide_is_integer_scale(): assert render_guide(spec,"playground","front").width % 16 == 0
```

- [ ] **Step 2: `pytest PACK/tools/tests/test_guides.py -v`로 실패 확인**
- [ ] **Step 3: `guides.py`와 `spec.json` 구현.** 설계 §5의 수치와 슬롯을 옮기고 관절 겹침 2px을 pivot에 반영한다. 조각은 흰 배경에 부위색으로 떨어뜨려 배치한다.
- [ ] **Step 4: 테스트 통과 확인**, 놀이터 앞 가이드 PNG를 직접 열어 확인한다.

### Task 2: 프롬프트와 ComfyUI 제출

**Files:**
- Create: `PACK/tools/prompts.py`, `PACK/tools/comfy_job.py`
- Test: `PACK/tools/tests/test_prompts.py`, `PACK/tools/tests/test_comfy_job.py`

**Interfaces:**
- Consumes: Task 1의 `render_guide`.
- Produces: `build_prompt(kind, stage, view, item=None) -> (positive, negative)`, `build_graph(positive, negative, images, seed, steps=20, prefix) -> dict`, `submit(graph, http) -> prompt_id`, `fetch(prompt_id, out_dir, http) -> dict`(raw, history, 소요 시간, 해시 기록).

- [ ] **Step 1: 실패 테스트 작성**

```python
def test_prompt_contains_stage_and_view()
def test_negative_always_has_safety_terms(): assert "nude" in neg and "bare torso" in neg
def test_positive_rejects_forbidden_terms(): # item에 "naked" 전달 시 ValueError
def test_base_prompts_state_undershirt_and_shorts()
def test_graph_uses_qwen_models(): # 설계 §6의 세 모델 파일명
def test_submit_refuses_busy_queue(): # 가짜 http의 queue_running 비어 있지 않으면 BusyQueue 예외, POST 없음
```

- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현.** 프롬프트 문구는 설계 §7을 그대로 쓴다. 그래프는 `output/design-comfy-20261031/town-alley-exploration-crate-v1-history.json`의 Qwen 노드 구성을 기준으로 입력만 바꾼다. FEVM 호출은 SSH 경유 스크립트로 한다(`output/design-comfy-20261031/fetch_student_activity_front.py` 방식).
- [ ] **Step 4: 통과 확인**

### Task 3: 후처리와 자동 검사

**Files:**
- Create: `PACK/tools/postprocess.py`, `PACK/tools/checks.py`
- Test: `PACK/tools/tests/test_postprocess.py`, `PACK/tools/tests/test_checks.py`

**Interfaces:**
- Consumes: Task 1의 `part_rects`, `spec.json`.
- Produces: `cut_parts(raw, spec, stage, view) -> dict[str, Image]`, `to_pixels(part, size, palette) -> Image`, `repair(part, mask) -> (Image, list[coords])`, `check_alpha(img)`, `check_coverage(cloth, base, declared) -> list[str]`, `check_repair_budget(part, coords) -> bool`.

- [ ] **Step 1: 합성 이미지로 실패 테스트 작성**

```python
def test_cut_fails_when_part_missing(): # raises PartMissing, 빈 조각을 내지 않는다
def test_pixels_alpha_is_binary()
def test_tshirt_passes_coverage_with_bare_forearm()
def test_long_sleeve_fails_when_arm_pokes_out()
def test_repair_over_5_percent_rejected()
```

- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현.** 배경 제거는 흰 배경 기준 플러드필로 시작하고, 부족하면 FEVM의 BiRefNet을 쓴다. 팔레트는 `Assets/Art/StudentLifePixels`에서 측정한 색 수를 상한으로 한다.
- [ ] **Step 4: 통과 확인**

### Task 4: 조합과 미리보기

**Files:**
- Create: `PACK/tools/compose.py`
- Test: `PACK/tools/tests/test_compose.py`

**Interfaces:**
- Consumes: `spec.json`의 `layer_order`, `pivots`.
- Produces: `compose(stage, view, outfit, hair, pose=None) -> Image`, `walk_frames(stage, view, outfit, hair) -> list[Image]`, `build_review_sheet(...)`, `build_matrix(...)`, `side_by_side_with_game_art(image) -> Image`.

- [ ] **Step 1: 합성 파츠로 실패 테스트 작성**

```python
def test_layers_follow_order()
def test_walk_frames_have_no_gap_pixels(): # 몸 영역 내부 투명 픽셀 0
def test_flipped_side_has_no_gap()
def test_clothing_does_not_move_body(): # 옷 유무로 베이스 조각 좌표 불변
```

- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현.** 걷기는 다리와 팔을 pivot 기준 ±1~2px 이동하는 검수용 4프레임이다. 게임 아트 비교에는 기존 학생 스프라이트와 `Assets/Art/SchoolPixels/sprites/door.png`를 같은 배율로 놓는다.
- [ ] **Step 4: 통과 확인**

### Task 5: 체크포인트 1 — 놀이터 앞모습

**Files:**
- Create: `PACK/prompts/`, `PACK/workflows/`, `PACK/raw/`, `PACK/parts/playground/front/`, `PACK/previews/checkpoint-1.png`

- [ ] **Step 1: 접속 확인.** `"whoami; hostname"`이 `jdyj`, `jdyj-FAEX9`이고 `/queue`가 비었는지 확인한다.
- [ ] **Step 2: 시드 2개로 생성.** 스타일 참조는 `Assets/Art/StudentLifePixels/Sprites`에서 사람 없는 소품 1장을 고른다.
- [ ] **Step 3: 원본을 직접 보고 안전 제약과 가이드 일치를 확인한다.** 어긋나면 폐기하고 시드나 프롬프트를 바꾼다(3회 제한).
- [ ] **Step 4: 후처리, 자동 검사, 게임 아트 비교 시트를 만든다.**
- [ ] **Step 5: 시트를 사용자에게 보여주고 멈춘다.** 승인 전에는 Task 6으로 넘어가지 않는다.

### Task 6: 체크포인트 2 — 베이스 전체

**Files:** `PACK/parts/{stage}/{view}/`, `PACK/previews/checkpoint-2.png`

- [ ] **Step 1: 승인된 놀이터 앞을 입력에 추가해 놀이터 뒤·옆을 생성한다.**
- [ ] **Step 2: 학교, 졸업 후 앞·뒤·옆을 차례로 생성한다.** 머리 조각은 놀이터 것을 재사용한다.
- [ ] **Step 3: 머리카락 3종을 앞·뒤·옆으로 생성한다.**
- [ ] **Step 4: 자동 검사와 3 × 3 시트를 만들고, 사용자 승인까지 멈춘다.**

### Task 7: 체크포인트 3 — 옷과 팩 완성

**Files:** `PACK/clothes/{stage}/{outfit}/{view}/`, `PACK/layer-order.json`, `PACK/spec.json`, `PACK/palette.png`, `PACK/previews/`, `PACK/README.md`

- [ ] **Step 1: 옷 5벌 × 3방향 15장을 생성한다.** 첫 장 생성 후 품질을 보고 나머지를 큐에 넣는다.
- [ ] **Step 2: 덮는 구간 검사와 보정 기록을 통과시킨다.**
- [ ] **Step 3: 조합 매트릭스와 걷기 미리보기를 만든다.**
- [ ] **Step 4: 전체 pytest와 자동 검사를 실행하고, README에 재실행 순서와 생성물·규칙 생성물 구분을 적는다.** FEVM과 로컬 사본의 해시가 같은지 확인한다.
- [ ] **Step 5: 사용자에게 보여주고 승인받는다.** 게임 연결은 승인 후 별도 설계로 진행한다.
