# Exploration Choice Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans for later approved implementation. Parent approved the design and38-entry set with 체력 for KO energy. Tests and implementation candidates are staged outside Assets; parent must prove red before applying. No Unity call, Assets application, locale import or test run was performed by the staging agent.

**Goal:** Remove visible test labels, raw localization/collector IDs and enum names from the actual exploration panel and prompt; preserve the completed Leave fix and every authored choice behavior. This is a separate zero-art code/localization phase, independent of art113.

**Architecture:** Add a small UI-only `ExplorationTextPresentation` helper using GameText. Translate the existing title literal as a compatibility catalog key and keep all SO bytes unchanged. The alternatives are changing two SO presentation-key fields (extra source scope) or changing global GameText behavior/entity-specific UI branches (reject). The parent approved this recommendation; staged application remains gated by the actual red run.

**Tech Stack:** Unity UGUI, GameText catalog/KO-EN JSON, existing shared font, public-UI EditMode tests and parent-owned native verification.

## Verified source facts

- `ExplorationChoicePanel.cs` currently assigns raw Model.DisplayNameKey, concatenates raw choice/preview/lock fields and prints Kind.ToString plus collector arrays. Result rect500x72/font16. `ExplorationInteractionPointInteractor.cs` Humanize produces an English prompt. Existing modal/canvas/font/raycast behavior remains unchanged.
- Root: `Assets/Data/StudentLife/ExplorationInteractions/Exploration_TestAlleyCrate.asset`; display/description literal `Exploration Test Alley Crate`, ID `exploration.test.alley-crate`. The catalog alias below removes that wording without a C# special case or SO edit.
- Choice files are under `Assets/Data/StudentLife/ExplorationInteractions/Choices/`:

| Actual SO | Condition | Actual effects |
| --- | --- | --- |
| Choice_Exploration_TestInspect.asset | None | Outcome_Exploration_TestItem: Wood1; Outcome_Exploration_TestTrait: Focus trait+1 |
| Choice_Exploration_TestCareful.asset | Focus trait>=1 | Outcome_Exploration_TestCareer: Chef hint; Outcome_Exploration_TestTrait: Focus+1. **No wood.** |
| Choice_Exploration_TestDiscovery.asset | None | Clue, encyclopedia, follow-up quest |
| Choice_Exploration_TestLeave.asset | None | DismissesInteraction=true; closes with no costs, records or saves |

- All three regular choices reference `ExplorationInteractions/RiskPolicies/RiskPolicy_Exploration_Test.asset`: Energy1, FocusCost0, Stress+1, no status, minutes or TP cost. Leave ignores this default risk.
- Lock: `ExplorationInteractions/Conditions/Condition_Exploration_TestTrait.asset`, minimum1, references `CareerPractices/Traits/Trait_Focus.asset` (trait.focus). Existing catalog: Focus / Korean Focus label. `CareerPractices/Careers/Career_Chef.asset` is the hint target.
- Wood: `Assets/Data/Items/Item_Wood.asset`, Id=Wood, DisplayKey=item.wood, already in the eight Registry.Items. item.wood currently has no translation.
- Discovery targets: `Assets/Data/DiscoveryClues/Definitions/DiscoveryClue_LibraryArchiveRumor.asset`, `Assets/Data/Encyclopedia/Entries/EncyclopediaEntry_FirstDiscovery.asset`, `Assets/Data/Quests/Quest_GatherWood.asset`. Do not invent force/delivery actions or call the collector's quest ID a newly accepted quest.

## Independent catalog baselines

Python UTF8/YAML audit: SO catalog555 unique rows; KO/EN JSON383 entries each. Both JSON keysets are identical strict subsets of the SO, with172 omissions and zero overlapping value mismatches. Preserve each baseline independently. **Append only the chosen new keys to all three; do not backfill172 old omissions.** Existing trait.focus/career.chef/facility.close stay untouched.

No relevant runtime EditMode test hard-locks555; the literal555 test hits are unrelated numeric fixtures. `GameTextCatalogTests.Catalog_UsesEnglishFallbackAndExposesMissingKey` intentionally requires missing-key echo at the low-level API: keep it. Historical staged packed/author helpers do assert555. Clone verifiers for this phase, preserving old proofs unchanged; verify the original555 rows exactly, not merely a loose >=555 count. `EncyclopediaReferenceBookTests` already demonstrates prefix-preserving append safety for theme keys; theme26 is unchanged.

## Exact proposed append set

38 keys: proposed totals593 SO rows and421 entries in each JSON. No unused `.desc` keys. If copy/keys are trimmed during review, derive the final total from the approved set.

| Key | Korean | English |
| --- | --- | --- |
| `Exploration Test Alley Crate` | 골목의 낡은 상자 | Old alley crate |
| `choice.test.inspect` | 살펴보기 | Inspect |
| `choice.test.inspect.preview` | 목재·집중력 성장 · 체력-1, 스트레스+1 | Wood & focus growth · energy -1, stress +1 |
| `choice.test.careful` | 조심히 조사하기 | Look closely |
| `choice.test.careful.preview` | 요리 힌트·집중력 성장 · 체력-1, 스트레스+1 | Chef hint & focus growth · energy -1, stress +1 |
| `choice.test.discovery` | 흔적 기록하기 | Record findings |
| `choice.test.discovery.preview` | 단서·도감·후속 퀘스트 · 체력-1, 스트레스+1 | Clue, journal & quest · energy -1, stress +1 |
| `choice.test.leave` | 그만두기 | Leave |
| `choice.test.leave.preview` | 변화 없이 닫기 | Close without changes |
| `exploration.condition.trait` | 집중력 1 필요 | Focus 1 required |
| `item.wood` | 목재 | Wood |
| `exploration.ui.title` | 조사 | Investigation |
| `exploration.ui.choice` | 선택하기 | Choose |
| `exploration.ui.hint` | 조사 방법을 선택해 주세요 | Choose an approach |
| `exploration.ui.locked` | 조건이 아직 부족해요 | Requirements not met |
| `exploration.ui.prompt` | [E] {0} 살펴보기 | [E] Inspect {0} |
| `exploration.ui.prompt.generic` | [E] 이곳 살펴보기 | [E] Inspect this place |
| `exploration.result.applied` | 조사를 마쳤어요 | Investigation complete |
| `exploration.result.invalidrequest` | 조사를 진행할 수 없어요 | Unable to investigate |
| `exploration.result.conditionfailed` | 아직 선택할 수 없어요 | This option is unavailable |
| `exploration.result.duplicaterequest` | 이미 조사한 곳이에요 | Already investigated |
| `exploration.result.cooldownactive` | 나중에 다시 조사해 주세요 | Try again later |
| `exploration.result.insufficientresources` | 필요한 자원이 부족해요 | Required resources are missing |
| `exploration.result.dismissed` | 조사를 그만뒀어요 | Investigation closed |
| `exploration.result.updated` | 조사 결과를 확인해 주세요 | Check the investigation result |
| `exploration.effect.item-gain` | +{0} ×{1} | +{0} ×{1} |
| `exploration.effect.item-spent` | -{0} ×{1} | -{0} ×{1} |
| `exploration.effect.item` | 아이템 | Item |
| `exploration.effect.clues` | 단서 {0}건 | Clues: {0} |
| `exploration.effect.entries` | 도감 {0}건 | Journal: {0} |
| `exploration.effect.careers` | 진로 힌트 {0}개 | Career hints: {0} |
| `exploration.effect.quests` | 후속 퀘스트 {0}건 | Follow-up: {0} |
| `exploration.effect.growth` | 성장 기록 {0}건 | Growth updates: {0} |
| `exploration.effect.other` | 변화 {0}건 | Other changes: {0} |
| `exploration.effect.energy` | 체력 {0} | Energy {0} |
| `exploration.effect.focus` | 집중 {0} | Focus {0} |
| `exploration.effect.stress` | 스트레스 {0} | Stress {0} |
| `exploration.effect.status` | 상태 변화 {0}건 | Status changes: {0} |


## Source and presentation contract

Proposed source scope, not implemented: create `Assets/Scripts/UI/StudentLife/ExplorationTextPresentation.cs`; modify only its panel/interactor callers plus the three localization sources. No domain/SO/registry/art/theme edits.

- Resolve title/name/preview/lock via GameText. Empty/whitespace or unchanged-key results use a generic localized fallback; if even fallback/catalog is absent, use a neutral em dash. Never Humanize/echo unknown keys or literals. No entity-ID or test-prefix branches. Preserve global GameText.Get/GetCatalog missing-key behavior.
- Known prompt titles use exploration.ui.prompt; unknown/missing titles use prompt.generic. Resolve at Render/Open, ShowResult and prompt getter, with no per-frame polling. Refresh the existing Close label when reopening after language change.
- Localize all seven result kinds; unknown enum values use result.updated, never raw names/numbers. Dismissed still closes immediately, so no new Leave result screen is required.
- Parse itemId:count from the **last** colon, requiring a positive integer. Resolve a unique exact Registry.Items ID, then its localized DisplayKey. Unknown/ambiguous/untranslated items use Item; malformed strings use neutral counts. Never expose the ID/protocol or an exception.
- Use nonempty distinct counts for typed clue/encyclopedia/career/follow-up arrays. These are neutral counts, not claims of newly accepted/discovered content.
- Trait/skill OutcomeIds can supply neutral growth counts, not a signed/named delta: these IDs do not encode amounts. Unknown unrepresented categories use Other changes. Category-protocol parsing is allowed; entity-ID branching is not.
- Avoid double counting typed facts plus OutcomeIds (for example Wood:1 and item-grant:Wood). Validate signed energy/focus/stress FatigueStatusIds and localize vital labels; unknown/malformed status IDs become neutral status-change counts.
- Fixed display order: localized status, reward/growth/count summary, vital/status summary; omit empty groups. No domain/result-array mutation.
- Actual result shapes: Inspect = Wood1 +growth1; Careful =career hint1 +growth1; Discovery =clue1 +journal1 +follow-up1. All regular cases then show Energy-1/Stress+1. Use at most3 rendered lines including status inside500x72/font16. For example: `Investigation complete` / `+Wood x1 ? Growth updates: 1` / `Energy -1 ? Stress +1`. Measure actual KO/EN text; do not infer fit from character count. Bound larger unknown/future input with localized aggregate counts instead of long IDs.
- Move existing Close center242,142 ->226,142, preserving64x32/listener. Right inset becomes22px; current title right190 and Close left194 leave4px separation. Keep choice ordering/positions; measure all combined labels inside456x32/font15. Shorten copy first if real text overflows.

## Implementation/checkpoints for the parent

1. **Freeze/read-only baselines:** ordered555 catalog triplets, original383 JSON maps/omissions, relevant source/SO/meta bytes, accepted112 and dismissal review/checkpoint hashes. Preserve prior evidence. No art-count increment.
2. **Red tests before source/data:** `ExplorationTextLocalizationTests` staged at `output/design-comfy-20261031/ExplorationTextLocalizationTests.cs.txt`, for parent MCP import first. Fixture-owned UI only; snapshot/restore exact language/provider state, close only its own lease, no live saves. Test actual public Open/ShowResult/Button.onClick/prompt behavior:
   - KO/EN real title/four labels/previews, Focus0 locked reason and Focus1 availability; assert the actual source trait/outcome/risk references and Leave flag.
   - Unknown title/name/hint/lock, missing fallback/catalog, malformed/unknown item/status/outcome and unknown result enum never expose tokens.
   - Source-faithful synthetic Inspect/Careful/Discovery collectors render localized summaries with no typed/OutcomeId double count and <=3 actual rendered lines. Known Registry.Items names and unknown-item fallback both covered.
   - Fake callbacks still receive each original choice ID exactly once. Open/Render/ShowResult preserve SOs, progress, input result arrays and fixture files; no real outcome execution needed for presentation tests.
   - Reuse the same panel KO->Close->EN->Open to catch stale Close/title/choice translations. Verify actual font, nonraycasting Text, result/choice bounds and new Close/title separation. Retain ownership8, dismissal10 and ordinary-action tests unchanged.
3. **Minimal UI source:** helper + callers +Close inset/language refresh. Review for ID branches, raw Kind/collector output, unsafe echo, duplicate summaries and invented effect amounts. Approved candidates are staged outside Assets and may be applied only after the parent records the actual red result.
4. **Guarded locale author:** confirm38 keys absent, append approved rows through parent-owned Unity/localization workflow, preserve original555 ordered rows and original383 JSON key/value maps/omissions. Check unique keys, KO/EN placeholder agreement and approved totals. Reverse-removing the additions must recover the original semantic maps. No172-key cleanup.
5. **Focused/scoped checks:** new localization tests plus existing catalog, exploration text, dismissal10, ownership8 and normal-choice tests. Missing-key low-level tests remain green. Derive broad counts from actual runs, not this plan.
6. **Fresh bundles/native, separate from art113:** new proof prefix and cloned verifier; check all original555 translations and every new value, theme26/the current accepted art checkpoint (113 if finalized first)/Leave-only flag unchanged. Fresh legitimate day4 QA copy; actual WASD/E shows localized prompt/title/options/lock/Close in KO/EN. Actual Leave remains cost/record/file-free through repeat/cold. A separately recorded actual Inspect may prove localized Applied text and authored Wood+1/Focus+1/Energy-1/Stress+1/no TP/minutes cost; do not falsely call that ordinary-action state unchanged. No injected energy/traits/completion/clock/pose and no unperformed native Careful/Discovery/zero-resource/completed-site claim.
7. **Delivery:** restore builder0/defaults; preserve failures, font/bounds screenshots and first/cold proof. Publish separate localization/code evidence, zero new PNGs. Keep completed112 art/dismissal history byte-exact; do not relabel old raw-text screenshots as localized.

**Review risks:** the compatibility title key is deliberate; labels/result fit still requires actual font measurement. Collector IDs cannot prove delta/newness. Baseline SO555 and JSON383 omissions are both protected. The approved table uses 체력 for KO energy; actual compile/font/layout/native verification remains parent-owned.

## Staged handoff (not applied)

- Test first: `output/design-comfy-20261031/ExplorationTextLocalizationTests.cs.txt` (12 semantic cases).
- Data proposal: `output/design-comfy-20261031/exploration-localization-entries.json` (38 entries; totals593/421).
- After actual red: `ExplorationTextPresentation.cs.txt`, `ExplorationChoicePanel-localized.cs.txt`, `ExplorationInteractionPointInteractor-localized.cs.txt` under the same output directory.
- Existing SOs and Assets remain untouched by this agent. Preliminary Malgun15 font widths are below456px; this is not a Unity layout/test pass.

