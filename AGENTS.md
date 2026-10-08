# ZZabmongus 작업 규칙

- 이 프로젝트는 어몽어스의 탑다운 이동과 파티 플레이를 참고한다. 고유한 번호 추리 규칙은 `Assets/GameDesign-v0.4.md`를 기준으로 한다.
- **패키지 추가·업데이트·재설치·제거 전에 반드시 사용자에게 물어보고 명시적인 승인을 받는다.** 이전 패키지 설치 승인을 다음 변경에 재사용하지 않는다. 이번 멀티 베이스 작업에는 패키지 변경이 승인되지 않았다.
- 기존 PurrNet과 Steamworks.NET을 사용한다. 로컬 테스트에는 UDP, Steam 테스트에는 PurrNet Steam Transport를 사용한다.
- 서버가 생성, 소유권, 이동, 대기실 상태를 결정한다. 실제 비밀 번호와 전체 진영 정보는 모든 클라이언트에 방송하지 않는다.
- Unity 씬, 프리팹, ScriptableObject는 Unity Editor API로 생성/수정한다.
- **사용자가 별도로 지시하지 않으면 Unity 에디터의 Play 모드가 필요한 테스트를 실행하지 않는다.** PlayMode Test Runner, 자동 Play 진입, 직접 Play 진입 모두 포함한다. 컴파일 진단과 Play 진입 없는 검증을 사용한다.
