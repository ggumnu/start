# 블아이작 (Mob_archive) - 핵심 상태 요약 (v1.77)

## 이 문서의 규칙 (항상 지킬 것)

- 이 문서는 **"지금 무엇이 맞는지"만** 담음. 설계 배경/버그 진단/세션 흐름은 적지 않음 - 그건 각 스크립트 상단 한국어 주석, `Docs\설계노트.md`,
  아래 아카이브에 있음(필요할 때 장치에서 읽음).
- 갱신은 형빈이 명시적으로 요청할 때만(세션 중간에 임의로 건드리지 않음). 갱신할 때는 기존 줄을 고치거나 한두 줄만 추가하고,
  지난 내용(예전 방식/버전별 경위)은 지움.
- **크기 상한 30,000자**: 넘으면 그 시점 문서를 장치 `Mob_archive\Docs\`에 새 아카이브로 그대로 보관(프로젝트 지식엔 올리지 않음)하고, 이 문서를 다시 짧게 정리.
- **변경 로그는 버전당 한 줄**(무엇을 했는지만, 파일 목록/버그 경위 X).
- 확인 필요 / 할 일·보류 항목은 `[N]` + 불릿 표기(마크다운 순서 목록 `1.` 금지 - 뷰어가 번호를 다시 매겨 번호 혼선 사고가 있었음).
  번호는 두 칸이 공유(새 항목은 가장 큰 번호 + 1). 확인 필요 = 형빈 확인/결정이 필요한 것(해결되면 지움), 할 일·보류 = 나중에 만들 것/참조 메모.
- 아카이브는 한 번 만들면 절대 수정/삭제하지 않음.

## 아카이브 목록

- 장치 `Mob_archive\Docs\`(Assets 밖, git 포함 - 프로젝트 지식엔 없음):
  - `claude_Mob_archive_진행상황_통합요약_v1.26.md` - v1.4~v1.26 전체 히스토리(설계 배경 + 버그 진단).
  - `claude_Mob_archive_핵심상태_아카이브_v1.27-v1.45.md` - 제어 체계/수류탄·해킹/애니메이션 경위, 툰 셰이더, TMP 전환.
  - `claude_Mob_archive_핵심상태_아카이브_v1.46-v1.52.md` - 상점 UI, 특수방, 비주얼/애니메이션/HUD 세부 수치.
  - `claude_Mob_archive_핵심상태_아카이브_v1.53-v1.57.md` - 아이템 확정값 전체, 상점 가격/진열, 슬롯 아이템 세부.
  - `claude_Mob_archive_핵심상태_아카이브_v1.58.md` - SD 신규 캐릭터 세부, 이어하기 세부.
  - 이 문서의 정리 전 원문(`claude_Mob_archive_핵심상태_아카이브_vX.md`): v1.61(시작/중단 화면·R 재시작·도감·보호막·HUD, 58/67/93/97번),
    v1.62(컷씬/헬기, bpy 애니메이션, 팝업 패턴), v1.64(방 범위/레벨 조명·밤 빛/2레벨 보스/등록소 목록), v1.65(인코딩 파일 목록, 배경/SD 파츠 절차/슬롯 아이템),
    v1.66(사운드/설정 창, 가속·뛰기·흙먼지·피해 숫자), v1.67(뛰기 연결, 2레벨 보스 패턴), v1.68(시작 화면·상점 카드·UI 색/글자),
    v1.69(SD 파츠 슬롯·흔들림 본·ID 맵·SD 텍스처 작업 세부, 피해 숫자 연속 피격 수치, 수류탄/해킹 창구 목록),
    v1.70(촬영 스튜디오 세부, 정리 전 플레이어/UI/아이템 줄 원문), v1.71(정리 전 원문 - 게임 클리어/조작법/n.5 건너뛰기, 작업 방식 세부 줄),
    v1.73(정리 전 원문 - 빌드 함정/판 정리 세부, 촬영 스튜디오 줄), v1.75(정리 전 원문 - 변경 로그 v1.71~v1.74, 슬롯/상점/전투 연출 줄),
    v1.76(정리 전 원문 - 배포/통계/업데이트 알림 세부, 플레이어·아이템·반복 작업 긴 줄).
  - `설계노트.md` - 스크립트 주석에서 옮긴 설계 배경 + 2레벨 보스 기획/구현 전체.
  - 배포 자료: `배포_README_초안.md`(GitHub README 원본), `플레이통계_AppsScript.gs`(구글 시트 수신 스크립트 원본).
- 프로젝트 지식: `claude/claude_Mob_archive_핵심상태_아카이브_v1.59.md` - 이 문서에서 줄인 v1.58/v1.59 아이템 확정값 원문.

## 작업 방식

- 형빈은 3D 모델러(코딩 X), Claude가 C# 전량 담당. 답변/안내는 항상 한국어.
- 장치마다 작업 폴더가 다르고 git(`저장하기.bat`)으로 동기화 - 연결된 폴더는 세션마다 확인.
- **스크립트의 원본은 장치 폴더**. 프로젝트 지식엔 스크립트 사본을 두지 않음(이 문서 + v1.59 아카이브 + SD 텍스처 도구 `claude/tools/sd_texture/`만) - 코드는 매번 장치에서 읽음.
- 여러 채팅이 동시에 작업할 수 있음 - 기존 파일은 고치기 직전에 다시 스테이징해 최신본 기준으로 편집. 이 문서도 갱신 직전에 다시 읽어 합침.
- 장치 파일 작업: 스테이징 → 편집 → 커밋(`expectedMtimeMs`) → **반드시 다시 스테이징해 `cmp`**(같은 출력 경로 재커밋 시 옛 내용이 들어감 - 이름을 바꿔 커밋).
  mtime 거부 = 장치 파일이 바뀜 → 재적용. 장치 파일 삭제는 못 함 - 형빈이 Unity에서(컴포넌트 먼저 떼고 파일).
- "분석부터"/"계획부터"/"바로 진행하지 말고"라고 하면 코드 없이 분석/계획만. "가능할까?"도 계획 + 정할 점으로 답함.
- 착수 전: 기획 이해 + 코드 확인 결과 + 계획 + 정할 점 제시, 승인 후 코딩. 형빈이 미리 "짚을 점 없으면 바로 진행"이라 한 턴은 예외.
  기획 문구에 모순이 있으면 두 해석을 숫자로 보여주고 고르게 함. 화면 꾸미기는 코드 전에 같은 계산으로 미리보기 이미지를 만들어 보여줌.
- 원인이 불확실한 시각 문제는 임시 측정 컴포넌트로 숫자를 재서 가름.
- 코드는 항상 완성된 전체 파일로 전달. 기존 스크립트 수정은 "어느 파일의 무엇을" 먼저 밝히고 승인 - 파일 수 최소화보다 안정성 우선.
  요청 범위 밖의 기존 동작은 그대로 두고 짚기만 함(형빈이 정함).
- 설계 전 실제 프리팹/코드/에셋(씬 오버라이드 포함)을 먼저 확인. 프리팹 구성(콜라이더/태그)에 의존하는 판정은 피함.
- **플레이어 찾기는 `FindFirstObjectByType<PlayerHealth>()`(루트)** - "Player" 태그는 자식에도 붙어 있음(위치만 쓸 땐 태그 OK).
- 이름 변경 시 Assets 전체(.asset/.prefab/.unity) 검색, 직렬화 필드는 `[FormerlySerializedAs]`, `[SerializeReference]` 클래스는 `[MovedFrom]`. 효과 클래스에 새 칸 =
  옛 에셋은 0/빈 값(코드에서 대비). **스크립트 파일 이름 변경은 형빈이 Project 창에서 F2로**(.meta 유지).
- ScriptableObject 클래스는 파일 이름과 같은 파일에 따로 둠(다른 파일 안에 두면 에셋이 깨짐).
- 유니티 안내는 메뉴/버튼/인스펙터 필드명까지 클릭 단위로(컴포넌트 찾기 = Hierarchy `t:이름`). 에셋/애니메이터 준비가 필요하면 안내 대신 메뉴 도구/자동 생성.
- 인코딩: 새 파일 = UTF-8(BOM) + CRLF. 기존 파일은 원래 인코딩/줄바꿈 보존(CP949/무BOM/LF/BOM 2개 파일 목록은 v1.65 아카이브 - 고치기 전 `file`로 확인).
- 요청 없는 버전 태그 주석 금지.
- **주석 정책(고치는 파일에만 적용)**: 한국어로 "왜"만(역할 요약, 버그 방지 경고, 연결, 툴팁). 긴 경위는 `Docs\설계노트.md`로. 보고 때 기능/주석/설계노트를 나눠 알림.
- 애니메이션 = 클라우드 `bpy`, 오디오 = ffmpeg/librosa, SD 텍스처 = `claude/tools/sd_texture/README_SD텍스처도구.md`, 컷씬 촬영 = v1.71 아카이브.
- 컴파일이 끝나기 전에 Play하면 튕길 수 있음 - 진행 표시가 끝난 뒤 Play하도록 안내.
- 컴파일 사전 검사: mono `mcs` + Unity API stub(mcs는 로컬 함수 미지원 - 일반 메서드로), apt가 막히면 pip `tree-sitter-c-sharp`로 문법만. 못 한 파일은 Console 확인 요청. TMP API = 장치 `Library/PackageCache/com.unity.ugui@*`.
- ScriptableObject 메뉴는 `MobArchive/<이름>`. 런타임 설정 에셋은 `Assets/Resources/<이름>` 자동 로드(없으면 기본값) 방식이 표준.
- 새 UI 글자는 TextMeshPro `HUDText.AddTo`/`SetOutline`. 같은 Canvas 안에서는 나중 자식이 위.
- Inspector `OnGUI`/`PropertyDrawer`에서 에셋 검색은 static 캐시로(렉 방지).
- **빌드 함정**(에디터는 Play마다 정적 메모리를 비우고 모든 셰이더를 찾음): `Shader.Find`로만 찾는 자체 셰이더 = Always Included Shaders(지금 9개), TMP Underlay =
  `Resources/Fonts/TMP_UnderlayKeep` 재질이 있어야 남음, 정적 구독/값은 판 정리, 플레이어는 태그로 찾지 않음 - 세부는 v1.71/v1.73 아카이브.
  빌드의 비동기 씬 불러오기에서는 `SceneManager.sceneLoaded`가 새 씬 `Start`보다 늦을 수 있음 - 씬 전환 판별은 `LoadingScreen.OnBeforeSceneChange` 기준으로.
- 입력: Active Input Handling = **Both**("Replace with InputSystemUIInputModule" 경고는 무시).

## 프로젝트 개요

- Unity 6.3 LTS(URP) 3D 탑다운 로그라이트. 아이작 리펜턴스식 룸 진행/아이템 시너지 + 블루아카이브식 등각 카메라/캐릭터/아이템.
- 플레이어 모델: **`SD_Player`(형빈 직접 모델링)만 관리**. 유니티짱(UCL 3.0)/레거시 레이사 프리팹은 남아 있지만 더 이상 맞추지 않음.
- **배포 중(2026-11, 넥슨 고객센터 비영리 배포 허락)**: 공개 저장소 `ggumnu/Reisa_archive-release`의 Release에 빌드 zip만(아래 [배포]). 코드 저장소는 비공개 유지.
  커뮤니티/유튜브 설명란에 링크로 "아는 사람만 아는" 배포(itch.io X). 포트폴리오 겸용.
- 식별자: 샬레 `Schale`, 청휘석 `Pyroxenes`, 등급 `Tier0`~`Tier3`, n.5 선택방 서쪽 `Gematria`(`gematriaChance` - FormerlySerializedAs 유지)/동쪽 `Schale`.
  Variant ID는 Config 에셋의 `variantId`. `ReloadTime` 표기 = "장전 시간"(작을수록 빠름).

## 확정 상태

### 배포 / 빌드 (`Docs\배포_README_초안.md` = GitHub README 원본)

- Player 설정: Company `GGUMNU` / Product `Reisa_Archive` **절대 변경 금지**(플레이어 PC 저장 폴더 = 도감/통계 번호가 사라짐). Scripting Backend **IL2CPP**,
  C++ Release, Managed Stripping **Minimal**(High는 쓰는 코드를 지울 위험), Windows x64, Development Build 끔. IL2CPP는 VS "C++ 데스크톱 개발" + Windows 11 SDK 필요.
- **Version = Release 태그와 반드시 일치**(통계 `version` + 업데이트 알림 비교 기준). 빌드 전 확인(0.9.0으로 배포된 사고 있었음).
- 빌드 = `Builds\vX.Y.Z`(gitignore) → `..._BackUpThisFolder_ButDontShipItWithYourGame`(+있으면 `BurstDebugInformation_DoNotShip`)를 `Builds_Debug\vX.Y.Z`로 이동
  (gitignore, 크래시 분석용 보관) → 나머지를 `Reisa_Archive` 폴더에 묶어 **`Reisa_Archive_Windows.zip`(이름 고정)**.
- GitHub: 새 버전 = 새 Release(태그 `vX.Y.Z`, 예전 것은 남김), 같은 버전 교체 = Release 편집 → zip 교체. 안내 링크 `…/releases/latest`. 업데이트 = 덮어쓰기.
  다운로드 수 = API `…/releases`의 `download_count` 합, 유입 = Insights → Traffic(14일).

### 플레이 통계 (`Assets/Script/Analytics`, 설정 `Resources/PlayStatsSettings`)

- `RunAnalytics`: 판마다 기록 1줄을 판이 끝날 때 구글 Apps Script 웹 앱으로 전송(항상 전송, 끄기 옵션 없음, README에 고지). 보낸 적 없는 기록은
  `persistentDataPath/play_stats.json`에 쌓았다 다음 실행 때 재전송, 진행 중 판은 15초/방 진입마다 저장 → 강제 종료 = 다음 실행 때 `crash`.
- 기록 항목은 `RunAnalytics.cs` 맨 위 주석(시작/종료 방식, **리롤 = 시작방을 안 나간 판**, 시간, 사망 위치·원인, 도감, 아이템). 에러 = `errors` 탭(경로/이름 제거). 개인정보 X.
- 연결: `GameFlowManager.StartNewGameNow` → `BeginRun`, `GameClearUI.Show` → `NotifyGameClear`, `ItemCodexRecord.MarkAcquired` → `NoteItemAcquired`,
  `GameFlowManager.MarkItemAssignedThisLevel`(상자/받침대/상점만 - 다음 레벨로 들고 간 슬롯 기록은 `AddAssignedThisLevel`) → `NoteItemAppeared`.
- **사망 원인 `PlayerDamageSource`**: `PlayerHealth.TakeDamage(피해량, 원인)` → `LastDamageSource`. 근접/원거리/보스 판별은 컴포넌트로, 공격 = contact/dash/bullet/artillery,
  자해 = grenade/item_artillery, 체력 특수방. 원인 없이 부르면 "unknown". 일반 적 이름은 `Enemy_`(variantId가 비어 있음 - 그대로 두기로 함).
- 에디터는 전송 X(`sendInEditor`, Console에 표시), 테스트 씬 기록 X. 메뉴 `MobArchive/플레이 통계/`(내 번호/아이템 목록 복사, 대기 기록 지우기).
- 구글 시트 `Reisa_Archive 플레이 통계`(ID `1Zr_U_rySqcOIzivwZ7AI-wxCOMlOPGIlgwI2yU0fGGM`, Claude가 Google Sheets 커넥터로 읽음): 탭 `runs`/`errors`(자동 생성)/`items`(아이템 목록 메뉴로 붙여넣기,
  아이템이 바뀌면 갱신). Apps Script = `Docs\플레이통계_AppsScript.gs`(TOKEN = 설정의 Token).
  **스크립트 수정 시 배포 관리 → 새 버전**(새 배포 = 주소가 바뀌어 기존 빌드의 기록이 끊김).
- 집계 규칙: `recordId` 중복 제거(전송 직후 종료 시 재전송됨), `editor` = 1 제외, **제외 번호 = 형빈 `2690c9e4…`, 동생 `c1895df3…`**. 클리어율·평균 시간은 리롤 제외.

### 업데이트 알림 (`UpdateNoticeUI` + `Resources/UpdateNoticeSettings` - 없으면 기본값에 저장소 주소)

- 시작 화면(`TitleScreenUI.Awake`에서 `UpdateNoticeUI.Ensure`)에서 GitHub 최신 Release 태그만 읽어 `Application.version`보다 새로우면 실행당 1번 창
  ([다운로드 페이지 열기] = 설정의 `https://github.com/` 주소만 엶 / [나중에]). 시작 연출·도감/조작법/설정 창이 끝난 뒤(`TitleScreenUI.IsBusy`) 띄움.
  자동 다운로드/설치는 하지 않음(보안 - 방식 A 확정). 실패하면 조용히 넘어감. `forceShowInEditor` = 모양 확인용.

### 게임 흐름

- 씬: `Title`(Build Profiles 0) + `LevelBuilder`(1). LevelBuilder 단독 Play도 동작.
- 전투 레벨 config는 `GameFlowManager.combatLevelConfigs`(Element 0 = 레벨 1)가 빌드 때마다 `LevelBuilder.config`에 덮어씀.
  레벨 = `CurrentLevelNumber` + `CurrentPhase`(Combat = n / Choice = n.5). 빌드 순서 = 조명 `LevelLighting.Apply` → `BuildLevel` → 밤 빛 자동 부착 → 배경.
- **n.5 건너뛰기** `GameFlowManager.skipChoiceLevels`(기본 켬, 데모용): 전투 n → n+1 직행. 다음 전투 레벨 진입 처리(레벨 아이템 기록 등)는
  `EnterNextCombatLevel` 공용. n.5 코드는 그대로 남김(끄면 예전 1 → 1.5 → 2).
- **깎인 최대 체력은 다음 레벨에도 유지**(3/5 → 3/5). 예전 규칙(빈 칸 정리 3/5 → 3/3)은 `GameFlowManager.trimMaxHealthOnNextLevel`(기본 끔)로 켤 수 있음.
- **게임 클리어**: `gameClearLevel`(기본 2) 보스 헬기 [예] → `HeliExtraction.BeginGameClearExtraction`(끌어올려질 때, ESC = 바로) → `GameClearUI`
  (`Resources/GameClearUISettings`, 글자 크기/위치 칸). 기록 = `GameFlowManager.PlayTimeSeconds`/`EnemiesKilled`/`RoomsCleared` - 세부는 v1.71 아카이브.
- `TitleScreenUI`: 게임 시작/도감/조작법/설정/나가기, 시작 연출(처음만), 오른쪽 아래 `creditText`(넥슨 저작권 + 음악 출처) - 세부는 v1.68 아카이브.
- `LoadingScreen`(두 씬에 배치, order 1000, `BlocksPlayerInput`). 모든 씬 전환은 이 경로 - 직전 알림 `OnBeforeSceneChange`(판 정리).
- 사망: `PlayerHealth.Die()` → `OnDied`/`OnAnyPlayerDied` → `GameOverUI`(order 100, 2초 뒤 [예]/[아니오]).
- ESC 중단 화면 `PauseMenuUI`(order 95, 버튼 두 줄 [계속하기][설정][조작법] / [도감][재시작하기][그만하기]), R 2초 재시작 `HoldRestart`(order 99) - 세부는 v1.61/v1.66 아카이브.
  씬을 떠나는 처리 = `PauseMenuUI.LeaveScene()`(이어하기 저장 자리). 레벨 이동 확인창 `LevelTransitionConfirmUI`.
- 설정 창 `SettingsUI`(볼륨, PlayerPrefs) / 조작법 창 `ControlsUI`(`Resources/ControlsUISettings` 키 목록) - 둘 다 order 150, `BlocksInput`.

### 연출 - 입장 컷씬 / 헬기 퇴각 (자동 생성, 설정은 `Resources`의 각 Settings 에셋 - 세부는 v1.62 아카이브)

- 입장 컷씬 `IntroCutscene`([게임 시작]일 때만, 건너뛰기 가능) / 헬기 퇴각·도착 `HeliExtraction`(포탈 프리팹 `HeliExtractionPortal`). 둘 다 `BlocksInput`/`IsPlaying`.
  헬기 연출 중엔 총을 숨김(`HideGun`/`RestoreGun`, 총 = `PlayerWeaponMode.gunModel` 우선). 두 연출 모두 카메라 끝 위치를 `CameraFollow.offset`에서 읽음.

### 포트폴리오 촬영 스튜디오 (`Assets/Scenes/CutsceneStudio.unity`, `Assets/Script/Cutscene` + `Assets/Editor/CutsceneStudio*.cs`) - 세부는 v1.70 아카이브

- 장면마다 `StudioShot` + 대본 `Assets/Cutscene/Shots/<장면>.txt`(문법은 `ShotScript.cs` 맨 위 주석), 적 배우 `StudioEnemyActors`, 녹화 `Recordings/`(ProRes 422).
- **게임 스크립트 안의 촬영용 연결(지우지 말 것)**: `Playermovement`(`CutsceneStudio.OverridesMovement` 등), `PlayerTravelBoost.TickForcedMax`,
  `HoldRestart`/`PauseMenuUI`(`CutsceneStudio.IsActive`), `EnemyRadialAttack.SkipChargeTime`.

### 사운드 (`Assets/Script/Audio`, 클립 `Assets/Audio/SFX`·`BGM`)

- 재생 창구 `GameAudio`: `Play(SfxId)`, 반복음 `PlayLoop`/`StopLoop`. 설정 `Resources/SoundLibrary`(파일 이름 `SFX_<SfxId 이름>(_N)`).
- **`SfxId` enum 값은 절대 바꾸지 않음**(에셋이 숫자로 저장) - 새 소리는 그 묶음의 빈 번호/끝에 추가(지금 끝 = 113). 기존 게임 알림 구독은 `SoundEventHooks`, 직접 호출 줄엔 `[사운드]` 주석.
- UI 소리 `UISound`(되돌릴 수 없는 버튼 = Danger), 액티브 공용음 `ItemActivate`(전용음이면 `IHasOwnUseSound`). 출처 `Assets/Audio/SFX/_출처.txt`.
- **BGM**: 인트로 → 반복 곡(`Crystal Gear Surge`, Eleven Music **무료 플랜** = 출처 표기 필수 → 타이틀에 "Music created in collaboration with ElevenLabs") - 세부는 v1.66 아카이브.

### 아이템 도감

- `ItemCodexUI`(설정 `Resources/ItemCodexSettings`), 기록 `ItemCodexRecord`(`MarkAcquired`, 통계용 `GetAcquiredIdsSnapshot`) - 세부는 v1.61 아카이브.

### 비주얼 / 셰이더

- `MobArchive/Toon_Environment`(배경) / `MobArchive/Toon_Character` - URP/Lit 프로퍼티 이름 호환(`_BaseMap`/`_BaseColor`/`_EmissionColor`/`_Surface` - 새 셰이더도 유지).
  툰 셰이더는 주광 **색만** 씀(세기/환경광/추가 조명 무시).
- 레벨별 조명 `LevelLighting.Apply`(레벨 설정 `lightingProfile`, v1.64), 밤 빛 `NightLight` 자동 부착 `NightLightAutoRules`(v1.65).
- 배경 외곽선 보정 `OutlineSmoothNormalBaker`(v1.61), 화면 배경 `LevelBackground.Apply`(레벨 설정 `backgroundMaterial`, v1.65). 굴절 셰이더(90/110번) = URP Opaque Texture.
- 빌드 VSync = Quality `PC` 단계 `Every V Blank`(프레임 제한 없던 문제).
- 게임 카메라 `GameCamera` + `CameraFollow`: 각도 (50, -45, 0), Offset (2.25, 3.75, -2.25) - 거리 조절은 세 값을 **같은 비율로**.

### 플레이어

- 관성 없는 CharacterController 이동, twin-stick 조준(우클릭 = 마우스 조준), 사거리 소멸 탄환, 무적 프레임 체력.
- 프리팹 `Assets/Player/Character/Player/SD_Player`(값은 이 프리팹만 고침 - `UnityChan_Player`/`Reisa_Player_Fixed`는 관리 안 함).
  쓰는 프리팹은 `LevelBuilder`/`ItemTestArenaBuilder`의 Player Prefab 칸으로 교체. 기본 이동 속도 2.
- **SD_Player 충돌 캡슐**: 높이 0.6 / 반지름 0.3 / Skin Width 0.03(줄이지 않음) / **Center Y 0.33**(발 접지 보정).
- `PlayerStats` 단일 소스: (기본 + 덧셈 합) x 곱셈 곱 → `statFloors`(최종 값에만). 새 `StatType`은 **enum 맨 끝**에 + 매핑 추가.
  `RemoveModifiersFromSource`/`RemoveModifiersFromSourceExcept(id, 남길 스탯)`. 특수효과가 준 스탯은 출처 `PassiveEffectIds.EffectStatSourceId(id)`
  (= id + "_effect", 잃어도 남는 "귀속" 스탯). 잃으면 사라지는 실시간 스탯은 별도 출처(예: 76번 id + "_passiveCount").
- **공격력 하한선 = 고정값(Absolute) 1**(Player 프리팹 `statFloors`). 하한선 없이 아이템 효과만 적용 = `PlayerStats.ApplyModifiersTo(StatType, 기본값)`(87번 아군).
- **최대 체력 상한 `maxHealthCap` 10** - 넘는 증가분은 기억하지 않고 그만큼 회복(`OnMaxHealthIncreaseCapped`). 테스트 프로필 `noMaxHealthCap`/`noShieldCap`으로 테스트 씬만 해제.
- **이동 속도 상한 `moveSpeedCap` 4** - 최종값만 자름(초과분은 모디파이어로 남아 기억됨).
- **비전투 가속 `PlayerTravelBoost`**(`Resources/TravelBoostSettings`): 비전투 1초 뒤 x1.5(상한 5), 공격/조준/장전/피격/전투방에서 풀림 - 세부는 v1.66 아카이브.
- **보호막**(아이작 소울하트, 최대 5): `PlayerHealth.AddShield(수, 연출 여부)`/`SetShield`, 피격 1번을 통째로 막음. 연출 `PlayerShieldBreakVfx`(`GetSharedMesh` - 2레벨 보스 막도 씀).
- **연사 = 클릭당 탄약 1발 + `Multishot` 발**. 알림 `PlayerShooting.OnAmmoConsumed`/`OnShotFired`/`OnReloadStarted`, `TryInstantReload()`. 투명 `PlayerInvisibility`(112번).
- **자동 장전** `TryStartAutoReload`(탄약 0이면, 연출 중엔 X), 다음 레벨은 탄창 가득 `RefillForNewLevel` - 세부는 v1.76 아카이브.
- 74번 돌진 `PlayerDash`(`IsDodging` - 회피는 피해원마다 확인), 액티브 사용 규칙 `IActiveItemUseRules.CanActivateNow`, 디버프 면역 `PlayerDebuffImmunity`(94번).
- 시작 자원은 `PlayerResources`. 무기 모드 `PlayerWeaponMode` 1 총/2 수류탄/3 해킹 - 0개여도 들어갈 수 있고, 0이 되는 순간에만 총으로 자동 복귀.
  수류탄/해킹 모드 자원 표시 = `WeaponModeResourceIndicator` + 탄약 보조 상자(`AmmoBoxDisplay`).
  **총 숨김** `PlayerWeaponMode.UpdateGunVisibility`(수류탄/해킹 모드, 뛰기, 획득 연출 중).
- 수류탄 `PlayerGrenadeThrower`/`Grenade`(폭발 알림 `OnGrenadeExploded` - 받는 곳마다 따로 실행, 중심 `CurrentGroundPoint()`), **118번 포격 `ArtilleryStrike`는 폭발 처리를 따로 구현**(반복 작업 [21]).
  **108번 폭죽탄 = 진짜 수류탄**(`Grenade.PrepareAsFirework` - 꼬리/폭발 이펙트/폭발음만 다름, `SpawnFireworkShell`/`NotifyFireworkExploded`/`IsExplodingFirework`) - 수류탄 아이템이 자동 적용.
  해킹툴 `PlayerHackTool`(`ApplyHackArea`/`ApplyHackToEnemy`, `OnHackDeployed`/`OnHackToolSpent`, 아이템이 자원 없이 펼치기 = `DeployFromItem`). 창구 목록은 v1.69 아카이브, 세부는 v1.53-v1.57 아카이브.
- 아이템 획득 연출: `ItemPickupEvents.Raise` → `PlayerItemPickupPresenter`(연출 중 행동 차단 `BlocksActions`). 여러 개 = `PassiveRerollUI.RaisePickupsInSequence`.
- F 상호작용 = 가장 가까운 하나(`InteractionTargetSelector`). 팝업이 떠 있으면 대상 없음.
- 테스트: `PlayerTestProfile` → `ItemTestArenaBuilder.testProfile`(39/72/111번은 실제 레벨에서).

### 플레이어 애니메이션

- Mixamo FBX 15개(Humanoid), Controller `ReisaAnimatorController`(SD 직접 사용).
- **지우면 안 됨**: `Reisa_AvatarSource.fbx`, `Animation/Generic/` 전체, `Animation` 원본 FBX, `Reisa_Fixed.fbx`, `Reisa_gun_Fixed.fbx`, `Assets/UnityChan`.
- 레이어 Locomotion/UpperBody/UpperBodyTorso(Sync), 조준 IK `HumanoidAimLookAt`, 반동 `UpperBodyRecoil`. 프리팹 `MeshRotationOffset` Y -20 = Mixamo 모션용 보정.
- **SD 전용 모션**(`Assets/Player/Character/Animation`): `SD_Reload`, `SD_IntroPose`, 뛰기 `SD_Run`(A)/`SD_Run_B`(B, 사용 중 - 메뉴 `MobArchive/애니메이션/비전투 달리기 - B`로
  다시 연결), 흙먼지 `PlayerRunDust` - 세부는 v1.66/v1.67 아카이브.

### SD 신규 캐릭터 (`Assets/Player/Character/Player/SD`) - 세부는 v1.58/v1.62 아카이브

- 알바디 `SD_AltBody_v02.fbx`(바이패드 `Bip001`, Humanoid, 기본 자세 신발 바닥 = 높이 0, 키 약 0.46) + `SDCharacterSkin`/`SDPartAttacher`(`Equip`/`Unequip`)/`SDFaceController`/`SDFaceExpressionSet`.
  셰이더 `MobArchive/Toon_Character_SD`/`Toon_Face_SD`. 재질 자동 설정 `MobArchive/SD 캐릭터 재질 자동 설정`.
- **파츠 슬롯** `SDPartDefinition`(Slot enum은 끝에만 추가). `PlayerHitFlash.ResetToNormal` 이름은 리플렉션으로 불림 - 바꾸지 말 것(v1.69).
- **효과 제외 표시 `PlayerEffectExclude`**: 붙은 오브젝트(자식 포함)는 피격 깜빡임/112번 투명/74번 잔상에서 빠짐(헤일로).
- 헤일로 `SDHaloController`, 총 `SDGunController`, 흔들림 본 `SDJiggleBones`, 색 영역 = ID 맵(최대 32) - 세부는 v1.69 아카이브.
- 현재 레이사 파츠: 머리 `Hair_Reisa02`(`Hair_Reisa01`은 예비), 전면 `AccFront_Reisa01`, 후면 가방 `AccBack_Reisa01`, 총 `Gun_Reisa01`(세부는 v1.69 아카이브).
- 표정(`SDFaceGameEvents`, 우선순위 큰 것이 보임): 기본(0) < 뛰기 `Run` 눈4/입1(5) < 장전(10) < 획득(20) < 피격(30) < 사망(100). 새 표정은 반복 작업 [30].
- **실시간 초상화 `PlayerPortraitCamera`**(SD_Player 루트, 카메라 2대, 하늘 색은 레벨 배경이 덮어씀, 레벨 조명 틴트 적용).

### 적 AI

- 근접 `EnemyAIController`, 원거리 `EnemyRangedAIController`, 보스 아래.
- **적 프리팹 Rigidbody는 Is Kinematic + 중력 끔**(아니면 판정 위치가 어긋남) - 돌진 부품이 없는 적 컨트롤러는 Awake에서 직접 맞춤(`BossLv2AIController`처럼).
- 레벨별 일반 적: `RoomGenerationConfig`의 일반 적 칸(`normalEnemyPool` 비우면 `RealEnemySpawner`의 Enemy Pool). 강화 적 = `Assets/Config/Enemy/lv1`·`lv2`의 EnemyDefinition.
- 적 탄환 = 코드 생성 구체 + `EnemyProjectile`(정적 목록 `Active`, 쏜 적 = `SetDamageSource`) - 한 발 = 한 번 명중.
- 적 수치: 공통 = `EnemyDefinition`(모든 적 Attack Power 1), 공격 방식별 = `attackConfig`.
- **목표 정하기 `CombatTargeting`**(`Resolve`/`CurrentTarget`/`TargetIsInvisiblePlayer`): 적 = 플레이어(투명 제외)/아군 중 가까운 쪽, 아군 = 가장 가까운 적.
  **새 적 AI/공격도 이 창구 사용**. 켜진 적 목록 `EnemyHealth.ActiveEnemies`.
- **87번 아군 `AllyFaction`**(세부는 v1.59 아카이브): Enemy 태그 제거(태그를 안 보는 새 효과는 `AllyFaction.IsAlly`로 제외).
- 정적 등록소 `Enemy...Override/Modifier`(목록 v1.64), 처치 이벤트 `EnemyHealth.OnAnyEnemyKilled`(구독은 try/catch).
- 피해 입구: `EnemyHealth.TakeDamage`(총알 등 - 공격력과 비교) / `TakeDotDamage`(지속) / `TakeWeakDamage`(아이템 부가 피해 - 항상 Weak) - 처리는 같고 피해 숫자 구분 플래그만 다름.
  비교 기준을 줄인 총알 = `TakeDamageWithReferenceScale`(63번 미사일 - 공격력 x 배율과 비교).
- 제어 체계 `EnemyControlState`: 강력(수면/해킹) 1칸 + 일반(매료) 1칸. 새 제어 = `EnemyControlType` 추가 + `GetTier()`(규칙은 v1.65 아카이브).
- 적 상태 `EnemyChillState`/`EnemyDotState`/`EnemySugarStack`, 이펙트 `EnemyStatusVfx`. 전투방 재입장 시 적만 재생성.
- 넉백 `ForcedPush.Apply`(적도 밈) - 밀리는 동안 `ForcedPush.IsBeingPushed`면 AI가 이동 판단을 쉼. 돌진 중 판정 = `IDashDamageSource.IsDashing`.

### 보스 (설계/기획 전체는 `Docs\설계노트.md` [2레벨 보스] 등)

- 보스 판별 = `GetComponent<BossAIController>() != null`(체력바, 매료/오한/넉백/87번 제외, 90번 규칙) - **새 보스 컨트롤러는 `BossAIController`를 상속**.
  보스 선택 = Level Config `bossVariants`의 variantId ↔ 씬 `RealEnemySpawner.bossPool`(빈 variantId = 기본 = 1레벨 보스). 보스 수치는 프리팹에 직접.
- 1레벨 `EnemyBoss_DroneElite_Boss_lv1`(5연사/전방위/연속 돌진), 2레벨 `..._lv2`(`BossLv2AIController` + `BossShield`, 세부는 설계노트).
- 보호막 `BossShield`(피해 흡수, 제어 필터 `IEnemyControlFilter`, 보호막 바 `BossHealthBarDisplay`), 소환 = `RealEnemySpawner.SpawnSummonedEnemy` +
  `EnemyKillObjective.TryAddAlongside`. 90번 물결 규칙은 v1.64 아카이브.

### 레벨 생성

- `RoomGenerationConfig` → `LevelGraphGenerator` → `LevelBuilder.BuildRooms`, `RoomVisibilityManager`가 현재 방만 활성화. 방 = 한 변 `cellSize`(6) 칸 하나.
  NavMesh는 방마다 런타임 베이크. 층 이동 = `LevelRoot` 통째 삭제 후 재생성. 방은 런타임 생성이라 정적 배칭 없음.
- 현재 방: `LevelBuilder.CurrentPlayerCell`/`GeneratedRooms`/`GetRoomController(id)`, 위치가 속한 방 = `RoomScope.RoomOf`.
  방 클리어 `RoomController.OnAnyRoomCleared`(일반방/보스방만), 방 입장 `OnAnyRoomEntered`(둘 다 받는 곳마다 따로 실행), `RequiresClear`. 전투 없는 방 = `SlotItemDropper.IsInCombatFreeRoom()`.
  상자 열림 공용 알림 `ContainerOpenEvents.OnAnyContainerOpened`(TreasureChest/ItemPedestal 처음 열 때만, 보관 좌대 X - 97번).
- **방 범위 규칙 `RoomScope`**: 그 방에서 생긴 일은 그 방에서만(소속 = 생성 순간 플레이어가 있던 방). `DestroyWhenPlayerLeaves(go)` = 방을 나가면 즉시 삭제,
  적 총알은 쏜 방을 벗어나면 소멸, 플레이어 없는 방의 수류탄/115/118 폭발은 조용한 폭발(특수방 잠금만 `TryUnlockByGrenade`).
- Wall 태그로 방 벽 구분 불가(120번은 칸 경계). 방 추가 `TryAddExtraRoom`, 텔레포트 `TryTeleportPlayerToRandomRoom`. 보물상자 `NormalRoomTreasureSpawner`.
- **레벨 내 아이템 중복 등장 방지**: `GameFlowManager` `MarkItemAssignedThisLevel`/`FilterOutAlreadyAssignedThisLevel`/`Unmark...`(레벨마다 초기화). ID로 찾기 `ResolveItemById`.
- 아이템 등장 가중치 = `ItemWeightModifier.GetWeight(item)`(권장 weight 등급별 100/80/60/40).

### 특수방 (크레딧 더미 `credit_pile`만 제작됨) - 세부는 v1.46-v1.52 아카이브

- `RoomCategory.Special`(4) 하나로 통합(5, 6번 비어 있음). 종류 = 레이아웃 에셋 `SpecialRoomLayout` → Level Config `specialLayouts`. 항상 막다른 방.
- 진입 조건(Free/Grenade/HackingTool/Health) 추첨, 잠금 `SpecialRoomLock`(수류탄/해킹 알림 구독, 목록 `SpecialRoomLock.All`), 체력 = 지날 때마다 0.5 피해.

### 상점 (편의점 `convenience_store`만 제작 중) - 세부는 v1.53-v1.57 아카이브

- 바닥 프리팹 안 `ShopNPC` + 보관 좌대 `HoldingPedestal` 4개(범용 좌대). 가격 = `ShopNPC.GetUnitPrice`. 창 `ShopUI`(order 85, 세부 v1.68).

### 팝업 / 공용 UI

- **블루 아카이브 스타일(전 UI)**: `UIPatternBox` + `Resources/PopupStyle`, 버튼 `UIButtonKind` Positive 노랑 / Quit 회색 / Normal 하늘색, **뗀 순간 클릭**.
  **새 UI도 이 부품/에셋 값 사용**(세부는 v1.68/v1.70 아카이브). 키 배지/수치 판 = 남색(`namePlateColor`) 평행사변형(`SetupShape`, 조작법 창/결과 화면).
- 팝업 공통 패턴(세부는 v1.62 아카이브): static `IsOpen` + `BlocksInteract` - `InteractionTargetSelector`/`HUDClickBlocker`/`PauseMenuUI`/`SlotItemDropUI`/`HoldRestart`가
  확인함. **새 팝업을 만들면 이 다섯 곳에 조건 추가**.
- 사용 불가 이유 알림 창 = `PassiveRerollUI.ShowMessage(플레이어, 문구)`.
- **머리 위 아이콘 연출 범용** `CreditPopupDisplay`(Item54effect.cs 안, order 8) - 대상별 줄 세우기. 성공/실패 알림 공용 `HeadAlertRoomRules`(39/103/111번).

### 슬롯 아이템 (장신구 / 일회용 Q / 음식 E) - 세부는 v1.53-v1.57 아카이브

- `PlayerTrinket`/`PlayerDisposableItem`/`PlayerFoodItem`: 먼저 얻은 것이 앞, `SlotCount` = 1 + `AddExtraSlots`, 꽉 차면 가장 먼저 얻은 것이 교체.
- 일회용/음식은 앞칸만, `CanUse` false = 소모 X, 버리기 Ctrl 0.8초 → `SlotItemDropUI`(세부는 v1.65 아카이브).

### 아이템 시스템

- 패시브: `ItemDefinition.specialEffects`(`[SerializeReference] IPassiveItemEffect` - `[Serializable]` + 매개변수 없는 생성자, 에셋의 효과 인스턴스를
  공유하므로 런타임 필드는 `[NonSerialized]`, `Apply`는 먼저 이전 구독 해제), 훅 `OnProjectileSpawned`/`OnShotFired`/`Projectile.OnDespawning`.
  "보유 중" 상시 동작은 런타임 러너 MonoBehaviour(지울 땐 `DestroyImmediate`). 정적 알림을 구독하는 효과는 플레이어가 사라지면 스스로 구독 정리(97번).
- **판 정리**: `OnBeforeSceneChange`에서 `PlayerInventory`/`PlayerTrinket`이 보유 효과 `Remove()`, 43번 버프·27/40번 음식 대기·`Next...Override` 무장도 해제(v1.73 아카이브).
- **총알 피해** `Projectile.Damage` = (기본 + `AddDamageBonusProvider`) x `AddDamageMultiplierProvider` → 하한 → 23번. 명중 = (`Damage` + `PlayerBulletBonusDamage`) x 받는 피해 배율
  (`OnDespawning` 중엔 충돌 순간 값 고정). 튕김 횟수 `BounceCount`/`WallBounceCount`/`EnemyBounceCount`.
- **총알 1발 = 적 1명**(소멸 확정 총알은 무시, 관통처럼 `CancelDespawn`한 총알만 계속 맞힘). 명중 알림 `OnPlayerBulletHitEnemy`/`OnPlayerBulletHitEnemyDetailed`.
- **관통**: 적 = `SetEnemyPiercing`(99번), 벽 = `SetWallPiercing`(120번), 둘 다 = `SetPiercing`(110번). 벽 관통 총알은 처음 있던 방의 칸을 벗어나면 소멸.
- **상극** `ItemDefinition.incompatibleItemIds` + `ItemCompatibility`(99↔69, 120↔35). 시너지 감시 `SynergyWatcher`(102+109). 확률 판정 창구 `LuckOdds.Roll`.
- **패시브 잃기** `IRemovablePassiveEffect.Remove`(새 패시브는 필수), `PlayerInventory.RemoveItem`/`ReplaceItems`/`TryAcquireItem`. 잃어도 최대 체력/특수효과 스탯/
  1회성 재화/보호막은 남음. 변화 알림 `OnPassiveItemsChanged` + `PassiveCount`.
- **1회 지급** `GameFlowManager.TryConsumeOneTimeGrant("{ID}_용도")`(한 판 유지), 새 사본 `AcquireItemCopy` = 매번 지급, 48/56번 `IgnoreOneTimeGrantLimit`.
- 액티브 `IActiveItemEffect`/`IDelayedActiveItemEffect`, 음식 `IFoodEffect`, 일회용 `IDisposableItemEffect`, 장신구 `ITrinketEffect`. 상태 있는 효과 재실행은 `JsonUtility` 복제.
  방에 적이 없으면 사용 불가인 액티브(29/43/108번 등) = `IActiveItemUseRules.CanActivateNow`(코스트 X, 알림 없음).
- 구현된 아이템 = `Assets/Script/Item`의 `ItemNN effect.cs`(상세 기획은 각 파일 주석, 번호 목록은 v1.64 아카이브). 1.0.x 전체 아이템 98개(`ItemDatabase` 기준) + 이후 59/60/63/108번 추가.
  (4번 폭발 효과 클래스 `ExplodeOnImpactEffect`는 `Item04effect.cs` 안에 있음. 94번 후광 피해는 `BulletHalo.cs`.)
- 아이템별 확정값: 10~86번 v1.53-v1.57 / 87~137번 v1.59 / 58·67·93·97번 v1.61 아카이브 + 각 파일 주석. 43번 = 같은 방 사용마다 x1.5 곱 중첩(상한 없음, 방 나가면 해제).
- 플레이어 총알 색: 등록소 `ProjectileColorOverride` + `BulletColorEffect`(23번 색이 최우선).
- ItemDefinition 에셋은 복제해서 만들어도 됨 - `Item Id` 변경 + ItemDatabase 등록 필수(도감도 ItemDatabase 기준). 상점에 나오게 하려면 Appearance Contexts에 `Store`.
- 정적 등록소 = 이름이 `...Override`/`...Modifier`인 클래스(아이템 번호별 목록은 v1.64 아카이브), 각각 해제 메서드 있음. 분류별 보정 공용 계산 `CategoryModifierSet`(67).
- 등급 색 = Tier0 흰 / Tier1 청록 / Tier2 노랑 / Tier3 자홍.

### 패밀리어 / 소환물 (59/60/63/108번 - 기획 세부는 각 파일 맨 위 주석)

- 공용 `FamiliarDrone`(모델 생성, 하얀 등장/흰 디졸브 퇴장, 관성 따라가기 + 호버 + 기울기, 자세(Pose)/반동, 프로펠러 회전, 회색 틴트, 뒤쪽 VFX) +
  `FamiliarChain`(플레이어에 자동 부착, **얻은 순서대로 줄 세우기**, 순간이동/헬기 사다리에 같이 따라감). 파티클 크기 변경 = `FamiliarVfx`(바꾼 뒤 `Restart` - Prewarm 거대 링 방지).
- 59번 `Item59DroneRunner`: 해킹 범위 75% 안의 "자기 의지로 움직이는" 적(해킹/수면/매료(보스 제외)/넉백/아군 제외) → `PlayerHackTool.DeployFromItem`(원래 반경, 자원 X).
  해킹 사용으로 침(특수방 해제/소리/보스 보호막 보너스 O, 58번 보호막 X). 쿨타임 15초 동안 회색.
- 60번 `Item60DroneRunner`: 피격(체력/보호막 깎임)마다 25%(`LuckOdds`)로 보호막 +1 + 드론 자세 + 뒤쪽 노란 빛.
- 63번 `Item63DroneRunner` + `HomingMissile`: 오른쪽 어깨 위 고정 자리, 10초마다 4 x `Multishot` 발(방 입장 0.5초 뒤부터, 가까운 적부터 고르게 분배). 미사일 =
  **진짜 총알 `Projectile` 미사일 모드**(`PlayerShooting.SpawnMissileProjectile`, `IsMissile`/`EffectScale` 0.5): 총알 아이템 적용(4/94/68/118/23번은 절반),
  35/99/120/11/113번 제외, 5번 첫 발 1회 분열, 69번 1회 튕김, 피해 숫자 기준 = 공격력 x 0.5. **미사일 방향은 `SetMissileDirection`(transform을 직접 돌리면 보간 Rigidbody가 멈춤)**.
- 108번 `FireworkTurret` + `FireworkShell`(액티브, 코스트 5): 플레이어 앞 바닥에 고정 포대 5초(0.5초부터 1초마다 4발), 포대에서 가장 먼 적 락온(죽으면 다음),
  포구 `Object42`가 포물선 각도로 기울고 반동. 폭죽탄 = 수류탄 피해/반경 x 25%, 자해 0.5 고정, 착탄 흩뿌림. 적이 없으면 잠긴 수류탄 바리케이드를 쏨.
  플레이어가 방을 나가면 즉시 삭제. 사용 조건 = 방에 적 또는 잠긴 수류탄 바리케이드. 여러 대 설치 가능. 발사 위치 = 프리팹 베리언트의 `FirePoint_1~4`(파란 화살표가 포구 밖).
- 패밀리어/소환물 공격 대상은 **87번 아군만 제외**(매혹된 적도 공격 - 매혹 제외는 59번 해킹 판정에만).

### 전투 연출

- **피해 숫자 `DamagePopupDisplay`**: 총알만 공격력과 비교(흰/Weak 노랑/Resist 하늘색), 지속·아이템 부가 피해는 항상 Weak, 보스 보호막 피해는 Resist(v1.69).
  비교 기준 배율 = `EnemyHealth.DamageReferenceScale`(63번 미사일 0.5).
- `EnemyHealthBarDisplay`, `CombatVfxSpawner`. 이펙트 조정 = `Assets/VFX/Custom/`에 복제해 수정.

### HUD

- `PlayerHUDManager` 단일 Canvas(1920x1080), 값은 `PlayerHUD` 프리팹(Overrides → Apply All). 글꼴 `Resources/Fonts/경기천년제목_Bold SDF`(**이름 변경 금지**).
- 새 스탯 한글 이름 = `ItemInfoPanelView.GetStatDisplayName`.
- **미니맵 `MinimapDisplay`**: 포탈 아이콘 없음, 보스방 아이콘은 클리어 후에도 유지(`keepIconAfterVisitCategories`).

## 반복 작업 (새 에셋/씬을 만들 때마다 확인)

- [1] Wall 태그 콜라이더는 새 프리팹도 Box/Convex 체크리스트 적용.
- [2] 새 원거리 적 프리팹엔 `EnemyRangedAIController` 확인.
- [3] 상체 동작이 몸 전체를 흔들면 RootFixed 사본 + Override 연결.
- [4] 교체 확인창이 필요한 슬롯 아이템은 아이템 에셋의 `Confirm Before Replace` 체크.
- [5] 새 모델/재질은 `MobArchive/셰이더 일괄 교체`로 툰 적용(텍스처만 넣으면 됨 - 노멀맵 때문에 안 바뀐 사례 있음). 새 씬은 조명/환경광/Skybox/Volume을 [비주얼] 값으로.
- [6] 포트폴리오에 유니티짱이 나오면 UCL 3.0 표기.
- [7] 새 게임 씬은 `LoadingScreen` 프리팹 배치 + Build Profiles 등록(Title 0번 유지).
- [8] 새 상점 바닥 프리팹 = `ShopNPC` + `HoldingPedestal` 4개, Shop Config와 아이템 Appearance Contexts를 맞춤.
- [9] 새 특수방 레이아웃: 레이아웃 에셋 + Room Visual Set + **레벨마다** Level Config의 Special Layouts에 등록.
- [10] 복제로 만든 ItemDefinition은 `Item Id`를 새 번호로 + ItemDatabase 등록. 상극이 있으면 `Incompatible Item Ids`에 상대 ID(한쪽만 적어도 됨).
- [11] 총알 색 아이템 = Special Effects에 `BulletColorEffect`. 새 제어 지속 이펙트 = 프리팹 복제 + 출처 효과에 설정 칸 + `Attach`.
- [12] 새 패시브 효과는 `IRemovablePassiveEffect.Remove` 구현(48/56/62번으로 잃을 수 있음). 1회성 지급은 `TryConsumeOneTimeGrant` + `IgnoreOneTimeGrantLimit`
  예외 처리.
- [13] 테스트 씬에서 48/56/62/80번을 쓰려면 아이템 에셋 Disposable Effect의 `Item Database` 칸을 채움.
- [14] 새 팝업 UI = `UIPatternBox` + `PopupStyle` + `IsOpen`/`BlocksInteract`를 위 다섯 곳에 연결 + ESC 겹침(`PauseMenuUI.CanOpenNow`) + UI 소리(`UISound`).
  시작 화면 창이면 `TitleScreenUI`의 막는 조건 + `UpdateNoticeUI.ShouldOpen`에도 추가.
- [15] 새 아이템 획득 경로: `AcquireItemCopy`, 추첨 `ItemWeightModifier.GetWeight`, 가격 `ShopNPC.GetUnitPrice`, 상극 `ItemCompatibility`, 넣을 땐 `TryAcquireItem`/`ReplaceItems`
  (직접 고치면 `ItemCodexRecord.MarkAcquired`). 수류탄 계열 자해 = `GrenadeSelfDamageOverride.IsBlocked` + `PlayerDash.IsDodging`.
- [16] 스스로 이동하는 새 적 AI/공격 컴포넌트를 만들면 `ForcedPush.IsBeingPushed(gameObject)` 검사 + `EnemyAttackSpeedModifier` 적용. 수류탄 폭발 위치는
  `OnGrenadeExploded`의 위치를 사용.
- [17] 새 총알 피해 효과 = `AddDamageBonusProvider`/`AddDamageMultiplierProvider`/`PlayerBulletBonusDamage`, 튕김 = `Redirect`/`RedirectFromEnemy`(관통이면 `IsPierceHit`).
  **새 총알 아이템은 63번 미사일(`Projectile.IsMissile`)에 적용할지/배율(`EffectScale`)을 곱할지 확인**.
- [18] 새 SD 파츠 절차는 v1.69 아카이브.
- [19] 몸에 붙이는 물건은 **`Bip001` 뼈** 아래에. 컴포넌트 Copy/Paste 후 참조 칸은 다시 연결. 피격/투명/잔상 제외는 `PlayerEffectExclude`.
- [20] 적을 대상으로 하는 새 플레이어 효과는 "Enemy" 태그로 고르거나 `AllyFaction.IsAlly`로 87번 아군 제외. 새 적 AI/공격은 `CombatTargeting`으로 목표를 정함.
- [21] 새 수류탄 계열 아이템은 `Grenade.cs`(108번 폭죽탄에도 자동 적용 - 수류탄 겉모습을 바꾸는 효과면 폭죽탄 제외 확인)와 118번 포격 `ArtilleryStrike.cs` 양쪽에 반영할지 확인.
- [22] 플레이어 머리 위 알림 + 이펙트는 `CreditPopupDisplay.Show(..., followTarget: true, onStarted)`로.
- [23] 새 플레이어 피해원은 `PlayerHealth.TakeDamage(피해량, PlayerDamageSource)`를 거쳐야 보호막이 막고 통계 사망 원인이 남음(적 탄환은 `EnemyProjectile.SetDamageSource`).
  피격 반응은 `IsHitInvincible`·`WillShieldBlockNextHit`로 거르고 `OnDamageTaken` 구독. 보호막 주기 = `AddShield`.
- [24] 외곽선 보정 배경 모델은 `Assets/Object` 아래, 정적 배칭 금지, 내보내기 전 Reset XForm.
- [25] 스크립트를 고칠 때 그 파일에 주석 정책 적용 + "레벨당 1회" 옛 주석/툴팁이 있으면 새 1회 지급 규칙에 맞게 고침.
- [26] 새 레벨: Level Config → `combatLevelConfigs` 등록 → 보스면 `bossPool` 같은 variantId. 새 보스 = `BossAIController` 상속 + Kinematic + `BossPortrait`.
  마지막 레벨이 바뀌면 `gameClearLevel`도 맞춤(세부는 v1.76 아카이브).
- [27] 방에 남는 새 오브젝트(투척물/VFX/지속 구역)는 `RoomScope.DestroyWhenPlayerLeaves`, 터지는 것은 플레이어 없는 방이면 조용한 폭발.
- [28] 새 창문/자판기/가로등 모델은 `NightLightAutoRules`에 등록(견본 Glow = `NightGlow_Mat`).
- [29] 새 소리 = `SfxId` 새 값(기존 번호 변경 금지) → `SoundLibrary` 메뉴 → `GameAudio.Play`. 방에 남는 반복음은 `PlayLoop(roomScoped)` + `StopLoop`, 출처는 `_출처.txt`.
- [30] 새 SD 표정은 `SDFaceExpression` 맨 끝에 추가 + 표정 세트 에셋 칸. 새 SD 직접 제작 모션이 정면 기준이면 `MeshRotationOffset` -20 보정을 그 상태 동안 풀어야 함.
- [31] **총알 말고 아이템이 주는 모든 부가 피해**(폭발/구역/틱/충돌/방 전체/87번 아군 공격 등)는 `EnemyHealth.TakeWeakDamage`(피해 숫자 항상 Weak), 지속 피해는
  `TakeDotDamage`로 줌. 일반 `TakeDamage`는 총알처럼 공격력과 비교할 피해에만.
- [32] 촬영용 새 적/보스 동작은 `StudioEnemyActors` + `ShotScript` 주석에 추가.
- [33] 새 조작 키를 만들면 조작법 창 목록(`ControlsUISettings.sections` 기본값 또는 `Resources/ControlsUISettings` 에셋)에도 추가.
- [34] 정적 구독/값을 남기는 새 효과는 판 정리 확인(패시브/장신구는 `Remove()` 자동, 그 밖은 `OnBeforeSceneChange`로 해제). 이름으로만 찾는 새 셰이더는 Always Included.
- [35] **새 버전 배포**: Version 올림(태그와 같게) → 아이템이 바뀌었으면 시트 `items` 갱신 → 빌드 → 디버그 폴더 이동 → 빌드 테스트(시트에 새 `version` 줄 확인) →
  zip(이름 고정) → 새 Release + 패치 노트(실행/통계 고지 문구 포함). 새 판 진행 경로(시작/끝)를 만들면 `RunAnalytics` 연결도 확인.
- [36] **새 패밀리어/소환물** = `FamiliarDrone`(+ 따라다니면 `FamiliarChain`) 재사용, 모델은 `MobArchive/셰이더 일괄 교체`. 발사 위치/회전 부품은 프리팹 베리언트에 빈 오브젝트로
  붙이고 이름으로 찾음. 목표는 `HomingMissile.CollectRoomEnemies`/`IsValidTarget`(87번 아군만 제외).

## 확인 필요 항목 (해결되면 지울 것)

- [96] 외곽선 2차 보정 확인(작은 소품 두께/창문 음각, `OutlineWidthSettings`).
- [99] (형빈 정리 중) `Assets/Audio/BGM/BGM_CrystalGearSurge_Original.wav`(비교용 원곡, 안 씀) 삭제 여부, 효과음 클립 음량/음색 다듬기.
- [100] BGM 출처 표기(무료 플랜 = 필수)를 `_출처.txt`와 포트폴리오 영상 크레딧에도 같은 문구로 넣기(타이틀은 완료).
- [111] 16:9 아닌 모니터에서 빌드 UI 위치 확인.
- [104] (형빈 정리, 선택) SD 테스트/레거시 에셋 삭제 여부(목록은 v1.76 아카이브).

## 할 일 / 보류 (나중에 만들 것, 참조 메모)

- [48] 행운(Luck) 스탯 미구현 - 반영 창구 `LuckOdds`(지금 60/106번). 54번 처치 확률/80번 개수 확률도 이 창구로 옮김.
- [49] 미니맵 마커 아이콘 8종 스프라이트 연결(포탈 아이콘은 폐지).
- [54] 등급 스프라이트 오른쪽 "태그 UI" 자리 남아 있음.
- [78] (참조만) 특수방 진입 비용 변형(수류탄 3개 등) 레벨별 검토.
- [79] 특수방 개수 규칙(Level1 Special 필수 1개 - 확인용)/레이아웃 가중치 조정 예정 + 테스트 레이아웃 정리 필요.
- [81] 비어 있는 방 번호 5, 6 - 나중에 새 방 종류에 사용.
- [82] (보류, 참조만) 이어하기(저장 슬롯 1개, 사망 시 삭제) - 세부는 v1.58 아카이브, 저장 자리는 `PauseMenuUI.LeaveScene()`(그만하기만). 저장은 `persistentDataPath`에(덮어쓰기 업데이트에도 유지).
- [87] **다음 작업 후보 - SD 외형 아이템 시스템 + 외형 설정 창**(파츠/헤일로/총 선택, ID 색 바꾸기 - 교체 창구는 준비됨). 세부는 v1.58 아카이브.
- [88] (보류) `SDPartAttacher` Skeleton Root 실수 방지 장치(캐릭터 밖 오브젝트면 자동 탐색).
- [95] (형빈 모델링) 골목 끝 바닥 틈 - `Field_Player` 스케일 미적용이 원인, Reset XForm 후 재수출 예정.
- [97] **3레벨 준비**: `Level3Config`(2레벨 복사본, Level Number 3으로 고칠 것) - 보스/강화 적/배경 `BG_ScreenSky_Lv3`/밤 조명 연결 필요.
- [98] (참조만) 90번은 `RoomScope` 방 범위 정리에서 일부러 제외.
- [101] (형빈) 빈 효과음 `GunShot`/`ChargeShotFire` 넣기.
- [102] (보류) BGM 여러 곡 - 지금은 한 곡 고정, 화면별 곡 교체 기능이 필요.
- [108] (보류) SD 걷기 발 가라앉음(2~3cm).
- [109] (보류) `Item104effect.cs` 맨 위 주석을 "클릭당 +3"으로 고칠 것(동작은 확정대로).
- [110] (보류, 참조만) n.5 레벨 재구상 - 지금은 `skipChoiceLevels`로 건너뜀(선택방 보상/아이템은 아직 미제작).
- [112] **통계 분석** - 형빈이 "통계 보여줘"라 하면 시트 `runs`/`errors` + GitHub 다운로드 수로 정리(집계 규칙은 [플레이 통계]). 초기 신호: 1레벨 보스 탄환 사망이 많음 - 표본이 쌓이면 난이도 판단.
- [113] (보류) 자동 패치 런처(방식 B) - 하게 되면 서명 검증 + 별도 런처 + 실패 복구가 필요(백신 오탐 위험). 지금은 알림(방식 A)만.
- [114] (형빈) 임시 효과음 교체: `Missile63Launch`/`Missile63Hit`(지금 포격 낙하음 사본), `Firework108Launch`/`Firework108Burst`(지금 수류탄 투척/폭발음 사본) - 새 클립을 같은 파일 이름으로 덮어쓰면 됨.
- [115] 다음 배포 때 시트 `items` 갱신 필요(59/60/63/108번 추가됨 - 반복 작업 [35]).

## 변경 로그 (버전당 한 줄)

- v1.4~v1.57: 기반 구축 ~ 신규 SD 캐릭터(각 버전 구간 아카이브 참고).
- v1.58~v1.67: 아이템 대량 추가, 보호막/도감/중단 화면, 컷씬·헬기, 2레벨 + 보스, 레벨별 조명/배경, 사운드/설정 창, 뛰기, 전 UI 블루 아카이브 스타일.
- v1.68~v1.70: SD 레이사 파츠 확장, 부가 피해 항상 Weak, 발 접지, 카메라 접근, 포트폴리오 촬영 스튜디오 + 컷씬 02~07 촬영.
- v1.71~v1.74: 데모 마무리(n.5 건너뛰기, 조작법 창, 게임 클리어 결과 화면, 저작권 표기), 빌드 전용 문제 정리(셰이더/판 정리/태그 찾기/알림 분리), 자동 장전.
- v1.75: 다음 레벨 진입 시 최대 체력 빈 칸 정리 폐지(깎인 최대 체력 유지, 옛 규칙은 설정 칸으로 남김).
- v1.76: GitHub 배포(IL2CPP 빌드, v1.0.0/v1.0.1), 익명 플레이 통계(구글 시트 + 사망 원인 `PlayerDamageSource`), 시작 화면 업데이트 알림.
- v1.77: 패밀리어 공용 체계(`FamiliarDrone`/`FamiliarChain`) + 59(해킹)/60(보호막)/63(유도 미사일 = 총알 판정) 드론, 108번 폭죽 포대(폭죽탄 = 수류탄).
