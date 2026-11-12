# 블아이작 (Mob_archive) - 핵심 상태 요약 (v1.65)

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
  - `claude_Mob_archive_핵심상태_아카이브_v1.61.md` - v1.61 시점 이 문서 원문(시작/중단 화면·R 재시작·도감·보호막·HUD 세부, 58/67/93/97번 확정값).
  - `claude_Mob_archive_핵심상태_아카이브_v1.62.md` - v1.62 시점 이 문서 원문(입장 컷씬/헬기 연출 세부, bpy 애니메이션 제작 요점, 팝업 공통 패턴/SD 세부).
  - `claude_Mob_archive_핵심상태_아카이브_v1.64.md` - v1.64 시점 이 문서 원문(방 범위 규칙/레벨 조명·밤 빛/2레벨 보스/등록소 목록 세부).
  - `설계노트.md` - 스크립트 주석에서 옮긴 설계 배경 + 2레벨 보스 기획/구현 전체.
- 프로젝트 지식: `claude/claude_Mob_archive_핵심상태_아카이브_v1.59.md` - 이 문서에서 줄인 v1.58/v1.59 아이템 확정값 원문.

## 작업 방식

- 형빈은 3D 모델러(코딩 X), Claude가 C# 전량 담당. 답변/안내는 항상 한국어.
- 장치마다 작업 폴더가 다르고 git(`저장하기.bat`)으로 동기화 - 연결된 폴더는 세션마다 확인.
- **스크립트의 원본은 장치 폴더**. 프로젝트 지식엔 스크립트 사본을 두지 않음(이 문서 + v1.59 아카이브 + SD 텍스처 도구만) - 코드는 매번 장치에서 읽음.
- 여러 채팅이 동시에 같은 프로젝트를 작업할 수 있음 - 기존 파일을 고치기 직전에 항상 다시 스테이징해 최신본 기준으로 편집
  (예전 outputs 사본과 `cmp`해서 다르면 다른 채팅이 고친 것). 형빈이 "새 내용이 안 보인다"고 하면 장치 파일부터 다시 받아 확인.
  이 문서도 갱신 직전에 다시 읽어 다른 채팅의 갱신 위에 합침.
- 장치 파일 작업: 스테이징 → 편집 → `SendUserFile` → 그 **`fileUuid`로 커밋**(`expectedMtimeMs`) → 다시 스테이징해 `cmp`. mtime 거부 = 장치 파일이 바뀜 → 다시 받아 재적용.
- "분석부터"/"계획부터"/"바로 진행하지 말고"라고 하면 코드 없이 분석/계획만. "가능할까?"도 계획 + 정할 점으로 답함.
- 착수 전: 기획 이해 + 코드 확인 결과 + 계획 + 정할 점 제시, 승인 후 코딩. 형빈이 미리 "짚을 점 없으면 바로 진행"이라 한 턴은 예외.
  기획 문구에 모순이 있으면 두 해석을 숫자로 보여주고 고르게 함. 화면 꾸미기는 코드 전에 같은 계산으로 미리보기 이미지를 만들어 보여줌.
- 코드는 항상 완성된 전체 파일로 전달. 기존 스크립트 수정은 "어느 파일의 무엇을" 먼저 밝히고 승인 - 파일 수 최소화보다 안정성 우선.
- 설계 전 실제 프리팹/코드/에셋을 먼저 확인(YAML/에셋 값/컴포넌트 순서, 모델 치수는 FBX 직접 파싱). 프리팹 구성(콜라이더/태그)에 의존하는 판정은 피함.
- **플레이어 찾기는 `FindFirstObjectByType<PlayerHealth>()`(루트)** - "Player" 태그는 자식에도 붙어 있음(위치만 쓸 땐 태그 OK).
- 이름 변경 시 Assets 전체(.asset/.prefab/.unity)를 대소문자 무시로 검색. 직렬화 필드 이름을 바꿀 땐 `[FormerlySerializedAs]`.
  `[SerializeReference]` 효과 클래스는 클래스 이름으로 저장됨 - 이름을 바꿀 땐 `[MovedFrom]` + 에셋 강제 저장 후 보호 속성 제거. 기존 효과 클래스에 새 칸을
  추가하면 옛 에셋은 그 칸이 0/빈 값일 수 있음 - 코드에서 빈 값 대비 또는 메뉴 도구로 채움(ScriptableObject 설정 에셋은 새 칸에 코드 기본값이 들어감).
  **스크립트 파일 이름 변경은 형빈이 Unity Project 창에서 F2로**(.meta 유지) - Claude는 내용만 고침.
- ScriptableObject 클래스는 파일 이름과 같은 파일에 따로 둠(다른 파일 안에 두면 에셋이 깨짐).
- 유니티 안내는 메뉴/버튼/인스펙터 필드명까지 클릭 단위로. 컴포넌트 찾기는 Hierarchy 검색 `t:컴포넌트이름`. 재질/프리팹/설정 에셋 준비가 필요하면
  안내 대신 메뉴 도구/자동 생성(`MobArchive/VFX/...` - 91번 오한/110번 굴절, `OutlineWidthSettings`).
- 인코딩: 새 파일 = UTF-8(BOM) + CRLF. 기존 파일은 원래 인코딩/줄바꿈 보존 - CP949 남은 파일: `Playeraimandattack`/`Ipassiveitemeffect`/
  `Statmodifier`/`Roomobjectives`/`Enemystats`/`Playerinventorydisplay`/`Playerresourcedisplay`/`AppearFromWhite`/
  `Dashattackconfig`/`Chaindashattackconfig`/`Choicelevelconfig`/`Idashdamagesource` 등(그대로 보존해도 되고, 고칠 때 UTF-8 BOM 변환도 가능). UTF-8 무BOM + CRLF: `Treasurechest`/`Itempedestal`/
  `Normalroomtreasurespawner`/`PlayerDisposableItem`/`PlayerFoodItem`/`PlayerActiveItem`/`PlayerTrinket`/`DisposableEffectContext`/`Treasurechestinfodisplay`/
  `HUDFont`/`Levelbuilder`/`Enemyhealth`/`Playerstats`/`Playerhealth`/`Roomcontroller`/`Toon_Environment.shader`/`ScreenBackground.shader`. 무BOM + LF: `PlayerResources`,
  v1.64 새 파일(`RoomScope`/`LevelLighting*`/`NightLight*`/`NightGlow.shader`). BOM + LF: `Enemyradialattack`/`PauseMenuUI`/`PauseMenuSettings`.
  BOM 2개(그대로 보존): `ItemInfoPanelView`/`PlayerSlotItemDisplay`/`PlayerCostDisplay`. SD 스크립트/셰이더(v1.57)는 UTF-8 무BOM + LF.
- 요청 없는 버전 태그 주석 금지.
- **주석 정책(스크립트를 고칠 때만 그 파일에 적용 - 일괄 정리 X)**: 주석은 "왜"만. 남김 = 파일 맨 위 역할 요약 1~3줄, 버그 방지 경고, 다른 스크립트와의 연결,
  `[Tooltip]`/`[Header]`(길면 한 줄로). 지움 = 코드를 그대로 풀어쓴 설명, C#/Unity 기초 설명, 주석 처리된 옛 코드/버전 이력. 긴 기획/결정 경위는
  장치 `Docs\설계노트.md`로 옮김(이미 이 문서/아카이브에 있는 내용은 다시 쓰지 않음, 커지면 그때 시스템별로 분리). 주석 언어는 한국어.
  결과 보고 때 기능 수정 / 주석 정리 / 설계노트 이동을 나눠 알림(툴팁 문구 변경은 따로).
- 토큰 절약: 폴더는 목록 먼저, 필요한 파일만 열기.
- **애니메이션 제작**: 클라우드에 `bpy`(Blender 파이썬 모듈)를 깔아 알바디 FBX에 직접 키를 잡음(형빈 PC에 Blender 없음, 스크립트는 남지 않음 - 요점은 v1.62 아카이브).
- **오디오 작업**: 클라우드에서 ffmpeg + numpy/librosa로 소리를 분석/가공(Claude는 직접 듣지 못함 - 수치로 판단하고 형빈이 귀로 확인).
- 파일을 넣은 직후 컴파일이 끝나기 전에 Play하면 튕길 수 있음 - 하단 진행 표시가 끝난 뒤 Play하도록 안내(튕기면 `Assets/_Recovery`와 비교).
- 컴파일 사전 검사: mono `mcs` + 손으로 쓴 Unity API stub(NuGet 차단), stub 오류와 실제 오류를 구분해 판단. 검사 못 한 파일은 형빈에게 알림(Console 확인 요청).
- ScriptableObject 메뉴는 `MobArchive/<이름>`. 런타임 설정 에셋은 `Assets/Resources/<이름>` 자동 로드(없으면 기본값) 방식이 표준.
- 새 UI 글자는 TextMeshPro: `HUDText.AddTo`/`HUDText.SetOutline`. 같은 Canvas 안에서는 나중 자식이 위(필요하면 `SetAsLastSibling`).
- 월드 위치에 뜨는 아이콘/숫자 연출은 Overlay 캔버스에 그림.
- Inspector `OnGUI`/`PropertyDrawer`에서 에셋 검색은 static 캐시로(렉 방지).
- 빌드 함정: 런타임 `CreatePrimitive` 도형/`Shader.Find` 재질은 빌드에서 핑크·누락 가능 - 재질/셰이더 칸을 두고 에셋을 넣음(비우면 `Shader.Find` 대체).
- URP 함정: Overlay 카메라 `clearDepth`는 읽기 전용, 카메라 하나에서만 숨기기 = `Renderer.forceRenderingOff`, 카메라 하나만 다른 하늘 = `Skybox` 컴포넌트.
- 입력: Active Input Handling = **Both**. 실행 중 EventSystem(StandaloneInputModule)이 자동 생성됨 - "Replace with InputSystemUIInputModule" 경고는 무시.

## 프로젝트 개요

- Unity 6.3 LTS(URP) 3D 탑다운 로그라이트. 아이작 리펜턴스식 룸 진행/아이템 시너지 + 블루아카이브식 등각 카메라/캐릭터/아이템.
- 플레이어 모델: **`SD_Player`(형빈 직접 모델링)**가 기본, 유니티짱(UCL 3.0)/레이사도 병행 유지. 배포 X, GitHub 비공개, 포트폴리오는 영상/PNG만.
- 식별자: 샬레 = `Schale`, 청휘석 = `Pyroxenes`(표시는 한국어). 등급 `ItemRarity` = `Tier0`~`Tier3`. n.5 레벨 서쪽 선택방 = 게마트리아
  (`Gematria`, `gematriaChance`에 `[FormerlySerializedAs("blackSuitChance")]` 유지), 동쪽 = `Schale`. Variant ID는 에셋 데이터
  (`Appearancecontextdrawer`가 Config 에셋의 `variantId`를 읽음 - static 캐시). `ReloadTime` 표기 = "장전 시간"(작을수록 빠름).

## 확정 상태

### 게임 흐름

- 씬: `Title`(Build Profiles 0) + `LevelBuilder`(1). LevelBuilder 단독 Play도 동작.
- 전투 레벨 config는 `GameFlowManager.combatLevelConfigs`(Element 0 = 레벨 1)가 빌드 때마다 `LevelBuilder.config`에 덮어씀.
  레벨 = `CurrentLevelNumber` + `CurrentPhase`(Combat = n / Choice = n.5). 빌드 순서 = 조명 `LevelLighting.Apply` → `BuildLevel` → 밤 빛 자동 부착 → 배경.
- `TitleScreenUI`: 버튼 게임 시작(로딩 + `gameSceneName`, 입장 컷씬 요청) / 도감 / 설정(`SettingsUI`) / 나가기. 도감/설정이 떠 있는 동안 버튼 입력 무시.
- `LoadingScreen`(프리팹을 두 씬에 배치, `DontDestroyOnLoad`, order 1000, 바로 검게 뜸, 로딩 중 조작 차단 `BlocksPlayerInput`).
- 사망: `PlayerHealth.Die()` → `OnDied`/`OnAnyPlayerDied` → `GameOverUI`(order 100, 2초 뒤 [예]/[아니오]).
- 레벨 이동 확인창 `LevelTransitionConfirmUI`.
- ESC 중단 화면 `PauseMenuUI`(order 95, 버튼 두 줄 - 윗줄 [계속하기][설정][도감], 아랫줄 [재시작하기][그만하기], 줄 간격 `buttonRowSpacing`),
  R 2초 꾹 누르기 재시작 `HoldRestart`(order 99) - 세부는 v1.61 아카이브. [설정]/[재시작하기]·[그만하기] 확인창이 뜨면 중단 화면 문구/버튼은 숨김(검은 막 위에 그 창만).
  씬을 떠나는 처리는 `PauseMenuUI.LeaveScene()`(이어하기 저장 자리). 둘 다 입장 컷씬/헬기 연출 중엔 안 열림/안 셈.
- **설정 창 `SettingsUI`**(설정 `Resources/SettingsUISettings`, order 150): BGM/효과음 볼륨 슬라이더 + [닫기]. `Open(최소 order, 막 표시 여부)` - 중단 화면에서는
  order 96 이상 + 자체 막 투명. 값은 `GameAudio.UserBgmVolume`/`UserSfxVolume`(PlayerPrefs, 닫을 때 저장). `IsOpen`/`BlocksInput`.

### 연출 - 입장 컷씬 / 헬기 퇴각 (자동 생성, 설정은 `Resources`의 각 Settings 에셋 - 세부는 v1.62 아카이브)

- **입장 컷씬 `IntroCutscene`**: 시작 화면 [게임 시작]일 때만(`RequestOnNextGameStart`) - 걸어 들어옴 → 장전 → '짜잔' `SD_IntroPose` → 나선 카메라로 게임 시점.
  Space/ESC/Enter 건너뛰기. 조작 잠금 = `LoadingScreen`과 같은 목록, 이동 = `PlayerMovement` 대본 방향, 카메라 = `CameraFollow`를 끄고 직접 움직임.
- **헬기 퇴각/도착 `HeliExtraction`**: 그림자 헬기 + 밧줄 사다리 + 먼지(`Assets/VFX/HeliExtraction`). 포탈 프리팹 `HeliExtractionPortal`(트리거 + `HeliExtraction` +
  `LevelPortal`) = LevelBuilder의 Level Portal Prefab. 도착 = 레벨 생성 직후 `TryStartArrival`(로딩 후, 그림자 헬기만 날아온 뒤 사다리 하강).
  헬기/사다리는 그 방에서만 보임. 두 연출 모두 `BlocksInput`/`IsPlaying`, 검은 막 order 998.

### 사운드 (`Assets/Script/Audio`, 클립 `Assets/Audio/SFX`·`BGM`)

- 재생 창구 `GameAudio`(자동 생성, `DontDestroyOnLoad`, 2D 소스 풀 24개): `Play(SfxId)`, 같은 프레임 중복 막기 `SuppressThisFrame`, 반복 소리 `PlayLoop`/`StopLoop`
  (방 소속 반복음은 다른 방 입장 시 자동 정지, `Time.timeScale` 0이면 일시 정지), `PlayBossAlert`. AudioListener 없으면 스스로 둠.
- 설정 에셋 `Resources/SoundLibrary`(만들기 메뉴 `MobArchive/사운드/사운드 라이브러리 만들기`): 슬롯별 clips(여러 개 = 무작위)/volume/pitchRange/minInterval,
  `footstepDistance`(걸음 = 이동 거리 기준). 클립 자동 배치 메뉴 `MobArchive/사운드/효과음 클립 자동 배치 (빈 칸만)` - 파일 이름 `SFX_<SfxId 이름>(_N)`.
- **`SfxId` enum 값은 절대 바꾸지 않음**(에셋이 숫자로 저장) - 새 소리는 그 묶음의 빈 번호/끝에 추가. 기존 게임 알림 구독은 `SoundEventHooks`, 직접 호출 줄엔 `[사운드]` 주석.
- UI 소리 `UISound`(Confirm/Cancel/Danger + 호버 `TrackHover`/`HoveredId`, uGUI 버튼은 `UIButtonHoverSound`). 되돌릴 수 없는 버튼(재시작/그만하기/확인 실행) = Danger.
  HUD 접기 버튼은 호버음 없음.
- 액티브 아이템 공용 사용음 `ItemActivate` - 전용 소리가 있는 효과는 `IHasOwnUseSound`(16/29/31/74/85/87/90/93/106/112번)로 공용음 생략. 머리 위 알림 아이콘
  소리는 `CreditPopupDisplay.Show(..., sound)`(실패/패널티 = `ItemFail`).
- 비어 있는 슬롯: `GunShot`, `ChargeShotFire`. 걸음 = `SFX_Footstep_1` 하나만 사용(음 높이 0.9~1.1). 출처 목록 `Assets/Audio/SFX/_출처.txt`(Kenney CC0, Sonniss GDC 번들 - 표기 불필요, 단독 재배포 금지).
- **BGM**: `bgmIntroClip`(게임 켠 뒤 1번만) → `bgmClip` 반복, 이음은 `PlayScheduled` + 인트로 실제 시작 시각 보정. 현재 곡 = ElevenMusic으로 만든 `Crystal Gear Surge`
  (172 BPM, 인트로 0~17.1초 `BGM_CrystalGearSurge_Intro` + 48마디 반복 `_Loop`). 일시정지 중 `bgmPausedVolumeRatio`(0.4)배.
  보스방 입장: 경고음과 함께 BGM 끊김 → 경고음 길이만큼(+`bossBgmSilenceAfterAlert`) 무음 → 처음부터(`bossBgmRestartWithIntro`) `bossBgmFadeInDuration` 동안 커짐.
  `bgmLoopStart/End`는 지금 안 씀(0 유지).

### 아이템 도감

- `ItemCodexUI`(시작/중단 화면, 설정 `Resources/ItemCodexSettings`), 기록 `ItemCodexRecord`(`MarkAcquired`, 초기화 메뉴 `MobArchive/아이템 도감 기록 초기화`) - 세부는 v1.61 아카이브.

### 비주얼 / 셰이더

- 조명/후처리/Volume 값은 LevelBuilder/ItemTestArena 동일(수치는 v1.46-v1.52 아카이브).
- `MobArchive/Toon_Environment`(배경, 외곽선 Pass, DBuffer 데칼) / `MobArchive/Toon_Character`(셀 셰이딩, 림라이트, 외곽선) - 둘 다 URP/Lit
  프로퍼티 이름 호환(`_BaseMap`/`_BaseColor`/`_EmissionColor`/`_Surface`) - 새 셰이더도 이 이름 유지. SD 캐릭터 전용 셰이더 2개는 [SD 신규 캐릭터].
  툰 셰이더는 주광 **색만** 씀(세기/환경광/추가 조명 무시).
- **레벨별 조명**: `LevelLightingProfile`(색온도 → 태양 색, 배경/캐릭터 그림자 틴트·밝기, `isNight`) → `RoomGenerationConfig.lightingProfile` → 빌드 전
  `LevelLighting.Apply`(비우면/n.5는 원래 낮). 툰 셰이더 4종이 전역값 `_MobLv*`("빼는 양", 0 = 그대로)로 반영, 레거시 레이사 셰이더는 미적용(의도). 2레벨 저녁/3레벨 밤.
- **밤 전용 빛 `NightLight`**(셰이더 `MobArchive/NightGlow`, 재질 `NightGlow_Mat`): 밤 레벨에서만 보임, 창문은 `lightChance` 추첨. 배치 = 자동 부착
  `NightLightAutoRules`(견본 `Object_Prefab/NightLight_Templates/NL_*` + `Same Shape Models`, `GameFlowManager.nightLightRules`) - 같은 메시 모델 자식으로 복제.
  벽 프리팹에 직접 붙이지 않음(그 벽은 자동 부착 건너뜀, 정리 메뉴 `MobArchive/Night Light/손으로 붙인 NightLight 모두 지우기`).
- **배경 외곽선 보정**(Toon_Environment 외곽선 Pass만): 에디터 `OutlineSmoothNormalBaker`가 `Assets/Object` 아래 모델 임포트 때 UV4에 외곽선 전용 노멀
  (볼록한 모서리만 평균) + 모델 크기를 저장, 셰이더가 작은 모델은 얇게. 설정 `Resources/OutlineWidthSettings`, 다시 굽기 = 메뉴 `MobArchive/외곽선/배경 모델 외곽선 노멀 다시 굽기`
  (세부는 v1.61 아카이브).
- 화면 굴절 셰이더 `MobArchive/RoomWave`(90번)/`MobArchive/HitRipple`(110번, `Assets/Shaders`) - URP 설정의 Opaque Texture 필요.
- 플레이어 재질 `Player_Mat/*`(FBX Remap), 적 재질 `Enemy_Mat`. 탄환 `Bullet_Player_Mat`(Unlit `FFE000`), 수류탄 `Grenade_Temp`. 일괄 교체 `MobArchive/셰이더 일괄 교체`.
- **화면 배경(레벨별)**: Skybox 재질 `MobArchive/ScreenBackground`(이미지 또는 "색으로 그라데이션" 위/아래 색 + 곡선, 마름모 패턴 흐름, 초상화 하늘 위/아래 색 칸).
  재질 `Assets/Background`: `BG_ScreenSky`(1레벨 이미지 - 기본) / `_Lv2`(노을 주황) / `_Choice`(n.5 회색~흰색, 패턴 연회색) / `_Lv3`(밤 남색~파랑).
  레벨 설정 `backgroundMaterial`(RoomGenerationConfig/ChoiceLevelConfig) → `GameFlowManager`가 빌드 직후 `LevelBackground.Apply` - Skybox + HUD 초상화 하늘 색 교체,
  비우면 씬 원래 Skybox + 프리팹 원래 초상화 색. 새 레벨 = 재질 복사해 색만 바꾸고 설정 칸에 넣기.

### 플레이어

- 관성 없는 CharacterController 이동, twin-stick 조준(우클릭 = 마우스 조준), 사거리 소멸 탄환, 무적 프레임 체력.
- 프리팹 3개(`Assets/Player/Character/Player`): `SD_Player` / `UnityChan_Player` / `Reisa_Player_Fixed` - **값을 바꿀 때는 세 프리팹 모두**.
  쓰는 프리팹은 `LevelBuilder`/`ItemTestArenaBuilder`의 Player Prefab 칸으로 교체.
- 비조준 자동 조준 `autoAimOnFire`(좌클릭 → 조준 → 발사 → 0.4초 뒤 복귀).
- `PlayerStats` 단일 소스: (기본 + 덧셈 합) x 곱셈 곱 → `statFloors`(최종 값에만). 새 `StatType`은 **enum 맨 끝**에 + 매핑 추가.
  `RemoveModifiersFromSource`/`RemoveModifiersFromSourceExcept(id, 남길 스탯)`. 특수효과가 준 스탯은 출처 `PassiveEffectIds.EffectStatSourceId(id)`
  (= id + "_effect", 잃어도 남는 "귀속" 스탯). 잃으면 사라지는 실시간 스탯은 별도 출처(예: 76번 id + "_passiveCount").
- **공격력 하한선 = 고정값(Absolute) 1**(Player 프리팹 `statFloors`). 하한선 없이 아이템 효과만 적용 = `PlayerStats.ApplyModifiersTo(StatType, 기본값)`(87번 아군).
- **최대 체력 상한 `maxHealthCap` 10** - 넘는 증가분은 기억하지 않고 그만큼 회복(`OnMaxHealthIncreaseCapped`). 테스트 프로필 `noMaxHealthCap`/`noShieldCap`으로 테스트 씬만 해제.
- **레벨 끝 최대 체력 손실**: n.5 → 다음 전투 레벨에서만 빈 최대 체력을 정수 칸만큼 제거(3.5/5 → 3.5/4, 최소 1칸) - `PlayerHealth.ApplyLevelEndMaxHealthLoss`.
- **보호막**(아이작 소울하트, 최대 5): `PlayerHealth.AddShield(수, 연출 여부)`/`SetShield`, 피격 1번을 통째로 막음. 연출 `PlayerShieldBreakVfx`(`GetSharedMesh` - 2레벨 보스 막도 씀).
- 회복 배율 등록소 `HealBoostOverride`(101번 x2).
- 사격 알림 `PlayerShooting.OnAmmoConsumed`/`OnAmmoRefunded`/`OnReloadStarted`, 즉시 장전 `TryInstantReload()`. 투명 `PlayerInvisibility`(112번, `_DitherFade`).
- 74번 돌진 `PlayerDash`(`IsDodging`/`BlocksPlayerInput`) - 회피는 피해원마다 확인. 액티브 사용 규칙 `IActiveItemUseRules`(`CanActivateNow` - 비용 지불 전 검사),
  디버프 면역 `PlayerDebuffImmunity`(94번).
- 시작 자원은 `PlayerResources`. 무기 모드 `PlayerWeaponMode` 1 총/2 수류탄/3 해킹 - 0개여도 들어갈 수 있고, 0이 되는 순간에만 총으로 자동 복귀.
  수류탄/해킹 모드 자원 표시 = `WeaponModeResourceIndicator` + 탄약 보조 상자(`AmmoBoxDisplay`).
- 수류탄 `PlayerGrenadeThrower`(자해 - `GrenadeSelfDamageOverride.IsBlocked`), 폭발 알림 `OnGrenadeExploded(위치, 반경)`(`ExplodingRoom` = 폭발한 방), 폭발 중심 =
  `Grenade.CurrentGroundPoint()`(77/81/92/115번 등록소). **118번 포격 `ArtilleryStrike`는 수류탄 폭발 처리를 따로 구현**(반복 작업 [21]).
  해킹툴 `PlayerHackTool` - `ApplyHackArea`/`ApplyHackToEnemy`, 알림 `OnHackDeployed`(효과 발동) / `OnHackToolSpent`(1개 실제 소모 순간 - 58번).
  범위 이펙트 크기 `GetRadiusVfxFactor`. 수류탄 겉모습 등록소 `GrenadeVisualOverride`. 세부는 v1.53-v1.57 아카이브.
- 아이템 획득 연출: `ItemPickupEvents.Raise` → `PlayerItemPickupPresenter`(연출 중 행동 차단 `BlocksActions`). 여러 개 = `PassiveRerollUI.RaisePickupsInSequence`.
- F 상호작용 = 가장 가까운 하나(`InteractionTargetSelector`). 팝업이 떠 있으면 대상 없음.
- 테스트: `PlayerTestProfile` → `ItemTestArenaBuilder.testProfile`/`testChestItems`. ItemTestArena엔 방 클리어가 없음 - 39/72/111번은 실제 레벨에서 테스트.

### 플레이어 애니메이션

- 애니메이션 FBX 15개(Mixamo, Humanoid), Controller `ReisaAnimatorController` 공유(SD/유니티짱 직접, 레이사는 Generic 복사본 + Override Controller).
- **지우면 안 됨**: `Reisa_AvatarSource.fbx`, `Animation/Generic/` 전체, `Animation` 원본 FBX, `Reisa_Fixed.fbx`, `Reisa_gun_Fixed.fbx`, `Assets/UnityChan`.
- 레이어 Locomotion(전신)/UpperBody(머리+팔)/UpperBodyTorso, 조준 IK `HumanoidAimLookAt`, 반동 `UpperBodyRecoil`, RootFixed = 루트 커브 고정 FBX.
- **SD 전용 직접 제작 모션**(`Assets/Player/Character/Animation`): `SD_Reload`(`SD_AnimatorOverride`로 장전 클립 2개 교체 + `PlayerShooting.reloadClip`),
  `SD_IntroPose`(Locomotion/UpperBody 레이어의 `IntroPose` 상태, 코드가 직접 넘김).
### SD 신규 캐릭터 (`Assets/Player/Character/Player/SD`) - 세부는 v1.58/v1.62 아카이브

- 알바디 `SD_AltBody_v02.fbx`(바이패드 `Bip001`, Humanoid) + `SDCharacterSkin`/`SDPartAttacher`(`Equip`/`Unequip`)/`SDFaceController`/`SDFaceExpressionSet`.
  셰이더 `MobArchive/Toon_Character_SD`/`Toon_Face_SD`. 재질 자동 설정 `MobArchive/SD 캐릭터 재질 자동 설정`. 게임 도중 `Equip`한 파츠는 아직 피격 깜빡임 목록 밖.
- **실시간 초상화 `PlayerPortraitCamera`**(SD_Player 루트): 카메라 2대(배경 하늘 `MobArchive/PortraitSkyGradient` - 색 `skyTopColor/skyBottomColor`는 레벨 배경이 덮어씀).
  레벨 조명 틴트도 초상화에 같이 적용됨.

### 적 AI

- 근접 `EnemyAIController`, 원거리 `EnemyRangedAIController` + `EnemyRangedAttack`, 보스 `BossAIController`(아래 [보스]).
- **적 프리팹 Rigidbody는 Is Kinematic + 중력 끔**(아니면 판정 위치가 어긋남) - 돌진 부품이 없는 적 컨트롤러는 Awake에서 직접 맞춤(`BossLv2AIController`처럼).
- 레벨별 일반 적: `RoomGenerationConfig`의 `overrideNormalEnemyCount`/최소·최대/개수별 가중치/`normalEnemyPool`(비우면 `RealEnemySpawner`의 Enemy Pool).
  레벨 전용 강화 적 = `Assets/Config/Enemy/lv1`·`lv2`의 EnemyDefinition(같은 프리팹 공유, 수치만 다름).
- **첫 돌진 충전 이펙트** `DashChargeVfxSettings`/`DashChargeVfx`(일반/연속/2레벨 보스/1레벨 전방위 `chargeVfx`) - EnemyDefinition Attack Config 칸이 프리팹보다 우선.
- 적 탄환 = 코드 생성 구체 + `EnemyProjectile`(정적 목록 `Active`) - 한 발 = 한 번 명중.
- 적 수치: 공통 = `EnemyDefinition`(모든 적 Attack Power 1), 공격 방식별 = `attackConfig`.
- **목표 정하기 `CombatTargeting`**(`Resolve`/`CurrentTarget`/`TargetIsInvisiblePlayer`): 적 = 플레이어(투명 제외)/아군 중 가까운 쪽, 아군 = 가장 가까운 적.
  **새 적 AI/공격도 이 창구 사용**. 켜진 적 목록 `EnemyHealth.ActiveEnemies`.
- **87번 아군 `AllyFaction`**(세부는 v1.59 아카이브): Enemy 태그 제거(태그를 안 보는 새 효과는 `AllyFaction.IsAlly`로 제외).
- 정적 등록소: `EnemyDetectRangeOverride`, `EnemySightOverride`, `EnemySpeedModifier`, `EnemyDamageTakenModifier`(41번), `EnemyAttackSpeedModifier`(91번).
- 처치 이벤트 `EnemyHealth.OnAnyEnemyKilled(적, 위치)` - 구독 코드는 try/catch로 감쌈.
- 제어 체계 `EnemyControlState`: 강력(수면/해킹) 1칸 + 일반(매료) 1칸, 강력 중엔 모든 AI 정지. 새 제어 = `EnemyControlType` 추가 + `GetTier()`.
  몸 색 우선순위 = 공격 충전 > 수면·해킹 > 매료 > 상태 색(`SetStatusTint`) > 원래 색. 짧은 발광 `FlashGlow`(90번). 매료(`EnemyCharmState`)는 보스에 안 걸림(색도 X).
- 적 상태: 오한 `EnemyChillState`(91번), 지속 피해 `EnemyDotState`(102+109), 슈가 중첩 `EnemySugarStack`(113번 - 아이콘은 적 위치 기준 고정, 회전해도 안 흔들림).
  제어 지속 이펙트 `EnemyStatusVfx`.
- 전투방 재입장 시 적만 초기 상태로 재생성. 다른 방의 적은 방 내용물째 꺼져 있음(`FindObjectsByType` 기본값이면 지금 방 적 + 87번 아군).
- 넉백 `ForcedPush.Apply`(적도 밈) - 밀리는 동안 `ForcedPush.IsBeingPushed`면 AI가 이동 판단을 쉼. 돌진 중 판정 = `IDashDamageSource.IsDashing`.

### 보스 (설계/기획 전체는 `Docs\설계노트.md` [2레벨 보스] 등)

- 보스 판별 = `GetComponent<BossAIController>() != null`(체력바, 매료/오한/넉백/87번 제외, 90번 규칙) - **새 보스 컨트롤러는 `BossAIController`를 상속**.
  보스 선택 = Level Config `bossVariants`의 variantId ↔ 씬 `RealEnemySpawner.bossPool`(빈 variantId = 기본 = 1레벨 보스). 보스 수치는 프리팹에 직접.
- 1레벨 `EnemyBoss_DroneElite_Boss_lv1`(흰+하늘색): 5연사 + 전방위(110번 강화 총알 외형 하늘색, 맞으면 굴절) + 연속 돌진. 초상화 `UI_BossIcon_DroneElite_lv1`.
- **2레벨 `EnemyBoss_DroneElite_Boss_lv2`**(흰+주황, `drone_elite_lv2`, 이름 Elite Drone Mk-II, 체력 700 / 보호막 500 / 연사 주기 2초 - 밸런싱 확정값):
  `BossLv2AIController` + `BossShield`. 평소 5연사 + 백스텝 샷건 → 보호막 동안 포격(`BossArtilleryStrike`) + 무한 벽 돌진(첫 돌진 방향은 충전 시작 때 고정,
  이후 추적) → 파괴 시 그로기 5초 → 주황 물결 + 모서리 소환 4기(`SummonMaterialize`) → 20초 뒤 재충전. 세부는 설계노트/v1.64 아카이브.
- 보호막 `BossShield`(피해 흡수, 제어 필터 `IEnemyControlFilter`, 보호막 바 `BossHealthBarDisplay`), 소환 = `RealEnemySpawner.SpawnSummonedEnemy` +
  `EnemyKillObjective.TryAddAlongside`. **90번 물결**: 총알/포격 표시 지움, 일반 적 공격 끊기·금지, 보스는 진행 중 연사만 끊김(세부는 v1.64 아카이브).

### 레벨 생성

- `RoomGenerationConfig` → `LevelGraphGenerator` → `LevelBuilder.BuildRooms`, `RoomVisibilityManager`가 현재 방만 활성화. 방 = 한 변 `cellSize` 칸 하나.
  NavMesh는 방마다 런타임 베이크. 층 이동 = `LevelRoot` 통째 삭제 후 재생성. 방은 런타임 생성이라 정적 배칭 없음.
- 현재 방: `LevelBuilder.CurrentPlayerCell`/`GeneratedRooms`/`GetRoomController(id)`, 위치가 속한 방 = `ClaymoreOverride.RoomOf`/`WallPierceOverride.RoomOf`/`RoomScope.RoomOf`.
  방 클리어 `RoomController.OnAnyRoomCleared`(일반방/보스방만), 방 입장 `OnAnyRoomEntered`, `RequiresClear`. 전투 없는 방 = `SlotItemDropper.IsInCombatFreeRoom()`.
  상자 열림 공용 알림 `ContainerOpenEvents.OnAnyContainerOpened`(TreasureChest/ItemPedestal 처음 열 때만, 보관 좌대 X - 97번).
- **방 범위 규칙 `RoomScope`**: 그 방에서 생긴 일은 그 방에서만(소속 = 생성 순간 플레이어가 있던 방). `DestroyWhenPlayerLeaves(go)` = 방을 나가면 즉시 삭제,
  적 총알은 쏜 방을 벗어나면 소멸, 플레이어 없는 방의 수류탄/115/118 폭발은 조용한 폭발(특수방 잠금만 `TryUnlockByGrenade`).
- 레벨 포탈 `LevelPortal`. Wall 태그로 방 벽 구분 불가(120번은 칸 경계로 판정). 런타임 방 추가 `TryAddExtraRoom`(16번), 텔레포트 `TryTeleportPlayerToRandomRoom`.
- 일반맵 보물상자 `NormalRoomTreasureSpawner`(내용물은 처음 열 때 정함 - 116번 `NormalChestItemGuaranteeOverride`면 아이템), 보스방 보상 `ResolveBossRewardContent`.
  아이템 후보 = `ItemDatabase.GetAllCategoryItemsForRoom(room, true)`(그 방 variant 전용 + 카테고리 공용 - variantId가 붙은 보스방도 'Boss' 공용 아이템이 나옴).
- **레벨 내 아이템 중복 등장 방지**: `GameFlowManager` `MarkItemAssignedThisLevel`/`FilterOutAlreadyAssignedThisLevel`/`UnmarkItemAssignedThisLevel`(상자/상점/48/56/62/80번
  공통, 다음 레벨에서 초기화 - 초기화 직후 들고 넘어온 슬롯 아이템(액티브/장신구/일회용/음식)은 다시 표시해 그 레벨 후보에서 제외). ID로 찾기 `ResolveItemById`.
- **아이템 등장 가중치**: 가중치로 뽑는 곳은 `ItemWeightModifier.GetWeight(item)` 사용(분류별 보정, 권장 weight 등급별 100/80/60/40).

### 특수방 (크레딧 더미 `credit_pile`만 제작됨) - 세부는 v1.46-v1.52 아카이브

- `RoomCategory.Special`(4) 하나로 통합(5, 6번 비어 있음). 종류 = 레이아웃 에셋 `SpecialRoomLayout` → Level Config `specialLayouts`. 항상 막다른 방.
- 진입 조건(Free/Grenade/HackingTool/Health) 추첨. 잠금 `SpecialRoomLock`(같은 도구 범위로 해제 - 118번 O, 81번 X, 폭발한 방의 잠금만),
  체력 = 지날 때마다 0.5 피해(보호막이 먼저 막음). 10/26번 = 잠금 자동 해제, 37번 = 체력 피해 막음.

### 상점 (편의점 `convenience_store`만 제작 중) - 세부는 v1.53-v1.57 아카이브

- 바닥 프리팹 안 `ShopNPC`(Shop Config) + 보관 좌대 `HoldingPedestal` 4개. 진열은 처음 열 때 뽑고 유지, 갱신 1 크레딧/최대 2회.
  가격 = `ShopNPC.GetUnitPrice(listing)`(`ShopPriceModifier`, 86번 `RegisterLevelFree`). 창 `ShopUI`(order 85). 보관 좌대 = 범용 좌대(9/106번 대상 아님).
- 상점 창 등급 이름 색은 일부러 어둡게(HUD 등급 색과 통일 X).

### 팝업 / 공용 UI

- 공용 스타일 에셋 `PopupStyle`(`Resources/PopupStyle`) - 버튼 평소/호버/글자 색 + **위험 버튼**(`dangerButtonColor` `E5484D`/호버 `B8383C`/글자 흰색,
  `GetDangerButtonColor`) - 되돌릴 수 없는 동작 버튼에 사용. 모든 검은 팝업/흰 창 버튼이 씀, **새 UI도 이 에셋 값 사용**. 둥근 사각형 `UIRoundedBox`.
- 팝업 공통 패턴(세부는 v1.62 아카이브): static `IsOpen` + `BlocksInteract` - `InteractionTargetSelector`/`HUDClickBlocker`/`PauseMenuUI`/`SlotItemDropUI`/`HoldRestart`가
  확인함. **새 팝업을 만들면 이 다섯 곳에 조건 추가**.
- 슬롯 교체 확인창 `SlotReplaceConfirmUI`(order 90). 사용 불가 이유 알림 창 = `PassiveRerollUI.ShowMessage(플레이어, 문구)`.
- **머리 위 아이콘 연출 범용** `CreditPopupDisplay`(Item54effect.cs 안, order 8): `Show(대상, 발밑 위치, 설정, 대체 아이콘, 따라다님, onStarted, sound)` - 대상별 줄 세우기,
  방 입장 시 건너뜀, 획득 연출과 순서 조정. 성공/실패 알림 공용 `HeadAlertRoomRules`(39/103/111번).
- 스탯 패널 강제 연출 `PlayerStatDisplay.RequestFlash`.

### 슬롯 아이템 (장신구 / 일회용 Q / 음식 E) - 세부는 v1.53-v1.57 아카이브

- `PlayerTrinket`/`PlayerDisposableItem`/`PlayerFoodItem`: 먼저 얻은 것이 앞, `SlotCount` = 1 + `AddExtraSlots`, 꽉 차면 가장 먼저 얻은 것이 교체.
- 일회용/음식은 앞칸만, `CanUse` false = 소모 X, 지연형 = `IsPendingAt`/`CompletePendingUse`/`CancelPendingUse`. 사용 연출 `ItemUsePopup.Show`(실제 소모 순간 +
  85/93번 재현 때 재현한 아이템 아이콘 - 즉시형은 사용 순간, 지연형은 발동 순간, 대기 취소면 X), 직전 소모 재현 `LastUsedFood`/`LastUsedDisposable`(93/85번),
  버리기 Ctrl 0.8초 → `SlotItemDropUI`.

### 아이템 시스템

- 패시브: `ItemDefinition.specialEffects`(`[SerializeReference] IPassiveItemEffect` - `[Serializable]` + 매개변수 없는 생성자, 에셋의 효과 인스턴스를
  공유하므로 런타임 필드는 `[NonSerialized]`, `Apply`는 먼저 이전 구독 해제), 훅 `OnProjectileSpawned`/`OnShotFired`/`Projectile.OnDespawning`.
  "보유 중" 상시 동작은 런타임 러너 MonoBehaviour(지울 땐 `DestroyImmediate`). 정적 알림을 구독하는 효과는 플레이어가 사라지면 스스로 구독 정리(97번).
- **총알 피해** `Projectile.Damage` = (기본 + `AddDamageBonusProvider`) x `AddDamageMultiplierProvider` → 하한 → 23번. 명중 = (`Damage` + `PlayerBulletBonusDamage`) x 받는 피해 배율
  (`OnDespawning` 중엔 충돌 순간 값 고정). 튕김 횟수 `BounceCount`/`WallBounceCount`/`EnemyBounceCount`.
- **총알 1발 = 적 1명**(소멸 확정 총알은 무시, 관통처럼 `CancelDespawn`한 총알만 계속 맞힘). 명중 알림 `OnPlayerBulletHitEnemy`/`OnPlayerBulletHitEnemyDetailed`.
- **관통**: 적 = `SetEnemyPiercing`(99번), 벽 = `SetWallPiercing`(120번), 둘 다 = `SetPiercing`(110번). 벽 관통 총알은 처음 있던 방의 칸을 벗어나면 소멸.
- **상극** `ItemDefinition.incompatibleItemIds` + `ItemCompatibility`(99↔69, 120↔35). 시너지 감시 `SynergyWatcher`(102+109). 확률 판정 창구 `LuckOdds.Roll`.
- **패시브 잃기** `IRemovablePassiveEffect.Remove`(새 패시브는 필수), `PlayerInventory.RemoveItem`/`ReplaceItems`/`TryAcquireItem`. 잃어도 최대 체력/특수효과 스탯/
  1회성 재화/보호막은 남음. 변화 알림 `OnPassiveItemsChanged` + `PassiveCount`.
- **1회 지급 규칙**: `GameFlowManager.TryConsumeOneTimeGrant("{ID}_용도")`(한 판 내내 유지). 새 사본 = `AcquireItemCopy(item, 새 사본인지, 획득)` = 매번 지급,
  다시 주운 것은 X, 48/56번은 `IgnoreOneTimeGrantLimit`로 항상 지급. 테스트 씬은 경고만 남기고 지급.
- 액티브(Space) `IActiveItemEffect`/`IDelayedActiveItemEffect`, `IActiveItemUseRules`, 음식 `IFoodEffect`, 일회용 `IDisposableItemEffect`, 장신구 `ITrinketEffect`.
  상태 있는 효과 재실행은 `JsonUtility` 복제.
- 구현된 아이템 = `Assets/Script/Item`의 `ItemNN effect.cs`(상세 기획은 각 파일 주석, v1.64 시점 번호 목록은 v1.64 아카이브 - 다른 채팅 진행분은 그 채팅 정리 때 반영).
  (4번 폭발 효과 클래스 `ExplodeOnImpactEffect`는 `Item04effect.cs` 안에 있음.)
- 아이템별 확정값: 10~86번은 v1.53-v1.57 아카이브, 87~137번은 v1.59 아카이브, 58/67/93/97번은 v1.61 아카이브 + 각 `ItemNN effect.cs` 주석.
  29번 = 방에 살아 있는 적이 없으면 사용 불가, 32번 = 처치 회복이 실제로 될 때만 머리 위 하트, 98번은 인스펙터로 밸런스.
- 플레이어 총알 색: 등록소 `ProjectileColorOverride` + `BulletColorEffect`(23번 색이 최우선). 38번 = `BA55D3`, 102+109 = 초록.
- ItemDefinition 에셋은 복제해서 만들어도 됨 - `Item Id` 변경 + ItemDatabase 등록 필수(도감도 ItemDatabase 기준). 상점에 나오게 하려면 Appearance Contexts에 `Store`.
- 정적 등록소 = 이름이 `...Override`/`...Modifier`인 클래스(아이템 번호별 목록은 v1.64 아카이브), 각각 해제 메서드 있음. 분류별 보정 공용 계산 `CategoryModifierSet`(67).
- 확정값: 4번 반경 2.5, 19번 ±30도 2발(탄약 1). 7번 잠든 적은 피격으로 안 깸.
- 상자 공통: `ItemPedestal` 통과 가능. 뚜껑 분리 `MobArchive/보물상자 뚜껑 분리` + `ChestLidOpener`, 상자 위 빛 `ItemGlowSpawner`.
- 등급 색(HUD 설명칸 이름/상자 빛/사용 연출) = Tier0 흰 / Tier1 청록 / Tier2 노랑 / Tier3 자홍(`PlayerItemInfoDisplay` 코드 기본값도 같음).

### 전투 연출

- `DamagePopupDisplay`(아군이 맞을 땐 숫자 X), `EnemyHealthBarDisplay`(보스 제외, 아군 = 초록), `CombatVfxSpawner`(총구/피격/벽/처치 슬롯 - 보호막이 막을 피격은
  적 피격 이펙트 X).
- 이펙트 조정 = 원본 프리팹을 `Assets/VFX/Custom/`에 복제해 수정(원본 팩 재질 수정 X). 크기 칸 = `ApplyVfxScale`. 따라가는 반복 이펙트는 위치만 따라감(38/78번).
- 110번 관통 충전 이펙트 칸 `piercingImplosionPrefab`, 피격 굴절 `ChargedHitRipple`(메뉴 `MobArchive/VFX/110번 피격 굴절 재질 만들기`). 91번 메뉴 `MobArchive/VFX/91번 오한 이펙트 만들기`.
- 탄환 꼬리 `BulletTrail`(Trail `SetPositions` 쓰지 말 것).

### HUD

- `PlayerHUDManager`의 단일 Canvas(1920x1080) 위에 각 스크립트가 패널 생성. 대부분 `PlayerHUD` 프리팹(`Assets/Config/Level/PlayerHUD.prefab`) 안 - 값 바꾸면
  Overrides → Apply All. 글자 = TMP, 글꼴 `Assets/Resources/Fonts/경기천년제목_Bold SDF`(**이름 바꾸면 `HUDFont.GetTMP()`가 못 찾음**).
- 체력 칸 `PlayerHeartDisplay`(한 줄 최대 10칸, 보호막 바는 하트 위) - 세부는 v1.61 아카이브. 스탯 표기는 `StatDisplayFormat`, 새 스탯 한글 이름은
  `ItemInfoPanelView.GetStatDisplayName`. 패널 접기 `CollapsibleHUDPanel`(Tab), 클릭 차단 `HUDClickBlocker`.
## 반복 작업 (새 에셋/씬을 만들 때마다 확인)

- [1] Wall 태그 콜라이더는 새 프리팹도 Box/Convex 체크리스트 적용.
- [2] 새 원거리 적 프리팹엔 `EnemyRangedAIController` 확인.
- [3] 상체 동작이 몸 전체를 흔들면 클립 루트 커브 문제 - RootFixed 사본을 Humanoid + `Animation/Generic` 둘 다 만들고 Override에도 연결.
- [4] 교체 확인창이 필요한 슬롯 아이템은 아이템 에셋의 `Confirm Before Replace` 체크.
- [5] 새 모델/재질은 `MobArchive/셰이더 일괄 교체`로 툰 적용. 새 씬은 조명/환경광/Skybox/Volume을 [비주얼] 값으로.
- [6] 포트폴리오에 유니티짱이 나오면 UCL 3.0 표기.
- [7] 새 게임 씬은 `LoadingScreen` 프리팹 배치 + Build Profiles 등록(Title 0번 유지).
- [8] 새 상점 바닥 프리팹은 `ShopNPC` + `HoldingPedestal` 4개. 상점별 상품은 Shop Config 등장 위치와 아이템 Appearance Contexts를 맞춤.
- [9] 새 특수방 레이아웃: 레이아웃 에셋 + Room Visual Set + **레벨마다** Level Config의 Special Layouts에 등록.
- [10] 복제로 만든 ItemDefinition은 `Item Id`를 새 번호로 + ItemDatabase 등록. 상극이 있으면 `Incompatible Item Ids`에 상대 ID(한쪽만 적어도 됨).
- [11] 총알 색 아이템 = Special Effects에 `BulletColorEffect`. 새 제어 지속 이펙트 = 프리팹 복제 + 출처 효과에 설정 칸 + `Attach`.
- [12] 새 패시브 효과는 `IRemovablePassiveEffect.Remove` 구현(48/56/62번으로 잃을 수 있음). 1회성 지급은 `TryConsumeOneTimeGrant` + `IgnoreOneTimeGrantLimit`
  예외 처리.
- [13] 아이템 테스트 씬(LevelBuilder 없음)에서 48/56/62/80번을 쓰려면 아이템 에셋 Disposable Effect의 `Item Database` 칸을 채움.
- [14] 새 팝업 UI = `PopupStyle` 값 사용(되돌릴 수 없는 동작 버튼은 위험 버튼 색) + `IsOpen`/`BlocksInteract`를 위 다섯 곳에 연결 + ESC 겹침
  (`PauseMenuUI.CanOpenNow`) 확인 + UI 소리(`UISound` 확인/취소/위험 + 호버).
- [15] 새 아이템 획득 경로를 만들면: 새로 뽑힌 사본이면 `GameFlowManager.AcquireItemCopy(item, true, ...)`로 감싸고, 가중치 추첨은 `ItemWeightModifier.GetWeight`,
  가격은 `ShopNPC.GetUnitPrice`, 상극은 `ItemCompatibility`로 거름. 인벤토리/슬롯에 넣을 땐 기존 `TryAcquireItem`/`ReplaceItems`를 거쳐야 도감에 기록됨
  (목록을 직접 고치는 새 경로면 `ItemCodexRecord.MarkAcquired` 추가). 새 수류탄 계열 자해/구역 피해 판정엔 `GrenadeSelfDamageOverride.IsBlocked` + `PlayerDash.IsDodging`.
- [16] 스스로 이동하는 새 적 AI/공격 컴포넌트를 만들면 `ForcedPush.IsBeingPushed(gameObject)` 검사 + `EnemyAttackSpeedModifier` 적용. 수류탄 폭발 위치는
  `OnGrenadeExploded`의 위치를 사용.
- [17] 새 총알 피해 효과: 더하기 `AddDamageBonusProvider`, 곱하기 `AddDamageMultiplierProvider`, 명중 시 고정 추가 `PlayerBulletBonusDamage`. 튕김/관통은
  `Redirect`(벽)/`RedirectFromEnemy`(적)로(관통 총알이면 `IsPierceHit` - 튕기지 않음).
- [18] 새 SD 파츠: 3ds Max에서 알바디 바이패드로 Skin(Figure Mode) + 재질 이름 `M_SD_{부위}` + 빈 더미 삭제 → FBX Rig Generic/Scale Factor 알바디와 같게 →
  텍스처 `T_SD_{부위}_Base`/`_Mask` → `MobArchive/SD 캐릭터 재질 자동 설정` → 파츠 정의 에셋에 FBX 넣고 Save Project. 얼굴 시트 이름은 `_Base`로 끝내지 않음.
- [19] 몸에 붙이는 물건(총/헤일로/장신구)은 **`Bip001` 뼈** 아래에. 다른 프리팹에서 컴포넌트를 Copy/Paste하면 오브젝트 참조 칸이 원본 프리팹을 가리킴 -
  붙여넣은 뒤 다시 연결.
- [20] 적을 대상으로 하는 새 플레이어 효과는 "Enemy" 태그로 고르거나 `AllyFaction.IsAlly`로 87번 아군 제외. 새 적 공격/AI는 `CombatTargeting`으로 목표를 정함.
- [21] 새 수류탄 계열 아이템은 `Grenade.cs`와 118번 포격 `ArtilleryStrike.cs` 양쪽에 반영할지 확인.
- [22] 플레이어 머리 위 알림 + 이펙트는 `CreditPopupDisplay.Show(..., followTarget: true, onStarted)`로.
- [23] 새 플레이어 피해원은 `PlayerHealth.TakeDamage`를 거쳐야 보호막이 막음. 피격 순간 이펙트/점멸/반응은 `IsHitInvincible`·`WillShieldBlockNextHit`로 거르고,
  피격 반응 아이템은 `OnDamageTaken`(보호막 피격엔 안 옴)을 구독. 보호막을 주는 새 효과는 `AddShield(수, 연출 여부)`.
- [24] 외곽선 보정이 필요한 배경 모델은 `Assets/Object` 아래에 둠(다른 폴더면 `OutlineSmoothNormalBaker.TargetFolders`에 추가 후 메뉴로 다시 굽기).
  이 모델들은 정적 배칭(Static 체크) 금지 - UV4 노멀/크기가 틀어짐. 3ds Max에서 내보낼 땐 떼어낸 면 등에 Reset XForm(스케일을 굽고 내보내기).
- [25] 스크립트를 고칠 때 그 파일에 주석 정책 적용 + "레벨당 1회" 옛 주석/툴팁이 있으면 새 1회 지급 규칙에 맞게 고침.
- [26] 새 레벨: Level Config(일반 적 칸 + `bossVariants` + `backgroundMaterial` + `lightingProfile`) → `GameFlowManager.combatLevelConfigs` 순서 등록 → 보스면
  `RealEnemySpawner.bossPool`에 같은 variantId 항목. 새 보스: `BossAIController` 상속, Rigidbody Kinematic 확인, 초상화/이름(`BossPortrait`), 처치 폭발/보상 상자 확인.
- [27] 방에 남는 새 오브젝트(투척물/VFX/지속 구역)는 `RoomScope.DestroyWhenPlayerLeaves`, 터지는 것은 플레이어 없는 방이면 조용한 폭발.
- [28] 새 창문/자판기/가로등 모델은 `NightLightAutoRules`에 등록(스왑 = `Same Shape Models`, 모양 다르면 새 견본). 견본 Glow Material = `NightGlow_Mat` 필수.
- [29] 새 아이템/적 공격/연출에 소리가 필요하면 `SfxId`에 새 값 추가(기존 번호 변경 금지) → `SoundLibrary` 메뉴로 칸 추가 → `GameAudio.Play`. 전용 사용음이 있는 액티브는
  `IHasOwnUseSound`, 방에 남는 반복음은 `PlayLoop(roomScoped)` + 끝날 때/`OnDestroy`에서 `StopLoop`. 새 효과음 파일은 `_출처.txt`에 출처 기록.

## 확인 필요 항목 (해결되면 지울 것)

- [96] 외곽선 2차 보정 확인 - 메뉴로 다시 굽기 후 작은 소품 두께/창문 음각 확인, `OutlineWidthSettings` 조절(음각에 선이 남으면 Z Offset 칸 검토).
- [99] (형빈 정리 중) `Assets/Audio/BGM/BGM_CrystalGearSurge_Original.wav`(비교용 원곡, 안 씀) 삭제 여부, 효과음 클립 음량/음색 다듬기.
- [100] BGM 출처: ElevenMusic 요금제(무료면 출처 표기 필요·상업 이용 제한 가능)를 약관으로 확인하고 `_출처.txt` + 포트폴리오 크레딧에 기록.

## 할 일 / 보류 (나중에 만들 것, 참조 메모)

- [48] 행운(Luck) 스탯 - 필드만 존재, 확률 반영 미구현. 반영 창구 = `LuckOdds`(지금 106번만 거침). 54번 처치 확률(25%), 80번 개수 확률(50/30/20)도 이 창구로 옮김.
- [49] 미니맵 마커 아이콘 8종 + 포탈 아이콘 스프라이트 연결.
- [54] 등급 스프라이트 오른쪽 "태그 UI" 자리 남아 있음.
- [78] (참조만) 특수방 진입 비용 변형(수류탄 3개 등) 레벨별 난이도로 검토.
- [79] 특수방 개수 규칙(Level1 Special 필수 1개 - 확인용)/레이아웃 가중치 조정 예정 + 테스트 레이아웃 정리 필요.
- [81] 비어 있는 방 번호 5, 6 - 나중에 새 방 종류에 사용.
- [82] (보류, 참조만) 이어하기(저장 슬롯 1개, 사망 시 삭제) - 세부는 v1.58 아카이브, 저장 자리는 `PauseMenuUI.LeaveScene()`(그만하기만).
- [87] **다음 작업 후보 - SD 외형 아이템 시스템 + 외형 설정 창**(슬롯별 선택, 색 바꾸기, `Equip` 파츠를 `PlayerHitFlash`에 추가). 이후 파츠 모델링
  모자 → 머리 장신구 → 옷 → 장갑 → 신발. 세부는 v1.58 아카이브.
- [88] (보류) `SDPartAttacher` Skeleton Root에 캐릭터 밖 오브젝트가 들어가면 무시하고 자동 탐색하는 실수 방지 장치.
- [95] (형빈 모델링) 골목 끝 바닥 틈 - `Field_Player` 스케일 미적용(1.091632)이 원인, Reset XForm 후 재수출 예정(급하면 바닥 아래 받침판 Quad).
- [97] **3레벨 준비**: `Level3Config`(2레벨 복사본) - Level Number가 2(로그 표시만, 3으로 고칠 것), `bossVariants` 비어 있음(지금은 1레벨 보스가 나옴),
  일반 적 칸 비어 있음(1레벨 적/마릿수). 3레벨 전용 보스/강화 적 제작 예정. 배경 `BG_ScreenSky_Lv3` + 밤 조명 프로필 연결.
- [98] (참조만) 90번은 `RoomScope` 방 범위 정리 대상에서 일부러 제외(현재 동작 유지).
- [101] (형빈) 비어 있는 효과음 `GunShot`(단발 총소리)/`ChargeShotFire`(110번 강화 총알 발사)는 다른 에셋에서 구해 `SFX_<이름>.ogg`로 넣고 자동 배치 메뉴.
- [102] (보류, 참조만) BGM 여러 곡(타이틀/설정용 차분한 곡 등) - 지금은 한 곡 고정, 화면별 곡 교체 기능이 필요.

## 변경 로그 (버전당 한 줄)

- v1.4~v1.57: 기반 구축 ~ 신규 SD 캐릭터(각 버전 구간 아카이브 참고).
- v1.58: 아이템 87(적 포섭 아군)/101/103/104/105/112/115/117, 사격·장전 알림 + 즉시 장전, 회복 배율, 무기 모드 0개 진입, 적 접촉 트리거·적 탄환 버그 수정.
- v1.59: 아이템 62/74/91/94/99/102+109/106/111/113/116/118/120/137, 머리 위 알림 줄 세우기 + 성공/실패 알림, 110번 굴절 셰이더, 적 상태 색/공격 속도 등록소,
  스탯 강제 연출.
- v1.60: 무기 모드 자원 표시, 최대 체력 상한 10 + 한 줄 체력 칸, 보호막 + 아이템 58/97, 실시간 초상화, 아이템 도감, ESC 중단 화면 도감/재시작, R 꾹 누르기 재시작.
- v1.61: 프로젝트 지식 정리(스크립트 사본 전부 제거, 아카이브 v1.26~v1.58 → 장치 Docs 폴더), 주석 정책 + 설계노트, 확인 필요/할 일·보류 분리, 93번 사용 연출,
  등급 이름 색 기본값 통일, 배경 외곽선 보정(볼록 모서리 노멀 굽기 + 크기별 두께).
- v1.62: SD 장전 모션 `SD_Reload`, 입장 컷씬 `IntroCutscene` + 전신 '짜잔' 포즈 `SD_IntroPose`, 헬기 퇴각/도착 연출 `HeliExtraction`(그림자 헬기 + 밧줄 사다리 + 먼지),
  Blender(bpy) 애니메이션 제작 방식 정리.
- v1.63: 2레벨(레벨별 일반 적 마릿수·종류, 2레벨 보스 6단계 패턴 - 보호막/그로기/소환/포격/벽 돌진), 1·2레벨 보스 초상화 + 1레벨 전방위 강화 총알 외형,
  90번 재정립(보스 비방해), 첫 돌진/전방위 충전 이펙트 칸, 보스 상자 아이템 후보·적 Rigidbody 버그 수정, 레벨별 화면/초상화 배경.
- v1.64: 방 범위 규칙 `RoomScope`(남은 수류탄/클레이모어/헬기·사다리/총알/VFX 정리 + 조용한 폭발), 헬기 도착 버그 수정 + 날아오는 도착 연출, 레벨 끝 최대 체력 손실 +
  상한 초과분 기억 X, 아이템 29/32/38/113 보완 + 들고 온 슬롯 아이템 중복 등장 방지, 2레벨 보스 첫 돌진 방향 고정, 레벨별 조명(2레벨 저녁/3레벨 밤) + 밤 전용 빛 자동 부착.
- v1.65: 사운드 시스템(`GameAudio`/`SoundLibrary`/`SfxId` 약 60칸 + 게임 전반 연결, 효과음 63개 배치), BGM(인트로 + 반복 곡, 보스방 경고음 동안 끊김 후 처음부터),
  설정 창(볼륨 - 시작 화면 + ESC 중단 화면), 중단 화면 버튼 두 줄.
