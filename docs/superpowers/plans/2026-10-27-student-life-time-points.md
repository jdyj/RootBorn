# Student Life Time Points Implementation Plan

> **For agentic workers:** Root executes this plan against the user's open Unity Editor. Assets C# changes go only through `script-update-or-create`.

**Goal:** Share the GDD weekday/weekend action budget across actual student activities and the future adult work service, preserving save data and alternative growth paths.

**Architecture:** `LifeCalendarDefinition` supplies daily budgets. `ActivityTimePolicyDefinition` supplies per-action cost, allowed weekdays, and optional once-per-day shared policy. A focused `StudentLifeProgress` partial owns spent TP and consumed daily policy IDs. Existing runners preflight policy before any effects; the existing component rebinds the registered calendar after new/load/identity changes.

**Tech Stack:** Unity C#, SO, JsonUtility, NUnit and input-driven PlayMode.

## Tasks

- [x] Add `Assets/Tests/EditMode/StudentLife/StudentLifeTimePointTests.cs` through Unity API. Complete test source: `output/full-design-20261027/calendar/StudentLifeTimePointTests.cs.txt`.
- [x] Run `python output/full-design-20261027/qa_step.py test calendar-expanded-red EditMode StudentLifeTimePointTests`: 17 expected failures due to missing calendar/budget API.
- [ ] Import the three new runtime files from `calendar/{LifeCalendarDefinition,ActivityTimePolicyDefinition,StudentLifeTimeBudget}.cs.txt`.
- [ ] Apply exact reviewed existing-file transformations in `calendar/stage_calendar.py`: saved daily fields/reset, six activity definitions, six runner paths, Registry calendar/policy fields, component binding. No ID-specific runtime branches.
- [ ] Run the calendar tests GREEN and existing StudentLifeActivity/DayResult/OutsideSchool/LocationActivity/PartTimeWork/TownHelp/DailyEvent affected tests.
- [ ] Add a persistent calendar and explicit policy assets through an idempotent Editor script. Weekday morning policy is free/weekday-only/once per day; ordinary activities cost one TP; passive choices and incidental interactions stay free. Record every changed definition and previous policy in an import manifest.
- [ ] Test saved Registry and policy references, real player component binding after save restore, and display remaining TP in Daily Plan with a visible localized explanation for unavailable actions.
- [ ] Verify actual Town input → outside-school action twice → third rejected → day-end UI → next day budget reset → save/reload; include weekday and weekend fixture journeys and native Game View capture.

## Contracts

```csharp
progress.ConfigureCalendar(registry.StudentLifeCalendar);
bool available = progress.CanSpendTimePoints(work.TimePointCost);
bool applied = progress.TrySpendTimePoints(work.TimePointCost);
// Rebinding never refills. A successful next-day transition clears spent points.
// Existing saves lacking new fields initialize spent TP to zero, not negative credit.
```

Tests cover boundary days 1/5/6/7/8, 2/3 paid actions, duplicates, no mutation on rejection, school optionality, free policy once per day across reload, calendar rebind, settled day rejection and all four outside-school runner entry points. UI refresh happens on panel binding/state changes; no catalog scans are added to Update.
