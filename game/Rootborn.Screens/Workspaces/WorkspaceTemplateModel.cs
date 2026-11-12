using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Workspaces;
using Rootborn.Game;

namespace Rootborn.Screens.Workspaces
{
    /// <summary>템플릿 한 줄의 표시 값: 이름 키, 값, 지금 적용할 수 있는지(Core 판단 — Applied면 된다, 아니면 그 이유).</summary>
    public sealed class WorkspaceTemplateRow
    {
        public WorkspaceTemplateRow(string id, string nameKey, IReadOnlyList<ItemAmount<string>> costs, WorkspaceResultKind check)
        {
            Id = id; NameKey = nameKey; Costs = costs; Check = check;
        }

        public string Id { get; }
        public string NameKey { get; }
        public IReadOnlyList<ItemAmount<string>> Costs { get; }
        public WorkspaceResultKind Check { get; }
        public bool CanApply => Check == WorkspaceResultKind.Applied;
    }

    /// <summary>
    /// 템플릿 고르기 화면의 표시 모델: 지금 열린 일터의 템플릿(정의 순서)과 줄마다 적용 가능 여부. GameSession 조회만 읽는다
    /// (TemplatesForActiveWorkspace, CanApplyTemplate). 가능 여부를 화면이 다시 판단하지 않는다.
    /// </summary>
    public sealed class WorkspaceTemplateModel
    {
        public static readonly WorkspaceTemplateModel Empty = new WorkspaceTemplateModel(Array.Empty<WorkspaceTemplateRow>());

        private WorkspaceTemplateModel(WorkspaceTemplateRow[] rows) { Rows = rows; }

        public WorkspaceTemplateRow[] Rows { get; }

        public int IndexOf(string templateId) => Array.FindIndex(Rows, r => r.Id == templateId);

        public static WorkspaceTemplateModel From(GameSession session)
        {
            if (session == null || session.ActiveWorkspace == null) return Empty;
            IReadOnlyList<WorkspaceTemplate> templates = session.TemplatesForActiveWorkspace();
            var rows = new WorkspaceTemplateRow[templates.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                WorkspaceTemplate t = templates[i];
                rows[i] = new WorkspaceTemplateRow(t.Id, t.DisplayNameKey, t.Costs, session.CanApplyTemplate(t.Id));
            }
            return new WorkspaceTemplateModel(rows);
        }

        /// <summary>
        /// 결과 종류 → 이유 문구 키(종류 하나에 키 하나, 화면의 표 한 곳). 표에 없는 종류는 InvalidRequest의 문구.
        /// DoesNotFit은 "자리가 맞지 않거나(물건이 든 상자와 겹침 포함) 저장하지 못함"이다.
        /// </summary>
        private static readonly Dictionary<WorkspaceResultKind, string> ReasonKeys = new Dictionary<WorkspaceResultKind, string>
        {
            [WorkspaceResultKind.Applied] = "ui.workspace.template.reason.applied",
            [WorkspaceResultKind.DuplicateRequest] = "ui.workspace.template.reason.duplicate-request",
            [WorkspaceResultKind.InvalidRequest] = "ui.workspace.template.reason.invalid-request",
            [WorkspaceResultKind.InvalidDefinition] = "ui.workspace.template.reason.invalid-definition",
            [WorkspaceResultKind.NotOwner] = "ui.workspace.template.reason.not-owner",
            [WorkspaceResultKind.InsufficientResources] = "ui.workspace.template.reason.insufficient-resources",
            [WorkspaceResultKind.DoesNotFit] = "ui.workspace.template.reason.does-not-fit",
            [WorkspaceResultKind.OverCapacity] = "ui.workspace.template.reason.over-capacity",
        };

        public static string ReasonKey(WorkspaceResultKind kind)
            => ReasonKeys.TryGetValue(kind, out string key) ? key : ReasonKeys[WorkspaceResultKind.InvalidRequest];
    }
}
