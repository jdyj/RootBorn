#!/usr/bin/env node
// Feedback detection hook.
// Reads the user prompt from stdin (Claude Code UserPromptSubmit hook contract)
// and if it contains feedback-like keywords, emits a system-reminder suggesting
// the feedback-constitution-update skill.

const KEYWORDS = [
  // Korean
  '잘못', '틀렸', '틀려', '그게 아니', '그렇게 하지', '그러지 마', '아니야', '아니라',
  '하지 마', '다시 해', '다시해', '왜 그렇게', '이상해', '이상하네',
  // English
  'wrong', 'incorrect', "don't do that", 'do not do that', 'that is not',
  "that's not", 'mistake', 'bad idea', 'misunderstood',
];

let raw = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => { raw += chunk; });
process.stdin.on('end', () => {
  let prompt = '';
  try {
    const payload = JSON.parse(raw || '{}');
    prompt = (payload.prompt || payload.userPrompt || payload.user_prompt || raw || '').toString();
  } catch {
    prompt = raw;
  }

  const lower = prompt.toLowerCase();
  const hit = KEYWORDS.some((k) => lower.includes(k.toLowerCase()));

  if (hit) {
    const reminder = [
      '[FEEDBACK DETECTED]',
      '사용자 발화에 부정적 피드백 키워드가 감지되었습니다.',
      '피드백이 AI가 생성한 산출물/규칙 위반에 대한 것이라면,',
      '`feedback-constitution-update` 스킬을 호출하여 다음을 수행하세요:',
      '  1) feedback-analyzer 에이전트가 원인 분석',
      '  2) constitution-updater 에이전트가 .claude/constitution.md 또는 rules/*.md 갱신',
      '  3) .claude/rules/changelog.md 에 변경 이력 기록',
      '단순 질문이나 다른 주제의 피드백이면 무시하세요.',
    ].join('\n');
    // Emit as additionalContext (stdout is consumed by the hook runner).
    console.log(reminder);
  }
});
