# 멀티플레이 베이스

어몽어스의 탑다운 이동/친구들과의 대기실을 참고한 회색 박스 프로토타입이다. 모델과 맵은 임시 Unity 기본 도형이다.

## 실행

1. `Assets/_Project/Scenes/MultiplayerBase.unity`를 열고 Play한다. 없으면 `ZZabmongus > Create or Open Multiplayer Base` 메뉴로 만든다.
2. 이름을 입력하고 `CREATE ROOM`을 누른다. 기본 UDP 포트는 5000이다.
3. 다른 실행 인스턴스에서 호스트의 IP와 같은 포트를 입력하고 `JOIN ROOM`을 누른다. 같은 PC에서는 `127.0.0.1`이다.
4. WASD/방향키로 이동하고 색상을 선택한다. 4~12명 모두 READY일 때 호스트가 START SESSION을 누른다. 연결/이동 확인은 1~3명으로도 가능하다.
5. 호스트는 RETURN TO LOBBY로 준비 상태를 초기화한다. 참가자는 LEAVE ROOM으로 나갔다가 대기실에 재접속할 수 있다. 진행 중인 세션은 신규 참가를 거부한다.

`ZZabmongus > Build Multiplayer Windows Test Player`는 이 씬만 포함한 Development 빌드를 `Build/MultiplayerBase/ZZabmongus.exe`에 만든다. 에디터와 이 빌드를 동시에 실행해 테스트할 수 있다. 기본 Build Settings의 SampleScene은 자동으로 바꾸지 않는다.

## 현재 구현 범위

- PurrNet 호스트/클라이언트 연결, 최대 12명, 연결 제한 시간, 나가기와 재접속.
- 서버가 캐릭터 생성과 소유권을 배정하고 이탈 시 캐릭터를 제거한다.
- 이름(최대 16자), 중복되지 않는 색상, 준비 상태를 서버 SyncVar로 복제한다.
- 소유자만 입력 RPC를 보낼 수 있다. 서버가 입력 길이를 제한하고 CharacterController로 이동/벽 충돌을 계산한다. 오래된 입력 패킷은 무시하고 0.25초 입력이 끊기면 멈춘다.
- 서버 권한 NetworkTransform이 위치를 전송하고 클라이언트가 보간한다. 입력은 20Hz, 네트워크 틱은 30Hz이다.
- 로컬 플레이어만 조종하고 카메라가 해당 플레이어를 따라간다.
- 4명 이상 모두 준비한 대기실에서 호스트만 세션을 시작/종료한다.

이 씬의 START SESSION은 대기실/세션 상태만 전환한다. 기존 Core의 번호 배정, 비밀 진영, 정보 장치, 미션, 채점은 아직 네트워크에 연결하지 않았다. 로컬 샌드박스의 Tab 플레이어 전환이나 개발자 노트북을 멀티 씬에 넣지 않는다.

## Steam P2P

이미 설치된 PurrNet Steam Transport와 Steamworks.NET을 사용한다. Connection 버튼으로 Steam P2P를 선택하고 호스트의 64비트 Steam ID를 입력한다. 호스트 자신의 Steam ID는 연결 후 상태 영역에 표시된다. Steam API는 Steam 모드를 선택해 접속할 때만 초기화하고 콜백을 관리한다.

실제 게임의 App ID와 실행 중인 Steam이 필요하다. 개발 시 승인된 App ID를 `steam_appid.txt`로 직접 설정하거나 Steam 실행 환경을 사용한다. 임의 App ID나 Spacewar 파일을 자동으로 생성하지 않는다. Steam 로비 검색/초대 UI는 아직 구현하지 않았다. 서로 다른 Steam 계정/PC 사이의 P2P 검증은 App ID 설정 후 진행한다. 기본 UDP는 LAN/로컬 테스트용이며 인터넷 접속을 위한 NAT 우회 기능이 없다.

기본 TMP 폰트는 Latin 글꼴이다. 한글 플레이어명/전체 한국어 UI용 CJK 폰트 설정은 별도 작업이다.

## 패키지 변경 정책

**패키지 추가·변경 전 항상 사용자에게 질문하고 승인을 받는다.** 이번 작업은 패키지를 추가하거나 버전을 바꾸지 않는다. 설치 메뉴는 사용자 확인 창을 띄우며 배치 모드 설치를 거부한다.

## 검증

Development 빌드의 `-zzNetProbe host|1|2|3 -zzNetPort 15000 -zzNetResult <절대 JSON 경로>`는 실제 네 프로세스 테스트에만 쓰는 선택 기능이다. 일반 실행에서는 비활성이고 Release 빌드에는 동작 코드가 없다. 호스트/3클라이언트 연결, 이름/색상/준비 복제, 원격 이동, 세션 시작, 참가자 이탈/제거, 재접속, 재시작, 호스트 종료 후 정리를 점검한다. 결과는 지정 JSON과 각 프로세스 로그에 저장된다.
