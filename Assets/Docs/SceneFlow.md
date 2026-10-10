# 로비 · 대기실 · 게임 씬

멀티플레이의 진입점은 `Assets/Scenes/LobbyScene.unity`이다. `ZZabmongus > Create or Open Lobby`로 연다. 기존 `Create or Open Multiplayer Base` 메뉴도 새 로비를 연다.

| 씬 | 역할 |
|---|---|
| LobbyScene | 국가 선택, Steam 방 목록, 이름 입력, 공개/비공개 방 생성과 입장 |
| WaitingRoomScene | 별도 대기실 맵, 이동, 이름·색상·준비, 방장의 게임 시작 |
| GameScene | 별도 게임 맵, 개인 진영 HUD, 서버 시간, 최종 제출, 결과 |

```text
LobbyScene -- 방 생성/입장 --> WaitingRoomScene -- 방장 시작 --> GameScene
                                   ^                            |
                                   +------ 판 종료/중단/복귀 ----+
모든 씬 -- 방 나가기/호스트 연결 종료 --> LobbyScene
```

## 연결과 씬의 수명

로비의 `SessionSceneEntry`는 세션이 없을 때만 `NetworkSession.prefab`을 생성한다. 프리팹의 NetworkManager는 연결을 자동 시작하지 않고, 전체 루트를 DontDestroyOnLoad로 유지한다. Steam 초기화·콜백·방 정보, 인증, 플레이어 생성과 씬 전환은 이 루트의 수명을 따른다. 로비로 돌아와도 매니저를 중복 생성하지 않는다.

서버는 `NetworkRoomState.prefab`과 캐릭터를 DDOL 씬에 생성한다. 대기실/게임 맵은 연결 객체와 캐릭터를 소유하지 않는다. 씬을 교체해도 소유권, 이름, 색상과 개인 진영 캐시를 유지한다. 방을 나가면 PurrNet이 네트워크 객체를 정리하고 Steam 검색용 방에서도 나간다. 다음 방을 만들 때는 새 방 상태를 생성한다.

방 입장 이후의 씬 전환은 서버의 PurrNet `ScenesModule.LoadSceneAsync`가 담당한다. 방 종료 후 오프라인 로비 복귀는 로컬 SceneManager가 담당하며, 이미 시작된 비동기 씬 로딩이 끝나기를 기다린다.

## 시작과 복귀

- 4~12명 모두 준비한 상태에서 방장만 시작할 수 있다.
- 게임 씬 전환 전에 참가자 소유자 ID를 고정한다. PurrNet이 기록한 해당 씬의 로딩 완료 목록에서 전원의 완료를 확인한다.
- 모두 로딩하기 전에는 Core의 판을 생성하거나 경기 시간을 진행하지 않는다. 로딩 중에는 이동, 준비, 프로필 변경과 신규 입장을 차단한다.
- 로딩이 끝나면 서버가 각 캐릭터를 해당 씬의 시작 좌표로 배치하고 위치를 강제로 동기화한다. 이후 번호·진영을 배정하고 경기를 시작한다.
- 게임 로딩 중 이탈 시 새 판을 만들지 않고 남은 참가자의 로딩 완료 후 대기실로 복귀한다. 진행 중 이탈도 판을 중단하고 대기실로 복귀한다. 준비 상태와 개인 진영을 초기화한다.
- 씬 로딩 확인이 45초를 넘기면 방을 닫고 로비로 복귀한다.
- 방장이 대기실 복귀를 누르면 같은 방과 참가자를 유지한 채 모두 돌아간다. 다시 준비해야 다음 판을 시작한다.

대기실/게임 씬을 연결 없이 직접 실행하면 로비로 이동한다. 빌드 시작 순서는 LobbyScene, WaitingRoomScene, GameScene이며 기존 목록의 다른 씬은 뒤에 보존한다. Windows 개발 빌드 메뉴도 세 씬을 포함한다. 기존 MultiplayerBase는 `Development > Create or Open Legacy Multiplayer Base`로 연다.

## 후속 작업과 검증

게임 맵은 바닥·벽·장애물로 만든 기본 맵이다. 홀짝 검사 장치 1개와 개인 단서 노트를 연결했고, 다른 정보 장치·포섭·협동 미션은 아직 온라인으로 연결하지 않았다. [InformationDevices.md](InformationDevices.md)를 참고한다.

패키지를 변경하지 않는다. 에디터 Play와 PlayMode 테스트를 실행하지 않는다. EditMode에서 로딩 대상/참가자 검증, 지연·중복 완료, 이탈, 시간 초과, 세 씬 구성과 세션 참조·빌드 설정을 확인한다. 실제 여러 클라이언트의 씬 동시 이동과 Steam 방 재입장은 사용자의 별도 실행 지시 후 검증할 항목이다.

2026-10-10, Unity 6000.3.22f1 배치 검증: **EditMode 86개 통과, 실패/건너뛰기 0개**. 기존 73개와 새 로딩 검증 9개, 씬·프리팹 구성 4개를 포함한다. PurrNet 코드 생성까지 컴파일했고 보고서는 `Logs/scene-flow-editmode.xml`에 저장한다. 패키지 manifest/lock 해시는 작업 전후 동일하다. 새 씬 흐름과 이전 단일 씬 빌드가 서로 섞이지 않도록 방 인증/Steam 목록 프로토콜을 3으로 올렸다.
