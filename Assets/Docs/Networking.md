# PurrNet + Steamworks.NET

2026-10-08에 공식 GitHub 최신 정식 릴리스 API를 확인했습니다.

| 패키지 | ID | 고정 버전 |
|---|---|---|
| PurrNet | `dev.purrnet.purrnet` | `1.24.1` (`v1.24.1`) |
| Steamworks.NET | `com.rlabrecque.steamworks.net` | `2025.164.1` |

PurrNet 1.24.1의 태그와 release 브랜치는 확인 시점에 `7061f0a5b46b0aaff85d5dfe0f9791c5b153012a`를 가리켰습니다. 개발 브랜치나 베타가 아닌 최신 정식 태그를 사용합니다. Unity의 `packages-lock.json`이 실제 해시와 전이 의존성을 기록합니다.

설치는 `UnityEditor.PackageManager.Client.AddAndRemove`로 처리합니다. 재설치 메뉴는 `ZZabmongus > Install Pinned Network Packages`이며 실행할 때 사용자 확인을 받습니다. 배치 모드 설치는 거부합니다. **패키지 추가·업데이트 등 변경 전에는 항상 사용자에게 물어보고 승인을 받습니다.** 자동 업데이트는 하지 않습니다.

설치 성공 로그는 `Logs/zzabmongus-package-install.txt`에 있습니다. 두 패키지를 포함해 Unity 컴파일, EditMode 46개, PlayMode 1개 테스트가 통과했습니다. 에디터 자동화 용도로 `com.unity.pipeline` 0.8.0-exp.1도 추가되어 있습니다. PurrNet의 Steam 어셈블리는 Steamworks.NET 패키지 감지를 통해 활성화되며 별도 수동 전역 define은 추가하지 않았습니다.

- [PurrNet 설치 문서](https://purrnet.dev/docs/getting-started/installation-setup)
- [PurrNet 1.24.1](https://github.com/PurrNet/PurrNet/releases/tag/v1.24.1)
- [PurrNet Steam Transport](https://purrnet.dev/docs/systems-and-modules/transports/steam-transport)
- [Steamworks.NET 2025.164.1](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.164.1)

PurrNet에 Steam Transport가 포함되어 있고 이 전송 계층은 Steamworks.NET을 요구합니다. Facepunch.Steamworks를 중복 설치하지 않습니다. 이후 멀티 베이스 작업은 기존 패키지만 사용했습니다.

`MultiplayerBase`에는 NetworkManager, UDP/SteamTransport, Steam 초기화/콜백 수명 관리, 국가별 Steam 방 검색/생성, 공개·비밀번호 입장, 정원 검증, PurrNet 대기실과 PlayerID별 캐릭터 생성/정리를 구성했습니다. Steam 친구 초대 UI는 아직 없습니다. Core의 실제 번호와 마피아 진영 전체를 SyncVar로 전체 전송하면 안 됩니다. 실행 방법은 [MultiplayerBase.md](MultiplayerBase.md)를 참고하세요.

로컬 `steam_appid.txt`는 사용자 설정 파일이며 Git에서 제외합니다. 로비는 Steam 실행과 유효한 App ID가 필요합니다. UDP 개발 연결과 로컬 샌드박스는 Steam 없이 사용할 수 있습니다. 별도 Steam 계정 간 검색/생성/P2P 입장은 실제 계정 환경에서 검증해야 합니다. 패키지를 추가하거나 변경하지 않고 기존 버전 위에 로비 기능을 구현했습니다.
