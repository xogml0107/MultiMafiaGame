# ZZabmongus Foundation

게임 기획서 v0.4를 바탕으로 만든 기초입니다. 멀티플레이는 **LobbyScene → WaitingRoomScene → GameScene**으로 구분했습니다. 규칙 검증용 FoundationSandbox와 기존 개발용 MultiplayerBase도 보존합니다.

멀티 실행은 `ZZabmongus > Create or Open Lobby` 메뉴 또는 `Assets/Scenes/LobbyScene.unity`에서 시작합니다. 국가를 선택하면 해당 국가의 Steam 방 목록을 표시합니다. 이름·공개 여부·비밀번호·정원으로 방을 만들고, 방을 더블 클릭해 입장합니다. 잠금 표시, 비밀번호 확인, 정원 초과 팝업을 포함합니다. 씬 전환은 [Docs/SceneFlow.md](Docs/SceneFlow.md), Steam 설정은 [Docs/MultiplayerBase.md](Docs/MultiplayerBase.md)를 참고하세요.

**패키지 추가·업데이트 전에는 항상 사용자에게 물어봅니다.** 이번 멀티 베이스에는 패키지를 추가하지 않았습니다.

멀티 대기실의 게임 시작을 Core에 연결했습니다. 서버 번호·진영 배정, 본인에게만 진영 전달, 서버 시간, 최종 답안 확정, 결과와 대기실 복귀를 포함합니다. 자기 번호는 숨깁니다. GameScene의 홀짝 검사 장치에 가까이 가서 **E키**로 사용하면 개인 단서 노트에 결과를 기록합니다. 포섭의 **F 길게 누르기** 키는 유지하며 온라인 포섭·미션은 후속 작업입니다. [Docs/InformationDevices.md](Docs/InformationDevices.md)를 참고하세요.

## 실행

1. Unity 6000.3.22f1에서 이 프로젝트를 엽니다.
2. `ZZabmongus > Create or Open Foundation Sandbox`를 선택합니다.
3. `Assets/Scenes/FoundationSandbox.unity`에서 Play를 누릅니다.
4. 기본 8명의 가상 플레이어를 Tab으로 번갈아 조종합니다. 다른 PC의 플레이어가 아닙니다.

씬 생성기는 기존 씬을 덮어쓰지 않습니다. 열려 있는 씬에 저장하지 않은 변경이 있으면 먼저 저장해야 합니다. 멀티플레이의 로비·대기실·게임 세 씬은 빌드 목록의 처음에 등록합니다. 규칙 테스트용 FoundationSandbox는 자동 등록하지 않습니다.

| 조작 | 동작 |
|---|---|
| WASD | 현재 플레이어 이동 |
| Tab / Next player | 다음 가상 플레이어로 전환 |
| 장치 근처에서 E | 포인트를 지불하고 검사, 또는 협동 릴레이 기여 |
| 대상 근처에서 F 유지 | 원조 마피아의 3초 포섭; 이동/키 해제 시 취소 |
| Speed | 시뮬레이션 1배/10배 전환; 포섭 시간도 같이 빨라짐 |
| +60 sec | 개발용 시간 건너뛰기; 진행 중 포섭 취소 |
| Guess / Suspect | 최종 번호 / 의심 대상 순환 선택 |
| LOCK FINAL ANSWER | 20분 이후 최종 답안 확정 |
| 후보 숫자 버튼 | 현재 플레이어의 개인 후보 메모 |
| Restart same seed | 같은 설정/시드로 재시작 |

설정은 `Assets/Settings/SandboxSettings.asset`에서 인원(4~12), 시드, 이동 속도, 장치 재사용 대기시간, 최소 포인트 주기를 변경합니다. 인원 변경은 Play 종료 후 적용하세요. 샌드박스 HUD는 개발용 영문 UI입니다. Steam 로비와 대기실은 한글 표시용 Noto Sans KR 글꼴을 사용합니다.

## 구현된 범위

- Unity에 의존하지 않는 `MatchSession`: 번호 셔플, 원조 마피아 1명, 개인 정보 뷰, 진영 변경.
- 원조만 가능한 근접 포섭: 7인 이상 5분에 1칸, 10인 이상 10분에 2칸. 최대 마피아 1/2/3명.
- 20분 흐름: 18분 수집 종료, 20분 제출 시작, 제출 제한시간 30초. 전원 제출하면 조기 판정.
- 자유 행동 45초 → 릴레이 미션 최대 45초 → 결과 25초. 성공 시 일찍 결과 단계로 전환.
- 릴레이 참여자 수가 `전체 인원 - 최대 마피아 수` 이상이면 +2. 동일 인물 중복 기여와 중복 보상 방지.
- 최소 정보 보장: 플레이 중 90초마다 팀 포인트 +1. 장치 이용자별 30초 재사용 대기시간.
- 홀짝/대소/범위/번호 차이/3개 후보 검사. 개인 또는 공개 단서 기록.
- 5/10/15분마다 각 Tier에서 규칙 1개씩 선택해 누적. 현재는 각 Tier 2종의 최소 구현.
  - Tier 1: 홀짝 결과 공개 / 모든 장치 2인 동행.
  - Tier 2: 범위 장치 봉쇄 / 대소 비교 결과 공개.
  - Tier 3: 무작위 두 사람 번호 교환 / 정밀 장치 봉쇄.
- 번호 교환 이전의 모든 단서를 `[OLD]`로 표시. 교환 대상은 결과 전까지 공개하지 않음.
- 시민 정답만 승리 계산. 결과 화면에서 번호 → 마피아 진영/포섭 시각 → 최근 사건 표시.
- 개인 후보 메모, 테스트 맵, 이동, 장치 상호작용, 결과 표시.

일반 미션 6종/번호 미션 5종, 규칙 각 5종, 음성, 튜토리얼은 아직 구현하지 않았습니다. 릴레이는 미션 상태 전환/중복 방지/보상 검증을 위한 첫 번째 단순 미션입니다. 온라인 GameScene에 Core의 판 생성/진영/시간/제출/채점과 홀짝 검사 장치·개인 노트를 연결했고, 다른 정보 장치·포섭·협동 미션의 온라인 상호작용은 후속 작업입니다.

## 임시로 결정한 세부 규칙

원문에 확정되지 않은 내용이므로 플레이테스트 후 변경할 수 있습니다.

1. 필요한 정답은 **최종 시민 수 × 2/3 올림**. 최대 포섭이 이뤄진 경우 원문 표와 일치하고, 포섭하지 않은 판은 실제 시민 수를 사용합니다.
2. 시민 과반이 **동일한 마피아 1명**을 지목하면 팀 보너스 +1. 마피아의 표는 제외하고, 과반 없음/동률/기권은 보너스 없음. 최대 +1이며 설정에서 끌 수 있습니다.
3. 포섭 권한은 원조에게만 있고 18분에 닫힙니다. 양쪽 중 한 명이 5cm를 넘게 움직이면 취소됩니다.
4. 번호 교환으로 과거의 관측 기록을 고치지 않습니다. 모든 플레이어의 이전 단서가 과거 정보임을 표시하므로 교환 대상이 유출되지 않습니다.
5. 20분에 제출 창을 열고 최대 30초 대기합니다. 미제출 시민은 오답으로 처리합니다. 제출은 수정 불가입니다.
6. 포인트는 팀 공유 자원입니다. 최소 지급/개인 쿨다운만 적용되어 있으며, 고의 독점 소비를 막는 예산/사용 권한 설계는 후속 온라인 작업에서 확정해야 합니다.

## 구조와 다음 작업

```text
Assets/
  Runtime/Core/       순수 C# 권위 상태, 시간, 포섭, 단서, 판정
  Runtime/Unity/      로컬 테스트 어댑터, 이동, 장치, uGUI HUD
  Runtime/Networking/ PurrNet 연결, 대기실, 플레이어, 서버 이동, Steam 수명 관리
  Editor/            멱등 테스트 씬 생성 메뉴
  Tests/EditMode/    인원/시간 경계/포섭/정보/승패 테스트
  Docs/              네트워크 패키지와 후속 작업
  GameDesign-v0.4.md  사용자가 제공한 기획 원문
  Scenes/            Unity API로 생성하는 테스트 씬
  Settings/          테스트 설정 ScriptableObject
  Art/Materials/     Unity API로 생성하는 테스트 머티리얼
```

`Core` 어셈블리는 Unity를 참조하지 않습니다. `Runtime`은 Core를 호출하고, Networking/Editor/Tests는 각각 별도 어셈블리입니다. Networking은 공개 방/경기 상태와 입력/위치를 복제하며, 개인 진영은 해당 소유자에게만 전달합니다.

`MatchSession`은 **서버/호스트에서만 보유할 객체**입니다. `GetPlayerView(id)`는 호출자를 인증하지 않는 로컬 API이므로 그대로 RPC로 노출하면 안 됩니다. 서버는 실제 연결의 소유자 ID를 사용해야 합니다. 다른 플레이어의 개인 뷰, 시드, 실제 번호, 비공개 사건을 클라이언트에 복제하면 안 됩니다. 현재 씬은 한 PC에서 모든 개인 화면을 번갈아 보는 개발 전용 도구입니다.

온라인 어댑터를 구현할 때는 요청자의 연결 ID, 이동 속도/좌표, 장치 위치, 미션 위치, 요청 빈도를 서버에서 검증하고 나서 Core 명령을 호출하세요. Core가 포인트·시간·중복·진영 조건을 검증하며, 현재 LocalSandbox는 장치 거리만 로컬에서 확인합니다. `Tick`과 `SetPosition`도 클라이언트가 임의 호출하는 네트워크 명령이 되어서는 안 됩니다.

네트워크는 사용자 선택에 따라 **PurrNet + Steamworks.NET**을 사용합니다. 고정 버전과 근거는 `Docs/Networking.md`에 기록했습니다. 대기실은 최대 12명이고 4명 이상 모두 준비해야 호스트가 시작합니다. 호스트 이탈 시 방이 종료되며 참가자는 대기실에서만 재접속할 수 있습니다. 다음 작업은 다른 정보 장치 확장 → 온라인 포섭 → 협동 미션 → 번호 기반 미션입니다.

## 검증

Unity 메뉴 `Window > General > Test Runner`에서 실행합니다.

**에이전트는 사용자가 별도로 지시하지 않으면 에디터 Play 모드가 필요한 테스트를 실행하지 않습니다.** 2026-10-11 EditMode 96개가 통과했습니다. 로비 검증은 [Docs/MultiplayerBase.md](Docs/MultiplayerBase.md), 온라인 판 연결은 [Docs/OnlineMatch.md](Docs/OnlineMatch.md), 씬 분리는 [Docs/SceneFlow.md](Docs/SceneFlow.md), E키 홀짝 검사와 개인 단서 노트는 [Docs/InformationDevices.md](Docs/InformationDevices.md)에 기록했습니다.

2026-10-08, Unity 6000.3.22f1 + PurrNet 1.24.1 + Steamworks.NET 2025.164.1 환경의 검증 결과:

- EditMode `ZZabmongus.Tests.MatchSessionTests`: **46개 통과**, 실패/건너뛰기 0개.
- PlayMode `ZZabmongus.Tests.SandboxSmokeTests`: **1개 통과**. 씬 로드, 8명 생성, 장치 6개, 이벤트 시스템, 버튼 연결, 개인 화면/폰트, 최종 제출, 결과, 재시작 검증.
- 테스트 보고서: 프로젝트의 `Logs/foundation-editmode.xml`, `Logs/foundation-playmode.xml`.
- 실제 여러 PC 사이의 PurrNet/Steam 접속은 아직 검증 대상이 아닙니다.

TMP 기본 리소스는 Unity API로 임포트했습니다. 처음 씬을 생성할 때 리소스 임포트를 비동기로 기다립니다. 배치 실행은 `-quit` 없이 `-executeMethod ZZabmongus.Editor.SandboxBuilder.CreateOrOpen`을 호출하세요. 생성기가 완료 후 종료합니다.
