# 환경 설정·접속 값 규약 (Environment Config)

> **적용 범위**: 모든 코드·설정·도구 — Unity 클라(`Assets/`), 서버(`server/`), 운영 도구(`tools/`), 스크립트(`scripts/`), MCP 서버, CI/CD 워크플로우 등 환경(dev/staging/prod/test)에 따라 값이 달라질 수 있는 모든 경로.
>
> **상위 규칙**: `constitution.md` §"금지 사항"의 "환경 의존 값 하드코딩 금지" 항목의 세부 규약.

## Why — 원리 (왜 이 규칙이 필요한가)

1. **환경 의존 값은 코드의 일부가 아니다.** host/port/url/credential/endpoint/path 는 환경 식별자에 따라 변하는 **데이터**다. 코드에 박으면 환경 추가·이동 시마다 코드 수정이 필요해 헌법 §1 (변경 가능성 우선)에 위배된다.
2. **환경 통념(convention)은 사실(fact)이 아니다.** "dev = localhost", "prod = 원격" 은 흔한 패턴일 뿐 프로젝트마다 다르다. AI 에이전트가 통념을 데이터 검증 없이 코드에 못 박으면 실제 인프라와 어긋난다 (예: dev DB가 원격 미니PC인 경우).
3. **인접 단서를 무시하지 않는다.** 같은 `.env` 또는 설정 파일 내 다른 키(REDIRECT_URI, PUBLIC_URL, CALLBACK 등)에 이미 호스트가 노출돼 있을 수 있다. 일관성 단서로 활용.
4. **누락 키 = 질문 신호.** 필요한 환경 변수가 `.env` 또는 설정 파일에 없으면 (a) 사용자에게 질문하거나 (b) 누락임을 명시적 에러로 신호. 코드 리터럴로 메우면 안 된다.

## 금지 (Hard No)

- 코드 내 리터럴 host/port/url/endpoint/credential/db-name/file-path 등 환경 의존 값
  - 예: `host = "127.0.0.1"`, `url = "http://58.123.57.182:8083"`, `redis://localhost:6379`, `/var/lib/coindefense/...`
- 환경 통념을 `if env == "dev"` / `switch (env)` 분기로 코드에 박는 것
  - 예: `if env == "dev": host = "127.0.0.1" else host = os.getenv("HOST")` ← dev 도 env에서 읽어야 함
- `.env` / 설정 파일에 누락된 키를 코드 리터럴 fallback 으로 메우는 것
- "암묵적 기본값"이 환경마다 다른데 한 환경 기준으로 박는 것

## 허용 패턴

### 1. env 키 읽기 + 누락 시 명시적 에러

```python
host = os.environ.get("POSTGRES_HOST")
if not host:
    raise RuntimeError("POSTGRES_HOST not set in .env.<env>")
```

```typescript
const host = process.env.POSTGRES_HOST;
if (!host) throw new Error("POSTGRES_HOST 누락 — .env.<env> 확인");
```

### 2. 환경별 .env 템플릿 (`.env.<env>.template`)

각 환경에 필요한 모든 키를 빈 값으로 명시. 신규 환경 추가 시 템플릿 복사 → 채우기.

```
# .env.dev.template
POSTGRES_HOST=
POSTGRES_PORT=
POSTGRES_USER=
POSTGRES_PASSWORD=
POSTGRES_DB=
```

### 3. 설정 파일(JSON/YAML) + 스키마 검증

여러 키가 묶이는 경우 JSON/YAML + Pydantic/Zod 등 스키마 검증.

### 4. 명시적 기본값 (제한적 허용)

코드에 fallback 기본값을 둘 때는:
- 그 값이 **모든 환경에서 안전**한지 자가 검증 (예: 테스트 전용 0.0.0.0:0)
- 주석으로 "왜 이 기본값이 안전한가" 명시
- 불확실하면 fallback 없이 missing-key 에러로 fail-fast

## PR 검증 절차

머지 전 **리뷰어 점검 항목**으로 다음을 수행 (자동화는 false positive 가 많아 권고 수준):

```bash
# 환경 의존 리터럴 검출 (참고용 휴리스틱)
grep -rE "127\.0\.0\.1|localhost|https?://[0-9]+\." tools/ scripts/ server/
grep -rE "host\s*=\s*['\"]" tools/ scripts/ server/
```

발견 시:
- 테스트 코드 / 명시적 안전 기본값 → 주석 보강 후 통과
- 운영 경로 → env 키화 의무

## 인접 단서 체크리스트 (.env 검토 시)

신규 환경 변수를 코드에 추가하기 전 **반드시 같은 .env 파일을 한 번 훑는다**:

- [ ] 같은 도메인의 다른 키(REDIRECT_URI / PUBLIC_URL / CALLBACK / WEBHOOK_URL / ADMIN_URL 등)에 호스트·포트가 이미 노출돼 있는가?
- [ ] 노출된 호스트와 내가 가정한 host 가 일치하는가? 불일치 시 사용자에게 질의.
- [ ] 같은 호스트의 다른 포트(8081/8083/55432 등)가 이미 사용 중이라면 같은 머신을 가리킬 가능성이 높다.
- [ ] `.env.example` 또는 `.env.<env>.template` 의 결격 키가 있는가?

## 환경 분리 작업 의무

새 환경(dev/staging/prod/test)을 추가하거나 기존 환경의 접속 방식을 변경할 때:

1. **각 환경의 접속 방식을 사용자에게 명시적으로 확인.** "dev 는 로컬일 것" 같은 단정 금지.
2. dev 도 prod 와 동일 수준으로 env 키 의무 — "dev 는 어차피 로컬이라 박아도 됨" 예외 없음.
3. 환경 분기 dispatcher 는 **데이터 기반**이어야 함 (env 변수 읽기) — 코드 분기로 환경별 다른 host 박지 말 것.
4. 새 환경 추가 시 `.env.<env>.template` 도 함께 갱신.

## 원격 환경 `.env` 갱신 패턴 (참고)

`.env.dev` 등 환경 파일이 원격 호스트의 컨테이너 안에 있을 때, 한 키만 안전하게 추가/갱신하는 멱등 패턴:

```bash
sudo docker exec -i <container> sh -lc 'f=/path/to/.env.<env>; \
  if grep -q "^<KEY>=" "$f"; then \
    sed -i "s|^<KEY>=.*|<KEY>=<VALUE>|" "$f"; \
  else \
    printf "<KEY>=<VALUE>\n" >> "$f"; \
  fi; \
  grep "^<KEY>=" "$f"'
```

원칙:
- 통째 덮어쓰기(`cat > file`) 금지 — 다른 키 손실 위험.
- 키 존재 여부 분기로 멱등성 확보.
- 마지막 grep 으로 결과 검증.
- 갱신 후 해당 환경 컨테이너 재기동(`docker compose --env-file ... up -d`)으로 변경 반영.

## 관련 문서

- `constitution.md` §"금지 사항" — 최상위 금지 항목 (이 파일이 세부 규약)
- `rules/path-based/server.md` — 서버 경로 한정 추가 규칙
- `rules/coding-standards.md` — Unity 클라 코드 일반 규칙

## 사례 (2026-05-01)

CoinDefense MCP 서버 dev/prod 분리 작업에서 AI 가 dev DB host 를 `"127.0.0.1"` 리터럴로 박았으나 실제 dev DB 는 원격 미니PC `58.123.57.182:55432`. 같은 `.env.dev` 안에 `DISCORD_REDIRECT_URI=http://58.123.57.182:8081/...` 가 이미 노출돼 있었음에도 통념(dev=localhost)이 단서를 덮음. 본 규칙으로 일반화.
