# 블아이작 (Mob_archive) - 핵심 상태 요약 (경량 문서, 항상 짧게 유지)

## 이 문서의 용도 (중요 - 앞으로 계속 지킬 것)

이 문서는 **"지금 무엇이 맞는지"만** 담는 짧은 문서입니다. 세션이 끝날 때마다 이 문서 전체를 다시 덮어쓰지만,
분량이 계속 늘어나지 않도록 **새 내용이 생기면 기존 줄을 고치거나 한두 줄만 추가**하는 것을 원칙으로 합니다.
자세한 설계 배경, 코드 확인 결과, 버그 진단 히스토리, 세션별 대화 흐름은 이 문서에 적지 않습니다. 그건 두 곳에
이미 있습니다.

1. **각 스크립트 파일 상단의 한국어 주석** - 이 프로젝트는 원래부터 "형빈이 이해할 수 있도록 설계 배경을 코드
   주석에 남긴다"는 방침이라, 왜 이렇게 만들었는지는 대부분 그 파일을 열어보면 나옵니다.
2. **아래 "아카이브 목록"의 문서들** - 예전에 쓰던 방식대로 세션 전체를 서술식으로 정리한 문서입니다. 아카이브는
   한 번 만들면 절대 지우거나 다시 쓰지 않고, 필요할 때 Claude가 `project_search`로 검색해서 필요한 부분만
   찾아봅니다. 이 문서(핵심 상태)가 다시 길어지기 시작하면 그 시점까지의 서술을 새 아카이브로 옮기고 여기는
   다시 짧게 정리합니다.

**이렇게 나눈 이유**: 예전 방식(`claude_Mob_archive_진행상황_통합요약_v1.NN.md`를 세션마다 통째로 다시 읽고
다시 쓰는 방식)은 매번 이전 버전 전체 + 이번 세션 내용을 합쳐 새로 쓰기 때문에, 문서가 세션을 거듭할수록
무한히 커지고 그만큼 매번 읽고 쓰는 비용도 계속 늘어났습니다(v1.25 12만 자 → v1.26 15만 자). 이 문서는 "지금
상태"만 담아 크기가 거의 늘지 않고, 서술은 아카이브로 분리해 필요할 때만 검색하는 구조로 v1.27부터 전환합니다.

**갱신 시점**: 이 문서도 예전과 동일하게 형빈이 명시적으로 요청할 때만 갱신합니다(세션 중간에 임의로 건드리지
않음).

## 아카이브 목록 (자세한 설계 배경/히스토리는 여기서 검색)

- `claude/claude_Mob_archive_진행상황_통합요약_v1.26.md` - v1.4~v1.26 전체 히스토리(적 AI, 던전 생성, HUD 1~7단계,
  패시브/액티브/음식/일회용/장신구 아이템 시스템, 레벨 전환, 보물상자, 미니맵, 전투방 초기화, 런타임 방 추가 등
  모든 기능의 설계 배경 + 세션별 변경 히스토리 + 버그 진단 기록 포함). 지금까지의 모든 상세 서술은 전부 여기
  있습니다.

## 작업 방식 (항상 지킬 것)

- 형빈은 3D 모델러이며 코딩은 담당하지 않음. Claude가 전량 C# 스크립팅 담당.
- **작업 장치/폴더(v1.44)**: 장치마다 폴더가 다르고 형빈이 장치 간 동기화를 git(`저장하기.bat` = `git add .` → commit →
  push)으로 함. v1.44 작업 폴더 = `C:\Users\User\OneDrive\바탕 화면\승춘파일\문서\형만이 임시 자료\Mob_archive`
  (v1.42는 ggumnu의 `C:\Users\beens\Mob_archive`). 연결된 폴더는 세션마다 확인할 것. 이 연결에는 장치 쪽 셸이 없어서
  파일은 스테이징 → 클라우드에서 편집 → 커밋 방식으로만 작업. 덮어쓰기 전에 다시 스테이징해 `cmp`로 원본이 그대로인지
  확인하고 `expectedMtimeMs`를 넘길 것.
- **분석 먼저 요청 시(v1.44)**: 형빈이 "분석부터"/"바로 진행하지 말고"라고 하면 코드를 쓰지 않고 분석/설계만 제시.
- 코드는 항상 완성된 전체 파일로 전달(스니펫/diff만 X). 기존 스크립트를 수정할 때는 "어느 파일의 무엇을
  바꾸는지"를 먼저 밝히고 승인받은 뒤 진행 - 파일 개수를 최소화하는 것보다 안정성/버그 위험 최소화가 항상 우선.
- 새 기능/버그 수정 착수 전에는 항상 먼저 기획 이해 확인 + 코드 확인 결과 + 계획을 제시하고, **결정할 게 있으면
  반드시 같은 턴에 질문 도구를 함께 띄워서** 답을 받은 뒤, 확정된 사양을 요약해 명시적 승인("맞아, 진행")을 받고
  나서 코딩을 시작함. 설계 메시지만 글로 보내고 질문 도구를 빼먹으면 형빈이 "끊긴 줄 알았다"고 되묻는 일이
  반복됐음(v1.39에서도 한 번 재발) - 항상 같이 보낼 것. (단, 형빈이 "짚을 점 있으면 요청하고 없으면 바로 진행해도
  좋아"처럼 먼저 승인해두면 그 턴에는 질문 없이 바로 진행 가능.)
- 설계 시 반드시 실제 프리팹/코드/에셋을 먼저 확인할 것(v1.39 교훈): 수류탄 착지점 보정을 "착지점 아래 콜라이더가
  무엇인지"로 판정했다가, 바닥 프리팹 안에 지면 메시/투명 이동 제한 벽/Wall 태그 장애물이 섞여 있어 두 번 실패함.
  결국 콜라이더와 무관한 좌표 계산(방 = cellSize 정사각형 칸)으로 해결 - 프리팹 구성에 의존하는 판정은 피할 것.
  애니메이션 문제는 `assimp`(apt `assimp-utils`)로 FBX를 JSON으로 내보내 뼈 회전을 직접 비교할 수 있음(v1.40).
- **이름 변경 시 에셋 데이터도 확인(v1.42 교훈)**: 코드 식별자만 바꾸면 에셋(.asset/.prefab/.unity)에 저장된 문자열
  (예: `RoomVariant.variantId`)과 직렬화 필드 이름은 그대로 남음. 이름을 바꿀 때는 Assets 전체(Config/Scenes/
  플레이어 프리팹 포함)를 대소문자 무시로 함께 검색할 것.
- Unity 에디터 작업 안내는 메뉴 이름/버튼 이름/인스펙터 필드명까지 클릭 단위로 정확히. 형빈이 한국어 안내를 원함(v1.45 -
  답변/안내는 항상 한국어). HUD/컴포넌트 위치를 안내할 때는 Hierarchy 검색칸 `t:컴포넌트이름`(예:
  `t:InteractPromptDisplay`)을 알려주면 찾기 쉬움(대부분의 HUD 컴포넌트는 씬의 `PlayerHUD` 프리팹 안에 있음, v1.45).
- **새 UI 글자는 항상 TextMeshPro(v1.45)**: 옛 `UnityEngine.UI.Text`는 화면 배율에 따라 흐려져서 HUD 글자 14개 파일을
  전부 TMP로 교체함. 새 글자를 만들 때는 `HUDText.AddTo(go, TextAnchor, wrap)` 한 줄(글꼴 `HUDFont.GetTMP()`, 줄바꿈/넘침/
  클릭 차단 기본값 통일), 외곽선은 `Outline` 컴포넌트 대신 `HUDText.SetOutline(text, 픽셀, 색)`(글자 크기를 먼저 정한 뒤
  호출). `HUDFont.Get()`(옛 Font)은 남아 있지만 새 코드에서는 쓰지 않음.
- 새로 만드는 파일: UTF-8(BOM 있음) + CRLF. 기존 파일 수정 시: 그 파일의 원래 인코딩/줄바꿈을 그대로 보존(파일마다
  다름 - CP949 파일도 있음: `Playeraimandattack`/`Playershooting`/`Aimtrajectoryindicator`/`Statmodifier`/
  `Playerhitflash`/AI 컨트롤러 3종/`Minimapdisplay`(끝 줄바꿈 없음)/`Playerinventorydisplay`/`Playerhudmanager`/
  `Levelportal`/`Playeriteminfodisplay`/`Appearancecontext`/`Appearancecontextdrawer`/`Playermovement`/
  `Playerresourcedisplay`/`Ammoboxdisplay`/`Leveltransitionconfirmui` 등, `PlayerResources`는 UTF-8 BOM 없음 + LF,
  `Gameflowmanager`/`Treasurechestinfodisplay`/`HUDFont`는 UTF-8 BOM 없음 + CRLF). 형빈이 요청하지 않은 버전 태그
  주석/헤더는 추가하지 않음. (단, 형빈이 "이 프로젝트는 혼자
  진행해서 주석/용어 정리가 굳이 필요 없다"고 밝힘(v1.38) - 다만 Claude 입장에서는 과거 설계 결정/확정 사항을
  코드만 보고 다시 파악할 때 주석이 실질적으로 도움이 되므로, 당장 걷어내지는 않고 필요할 때 다시 논의하기로 함.)
- 장치 폴더에 파일을 저장한 뒤에는 항상 폴더를 다시 조회해서 크기가 맞는지, `-1` 같은 이름의 중복 파일이 새로
  생기지 않았는지 확인할 것(v1.37 `-1` 복사본 컴파일 오류, v1.38 OneDrive가 커밋된 파일을 이전 버전으로 되돌린
  사례). **v1.40 교훈: 같은 outputs 경로로 다시 커밋하면 이전 버전이 캐시되어 그대로 써지는 일이 있었음 - 커밋할
  때마다 outputs 아래 새 하위 폴더(v2, v3...)를 쓰고, 커밋 후 다시 스테이징해서 `cmp`로 내용까지 확인할 것.**
  **v1.43에서 재발**: 배경 이미지 `BG_SkyGradient.png`를 같은 outputs 경로로 다시 커밋했더니 "written"으로 응답했지만
  장치에는 예전 이미지가 남아 있었음(형빈이 Reimport All 후에도 예전 그림이 보인다고 알려줘서 발견, `force: true` 재커밋 후
  스테이징해 픽셀 값으로 확인). 텍스트뿐 아니라 이미지 등 모든 파일에 위 규칙을 지킬 것.
  커밋은 스테이징 때 받은 `expectedMtimeMs`를 함께 넘김. 이미 저장된 파일을 다운로드용으로 다시 첨부하지 않음.
- 스크립트를 프로젝트 문서에 동기화하는 시점: 이 프로젝트는 "바꿀 때마다 즉시" 동기화(형빈 확정 - 여러 세션이
  같은 파일을 건드릴 때 최신 상태가 어긋나는 것 방지). 세션 끝에 형빈이 요청하면 그 세션의 모든 스크립트를 로컬
  폴더 실제 파일 기준으로 한 번 더 재동기화(CP949 파일은 UTF-8로 변환해서 문서에 올림). 이 핵심 상태 문서는
  명시적 요청이 있을 때만 갱신.
- Claude가 직접 파일을 지울 수 없음 - 더 이상 안 쓰는 스크립트는 형빈에게 유니티 Project 창에서 삭제하도록 안내.
- 사용량(토큰) 절약 원칙(v1.27 추가): 폴더 전체를 훑을 땐 먼저 목록만 보고 꼭 필요한 파일만 열어볼 것. 검증용
  스크립트를 반복 실행할 때는 결과 요약만 콘솔에 남기고 장황한 로그를 전부 다시 읽지 않을 것.
- 컴파일 사전 검사(v1.39 추가): 작업 환경에 mono `mcs`를 설치해 최소 Unity API 대체 코드(stub)로 새/수정
  스크립트를 컴파일해보는 방식으로 오타/타입 오류를 미리 걸러낼 수 있음(실제 유니티 컴파일 대체는 아님). mcs는
  `default` 리터럴/`??=`/제네릭 지역 함수를 지원하지 않음 - 앞의 두 개는 유니티에선 되므로 그대로 두고, 제네릭 지역
  함수는 private static 제네릭 메서드로 작성.
- ScriptableObject 생성 메뉴는 전부 `MobArchive/<이름>` 한 단계로 통일(v1.40, 형빈 확정).
- **빌드 전용 함정(v1.45 교훈)**: 실행 중 `GameObject.CreatePrimitive`로 만든 도형은 옛 렌더링 방식(Built-in) 기본 재질을
  써서 에디터에선 멀쩡해도 **빌드에선 핑크**로 나옴(수류탄 임시 구체가 그랬음). 런타임 도형에는 반드시 URP 재질을 직접
  입히거나(적 탄환처럼 `Shader.Find("Universal Render Pipeline/Unlit")`) 재질이 붙은 프리팹을 쓸 것.

## 프로젝트 개요

- Unity 6.3 LTS(URP) 3D 탑다운 로그라이트. *The Binding of Isaac: Repentance*의 룸 기반 진행/아이템 시너지 +
  블루아카이브식 등각(마름모) 시야 카메라 + 블루아카이브 캐릭터/아이템 소스.
- 플레이어 캐릭터 모델(v1.44): 기본은 **SD 유니티짱**(Asset Store "SD Unity-chan 3D Model Data" v3.0a, `Assets/UnityChan`,
  Unity Toon Shader 0.13.0-preview 사용, 라이선스 UCL 3.0 - 포트폴리오 영상/이미지에 유니티짱이 나오면 라이선스 로고/크레딧
  표기 필요). 예전 블루아카이브 립핑 SD 모델(레이사)도 그대로 동작하도록 남겨 두었음(아래 [플레이어 애니메이션]). 배포 계획
  없음, GitHub 비공개, 포트폴리오에는 플레이 영상과 일부 PNG만 사용(형빈 확정).
- 영문 코드 식별자 확정: 샬레(학원 연합체) = `Schale`, 청휘석(재화) = `Pyroxenes`. 게임 내 한국어 표시 텍스트는
  "샬레"/"청휘석" 그대로 - 이 표기는 C# 필드/enum 이름에만 적용.
- 등급 명칭: `ItemRarity` = `Tier0`~`Tier3`(표시는 "Tier 0"~"Tier 3").
- 용어 확정(v1.41): n.5 레벨 서쪽 선택방 = "게마트리아"(영문 식별자 `Gematria` - `StatType.GematriaChance`,
  `PlayerStats.gematriaChance`(`[FormerlySerializedAs("blackSuitChance")]` - 이전 저장값 연결용이라 유지),
  예전 "검은 양복"/BlackSuit는 전부 교체). v1.42에서 남아 있던 에셋 데이터 2곳도 교체 완료:
  `ChoiceLevelConfig.asset`의 서쪽 Variant Id `blacksuit` → `Gematria`(동쪽은 `Schale`), `Reisa_Player.prefab`의
  저장 필드 이름 `blackSuitChance` → `gematriaChance`. Appearance Contexts의 ID 드롭다운
  (`Appearancecontextdrawer`)은 모든 RoomGenerationConfig/ChoiceLevelConfig 에셋의 `variantId` 문자열을 읽어서
  만들어지므로, ID는 코드가 아니라 에셋 데이터임.
  `ReloadTime` 스탯의 한국어 표기는 "장전 시간"(예전 "장전 속도" 표기 전부 교체 - 값이 작을수록 빠름).

## 확정 상태 다이제스트 (핵심 값만 - 자세한 배경은 위 아카이브 참고)

### 게임 흐름 - 시작 화면 / 로딩 화면 (v1.45 신규)

- **씬 구성**: `Assets/Scenes/Title`(Build Profiles 0번) + `LevelBuilder`(1번). 시작 화면과 게임을 씬 단위로 나눠, 시작 화면
  복귀/재시작 때 플레이어/HUD/정적 등록소 정리가 씬 로드로 저절로 됨. LevelBuilder 씬을 에디터에서 바로 Play해도 예전처럼 동작.
- **`TitleScreenUI`(Title 씬의 `TitleScreen` 오브젝트)**: Canvas/배경/버튼 코드 자동 생성(라이브 트윅). 배경 스프라이트는 비율
  유지 꽉 채움(AspectRatioFitter EnvelopeParent), 버튼 3개(게임 시작/설정/나가기)는 가운데 살짝 아래(`buttonsCenterOffset`
  기본 (0,-200)), 공통/버튼별 스프라이트(9-slice 자동), 색 전환(스프라이트를 넣으면 Normal Color를 흰색으로), 글자 비우면 숨김.
  게임 시작 = 로딩 화면 + `gameSceneName`(LevelBuilder) 로드, 설정 = 빈 `OnSettingsClicked()`(기능 없음, 형빈 확정),
  나가기 = `Application.Quit()`(에디터는 Play 종료).
- **`LoadingScreen`(프리팹 하나를 Title/LevelBuilder 두 씬에 모두 배치, `DontDestroyOnLoad` 싱글턴 - 먼저 생긴 하나만 남고
  중복은 스스로 지움)**: 검은 배경 + 캐릭터 아이콘이 좌우로 갸우뚱(Z축 사인, `tiltAngle` 10°/`tiltPeriod` 1.2초), sortingOrder
  1000(모든 UI 위). 나타날 땐 즉시, `minDisplayTime` 1초 + 작업 뒤 착지 대기 `settleTimeAfterWork` 0.5초를 둘 다 채우면
  `fadeOutDuration` 0.2초 페이드. 실제 시간 기준. 진행 중 게임에서는 검은 화면을 한 프레임 그린 뒤 빌드(끊김을 가림), 씬이 막
  열린 Start에서는 바로 빌드(Start 순서 유지). 로딩 중 조작 차단: 사격/무기 전환/수류탄/해킹/액티브/일회용/음식 컴포넌트를
  껐다가 페이드 시작 때 복구(투척·해킹 동작 중이면 그 컴포넌트는 안 끔), 이동은 `PlayerMovement`가 `LoadingScreen.
  BlocksPlayerInput`을 보고 입력만 0(중력은 그대로라 낙하 정상). 씬에 없으면 예전과 100% 동일.
  공개 API: `RunLevelWork(work, waitOneFrame)`, `LoadSceneWithLoading(startLoad)`, `StartSceneLoad(name)`/
  `StartReloadActiveScene()`/`CanLoadScene(name)`(Build Profiles에 없어도 에디터에선 경로로 로드).
- **로딩 화면이 뜨는 때(형빈 확정)**: 게임 시작, 포탈로 다음 레벨 진입(`GameFlowManager.AdvanceToNextPhase` - 한 프레임 대기
  중 중복 요청 무시), 게임 오버 [예] 재시작, 게임 오버 [아니오] 시작 화면 복귀. `GameFlowManager.StartNewGame()`도 로딩 화면을
  거침.

### 비주얼 / 셰이더 (v1.43 신규 - 블루아카 인게임 툰 스타일, "아침의 산뜻한" 톤)

- **조명/후처리(씬마다 따로 저장 - LevelBuilder/ItemTestArena 둘 다 같은 값으로 적용 완료)**: Directional Light =
  Light Appearance `Color`, 색 `F5F9FF`(살짝 푸른 흰색), Intensity 1.6, Shadow Strength 0.55, 회전 (50,-30,0).
  Lighting 창 Environment: Environment Lighting Source = `Color`, Ambient Color RGB(200,218,240)(HDR 창이라 Hex 칸 없음),
  Environment Reflections Intensity Multiplier = 0, Skybox Material = `BG_ScreenSky`. `PC_Renderer`의 SSAO Renderer
  Feature는 끔(체크 해제 - 다시 켜면 복구). Global Volume 프로필 = `Assets/Settings/SampleSceneProfile`(두 씬 공유 - 한
  쪽에서 바꾸면 같이 바뀜): Tonemapping Neutral, Bloom(threshold 1/intensity 0.25), Vignette 끔, Color Adjustments
  (Post Exposure 0.15/Saturation 8), White Balance(Temperature -15). URP 렌더링 경로 = Forward+, 데칼 = DBuffer +
  Decal Layers 켜짐(PC_Renderer).
- **카메라(`GameCamera` 프리팹 - 두 씬 공통)**: Background Type = `Skybox`(배경은 아래 화면 고정 배경 셰이더가 그림),
  Post Processing 켬. (1단계 중 잠시 Solid Color `A9BED3`였다가 v1.43 끝에 Skybox로 되돌림.)
- **`MobArchive/Toon_Environment`(배경 - 벽/바닥/장애물/소품, `Assets/Shaders`)**: 매트 + 부드러운 2단 명암
  (`_ShadowThreshold` 0/`_ShadowSmoothness` 0.1) + 색 있는 그림자(`_ShadowColor` 기본 (0.72,0.78,0.92)), 빛은 "색"만 반영
  (세기 무시 - 밝기는 재질 `_Brightness`), 그림자 진하기는 Light의 Shadow Strength 반영, 추가 조명(Point/Spot)은 무시,
  선택 하이라이트(`_SpecularStrength` 기본 0, MetallicGlossMap A채널), DBuffer 데칼 받기(`ApplyDecalToBaseColor`),
  외곽선 Pass(`SRPDefaultUnlit`, 기본 꺼짐 - 지금은 배경 재질 전체 켜짐, 건물 `Building_Mat 1~4`는 두께 2.5로 조금 두껍게).
  DepthNormals는 데칼 레이어 호환을 위해 `UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"`(그래서 SRP Batcher 비호환 -
  성능 영향 미미). 배경 재질 노멀 맵 세기 0.4.
- **`MobArchive/Toon_Character`(캐릭터 - 플레이어/적/캐릭터 소품)**: 또렷한 셀 셰이딩(`_ShadowSmoothness` 0.02),
  그림자 색 기본 (0.82,0.74,0.84), `_ShadeAmount`(명암 적용 정도 - 얼굴/눈입은 0), `_ReceiveShadowAmount`, 림라이트
  (`_RimStrength` 0.25/`_RimWidth` 0.3), 선택 하이라이트, 외곽선 Pass(뒷면 부풀리기, 두께는 화면 기준 1080p 대략 픽셀 -
  `_OutlineWidth` 기본 2, `_OutlineColor` (0.22,0.18,0.24), `_OutlineBaseColorBlend` 0.5, 꺼짐이면 정점을 한 점에 모아 안 그림).
  외곽선은 플레이어 + 적 모두 켬(형빈이 적에도 적용).
- **두 툰 셰이더 공통 호환 규칙**: URP/Lit과 프로퍼티 이름이 같아서(`_BaseMap`/`_BaseColor`[MainColor]/`_BumpMap`/
  `_BumpScale`/`_MetallicGlossMap`/`_Smoothness`/`_EmissionColor`/`_Cull`/숨김 `_Color`·`_MainTex`/투명 전환용 숨김
  `_Surface`·`_SrcBlend`·`_DstBlend`·`_ZWrite`) 셰이더만 바꿔도 텍스처/색 유지, Lit으로 되돌리면 원상 복구. 그래서
  `EnemyHealth`(피격 색/발광)/`EnemyMeleeAttack`(돌진 예고 색)/`DissolveAndVanish`(투명 전환)/`PlayerHitFlash`
  (`material.color` + 렌더러 점멸)를 수정하지 않고 그대로 동작. 새 셰이더를 만들 때도 이 이름들을 유지할 것.
- **플레이어 재질**: 실제 프리팹 `Reisa_Player_Fixed`는 `Reisa_Fixed.fbx`/`Reisa_gun_Fixed.fbx`의 내장 재질을 쓰고 있었음 →
  `Enemy_Mat`의 `Reisa_Mat 1~5`(1=Body/2=EyeMouth/3=Face/4=Hair/5=Weapon, 텍스처 `CH0167_*`)를 복제해
  `Assets/Player/Character/Player/Player_Mat/Player_Body·EyeMouth·Face·Hair·Weapon` 생성(Toon_Character, 외곽선 켬,
  Face/EyeMouth는 명암 적용 정도 0) → 두 FBX의 Materials 탭 Remapped Materials(`CH0167_*` 슬롯)에 연결. 적 재질
  (`Enemy_Mat`의 `Drone_Mat 1~3`, `Reisa_Mat 1~5` - 구 `Reisa.fbx`/적 Reisa가 사용)은 별도.
- **탄환/수류탄 재질(v1.45, `Assets/Player/Bullet`)**: 플레이어 총알 `Bullet_Temp` 프리팹 = `Bullet_Player_Mat`(URP Unlit,
  노랑 `FFE000` - 예전엔 URP 패키지 기본 Lit 재질이라 직접 수정 금지였음). 수류탄 = `Grenade_Temp` 프리팹(구체 스케일 0.1,
  콜라이더 제거, `Grenade_Mat` = Toon_Character 어두운 초록 `1F4D1F` + 외곽선) → 두 플레이어 프리팹의 `PlayerGrenadeThrower.
  grenadePrefab`에 연결(빌드 핑크 해결). 적 탄환은 코드 생성 Unlit 빨강(1,0.15,0.15) 그대로. **총알/적 탄환의 은은한 빛 번짐은
  Bloom(threshold 1)이 최대 밝기 Unlit 색을 번지게 하는 정상 동작 - 형빈이 유지로 확정.**
- **셰이더 일괄 교체 도구(`ToonShaderBatchConverter`, 메뉴 `MobArchive/셰이더 일괄 교체`, `Assets/Editor`)**: Assets 안의
  URP/Lit 재질(.mat)만 목록화 → 체크 + 배경용/캐릭터용 선택(`/Player/Character/` 경로는 캐릭터용 기본) → 일괄 교체
  (배경 노멀 세기 0.4, 캐릭터 외곽선 켬 옵션, Undo 가능). TutorialInfo/VFX/UI_Sprite/Settings 폴더는 기본 체크 해제.
  FBX 내장 재질(Remap 안 된 것)은 따로 표시 - Materials 탭 "Extract Materials..."로 꺼낸 뒤 다시 검색(자동차 3종은 이렇게
  꺼내서 교체 완료, 애니메이션 FBX는 무시). "배경용 재질 외곽선 전체 켜기/끄기" 버튼 있음. 새 모델/재질을 추가하면 이
  도구로 교체할 것.
- **화면 고정 배경 `MobArchive/ScreenBackground`(스카이박스 재질 `Assets/Background/BG_ScreenSky`)**: 방향이 아니라 화면
  좌표(`GetNormalizedScreenSpaceUV`)로 그려서 카메라가 이동해도 고정, 왜곡 없음, 맵이 안 그려진 빈 공간만 채움. 배경
  이미지는 비율 유지 꽉 채움(cover), 패턴 타일을 화면 높이당 `_PatternTiles`(기본 14)번 반복 + `_PatternOpacity` 0.35 +
  `_PatternScroll`(흐름, 기본 0) + `_Tint`/`_Exposure`. 임시 이미지: `BG_SkyGradient.png`(2048x1152, 세로 그라데이션 - 위
  (20,112,222) 진한 파랑 → 가운데 (58,158,238) → 아래 (118,200,246) 파란 하늘색, Compression None) +
  `BG_DiamondPattern.png`(128x128 투명 배경 흰 마름모 2개 엇갈림 타일). 배경도 후처리(White Balance/채도)를 거침.
  최종 이미지로 바꿀 땐 재질의 배경 이미지만 교체(2048보다 크면 Max Size 4096).
- 기존 `SpriteFlash.shader`(아이템 획득 아이콘용, `Assets/VFX/Shaders`)와 아이템 아이콘/이펙트/UI 셰이더는 손대지 않음
  (형빈 확정 - 툰 셰이더는 캐릭터/적/장애물/벽/바닥에만).

### 플레이어

- 관성 없는 CharacterController 이동, 이동방향/우클릭 마우스 조준 전환식 twin-stick 조준, 사거리 기반 소멸
  탄환, 무적 프레임 있는 체력(`PlayerHealth`).
- 플레이어 프리팹 2개(v1.44, `Assets/Player/Character/Player`): `UnityChan_Player`(SD 유니티짱)와 `Reisa_Player_Fixed`(레이사).
  LevelBuilder(테스트 씬은 ItemTestArenaBuilder)의 플레이어 프리팹 칸에 어느 쪽을 넣느냐로 교체. 게임 로직 컴포넌트는 두
  프리팹이 같고(`UnityChan_Player`는 `Reisa_Player_Fixed`를 복제해 만듦) 모델/Animator 쪽만 다름. `UnityChan_Player` 구조:
  루트 → `MeshRotationOffset`(Y -20°) → `SD_unitychan_humanoid`(스케일 0.6, Layer/Tag = Player, 데모용 컴포넌트 제거,
  `HumanoidAimLookAt`/`TorsoLayerWeightByState` 부착), 총 `Reisa_gun_Fixed`는 `Character1_RightHand` 아래 + 총구 방향 표시용
  자식 `MuzzleDirection`. 플레이어 컴포넌트 값을 바꿀 때는 두 프리팹 모두 같은 값으로 맞출 것.
- **비조준 사격 시 자동 조준(v1.44, `PlayerAimAndAttack`, 두 캐릭터 공통)**: 우클릭 없이 좌클릭하면 비조준대기 → 조준대기 →
  사격 → 비조준대기 순서. 조준 자세가 갖춰지면(`AimBlend` ≥ `aimReadyBlend` 1 + `aimSettleTime` 0.08초, 최대
  `maxAimWaitTime` 0.4초) 발사, 연사가 끝나고 `shotAimHoldTime` 0.4초 동안 다시 안 쏘면 비조준 복귀(그 사이 클릭은 바로 발사).
  자동 조준 중에도 캐릭터는 마우스가 아니라 이동 방향을 바라보고(형빈 확정), 조준선은 우클릭 조준 때만 보임(`IsAiming`
  프로퍼티 = 우클릭 여부만). 장전 중/탄약 0이면 자동 조준 없이 예전 규칙(탄약 0 → 자동 장전). 대기 중 무기 모드 변경/획득
  연출/장전 시작이면 발사 취소. `autoAimOnFire`를 끄면 예전 허리쏘기. Animator의 `IsAiming`/`AimBlend`는 우클릭 조준 +
  자동 조준을 합친 값.
- `PlayerStats`가 스탯 단일 소스: 최종값 = (기본값 + 모든 덧셈 모디파이어 합) x (모든 곱셈 모디파이어의 곱) →
  스탯별 하한선(`statFloors`, 기본 전 스탯 Ratio 0.5) 적용. 획득 순서 무관. `StatType`에 새 스탯을 추가할 때는
  반드시 enum 맨 끝에 추가(에셋의 스탯 종류가 밀리지 않도록). 이미 저장된 `statFloors` 목록에 새 스탯 줄이 없으면
  `EnsureAllStatFloors()`가 자동으로 채움(`OnValidate` + 실행 시). 새 스탯 추가 시 `GetBaseFieldValue`와
  `SetBaseFieldValue`(v1.40 추가) 둘 다에 매핑 추가.
- 연사(Multishot)는 발수만큼 탄약 실제 소모, 부족하면 그 자리에서 연사 중단(장전 후 이어지지 않음).
- 소모성 자원(수류탄/해킹툴/크레딧/청휘석) 시작 지급량을 `PlayerResources`에서 설정 가능(기본 0).
- 조준 궤적선(`AimTrajectoryIndicator`), 피격 무적 점멸(`PlayerHitFlash`, `blinkInterval`=0.08초/
  틴트=흰색, 확정) 있음. `AimTrajectoryIndicator`는 방향(최대 2개 - 19번) x 구간(35번 튕김 미리보기) 배열 구조 -
  `LineRenderers`(복수 배열)를 `PlayerHitFlash`가 무적 점멸 제외 대상으로 참조. 플레이어 자식으로 붙는 표시용
  오브젝트는 `PlayerHitFlash`가 Awake에서 렌더러를 모으기 때문에 Start에서 만들면 점멸 대상에서 자동 제외됨
  (수류탄/해킹 조준 표시가 이 방식).
- **무기 모드(v1.39, v1.40 확장)**: `PlayerWeaponMode`가 1키 총 / 2키 수류탄 / 3키 해킹툴 전환을 전담(자원 0개면
  전환 불가, 마지막 1개를 쓰면 총으로 자동 복귀 + 그 클릭으로 총이 같이 나가지 않게 `BlocksGunFireThisFrame`). 총
  사격(`PlayerAimAndAttack`)/R키 재장전(`PlayerShooting`)/총 조준선(`AimTrajectoryIndicator`)은 "총 모드인가?"만
  확인. v1.40: 모드가 바뀔 때마다 Animator Int `WeaponMode`(0 총/1 수류탄/2 해킹) 전송 + `gunModel`(프리팹에
  `Reisa_gun_Fixed` 연결) 오브젝트를 `hideGunInGrenadeMode`(켬)/`hideGunInHackMode`(끔)에 따라 SetActive로 숨김
  (Renderer가 아니라 오브젝트를 끄는 이유: 피격 점멸이 Renderer를 다시 켜기 때문). 수류탄 투척 동작 중
  (`PlayerGrenadeThrower.IsThrowing`)에는 전환/자동 총 복귀를 미루되, 수류탄이 손을 떠난 뒤(`CanInterruptThrow`)에는
  1번/3번으로 투척을 즉시 끊고 전환 가능(손 떠나기 전엔 모든 전환 무시, 끊으면 투척 대기시간도 사라짐 - 1→2 빠른
  재투척 허용, 형빈 확정).
- **수류탄 투척(v1.39, v1.40 타이밍 추가)**: `PlayerGrenadeThrower`/`Grenade`/`GrenadeAimIndicator`. 우클릭 조준
  중에만 좌클릭 투척. 조준 표시 = 사거리 흰 테두리 원 + 착지점 폭발 반경 반투명 원 + 곡사 조준선(`Grenade.
  EvaluateArc`). 비행 시간 고정(`flightTime`=0.6초), `arcHeight`=2. 착지 즉시 폭발(원기둥 판정 - 적 하나당 1회),
  자해 데미지. 방 밖/벽 위를 찍으면 "플레이어가 있는 칸 중심 ± (cellSize/2 - `roomInnerMargin`=1)" 사각형으로
  잘라냄(`LevelBuilder` 없는 씬은 보정 없음). 착탄 지점 원 표시, 폭발 이펙트 슬롯, 훅 `OnGrenadeThrown`/
  `OnGrenadeExploded`. v1.40: 클릭 순간 수류탄 소모 + 착지점 고정 + `Throw` 트리거 → `releaseDelay`초 뒤 실제
  발사(그 시점 위치 기준으로 사거리/방 범위 재보정) → `throwDuration`초까지 연타 금지, 마지막 수류탄의 총 복귀도
  동작이 끝난 뒤(값은 형빈이 클립에 맞춰 프리팹에 입력). v1.45: 모델 = `Grenade_Temp` 프리팹(위 [비주얼] 참고, 크기는 프리팹
  Scale로 조절). 프리팹 칸이 비면 임시 구체에 URP Unlit 재질 + `fallbackSphereColor`(기본 어두운 초록)를 입힘(재질은 하나를
  만들어 공유, 빌드 핑크 방지).
- **해킹툴 전개(v1.39)**: `PlayerHackTool`/`HackRangeIndicator`. 해킹 모드에서 조준 없이 좌클릭 즉시 전개(범위
  안에 적이 없어도 소모됨), 우클릭 중에는 발밑 범위 원. 반경 안의 적에게 해킹 데미지 + 해킹(강력 제어). 이펙트
  2종, 훅 `OnHackDeployed`. v1.41: 클릭 순간 해킹툴 소모 + `Hack` 트리거 → `deployDelay`초 뒤(그 순간 플레이어 위치
  기준) 효과 발동 → `deployDuration`초까지 사용 동작(`IsDeploying`, 그동안 모드 전환 미룸). 효과 발동 뒤
  (`CanInterruptDeploy`)에는 1/2번으로 동작을 끊고 전환 가능(수류탄과 같은 규칙). 해킹 모드에서도 총 숨김.
  값은 형빈이 클립에 맞춰 프리팹에 입력(Delay 0.2/Duration 1).
  **v1.45 적 총알 제거**: 효과 발동 순간, 적 해킹과 같은 원기둥(반경 `HackRadius`, 높이 `effectHeight`) 안의 적 총알(보스
  전방위 탄환 포함)을 모두 없앰(`clearEnemyBullets` 기본 켬, 적이 없어도 동작, 형빈 확정). `EnemyProjectile`이 살아 있는
  총알을 정적 목록(`Active`)에 등록하고 `ClearInRadius(중심, 반경, 높이, 위치목록)`로 제거(물리 검사 아님 - 바닥 평면 거리 ≤
  반경, 높이 (중심-반경)~(중심+높이+반경)). 사라질 때 이펙트 슬롯 `bulletClearVfxPrefab`(+오프셋/크기/유지 시간, 비면 이펙트 없음).
- **스킬 스탯(v1.39, `PlayerStats` 14/15번 그룹)**: 수류탄 데미지 10/자해 데미지 1(하한 고정 0)/폭발 반경 2.5/
  사거리 8, 해킹 데미지 5/범위 3/지속시간 2초. 전부 총 공격력과 무관한 독립 고정값, 아이템 모디파이어로 변경 가능.
- **장전(v1.40)**: 장전 중 총 모드가 아니게 되면(2/3번) 장전 취소(탄창 안 채워짐, 다시 R/빈 탄창 클릭으로 처음부터).
  `CurrentReloadDuration`(15번 보너스 등 전부 반영된 실제 장전 시간)이 진행률 UI와 애니메이션 속도의 공통 기준.
- **아이템 획득 연출(v1.41, 아이작식)**: 보물상자/동아리방 보물상자에서 플레이어 아이템(패시브/액티브/장신구/
  일회용/음식)을 실제로 가져간 순간 `ItemPickupEvents.Raise(item)`(정적 신호 - 기본 아이템 묶음은 안 보냄) →
  `PlayerItemPickupPresenter`가 `ItemPickup` 트리거 + `IsPickingUp` Bool로 획득 클립(`ItemPickup_RootFixed.fbx`,
  클립 "ItemPickup" 0~45) 재생, 머리 위(`iconHeight` 1.6, 스케일 0.05, 각도 (55,-45,0) 고정)에 아이콘이 흰색
  플래시(`SpriteFlash` 셰이더)와 함께 떠올랐다(0.35초/0.3 상승) 가라앉으며(0.35초/0.4 하강) 사라짐. 연출 중
  (`PlayerItemPickupPresenter.BlocksActions`)에는 사격/Space/Q/E/수류탄/해킹/무기 전환/새 R 장전 전부 차단(진행 중
  장전은 계속), 총 숨김, 조준선 숨김, 캐릭터는 카메라 쪽으로 회전(`pickupFacingRotationSpeed` 20). 끝나면 항상
  총 모드로 복귀(`TrySwitch(Gun)` 후 Bool 해제). 클립이 비면 `fallbackDuration` 1.5초. 사망 시 비활성화.
- **F 상호작용 대상 = 가장 가까운 하나(v1.42)**: `InteractionTargetSelector`(정적, 프레임마다 한 번 계산해 캐시)가
  보물상자/동아리방 보물상자/포탈 중 XZ 평면 거리로 가장 가까운 대상 하나만 고름. `TreasureChest`/`ItemPedestal`/
  `LevelPortal`의 Update는 `IsCurrentTarget(this)`가 아니면 F를 무시. F 프롬프트(`InteractPromptDisplay`)와
  아이템/보물상자 설명칸(`PlayerItemInfoDisplay`/`TreasureChestInfoDisplay`)도 `InteractionTargetSelector.Current`를
  따라감(겹친 상자 중 F로 열릴 대상의 설명이 보임).
- **슬롯 교체 확인창(v1.42)**: `SlotReplaceConfirmUI`(자동 생성, 씬 배치 불필요). 슬롯형 아이템(액티브/장신구/
  일회용/음식)을 상자에서 주워 기존 장착 아이템과 교체될 때, **장착 중인 아이템**의 `ItemDefinition.confirmBeforeReplace`
  (v1.42 추가 체크박스, 예: 교체 패널티가 있는 20번)가 켜져 있으면 "기존 → 새 아이템" 확인창을 띄움(이름은 등급 색 -
  `PlayerItemInfoDisplay`의 등급 색, 없으면 기본 색). F = 예, Esc = 아니오(창이 뜬 그 프레임 입력은 무시). 게임은
  멈추지 않고 플레이어 조작만 막음(이동/조준·사격/장전/무기 전환/액티브/일회용/음식 컴포넌트를 끄고 Animator 이동
  값을 0으로 만든 뒤 닫힐 때 복구, 수류탄/해킹은 클릭 차단만). 창이 떠 있는 동안 피격되면 아니오로 닫힘, 사망 시
  복구 없이 닫힘. 열려 있는 동안 + 닫힌 그 프레임(`BlocksInteract`)에는 상자/좌대/포탈 F 처리 안 함.
  `TreasureChest`/`ItemPedestal`의 슬롯 교체는 `IsSlotCategory`/`GetEquippedSlotItem`/`DoSlotSwap`/`ConfirmSlotSwap`로
  정리됨.
- **사망/게임 오버(v1.40)**: `PlayerHealth.Die()` → `OnDied`(인스턴스)/`OnAnyPlayerDied`(정적) 이벤트.
  `PlayerDeathHandler`(PlayerHealth가 Awake에서 자동 부착): 조작 컴포넌트 전부 끔(이동/조준·사격/장전/무기
  전환/수류탄/해킹/액티브·음식·일회용), 무기 모드 총으로 복귀, 조준선 숨김, 피격 점멸을 원래 상태로 되돌린 뒤
  끔(private `ResetToNormal`을 리플렉션으로 호출 - 이름 바뀌면 경고), Animator를 UnscaledTime으로 바꾸고 Bool
  `IsDead`=true(+ 호환용 `Die` 트리거). `GameOverUI`(씬의 빈 오브젝트에 붙임, 런타임 생성 Canvas sortingOrder 100):
  사망 즉시 `Time.timeScale=0`(게임 전체 정지) + F키 상호작용(포탈/동아리방 보물상자/일반맵 보물상자) 비활성화 +
  열린 포탈 확인창 숨김 → `showDelay`(실제 시간, 기본 2초 - 사망 클립 길이에 맞춤) 뒤 "게임 오버 / 다시
  시작하시겠습니까?" + [예]/[아니오]. [예] = 정지 해제 후 로딩 화면과 함께 현재 씬 다시 불러오기(레벨 1부터 새 랜덤 구조,
  모든 상태 초기화). [아니오](v1.45) = 정지 해제 후 로딩 화면과 함께 `titleSceneName`(기본 "Title") 씬으로 복귀(씬을 못 찾으면
  오류 로그만 남기고 창 유지). 씬 로드는 `LoadingScreen.StartReloadActiveScene`/`StartSceneLoad` 사용(에디터에선 Build
  Profiles 목록에 없어도 경로로 로드).
- **테스트 프로필(v1.40)**: 테스트 씬도 진짜 프리팹(`Reisa_Player_Fixed`)을 쓰고, `PlayerTestProfile` 에셋
  (Create > MobArchive > Player Test Profile, `Assets/Config`)을 `ItemTestArenaBuilder.testProfile`에 연결해
  덮어씀 - 스탯(목록에 넣은 스탯만), 자원(체크한 항목만: 시작 수류탄/해킹툴/크레딧/청휘석/코스트/최대 코스트),
  무적 모드(`PlayerHealth.testInvincible` - 피해 완전 무시, 피격 이펙트는 뜸), 자원 무한(`PlayerResources.
  testInfiniteResources` - 수류탄/해킹툴/코스트 소모 안 함). 두 스위치는 `[NonSerialized]`라 프리팹에 저장 안 됨,
  Play 중에도 실시간 반영. 플레이어를 꺼진 부모 아래 생성 → 덮어쓰기 → 꺼내서 켜기 순서라 Awake(시작 체력/탄창/
  자원/하한선)가 전부 덮어쓴 값 기준(하한선 Ratio도 덮어쓴 값 기준). 실제 게임 씬에는 영향 없음. 현재 프로필 값:
  최대 체력 100/장전 0.2/공격력 100/탄창 10/수류탄·해킹툴 100/코스트 10.
- **테스트 보물상자(v1.42, `ItemTestArenaBuilder`)**: `testChestItems` 목록에 넣은 아이템마다 진짜 `TreasureChest`를
  자동으로 한 줄 배치(내용물이 그 아이템으로 고정, 기본 줄 오프셋 (0,0,8)). `testChestsStartOpened`(처음부터 열린
  상태), 다 가져가면 `testChestRespawnDelay`초 뒤 다시 생성. 상자 모양은 프리팹 또는 임시 금색 큐브, 아이콘
  스케일/각도/높이 필드 있음.

### 플레이어 애니메이션 (v1.40 신규)

- **v1.44 유니티짱 교체 구조 (지금 기준 - 아래 v1.40~41 서술보다 우선)**:
  - 애니메이션 FBX 15개(`Assets/Player/Character/Animation`, Mixamo)는 전부 **Humanoid**로 전환 → 유니티짱(Humanoid)이 그대로
    사용. 같은 Controller(`ReisaAnimatorController`)를 두 캐릭터가 공유.
  - 레이사는 Humanoid 아바타가 엉덩이 위치를 잘못 잡아 공중에 뜸(Animator 오브젝트 스케일 60.8 + 3ds Max Biped 뼈) →
    Generic 복사본으로 해결: `Animation/Generic/`에 `*_Generic.fbx` 15개(Generic, 아바타 원본 =
    `Player/Reisa_AvatarSource.fbx`) + `ReisaAnimatorOverride_Generic.overrideController`(같은 Controller의 클립만 Generic
    복사본으로 교체). `Reisa_Player_Fixed`의 Animator = Controller `ReisaAnimatorOverride_Generic` + Avatar `Reisa_FixedAvatar`.
  - **지우면 안 됨**: `Player/Reisa_AvatarSource.fbx`, `Animation/Generic/` 폴더 전체, `Animation`의 원본 FBX들,
    `Reisa_Fixed.fbx`, `Reisa_gun_Fixed.fbx`, `Assets/UnityChan`(패키지 원본 - 데모 씬 등도 그대로 둠).
  - 레이어: Locomotion(IK Pass 켬) / UpperBody(마스크 `Generic/UpperAvatarMask_Combined` = 예전 NoRoot의 뼈 목록(Generic용) +
    Humanoid 부위 목록(Head/양팔/양손가락/손 IK, **Body 끔** - 총 조준 때 허리가 꺾이는 "림보 자세" 방지), IK Pass 켬) /
    **UpperBodyTorso**(Sync, Source = UpperBody, 마스크 `Generic/TorsoAvatarMask` = Body만, Weight 0, Override. Sync 레이어는
    상태별 Motion이 비어서 시작하므로 Grenade_Ready(Ready)/Grenade_Throw/Hack_Ready(Ready)/Hack(Hack_RootFixed)/ItemPickup에
    클립을 직접 넣어 둠). 예전 `UpperAvatarMask`/`UpperAvatarMask_NoRoot`/`UpperAvatarMask_Humanoid`는 삭제됨.
  - `TorsoLayerWeightByState`(유니티짱 Animator 오브젝트에만): 수류탄 준비/투척, 해킹 준비/사용, 아이템 획득 상태가 재생되는
    동안만 UpperBodyTorso Weight를 올려(속도 8) 몸통이 따라 움직이게 함. 레이사는 이 컴포넌트가 없어 Weight 0 그대로.
  - `HumanoidAimLookAt`(유니티짱 Animator 오브젝트, `[DefaultExecutionOrder(-50)]`): 조준 중(`AimBlend` 기준)에만 ① LookAt
    (시선/몸 기울임, Locomotion 레이어 IK) ② 손 IK로 손과 총을 내림(`handOffset`, 플레이어 기준 m) + 걸을 때 팔 흔들림 안정화
    (`handStabilize` 0.8/`stabilizeSmoothTime` 0.4, 팔 길이 초과 방지 `maxArmReach` 0.95) ③ 총구 자동 정렬(LateUpdate에서
    `MuzzleDirection`이 수평 정면을 보도록 오른손을 돌리고 그 보정을 손 IK 회전에 누적). 장전/수류탄/해킹/획득/사망 상태에서는
    꺼짐(`blockedStates`). 총은 원래 팔뚝 소매 뼈(J_R_Sode_D01)에 붙어 있어 안정화가 안 먹었음 → 손 뼈로 옮겨 해결.
  - `UpperBodyRecoil` 뼈 찾기: 직접 지정 → Humanoid면 `humanoidBone`(기본 Chest = `Character1_Spine2`, 없으면 Spine) →
    아니면 `boneName`(`Bip001 Spine1`). 실행 순서상 총구 정렬 뒤에 반동이 적용되어 총이 가슴과 함께 위로 튐.
    프리팹 값: `recoilEuler` (-12,0,3)/`returnDuration` 0.5(두 프리팹 따로 조절 가능).
  - `UpperBodyAngleCorrector`는 v1.44에서 삭제(Fire 상태가 없어 쓸 곳이 없었음). 미사용 `Reloading.fbx`/`Walk.fbx`/
    `TestController.controller`도 삭제. `Reisa_Player.prefab`/`Reisa.fbx`/`Reisa_gun.fbx`는 적 프리팹이 쓸 수 있어 보류.
- (이하 v1.40~41 서술 - Controller 구조/파라미터는 그대로 유효) Animator(`ReisaAnimatorController`): Locomotion 레이어(전신,
  Death 포함) + UpperBody 레이어(마스크는 위 v1.44 참고, Weight 1). 파라미터: IsAiming/MoveX/MoveZ/Speed/IsDead(Bool)/
  WeaponMode(Int 0 총/1 수류탄/2 해킹)/Throw·Hack·ItemPickup(Trigger)/ReloadSpeed(Float, 기본 1)/IsReloading(Bool)/
  AimBlend(Float 0~1, `PlayerAimAndAttack`이 MoveTowards 속도 10으로 전송)/IsPickingUp(Bool). (예전 `Attack` 트리거는 삭제.)
- **몸 흔들림/가라앉음의 진짜 원인(v1.41)**: 클립의 최상위 노드(CH0167_Mesh = 모델 루트 "")에 위치/회전 커브가
  있으면 상체 레이어에서 마스크와 무관하게 몸 전체에 적용됨(마스크에서 루트를 빼도 효과 없었음). 해결 = 해당 FBX를
  복사해 루트 커브를 Idle 평균값 상수로 고정한 "RootFixed" 사본 사용(Claude가 바이너리 FBX를 직접 읽고 써서 만듦,
  meta는 새 guid). Idle 기준 Tra(-0.273,-2.315,1.089)/Rot(82.777,-1.913,-54.813), 조준 Idle 기준 Tra(-4.46,-1.82,1.16)/
  Rot(73.95,4.03,-55.14). 사본: `Animation/Hack_RootFixed`, `Hack_Idle_RootFixed`, `Reloading_RootFixed_Aim`,
  `Reloading_RootFixed_NonAim`, `ItemPickup_RootFixed`. 새 상체 클립이 몸을 흔들면 같은 방식으로 사본을 만들 것.
- **해킹(UpperBody, v1.41)**: `WeaponMode` == 2에서 Hack_Idle, `Hack` 트리거로 Hack 사용 동작(위 [플레이어] 해킹툴 참고).
- **장전 블렌드(v1.41)**: Reloading 상태 = 블렌드 트리(AimBlend 0 = NonAim 사본, 1 = Aim 사본) - 조준/비조준 어느 쪽에서
  장전하거나 도중에 우클릭을 바꿔도 몸이 튀지 않음.
- **사망**: 두 레이어 모두 Any State → Death(`IsDead` true, Has Exit Time 끔, Can Transition To Self 끔).
- **수류탄(UpperBody)**: `Grenade_Idle`(Loop) ↔ 총 Idle(`WeaponMode` == 1 / != 1), `Grenade_Ready`(Grenade_Throw
  원본 0~15프레임, 우클릭 동안 마지막 자세 유지), `Grenade_Throw`(16~50프레임, `Throw` 트리거, 끝나면 Grenade_Idle).
  Grenade_Throw/Ready에서 WeaponMode != 1이면 IsAiming에 따라 AimIdle/NonAimIdle로 즉시 탈출. 하체는 평소 걷기 유지.
- **장전(UpperBody `Reloading`, Reloading.fbx 0~99프레임 - v1.41부터 위 RootFixed 두 사본의 블렌드 트리)**: `IsReloading` true로 진입/false로 탈출(WeaponMode == 1이면
  Grenade_Idle로 우선). 상태 Speed Multiplier = `ReloadSpeed` 파라미터, `PlayerShooting`이 장전 시작 시
  `클립 길이 ÷ CurrentReloadDuration`을 넣어 애니메이션 끝 = 탄창 채워짐. 클립 길이는 `PlayerShooting.reloadClip`
  (프리팹에 Reloading 클립 연결)에서 읽음 - 비면 1배속. v1.44: `Reloading.fbx` 삭제에 맞춰 두 프리팹 모두
  `Reloading_RootFixed_Aim`(같은 0~99프레임)으로 다시 연결 완료.
- **사격 = 클립 대신 코드 반동(`UpperBodyRecoil`)**: Fire 클립은 폐기(Animator에서 Fire 상태 삭제). `PlayerShooting.
  OnShotFired(float nextShotDelay)`(사격 틱 = 탄약 1발마다 한 번, 19번 양갈래도 한 번, 5번 분열탄엔 발생 안 함)를
  구독해 LateUpdate에서 상체 뼈(v1.44부터 위 뼈 찾기 규칙)를 짧게 뒤로 젖혔다 복귀. 기본값: `recoilEuler`(-8,0,0)/`kickDuration` 0.04/
  `returnDuration` 0.15/EaseInOut 복귀 곡선/쌓임 끔. 연사 중 다음 발이 있으면 반동을 `다음 발까지 시간 x
  burstFillRatio(0.9)` 안에, 그중 `burstKickRatio(0.3)`를 젖힘에 써서 압축(마지막 발/단발은 원래 시간). 사망 시
  즉시 꺼짐. 형빈이 프리팹에서 최종 값 조정 완료.
- **상체 마스크 근본 문제(v1.40 FBX 분석)**: `UpperAvatarMask`가 루트("")/`bone_root`/`Bip001` + `Spine1` 이상은
  상체(클립)에서, `Pelvis`/`Spine`은 하체(걷기)에서 가져옴. 그래서 몸 전체를 돌리고 허리를 되비트는 클립(예: Fire -
  몸을 돌리고 허리로 총구를 정면에 맞춤)은 "돌린 부분"만 들어오고 "되비튼 부분"이 빠져 총이 옆으로 꺾임. 상체 마스크
  애니메이션이 잘 안 맞던 원인. (v1.44: 대응 도구였던 `UpperBodyAngleCorrector`는 삭제. 레이사 마스크는 지금
  `UpperAvatarMask_Combined`의 뼈 목록을 씀 - 루트 해제 등 근본 정리는 미시행.)

### 적 AI

- 근접: `EnemyAIController`(Idle/Chase, 공격은 `IEnemyAttackBehavior` 구현체에 전권 위임 - `EnemyMeleeAttack`
  돌진형/`EnemyContactOnlyAttack` 접촉형).
- 원거리: `EnemyRangedAIController`(engageRange=사격 가능/chaseRange=접근 지속/retreatRange=후퇴 개시 3단계 +
  retreatDuration 후퇴 커밋 타이머). `EnemyRangedAttack`이 연사(burstCount/burstInterval)와 샷건 스프레드
  (pelletCount/totalSpreadAngle) 지원. 기본값 확정: engageRange=6/chaseRange=5/retreatRange=4/
  retreatDuration=0.5초, entryAttackDelay=0.5초(근접/원거리 공통). 적 탄환은 `EnemyRangedAttack`/`EnemyRadialAttack`이
  구체를 만들고 `EnemyProjectile`을 붙이는 방식(프리팹 없음) - v1.45부터 `EnemyProjectile.Active` 정적 목록에 자동 등록.
- 보스: `BossAIController`(EnemyDefinition/RealEnemySpawner를 거치지 않는 별도 개체 - `chaseRange`/
  `retreatDuration` 등은 `EnemyStats`/`BossAIController` 인스펙터에 직접 입력해야 함, 보스도 원거리와 동일한
  기본값으로 입력 완료).
- `EnemyContactOnlyAttack`(기본 근접 - 회전 연출) 기본값 확정: attackRange=1.4/spinDuration=0.4초/
  spinRotations=1바퀴/postAttackCooldown=0.6초. `RealEnemySpawner.minPlayerDistance`=3(출입구 근처 스폰 방지).
- 피격/충전 발광(Emission) 보정값 전부 기본값 그대로 확정: `EnemyHealth.flashEmissionBoost`=2,
  `EnemyMeleeAttack.telegraphEmissionBoost`=2, `EnemyChainDashAttack`도 동일, `EnemyRadialAttack.
  chargeEmissionBoost`=2, `DissolveAndVanish.whitenPortion`=0.35/`emissionBoost`=1.5,
  `AppearFromWhite.appearPortion`=0.5/`emissionBoost`=1.5.
- 정적 등록소로 아이템이 AI에 개입(전부 "등록 없으면 원래 동작과 100% 동일"): `EnemyDetectRangeOverride`(탐지
  거리 상한, 가장 작은 값 적용), `EnemySightOverride`(시야 규칙 - 벽 뒤로 놓치면 Idle로 복귀), `EnemySpeedModifier`
  (이동속도 배율의 곱, 돌진/대시는 영향 없음), `EnemyDamageTakenModifier`(v1.42 - 적이 받는 피해 배율의 곱,
  `EnemyHealth.TakeDamage`가 피해량 > 0일 때 곱함, 보스 포함. 등록 = 키 + 소유자 + 판정 함수, 소유자가 사라지면 자동
  정리, 씬 재시작 시 초기화).
- 사망/생성 연출: `DissolveAndVanish`(흰색으로 물들며 소멸)/`AppearFromWhite`(흰색에서 나타남). 충전 중 피격 시
  색 유지: `IEnemyColorOverride`(현재 `EnemyMeleeAttack`/`EnemyChainDashAttack`/`EnemyRadialAttack`이 구현).
- **제어 효과 2단계 체계(v1.39 개편)**: `EnemyControlState`(적마다 필요 시 자동 부착)가 강력 제어 칸 1개 + 일반
  제어 칸 1개를 관리. 강력 제어 = 기절(31번)/해킹(해킹툴), 일반 제어 = 매료(38번). 규칙: 같은 등급끼리는 새 것으로
  교체(같은 종류 재적용도 새 지속시간으로 교체, 누적 없음) / 강력 제어가 걸리면 걸려 있던 매료는 즉시 종료 +
  재매료 면역·사격 유예 부여 / 강력 제어 중엔 매료가 아예 안 걸리고 면역도 안 생김 / 매료 중·면역 중 재매료는
  무시. 강력 제어 중에는 근접·원거리·보스 AI 전부 완전 정지(`IsStrongControlledFor`, 보스 예외 없음), 걸리는
  순간 진행 중인 공격 전부 취소. 강력 제어 지속시간에는 6번 패시브 보너스 적용. 제어 색은 이 컴포넌트 한 곳에서만
  칠함(강력 색 > 매료 색 > 원래 색, 발광 없음, 매 프레임 `LateUpdate` 고정, 원래 색은 `EnemyHealth.OriginalColor`/
  `OriginalEmission` 사용, 공격 충전 색(`IEnemyColorOverride`)에는 양보). 새 제어 추가 시 `EnemyControlType`에
  종류 추가 + `GetTier()`에 등급만 지정. (구 `EnemyStunState`는 삭제됨.)
- 38번 매료 고유 동작은 `EnemyCharmState`가 계속 담당: 이동속도 감소("charm" 배율)/원거리 적 강제 접근
  (`SuppressesMovement`)/새 스킬·사격 개시 금지(`SuppressesBehavior`, 종료 후 `postCharmAttackDelay` 유예)/진행
  중 공격 취소(`ICancellableAttack.CancelAttack` + `EnemyRangedAttack.CancelBurst`)/재매료 면역. 보스는 감속만.
- 적 사망 처리 견고성: `EnemyHealth.IsDead` 공개 + AI 컨트롤러 Update() 최상단 가드. `DissolveAndVanish.
  FreezeBehaviours()`의 Missing Script null 가드.
- 전투방 이탈(텔레포트 등) 후 재입장 시 **적만** 초기 스폰 위치/종류/수로 재생성(다른 방/보물상자는 그대로) -
  `RealEnemySpawner`(`IEnemyRespawner`)+`RoomController.HandlePlayerLeft/ResetCombatRoom`.

### 레벨 생성

- `RoomGenerationConfig`(카테고리별 min/max 개수 + 가중치 `roomCountRules`) → `LevelGraphGenerator`(그리드 트리,
  Boss/Club/Store/Consumption/Mystery는 막다른 방에만, 시드 없는 랜덤 - 매번 다른 구조) → `LevelBuilder.BuildRooms`
  (바닥/벽 4면/`RoomController`/방별 NavMesh/동아리방 보물상자). `RoomVisibilityManager`가 현재 방만 활성화. 레벨
  에셋(Level1Config 등)의 `roomCountRules` 값 입력 완료. `LevelBuilder.cellTransitionMargin`=0/
  `roomEntryPushDistance`=0.1, NavMesh Agent Type Radius=0.3 전부 확정. 방은 항상 한 변이 `cellSize`인 칸
  하나(중심 = 칸 좌표 x cellSize).
- **레벨 포탈 모양(v1.45, 코드 수정 없음)**: `LevelBuilder.levelPortalPrefab`(비어 있으면 하늘색 캡슐 플레이스홀더)에
  `LevelPortal` 프리팹 연결 - 루트(바닥 높이) = Capsule Collider(Is Trigger, 반지름 0.6/높이 2.4/중심 Y 1.2) + `LevelPortal`,
  자식 = `vfx_Portal_Level`(`FreeQuickEffectsVol1/Prefabs/vfx_Portal_01` 복제본 - 원본은 그대로, 크기/방향/높이는 형빈이 맞춤).
  보스방 클리어 포탈 + n.5 레벨 시작방 포탈 공통. 루트의 1초 열림 연출은 방에 다시 들어오면 한 번 더 재생(형빈 확정 - 그대로 둠).
  F 안내 기준점은 이제 바닥(예전 캡슐은 +1m).
- 바닥 프리팹(`Floor_Normal_*`) 안에 지면 메시(`Field_Upper`, 태그 없음)/투명 이동 제한 벽(`Field_Player_Wall_
  Collider`, 태그 없음, 약 8x8)/Wall 태그 장애물(자동차·드럼통 등)이 섞여 있고, 방 벽 프리팹(Upper/East/South
  계열)에도 Wall 태그 소품이 섞여 있음 - 태그/콜라이더로 방 벽과 장애물을 구분할 수 없음(v1.39 확인).
- 런타임 방 추가(16번 아이템 등): `LevelBuilder.CanAddExtraRoom()`/`TryAddExtraRoom(category)` - Level Config의
  방 개수/맵 크기 제한과 무관, 적 없는 비보스방(전투 없거나 이미 클리어)에 무제한으로 이어 붙일 수 있음(연쇄
  가능). n.5 선택방 레벨에서는 사용 불가.
- 텔레포트: `LevelBuilder.TryTeleportPlayerToRandomRoom(bool preferUnvisited, params RoomCategory[])` 오버로드(v1.42) -
  `preferUnvisited`가 켜지면 아직 안 들어간 방 중에서 먼저 고르고, 전부 다녀왔을 때만 전체에서 고름.
- 일반맵 보물상자: `NormalRoomTreasureSpawner`(적 수 비례 확률, [기본 아이템 묶음] 또는 [패시브 아이템] 중
  가중치 랜덤, `basicItemBundlePool` 등록 완료). 보스방 보상은 `ResolveBossRewardContent`가 `BossChestOpenEvents`를
  먼저 울린 뒤 실제 뽑기(`ResolveBossRewardContentInner`)를 하고, 끝나면 try/finally로 27번 무장 플래그를 해제.
  `ResolvePassiveItemCandidate(room, applyLegendaryGuarantee)`가 무장 시 Tier3만 후보로 거름. 미니맵: `MinimapDisplay`가 `LevelBuilder.LevelBuildVersion`
  변경을 감지해 아이콘/연결선 갱신, 방문한 방에 남은 아이템 종류별 아이콘(`IMinimapMultiMarker`) + 포탈이
  있으면 포탈 아이콘 하나만 정중앙 표시.
- 3D 아이템 좌대/보물상자 아이콘 표시값 확정: `itemIconScale`=(0.05,0.05,0.05),
  `itemIconFixedEulerAngles`=(55,-45,0)(카메라 각도와 일치). 보스방 클리어 보상 보물상자는 구현 완료 -
  상점(편의점)은 아직 미구현(15번 참고).

### 아이템 시스템

- **패시브**: `ItemDefinition.specialEffects`(`[SerializeReference] List<IPassiveItemEffect>`). 탄환 발사/소멸
  훅(`PlayerShooting.OnProjectileSpawned`/`Projectile.OnDespawning`)으로 특수효과 구현 가능(여러 개가 독립적으로
  같은 훅을 구독해도 서로 몰라도 자연스럽게 공존). 탄환 데미지 보너스도 같은 식으로 공존 가능한 등록소
  (`Projectile.AddDamageBonusProvider` - Damage를 읽을 때마다 등록된 모든 보너스를 합산)가 있어, 11번(거리
  비례)/35번(튕길 때마다 증가) 등 여러 데미지 보너스 아이템을 동시에 들어도 자동으로 함께 적용되고, 4번(폭발)
  같은 다른 효과도 `Projectile.Damage`를 읽기만 하면 그 보너스들이 자동 반영됨. (`OnProjectileSpawned`는 5번
  분열탄 생성에도 발생 - "사격할 때마다"가 필요하면 `OnShotFired`를 쓸 것.)
- **액티브**(스페이스 바): `ItemDefinition.activeCost`+`activeEffect`(`IActiveItemEffect`), `PlayerActiveItem`이
  코스트 소모 관리. 지연 대기가 필요하면 `IDelayedActiveItemEffect`(선택적 확장) 구현 - `CanActivate`(코스트 내기
  전 사용 가능 검사)/`CancelPending`(대기 중 교체 시 취소, 코스트 환불 없음). 대기 중엔 액티브 아이콘 반투명
  (`pendingAlpha`) + 재사용 불가. **실제 소모량(v1.42)** = `PlayerActiveItem.GetEffectiveCost(item)` =
  `ActiveCostModifier.Resolve(activeCost)`(정적 등록소, 감소량 합산, 결과 최소 1, 원래 1 이하면 그대로) -
  `TrySpend`와 좌상단 코스트 표시(`PlayerCostDisplay`)가 같은 값을 씀.
- **음식**(E키, Q 일회용과 별개 슬롯): `IFoodEffect`(즉시형/지연형 + 획득 시 1회성 `OnAcquired`), `PlayerFoodItem`.
  `LastUsedFood`로 마지막 실제 소모 음식을 추적(13번 아이템이 재발동에 사용).
- **일회용**(Q키): `IDisposableItemEffect`, `PlayerDisposableItem`. **장신구**: `ITrinketEffect`, `PlayerTrinket`.
- **교체 확인 체크박스(v1.42)**: `ItemDefinition.confirmBeforeReplace`(category 바로 아래) - 위 [플레이어] 슬롯 교체 확인창 참고.
- 상태를 가진 효과 인스턴스를 다른 곳에서 다시 실행해야 하면(13번처럼) `JsonUtility`로 복제해서 실행 - 원본
  인스턴스는 여러 곳이 공유하므로 직접 재사용하지 않음.
- 구현된 아이템 번호(상세 기획은 각 `ItemNN effect.cs` 파일 주석 참고 - 이 목록은 일부 누락이 있을 수 있음, 정확한
  목록은 `Assets/Script/Item` 폴더 기준): 패시브 2, 4, 7, 8, 11, 18, 19, 21, 24, 30, 35, 36, 38, 39, 41, 43, 48 /
  음식 9, 27, 34 / 액티브 13, 16, 29, 31 / 일회용 22, 25 / 장신구 20, 32, 45.
- **v1.42 아이템 변경**:
  - 9번(음식, `DuplicateRoomChestFoodEffect`): 지금 방의 "살아 있는" 상자(미개봉이거나 아직 가져갈 게 남은 상자) 전부를
    각각 하나씩 복제(상자마다 `Physics.SyncTransforms()`로 겹침 방지). 25번으로 9번을 두 번 쓰는 경우도 정상 동작.
  - 22번(일회용): 안 가본 동아리방 우선 텔레포트(`TryTeleportPlayerToRandomRoom(true, RoomCategory.Club)`).
  - 27번(음식): 대상이 "다음에 여는 동아리방 또는 보스방 보물상자 중 먼저 여는 하나"로 확장(`BossChestOpenEvents` 신규,
    무장 플래그는 `NextClubPedestalLegendaryOverride` 공용, 9번으로 복제된 보스방 상자 포함).
  - 41번(패시브, `HackVulnerabilityEffect`): 획득 시 해킹툴 `hackingToolGrant` 3개 + 해킹에 걸린 적이 받는 모든 피해
    x`damageMultiplier` 1.5(곱연산, 보스 포함, 해킹을 거는 그 첫 피해는 제외 - 형빈 확정).
  - 45번(장신구, `ActiveCostReductionTrinketEffect`): 장착 시 코스트 `costOnFirstEquip` 3 충전(레벨당 1회,
    `TryConsumeOneTimeGrant`, 최대 코스트 초과분은 버림) + 장착 중 모든 액티브 소모량 -`costReduction` 1(최소 1),
    교체/상실 시 즉시 해제.
  - 48번: 장신구 → 패시브로 변경(같은 효과, `WeightScramblePassiveEffect` - `ContainerOpenEvents` 구독, 소유자 확인,
    플레이어가 사라지면 스스로 구독 해제). 장신구 버전 클래스도 파일에 그대로 남아 있음.
- 31번(방 전체 기절)은 v1.39부터 `EnemyControlState.TryApplyStrong(Stun)`을 호출(기절 중 재기절은 새 시간으로 교체).
- 크레딧 획득/보물상자 등장확률에 보너스를 거는 아이템(18/30번 등)은 `CreditGainBonusOverride`/
  `TreasureChanceBonusOverride`(적 AI 쪽 정적 등록소들과 동일한 패턴 - 등록 없으면 원래 동작과 100% 동일,
  여러 출처가 동시에 있어도 곱연산으로 함께 합산)를 통해 각각 `PlayerResources.Add`/
  `NormalRoomTreasureSpawner.TrySpawnTreasure`에 개입. 장신구의 "장착 시 1회 한정 지급"(20/32/39/45번)은
  `GameFlowManager.TryConsumeOneTimeGrant`로 레벨당 한 번만 지급되도록 통일.
- `WallBounceEffect`(35번 - 클래스/프로젝트 문서상 파일명은 `WallBounceEffect.cs`, 실제 유니티 파일명은
  `Item35effect.cs`)/`ExplodeOnImpactEffect`(4번, 튕기는 총알/폭발 총알) `ItemDefinition` 등록 + 설명 문구 작성
  완료. `ExplodeOnImpactEffect` 기본값 확정: `explosionRadius`=2.5/`explosionDamageMultiplier`=1/
  `explosionVfxLifetime`=2초. `WallBounceEffect` 확정값(v1.38): 튕길 때마다 사거리가 그 시점 기준 75%로
  줄어듦(`RangeMultiplierPerBounce`=0.75, 복리 - 무한 튕김 방지)/튕길 때마다 데미지가 기본 데미지의 25%씩
  합연산 증가(`DamageBonusPerBounce`=0.25, `Projectile.BounceCount` 기반). 튕긴 이후 궤적도
  `AimTrajectoryIndicator`에 흰 조준선으로 미리 표시됨(`WallBounceOverride`가 19번의 `SplitShotOverride`와
  동일한 정적 등록소 패턴으로 조준선에 보유 여부 알림).
- 19번(v1.38): 탄창의 탄환 하나당 좌우 ±30도로 2발 동시 발사(탄약은 발당 1개만 소모) + 대응하는 이중
  조준선. `SplitShotOverride`(정적 등록소, 각 아이템 등록소들과 동일 패턴)/`Item19effect.cs`.
- `Projectile`에 `totalDistanceTraveled`(v1.38 추가, 튕겨도 리셋 안 되는 진짜 누적 이동 거리) 필드 있음 -
  `DistanceTraveled` 프로퍼티가 이 값을 반환. 사거리 판정용 `distanceTraveled`(튕길 때마다 리셋)와는 별개.
  11번(거리 비례 데미지)은 항상 `DistanceTraveled`(=총 누적 거리)를 기준으로 계산됨.
- 신규 적 3종(기본 근접/연사 원거리/샷건 원거리) 프리팹/`EnemyDefinition` 등록 완료.
- **상자 위 아이템 아이콘 빛(v1.41)**: `ItemGlowSpawner`(씬에 하나, 게임 씬 + 테스트 씬 각각 배치)에 `vfx_LootDrop_02`
  연결. `TreasureChest`/`ItemPedestal`이 LateUpdate마다 `ItemGlowSpawner.Sync(아이콘, 아이템)` 한 줄만 호출 - 아이템이
  바뀔 때만 생성/색 갱신/제거, 아이콘의 자식이라 주우면 함께 사라짐. 플레이어 아이템만 빛나고 기본 아이템 묶음은 안 빛남.
  등급 색(씬 값 기준): Tier0 흰색/Tier1 파랑/Tier2 노랑/Tier3 보라. 추가 레이어(`extraLayers`)에 `vfx_Projectile_01`의
  입자만(원본 모양 그대로, 등급 색) 겹쳐 씀. 크기는 월드 기준 배율(`glowScale`), 아이콘보다 뒤에 그려지도록
  sortingOrder/위치 보정. 씬에 없으면 빛 없이 기존과 동일.

### 전투 연출/이펙트 (v1.37 신규)

- 피해량 팝업(`DamagePopupDisplay.cs`, 신규): 적이 맞은 위치 위로 데미지 숫자가 떠올랐다 사라짐(화면공간 UI,
  월드 좌표를 따라감). `EnemyHealth.OnAnyEnemyDamaged`(신규 정적 이벤트, 피격 대상+피해량)를 구독.
- 적 체력바(`EnemyHealthBarDisplay.cs`, 신규): 보스를 제외한 일반 적 머리 위에 흰 테두리 틀 + 검은 바탕 + 빨간
  체력 게이지(첫 피격 전까지 숨김, 렌더러 높이 기준으로 위치 자동 계산). 처치 연출은 `dyingStyle` 인스펙터
  값으로 선택 - 기본값 Pop(`popDuration`/`popScale`, 잠깐 커졌다 즉시 사라짐)과 Fade(기존 방식, 적 본체의
  `DissolveAndVanish` 타이밍에 맞춰 서서히 흰색으로 물들다 투명해짐) 중 Pop으로 확정.
- 전투 VFX 일괄 관리(`CombatVfxSpawner.cs`, 신규, 씬에 하나만 배치) - 에셋 스토어 "VFX - Impact and Hit - Light
  Version" 사용, 8가지 상황을 각각 인스펙터 슬롯(프리팹/오프셋/회전/크기배율)으로 조절:
  ① 플레이어 발사 총구 ② 적 발사 총구 ③ 플레이어 근접 피격 ④ 플레이어 원거리 피격 ⑤ 적이 플레이어 총알에 피격
  ⑥ 플레이어 총알 벽 충돌 ⑦ 적 총알 벽 충돌 ⑧ 적 처치 시 폭발(보스 포함, 죽은 위치 중심으로 가로/세로 랜덤
  위치에 `burstCount`발을 `burstDelayRange` 랜덤 텀을 두고 순서대로 터뜨림 - `horizontalScatterRadius`/
  `verticalScatterRange`/`randomizeRotation`으로 조절).
  - ①②(총구 이펙트)는 생성 뒤 총구(firePoint) Transform의 자식으로 붙여서, 총이 움직이거나 회전해도 이펙트가
    사라질 때까지 총구를 계속 따라감(`SetParent(firePoint, true)`).
  - ③④(플레이어 피격)는 `PlayerHealth.IsDead`/`IsInvincible`과 동일한 조건으로 가드해, 무적 시간 중에는 뜨지
    않고 실제로 체력이 깎일 때만 재생됨(테스트 무적 모드는 이 가드와 별개라 이펙트가 뜸).
  - 이펙트 크기 배율은 `Transform.localScale`이 아니라 `ParticleSystem.main.startSizeMultiplier`/
    `startSpeedMultiplier`를 직접 곱하는 방식 - 일부 에셋이 파티클 시스템의 Scaling Mode 설정 때문에
    localScale을 바꿔도 반응하지 않는 문제를 우회(수류탄/해킹 이펙트 크기 조절도 같은 방식).
  - 연결을 위해 기존 스크립트에 추가한 정적 이벤트(전부 기존 로직/구독자는 그대로 두고 추가만 함):
    `EnemyRangedAttack.OnAnyEnemyProjectileSpawned`(Action<Transform,Vector3> - 위치가 아니라 Transform을
    넘겨 위 총구 추적에 씀), `EnemyProjectile.OnAnyEnemyBulletHitPlayer`/`OnAnyEnemyBulletHitWall`,
    `EnemyMeleeContactTrigger.OnAnyMeleeHitPlayer`.

### HUD

- `PlayerHUDManager`가 만든 단일 Canvas(`CanvasRoot`, 1920x1080 기준, 종횡비에 따라 자동 스케일 전환) 위에 각
  스크립트가 자기 패널을 직접 생성, 매 프레임 라이브 트윅(인스펙터 값 즉시 반영). 대부분의 HUD 컴포넌트는 씬의
  `PlayerHUD` 프리팹 안에 있음(Hierarchy 검색 `t:컴포넌트이름`으로 찾기).
- **글자 = TextMeshPro(v1.45)**: 옛 `Text`가 화면 배율에 따라 흐릿하고 테두리가 깨져서(에디터 Game 창 축소 문제가 아니라
  빌드에서도 확인) HUD 글자 14개 파일을 전부 TMP로 교체 - `Playerresourcedisplay`/`Ammoboxdisplay`/`Playerstatdisplay`(+툴팁)/
  `PlayerCostDisplay`/`Minimapdisplay`(현재 위치 버튼)/`BossHealthBarDisplay`/`ItemInfoPanelView`(설명칸+툴팁 공용)/
  `Treasurechestinfodisplay`/`InteractPromptDisplay`/`DamagePopupDisplay`/`SlotReplaceConfirmUI`/`GameOverUI`/
  `Leveltransitionconfirmui`/`TitleScreenUI`. 글꼴 에셋 = `Assets/Resources/Fonts/경기천년제목_Bold SDF`(TTF 우클릭 > Create >
  TextMeshPro > Font Asset, Dynamic, Atlas 2048, Multi Atlas 켬, Clear Dynamic Data On Build 켬 - **이름을 바꾸면
  `HUDFont.GetTMP()`가 못 찾음**), `Assets/TextMesh Pro`(TMP Essential Resources) 가져옴. 공용 도우미 `HUDText`(위 작업 방식
  참고). 코스트 숫자는 이미 굵은 글꼴에 코드 Bold를 한 번 더 걸던 걸 뺌. 스탯 숫자의 세로 Truncate는 TMP에서 글자가 사라질
  수 있어 Overflow로.
- **글자 외곽선(v1.45)**: TMP 외곽선은 테두리선 중심으로 안팎 반씩 그려져 가는 획을 덮었음(F 안내가 회색 덩어리) →
  `HUDText.SetOutline`이 외곽선과 같은 값의 Face Dilate를 함께 걸어 글자 본체는 원래 굵기, 테두리는 바깥에만. 픽셀 →
  TMP 값 = 픽셀 / ((Padding+1) x 글자크기 / Sampling Point Size), 최대 0.5(글꼴 에셋 기본값 기준 글자 크기 28이면 약 1.5픽셀이
  최대 - 더 두껍게 하려면 글꼴 에셋 Padding을 키워 다시 생성). 조절 위치: F 안내 = `InteractPromptDisplay`의
  `interactPromptOutlineThickness`(기본 1.5)/`interactPromptOutlineColor`(검정 80%), 보스 이름 = `BossHealthBarDisplay.
  nameOutlineThickness` + 보스 프리팹 `BossPortrait.nameOutlineColor`, 데미지 숫자 = `DamagePopupDisplay.outlineThickness`/
  `outlineColor`. 두께 단위는 전부 픽셀(0이면 없음).
- 구성 요소: 초상화+체력 하트, 좌하단 스탯 패널(여러 열, 0번이 왼쪽 위), 좌상단 액티브 아이템+코스트
  (`PlayerCostDisplay`, 스프라이트 5종 적용 완료, 추가 보조 아이콘 자리 있음 - 지금은 13번 전용), 우측 중앙
  장신구/일회용/음식 슬롯(`PlayerSlotItemDisplay`, 대기 중 반투명), 패시브 아이템 인벤토리 그리드
  (`PlayerInventoryDisplay`: panelWidth=320/panelHeight=600/columns=5/iconSize=64/spacing=8, 확정), 소모성
  자원 카운터, 탄약 박스(연사 숫자+장전 시계방향 소멸), 미니맵, 화면 위 보스 체력바+초상화+이름, 근처 아이템/
  보물상자 설명칸(등급 스프라이트, 9-slice 배경 칸 있음).
- **HUD 패널 접기(v1.42)**: `CollapsibleHUDPanel`(공용 클래스) - 스탯 패널은 아래로, 슬롯/인벤토리 패널은 오른쪽으로
  부드럽게 접힘. 접기 버튼(반투명 검은 둥근 사다리꼴 + 흰 화살표, 런타임 생성 텍스처)은 접혀도 화면에 남음.
  `Tick(settings, panelRect, panelSize, edgeMargin, basePosition)`이 패널 위치를 직접 정한 뒤 클릭 판정을 함(위치를
  먼저 정해야 접힌 상태에서 버튼이 눌림 - v1.42 버그 원인). Tab(`PlayerHUDManager.collapseAllKey`) = 하나라도 펼쳐져
  있으면 전부 접고, 전부 접혀 있으면 전부 펼침. 클릭은 프레임당 한 번만 소비.
- **미니맵 확대/드래그(v1.42, `Minimapdisplay`)**: 모서리 둥근 반투명 검은 구역(Mask) 안에서 휠 확대/축소(커서 위치
  기준, 0.4~2배), 좌클릭 드래그 이동, 지도가 구역 밖으로 나가지 않게 중심 제한, 방을 옮기면 현재 방을 따라가며
  `resetZoomOnRoomChange`로 확대도 초기값으로 복귀. 구역 오른쪽 아래 "현재 위치" 버튼(흰 둥근 사각형 + 검은 글씨) =
  현재 방으로 부드럽게 이동 + `initialZoom`으로 복귀. 정적 `IsPointerOverZone()`.
- **HUD 클릭 차단(v1.42)**: `HUDClickBlocker.BlocksLeftClick` = 접기 버튼 위 || 미니맵 구역 위 ||
  `SlotReplaceConfirmUI.BlocksInteract`. 총 사격(`PlayerAimAndAttack`)/수류탄(`PlayerGrenadeThrower`)/해킹
  (`PlayerHackTool`)이 좌클릭 처리 전에 확인. EventSystem 없이 `RectangleContainsScreenPoint(rect, mouse, null)`로 직접
  판정(상태 저장 없이 그때그때 계산 - 실행 순서 무관). 클릭형 HUD가 새로 생기면 여기에 조건만 추가.
- 좌하단 스탯 패널(`Playerstatdisplay.cs`): 열당 `rowsPerColumn`(현재 인스펙터 7)개씩 위→아래, 넘치면 오른쪽 열.
  v1.39부터 `StatEntry.isSkillStat` 체크박스가 켜진 스탯(수류탄 3종 + 해킹 3종 - 형빈이 인스펙터에서 체크 완료)은
  일반 스탯 마지막 열이 꽉 찼든 아니든 항상 그 오른쪽 새 열에 한 열로 따로 배치(묶음 간격 = 일반 열 간격 20),
  모든 열의 윗줄은 가장 긴 열 기준으로 맞춤. 스탯 아이콘 호버 툴팁(`tooltipName`/`tooltipDescription`, 둥근
  모서리는 런타임 생성 텍스처) 있음.
  **수치 변화 연출(v1.45)**: 화면에 보이는 숫자 글자가 바뀌는 순간(원인 무관 - 아이템/20번 흡수/음식·액티브 효과 등, 형빈
  확정) 그 줄 숫자만 `flashPopDuration` 0.08초 동안 `flashPeakScale` 1.4배 + 색 → `flashReturnDuration` 0.4초 동안 원래
  크기/흰색으로(실제 시간 기준, 연출 중 또 바뀌면 처음부터). 유리 = `increaseFlashColor` 노랑, 불리 = `decreaseFlashColor`
  빨강. 작을수록 좋은 스탯은 `StatEntry.lowerIsBetter` 체크박스(장전 시간/연사 간격/수류탄 자해 데미지는 처음 한 번 자동
  체크 - 숨김 플래그 `lowerIsBetterInitialized`로 이후엔 직접 바꾼 값 유지). 시작/줄 재구성 직후 `flashStartupGrace` 0.5초는
  기준값만 잡음. 숫자 칸 기준점은 왼쪽 가운데(커질 때 왼쪽 끝 기준). `flashOnChange`로 끄기 가능.
- 상자/좌대/포탈 상호작용 프롬프트(F키, `InteractPromptDisplay`)는 화면공간(Screen Space - Overlay)으로
  렌더링해 벽에 가려 잘리는 문제 해결(Canvas sortingOrder=10). 장전 표시(`PlayerReloadIndicator`)는
  플레이어 머리 오른쪽에 탄약 박스와 동일한 방식(반전 R 아이콘이 시계방향으로 사라짐)으로 뜨되, 장전이 끝나면
  아이콘 전체가 완전히 사라짐(Canvas sortingOrder=5, F프롬프트보다 아래 레이어).
- 아이템 호버 툴팁(`ItemTooltipDisplay`, Canvas sortingOrder=20): 인벤토리/슬롯 아이콘에 커서를 올리면
  원래 아이템 설명칸과 동일한 모양으로 재표시. 패널을 그리는 로직은 `ItemInfoPanelView`(공용 클래스)로 분리,
  스타일 값은 `PlayerItemInfoDisplay` 인스펙터가 보유. 능력치/특수 효과 텍스트 색은 등급(Tier0~3)별. 새 스탯의
  한글 이름은 `ItemInfoPanelView.GetStatDisplayName`에 추가해야 함(수류탄/해킹 스탯 추가 완료). v1.42: 능력치
  텍스트가 없는 아이템은 특수 효과 앞의 섹션 간격(`sectionSpacing`)을 빼서 빈 틈이 생기지 않음.
- 게임 오버 창(`GameOverUI`, sortingOrder 100) - 위 [플레이어] 사망 항목 참고. 로딩 화면은 sortingOrder 1000.
- **초상화 테두리(v1.44, `PlayerHUDManager`)**: `portraitBorderColor`(기본 흰색)/`portraitBorderThickness`(기본 4, 0이면 숨김) -
  초상화 바깥쪽으로 상하좌우 막대 4개, 실시간 반영. 초상화 스프라이트 "레이사 프사 2"는 Sprite Editor(Multiple 모드)에서
  상하좌우 90씩 잘라 사용.
- **스탯 화면 표기 규칙(v1.41, `StatDisplayFormat` 한 곳)**: 스탯 패널과 아이템 설명칸/툴팁이 함께 사용. 총알 크기 =
  x100(0.05 → 5), 게마트리아/샬레 확률 = x100 + "%", 나머지는 원래 값. 아이템 설명칸의 곱하기 효과는 증감 퍼센트
  (x1.2 → "+20%", x0.8 → "-20%"). 다른 스탯 표기를 바꾸려면 `GetMultiplier()`/`GetSuffix()`에 한 줄 추가.

## 확인 필요 항목 (다음 세션 우선순위 - 계속 갱신, 해결되면 지울 것)

**번호 표기 안내(v1.30)**: 아래는 마크다운 순서 목록(`1.` `2.` `3.`)이 아니라, 각 항목 앞에 원래 번호를
`[ ]`로 붙인 불릿입니다. 순서 목록 문법을 썼더니 일부 마크다운 뷰어가 실제 타이핑된 번호를 무시하고
화면에 1씩 증가하는 번호를 새로 매겨서, 형빈과 Claude가 서로 다른 번호를 보고 대화하는 사고가 있었습니다.

- [15] 상점(편의점) 등장 로직 미구현(보스방 보물상자는 구현 완료 - 위 [레벨 생성] 참고).
- [19] 애니메이션 레이어 시스템 절차 생성 레벨 포함 최종 검증(v1.44 유니티짱/레이사 두 프리팹 모두).
- [20] 캐릭터 정식 모델링 교체 예정(Scale/회전 보정 재점검 필요). 교체 시 FBX Materials 탭에서 `Player_Mat` 재질로 다시
  Remap해야 툰 셰이더/외곽선이 유지됨(v1.43). v1.44: 지금 기본 모델은 SD 유니티짱(임시) - 새 모델이 Humanoid면
  `UnityChan_Player` 구조(MeshRotationOffset, 손 뼈의 총 + MuzzleDirection, `HumanoidAimLookAt`/`TorsoLayerWeightByState`)를
  그대로 따라 만들면 됨. 유니티짱은 Unity Toon Shader 재질이라 `Toon_Character` 외곽선/명암 규칙과 다를 수 있음.
- [23] Wall 태그 콜라이더는 새 프리팹도 Box/Convex 체크리스트 계속 적용 필요.
- [31] `NormalRoomTreasureSpawner.walkthroughLayerName` 실사용 여부/레이어 이름 확인(필수는 아님).
- [34] `PlayerInventoryDisplay` 스크롤 동작을 위한 씬 `EventSystem` 존재 확인.
- [37] `ExplodeOnImpactEffect.explosionVfxPrefab` 미할당 - 폭발 이펙트 프리팹 준비되는 대로 인스펙터에 연결
  필요(비어 있어도 피해 자체는 정상 적용되고 폭발 시각효과만 없음).
- [43] 원거리 적 프리팹에 `EnemyRangedAIController`가 미리 붙어있는지(새 프리팹 제작 시 체크리스트).
- [48] "행운(Luck)" 스탯 - `PlayerStats.luck` 필드만 존재, 아이템 확률 반영 미구현.
- [49] 미니맵 마커 아이콘 8종 + 포탈 아이콘 스프라이트 연결.
- [54] 등급 스프라이트 오른쪽 "태그 UI" 자리는 범위 밖으로 남아있음.
- [59] 16번 `roomOptions`에 소비방/신비방을 넣지 말 것(콘텐츠 미구현 - 빈 방으로 생성됨). 거래방/편의점도 콘텐츠
  상태 확인 필요.
- [60] 16번은 제한 없이 연쇄 확장 가능(형빈 확정 사항) - 남용 방지 제한이 필요한지 실제 플레이로 판단.
- [61] "추가 보조 아이콘"(`PlayerCostDisplay`)은 지금 13번만 사용 - 다른 액티브 아이템이 쓰려면 `SyncAuxIcon()`
  분기만 확장.
- [62] 7번 - 잠든 적은 피격으로 깨어나지 않음(형빈 확정, 그대로 둠). 나중에 바뀌면 근접/원거리 AI 깨어남 판단
  지점에 함께 반영 필요.
- [64] 35번(벽 튕김)이 복리로 사거리를 줄이도록 v1.38에서 변경(10 → 7.5 → 5.625 ...) - 벽 배치에 따라 여전히
  체감상 꽤 오래 튕길 수 있음, 실제 플레이로 밸런스 이상 없는지 계속 지켜볼 것.
- [65] 수류탄 모델은 임시 `Grenade_Temp`(v1.45) - 정식 모델이 생기면 그 프리팹으로 교체. 수류탄 폭발/해킹/해킹 총알 제거
  (`bulletClearVfxPrefab`) 이펙트 프리팹은 준비되는 대로 연결(원본 이펙트는 복제해서 사용).
- [66] 해킹툴 본래 용도(특수 보물상자/막힌 문 열기) 미구현 - 해당 콘텐츠가 생길 때 `PlayerResources`의
  HackingTool 소모로 연결.
- [67] 무기 모드 표시 HUD 없음(지금 1/2/3 중 어느 모드인지 화면에 안 보임) - 필요하면 `PlayerWeaponMode.
  OnModeChanged`를 구독해서 추가.
- [68] 기존 `Assets/Script/Level/RoomBoundaryWall.cs`(v1.39 도중 만들었다가 폐기한 파일)가 유니티 Project 창에
  남아 있다면 삭제(어디서도 쓰지 않음).
- [70] 상체 동작이 몸 전체를 흔들거나 가라앉게 하면 마스크가 아니라 클립의 루트 커브 문제 - 위 [플레이어
  애니메이션]의 RootFixed 사본 방식으로 해결. 새 RootFixed 사본을 만들면 Humanoid 원본 + `Animation/Generic`의 Generic
  복사본(레이사용)을 둘 다 만들고 Override Controller에도 연결할 것(v1.44).
- [71] `Reisa_Player_Test` 프리팹은 더 이상 안 씀 - 아직 Project 창에 남아 있다면 삭제. `Reisa_Player_Fixed` 프리팹에
  총 모델(`Reisa_gun_Fixed`)이 비활성 상태로 저장돼 있었음(실행 시 `PlayerWeaponMode`가 켜서 문제는 없음 - 의도한
  게 아니면 다시 켜둘 것).
- [72] 수류탄 모드에서 사망하면 사망 처리(총 모드 복귀) 때문에 총 모델이 다시 보임 - 거슬리면 보완.
- [73] `PlayerItemInfoDisplay.cs`의 등급 이름 색 코드 기본값(Tier2 보라/Tier3 금색)이 실제 씬 값과 다름(씬 값은 정상) -
  새 씬에 붙일 때 틀린 색으로 시작함. 고칠지 형빈 결정 대기. (교체 확인창 이름 색도 이 값을 씀.)
- [74] 이전 "장전 속도"/"검은 양복" 문구가 씬에 저장된 텍스트(스탯 패널 `tooltipName`/`tooltipDescription`, 아이템
  에셋 설명문)에 남아 있을 수 있음 - 스크립트로는 못 바꾸므로 인스펙터에서 확인. (v1.42 검색 결과 Config 에셋/
  씬 3개/플레이어 프리팹에는 BlackSuit/검은 양복 문자열이 더 이상 없음.)
- [75] 아이템 획득 연출/빛 이펙트는 보물상자에서 주울 때만 동작 - 다른 경로(상점 등)가 생기면 그 지점에서도
  `ItemPickupEvents.Raise` 호출 필요.
- [76] 교체 확인창이 필요한 슬롯 아이템(20번 등)은 각 아이템 에셋에서 `Confirm Before Replace` 체크 여부 확인.
- [77] 외곽선(뒷면 부풀리기 방식)은 상자처럼 하드 엣지인 모델에서 모서리 선이 살짝 끊길 수 있음 - 거슬리면 (a) 외곽선
  전용 부드러운 노멀을 미리 계산해 넣는 도구 또는 (b) 화면 전체 경계 검출 외곽선(후처리) 중 선택해 보완(v1.43 논의, 미시행).
- [78] 배경은 임시 그라데이션/마름모 이미지 - 최종 배경 이미지를 만들면 `BG_ScreenSky` 재질의 배경 이미지만 교체하고,
  하늘 톤에 맞춰 툰 셰이더 그림자 색/White Balance를 한 번 다시 맞출 것.
- [82] (v1.44) 유니티짱 라이선스(UCL 3.0) - 포트폴리오 영상/이미지에 라이선스 로고 또는 크레딧 표기.
- [79] 새 모델/재질을 추가할 때는 `MobArchive/셰이더 일괄 교체` 도구로 툰 셰이더 적용(FBX 내장 재질은 먼저 Extract).
  새 씬을 만들면 조명/환경광/Skybox Material/Global Volume(SampleSceneProfile)을 위 [비주얼/셰이더] 값으로 맞출 것.
- [83] (v1.45) 새 씬/새 게임 씬을 만들면 `LoadingScreen` 프리팹을 배치하고 File > Build Profiles 씬 목록에 등록(Title 0번 유지).
  시작 화면 설정 버튼은 기능 없음 - 설정 화면이 생기면 `TitleScreenUI.OnSettingsClicked()`에 연결.
- [84] (v1.45) 스탯 패널 `Lower Is Better` 자동 체크와 기타 인스펙터 변경은 `PlayerHUD` 프리팹 Overrides → Apply All 해야
  프리팹에 저장됨 - 적용 여부 확인.
- [85] (v1.45) TMP 글자 테두리는 글꼴 에셋 Padding 범위까지만 두꺼워짐 - 더 두꺼운 테두리가 필요하면 `경기천년제목_Bold SDF`의
  Padding을 키워 다시 생성. TMP로 바꾸면서 글자 위치/간격이 몇 픽셀 달라졌을 수 있으니(특히 아이템 설명칸/스탯 툴팁)
  거슬리는 곳은 인스펙터 값으로 미세 조정.
- [86] (v1.45) 레벨 포탈 교체 후 F 안내 기준점이 바닥이 되어 예전보다 낮게 뜸 - 거슬리면 보완(아직 형빈 이의 없음).

## 변경 로그 (한 줄만 - 자세한 내용은 그 시점 아카이브 참고)

- v1.4~v1.18: 마름모 시야 전환, 적 AI/스폰 구축, HUD 1~6단계, 패시브 아이템 기초, 조준 궤적선, 레벨 생성 규칙
  개편, 무적 점멸, 소모성 자원/인벤토리 HUD 등. (그 시점 상세 서술은 유실됨 - 예전 버전 문서가 이미 삭제됨)
- v1.19~v1.23: 패시브 특수효과 아키텍처, 신규 적 3종, 원거리/보스 이동 판단 정교화, 사망/생성 연출, 음식
  카테고리 신설(27/34번), 등급 명칭 Tier0~3 개편, 영문 고유명사(Schale/Pyroxenes)/"좌대"→"보물상자" 용어 정리.
- v1.24: 코스트/등급 HUD, 장신구·일회용·음식 슬롯 HUD, 보스 체력바, 전투방 초기화, 미니맵 잔여 아이템 확장,
  음식 사용 키 E 정정.
- v1.25: HUD를 형빈이 만든 UI 이미지로 교체(폰트/탄약 박스/배경 슬롯/보스 이름/스탯 열/코스트/9-slice/SPACE
  안내창).
- v1.26 (2026-09): 패시브 아이템 2(발자국)/7(탐지거리 제한)/8(벽 뒤 은신)/11(비행거리 피해) + 액티브 아이템
  13(직전 음식 재발동)/16(런타임 방 추가) 구현. 액티브 슬롯에 대기(pending) 구조(`IDelayedActiveItemEffect`)
  신설, HUD에 "추가 보조 아이콘" 추가.
- v1.27: 문서 구조를 "경량 핵심 상태 문서 + 아카이브"로 전환(이 문서 신설, v1.26을 첫 아카이브로 지정). 이번
  세션에 코드 변경은 없음.
- v1.28: 형빈이 확인 필요 항목을 정리 - 해결된 25개 항목 삭제, 인스펙터에서 확인 가능한 값들(9/11/24/33/36/
  38/41번) 위치 안내 후 삭제, 13번(아이템 아이콘 스케일/각도) 확정값을 확정 상태로 이동, 15번은 상점만 남기고
  보스방 보물상자 부분은 삭제(구현 확인됨), 6/14번은 상시 진행 항목이라 삭제, 37번은 값이 아니라 프리팹 할당
  여부 확인으로 재정리. 이번 세션에 코드 변경은 없음.
- v1.29: 32번(`PlayerResourceDisplay` 아이콘 4종)은 형빈이 실제로 해결해 27번 문구에서 제거. **[v1.30에서 정정]**
  이 시점에 20번(캐릭터 모델 교체)/37번(`explosionVfxPrefab`)도 해결된 것으로 착각해 함께 삭제했었으나, 둘 다
  아직 해결되지 않은 상태였음이 v1.30에서 밝혀져 다시 복원함.
- v1.30: 형빈과 Claude가 서로 다른 항목 번호를 보고 있던 사고의 원인을 확인 - 이 문서가 마크다운 순서 목록
  (`15.` `19.` `27.`...) 문법을 쓰고 있었는데, 형빈이 보는 마크다운 뷰어가 실제 번호를 무시하고 1씩 증가하는
  번호로 다시 매겨서 보여주고 있었음(예: 15/19/27/30...으로 적었는데 화면엔 15/16/17/18...로 보임). 앞으로
  이런 사고가 없도록 확인 필요 항목 표기를 `[N]` + 불릿 방식으로 전환(순서 목록 문법 사용 안 함). 이 과정에서
  잘못 삭제됐던 20번/37번을 복원. 이번 세션에 코드 변경은 없음.
- v1.31: v1.30에서 원인을 밝힌 번호 혼선이 그 이전 삭제 라운드(v1.28)에도 영향을 줬을 위험이 있다고 형빈이
  지적 - v1.28에서 "번호만 불러준" 항목 중 순서 목록 문법 때문에 다른 항목을 가리켰을 수 있는
  9/11/23/24/25/26/30/33/35/36/38/39/41/42/43/44/47/51번을 전부 복원해 재확인 대상으로 되돌림(30번은 원래도
  결정 보류 상태로 남아있던 항목). 반대로 형빈이 번호가 아니라 내용을 직접 설명해 확인된 것들(1~21번 중
  언급분, 6/13/14/15/48/52번, 그리고 이번/직전 세션에 `[N]` 표기로 명확히 확인된 27/31/40/45/46/58/63번)은
  안전하다고 보고 그대로 유지. 20/37번은 v1.30에서 이미 복원됨. 이번 세션에 코드 변경은 없음.
- v1.32: 형빈이 `[N]` 표기 기준으로 복원됐던 항목들을 다시 확인 - 24/26/35/38/42/47/51번은 완료 확인되어
  삭제. 9(NavMesh Agent 반경 제외)/11/25/33/36/39/41/44번은 "프로젝트 내 코드 기본값을 보고 기록"해달라는
  요청에 따라 각 스크립트의 현재 기본값을 찾아 확정 상태로 옮김(엔진 인스펙터에서 형빈이 직접 다른 값으로
  바꿔뒀다면 다시 알려줘야 함) - 9번은 `cellTransitionMargin`/`roomEntryPushDistance`만 기록하고 NavMesh
  Agent 반경은 스크립트에 없는 값이라 형빈 확인 대기로 남김. 52번은 실제로 `PlayerResources.cs`의 임시
  디버그 로그 2곳(Awake 로그, 코스트 로그)을 코드에서 제거해 파일로 전달(코드 변경 있음, 이번 세션 유일).
- v1.33: 형빈이 NavMesh Agent Type의 Radius=0.3을 확인해줘서 9번 완전히 해결 - 확정 상태로 이동.
- v1.34 (2026-09-23): HUD에 세 가지 개선 구현 및 형빈 확인 완료. (1) 상자/좌대/포탈 F키 상호작용 프롬프트를
  3D 월드공간에서 화면공간(Screen Space - Overlay)으로 전환해 벽에 가려 잘리던 문제 해결(`InteractPromptDisplay.cs`).
  (2) 장전 표시 신설(`PlayerReloadIndicator.cs`, 신규) - 플레이어 머리 오른쪽에 탄약 박스와 동일한 시계방향
  소멸 아이콘이 뜨되, 탄약 박스와 달리 장전 완료 시 아이콘 전체가 사라짐, F프롬프트보다 아래 레이어. (3) 아이템
  호버 툴팁 신설(`ItemTooltipDisplay.cs` 신규, `ItemInfoPanelView.cs` 신규 - `PlayerItemInfoDisplay`의 패널
  로직을 공용 클래스로 분리) - 인벤토리/슬롯 아이콘에 커서를 올리면 원래 아이템 설명칸과 동일한 모양으로
  재표시, 액티브 아이템은 원래 설명칸 자리에 뜨되 근처 아이템/보물상자 설명칸과 겹치면 오른쪽으로 밀림
  (`TreasureChestInfoDisplay.cs`에도 겹침 회피용 접근자 추가, `PlayerInventoryDisplay.cs`/
  `PlayerSlotItemDisplay.cs`/`PlayerCostDisplay.cs`에 호버 아이템 조회 메서드 추가). 특수효과 설명 및 능력치
  텍스트 색도 이름과 동일하게 등급별로 표시되도록 변경(전용 색 필드 `statTextColor`/`specialEffectTextColor`
  제거). 이번 세션에 변경/신규된 스크립트 9개 전부 Claude Project 문서에도 동기화 완료.
- v1.35 (2026-09-24): 신규 아이템 7종 구현 - 패시브 18(일반맵 보물상자 등장확률 +50%)/24(최대 코스트 +2,
  즉시 풀채움)/30(획득 시 크레딧 30 지급 + 획득마다 크레딧 +1), 액티브 29(코스트 3, 현재 방 안 모든 적에게
  공격력 100% 피해, 보스방이면 보스도 포함), 일회용 25(다음 음식 사용을 지연 소모 상태로 만들어 소모 없이
  재사용 가능하게 함, 25 자신은 소모), 장신구 20(장착 시 크레딧 10 지급 + 이후 획득하는 모든 크레딧을
  흡수해 흡수량 1당 공격력 0.1, 교체/이탈 시 흡수분 전부 상실)/32(장착 시 체력 2 회복 + 처치마다 25% 확률로
  체력 1 회복). 신규 정적 등록소 `CreditGainBonusOverride`/`TreasureChanceBonusOverride` 추가(18/30번용).
  `PlayerFoodItem`/`PlayerInventory`/`PlayerResources`/`NormalRoomTreasureSpawner`/`PassiveEffectContext`
  연동 수정. 이번 세션에 변경/신규된 스크립트 14개 전부 Claude Project 문서에도 동기화 완료.
- v1.36 (2026-09-24): 패시브 아이템 38번(총알 매료) 신규 구현 - 매료된 적은 1.5초간 이동속도 -30%, 새
  스킬/사격 개시 금지 + 원거리 적은 후퇴 대신 강제 접근, 진행 중이던 공격은 즉시 강제 중단, 종료 후 3초
  재매료 면역(면역 중 감속 없음), 보스는 감속만 적용. 신규 `EnemyCharmState.cs`(적별 상태 관리)/
  `Item38effect.cs`(조절 가능한 인스펙터 값 전부 보유)/`ICancellableAttack.cs`(진행 중 공격 취소용 신규
  인터페이스, `EnemyMeleeAttack`/`EnemyChainDashAttack`이 구현). 구현 과정에서 발견/수정한 부수 버그 세
  가지: (1) `EnemyHealth.Die()` 이후에도 AIController가 죽은 적을 계속 조작하려다 NavMeshAgent 오류가 나던
  문제(`IsDead` 가드 추가), (2) `DissolveAndVanish.FreezeBehaviours()`가 Missing Script로 인한 null
  컴포넌트 때문에 죽음 연출 전체가 실패하던 문제(모든 적 사망에 영향, null 가드 추가), (3) 매료 색상을
  `EnemyCharmState`가 직접 캐시하면서 최초 피격 플래시 색(흰색)을 원래 색으로 잘못 저장해 매료/매료 종료
  후 모두 흰색으로 보이던 문제(원래 색/발광을 `EnemyHealth`가 스폰 시점에 캐시해둔 값에서 가져오도록 수정).
  이번 세션에 변경/신규된 스크립트 11개 전부 Claude Project 문서에도 동기화 완료.
- v1.37 (2026-09-28): 전투 피드백/VFX 대거 구현 및 형빈 확인 완료. 피해량 팝업(`DamagePopupDisplay.cs`,
  신규), 적 체력바(`EnemyHealthBarDisplay.cs`, 신규 - 처치 연출 Pop/Fade 중 Pop으로 확정), 스탯 아이콘 호버
  툴팁(`Playerstatdisplay.cs`에 기능 추가, 둥근 모서리 포함), 에셋 스토어 VFX 8종 연동(`CombatVfxSpawner.cs`,
  신규 - 총구/피격/벽 충돌/처치 폭발, 총구 이펙트가 총을 계속 따라가도록 처리, 무적 시간 가드, 일부 이펙트의
  `Scale Multiplier` 미적용 문제 수정)을 구현. 도중 발생한 문제 두 가지: (1) 일부 파티클 이펙트가
  `Scale Multiplier`를 조정해도 작아지지 않던 문제 - `ParticleSystem.main`의 크기/속도 배율을 직접 곱하는
  방식으로 해결. (2) 총구 이펙트가 총을 따라가지 않던 문제 - 총구 이벤트가 위치(Vector3) 대신 Transform을
  넘기도록 바꿔 이펙트를 총구의 자식으로 붙여 해결. 저장 과정에서 `EnemyRangedAttack.cs`가 원인 불명의 파일
  잠금으로 `-1` 복사본에 저장되어 클래스 중복 컴파일 오류가 났던 사고가 있었음(유니티 Project 창에서 복사본
  삭제 + 재저장으로 해결, 위 작업 방식에 재발 방지 문구 추가). 이번 세션에 변경/신규된 스크립트 8개
  (`DamagePopupDisplay.cs`, `EnemyHealth.cs`, `EnemyHealthBarDisplay.cs`, `Playerstatdisplay.cs`,
  `CombatVfxSpawner.cs`, `EnemyRangedAttack.cs`, `EnemyProjectile.cs`, `EnemyMeleeContactTrigger.cs`) 전부
  Claude Project 문서에도 동기화 완료.
- v1.38 (2026-09-30): 패시브 19번(신규) + 35번(벽 튕김) 대개편, 형빈 확인 완료. 19번: 탄창의 탄환 하나당
  좌우 ±30도로 2발 동시 발사(탄약은 발당 1개만 소모) + 대응하는 이중 조준선(`SplitShotOverride.cs`/
  `Item19effect.cs` 신규). 이를 위해 `AimTrajectoryIndicator.cs`를 방향×구간 배열 구조로 재설계(`LineRenderer`
  단수 → `LineRenderers` 복수 배열, `PlayerHitFlash.cs`의 무적 점멸 제외 로직도 배열 순회로 수정). 35번은
  형빈 요청으로 세 차례 개편: (1) 튕길 때 사거리를 그 지점부터 다시 채우도록 변경했다가(무한 튕김 위험 발견),
  (2) 최종적으로 튕길 때마다 사거리가 그 시점 기준 75%씩 복리로 줄어들도록 변경(`RangeMultiplierPerBounce`=
  0.75, 무한 튕김 방지) + 튕길 때마다 데미지가 기본 데미지의 25%씩 합연산으로 증가(`DamageBonusPerBounce`=
  0.25, `Projectile.BounceCount`+기존 `AddDamageBonusProvider` 재사용 - 4번 폭발/11번 거리 데미지에도 자동
  반영됨, 형빈이 의도된 동작임을 확인). (3) 튕긴 이후 궤적도 흰 조준선으로 미리 보이도록
  `AimTrajectoryIndicator.cs`에 다중 구간(`maxPreviewBounces`) 지원 추가. 과정에서 발견/수정한 버그 네 가지:
  ① 벽 법선에 Y 성분이 섞여 반사 방향이 위로 꺾이던 문제(법선 Y를 지우고 정규화 후 반사 - 4번/35번/조준선
  전부 적용). ② 실제 총알의 벽 감지용 SphereCast가 한 물리 스텝만큼의 매우 짧은 거리에서 시작해 벽에 거의
  닿은 채로 캐스트가 시작되는 바람에 법선을 잘못 구해 거의 항상 오던 방향으로 되돌아가던 문제(`Projectile.
  ComputeWallNormal`의 캐스트 시작점을 반지름만큼 뒤로 물려서 해결 - 조준선 쪽은 반대로 이 방식이 튕긴 직후
  조각을 아예 못 찾게 만들어 원래 방식으로 되돌림). ③ 벽에 튕긴 직후인데 사거리 소진 판정이 먼저 실행되는
  처리 순서 문제로 튕기지도 못하고 사라지던 버그(사거리 체크를 `FixedUpdate`에서 `Update`로 이동 - 그 물리
  스텝의 벽 충돌 처리가 끝난 뒤에 실행되도록). ④ 11번(거리 비례 데미지)이 읽는 이동 거리가 35번이 튕길 때마다
  리셋하는 것과 같은 필드를 같이 쓰고 있어, 튕긴 뒤 11번의 거리 계산이 "이번 구간"만 보고 총 비행거리를
  못 보던 문제(`Projectile`에 튕겨도 리셋 안 되는 `totalDistanceTraveled`를 별도로 추가하고 `DistanceTraveled`
  프로퍼티가 이 값을 반환하도록 변경 - 11번 스크립트는 무수정, 35번의 사거리 판정 로직도 무수정). 이번
  세션에 변경/신규된 스크립트 8개(`Projectile.cs`, `Playershooting.cs`, `claude/AimTrajectoryIndicator.cs`,
  `claude/WallBounceEffect.cs`(=`Item35effect.cs`), `claude/PlayerHitFlash.cs`, `claude/Item19effect.cs`(신규),
  `claude/SplitShotOverride.cs`(신규), `claude/WallBounceOverride.cs`(신규)) 전부 Claude Project 문서에도
  동기화 완료.
- v1.39 (2026-09-30): 플레이어 특수 공격 2종 + 제어 체계 개편, 형빈 확인 완료. (1) 수류탄 투척(2키, 곡사 조준선/
  사거리·폭발 반경 원/착탄 지점 표시/자해 데미지/이펙트 오프셋·크기) - 착지점 보정은 콜라이더 판정 두 번 실패
  후 좌표 계산 방식으로 확정(`LevelBuilder`의 이름표 방식은 되돌림). (2) 해킹툴 전개(3키, 즉시 사용/범위 원/
  해킹 = 강력 제어). (3) 무기 모드 전환 전담 `PlayerWeaponMode` 신설(1/2/3키, 자원 0이면 전환 불가, 다 쓰면 총
  복귀, 비총 모드에서 사격/재장전/총 조준선 차단). (4) 제어 2단계 체계 `EnemyControlState` 신설(강력: 기절·해킹 /
  일반: 매료, 교체 규칙, 색 일원화) - `EnemyStunState` 폐기, 31번 기절 누적 → 교체로 변경. (5) 스킬 스탯 7종
  `PlayerStats` 통합(14/15번 그룹, `statFloors` 자동 보충). (6) 스탯 패널에 스킬 스탯 전용 열(`isSkillStat`).
  이번 세션 신규/수정 스크립트 20개 - 신규: `PlayerWeaponMode`, `PlayerGrenadeThrower`, `Grenade`,
  `GrenadeAimIndicator`, `PlayerHackTool`, `HackRangeIndicator`, `EnemyControlState` / 수정: `StatModifier`,
  `PlayerStats`, `ItemInfoPanelView`, `PlayerStatDisplay`, `Playeraimandattack`, `Playershooting`,
  `AimTrajectoryIndicator`, `EnemyCharmState`, `EnemyAIController`, `EnemyRangedAIController`, `BossAIController`,
  `Item31effect`, `LevelBuilder`(한 번 수정 후 원래대로 되돌림) - 전부 로컬 실제 파일 기준으로 Claude Project
  문서에 최종 동기화 완료.
- v1.40 (2026-10-01): 사망 처리 + 플레이어 애니메이션 대거 적용 + 테스트 환경 정리, 형빈 확인 완료. (1) 사망 →
  게임 정지 → 게임 오버 창 → [예] 씬 재시작(`PlayerDeathHandler`/`GameOverUI` 신규, `PlayerHealth` 사망 이벤트).
  (2) 사망 애니메이션(두 레이어 Any State, Bool `IsDead`). (3) 수류탄 Idle/준비/투척 애니메이션 + `WeaponMode` Int +
  수류탄 모드 총 숨김 + 손 떼는 시점 지연 발사/연타 금지 + 손 뗀 뒤 1/3번 투척 캔슬. (4) 장전 애니메이션을 장전
  시간에 맞춰 가속/감속(`ReloadSpeed` = 클립 길이 / 장전 시간) + 장전 중 무기 전환 시 장전 취소. (5) 테스트 씬도 진짜
  프리팹 사용 + `PlayerTestProfile` 덮어쓰기(꺼진 상태 생성 → 적용 → 켜기, 무적/자원 무한). (6) Fire 클립은 상체
  마스크 문제(FBX 분석으로 원인 규명)로 폐기하고 코드 반동 `UpperBodyRecoil`(사격 틱 이벤트 `OnShotFired` + 연사 압축)로
  대체, `Attack` 트리거 정리. ScriptableObject 메뉴 `MobArchive/` 통일. 신규 스크립트 5개: `PlayerDeathHandler`, `GameOverUI`, `PlayerTestProfile`,
  `UpperBodyAngleCorrector`(보관용), `UpperBodyRecoil` (+ 문서 경로 `claude/`) / 수정 8개: `PlayerHealth`,
  `PlayerWeaponMode`, `PlayerGrenadeThrower`, `PlayerShooting`, `PlayerStats`, `PlayerResources`,
  `ItemTestArenaBuilder`, `PlayerAimAndAttack` - 전부 로컬 실제 파일 기준으로 Claude Project 문서에 최종 동기화 완료.
- v1.41 (2026-10-02): 해킹/장전/획득 연출 애니메이션 + 상자 아이템 빛 + 스탯 표기 정리 + 용어 교체, 형빈 확인 완료.
  (1) 해킹툴 사용 애니메이션 + 발동 지연/동작 시간/캔슬 + 해킹 모드 총 숨김. (2) 상체 클립이 몸을 흔들고 가라앉게 하던
  원인을 클립 루트 커브로 규명 - 바이너리 FBX를 직접 수정한 RootFixed 사본으로 해결(마스크 수정은 효과 없었음).
  (3) 장전 흔들림: 조준/비조준 두 사본 + AimBlend 블렌드 트리. (4) 상자 위 아이템 아이콘 빛(`ItemGlowSpawner`, 등급 색,
  추가 입자 레이어). (5) 아이작식 아이템 획득 연출(`PlayerItemPickupPresenter`, 머리 위 아이콘 + 흰색 플래시, 모든 행동
  차단, 카메라 쪽 회전, 조준선 숨김, 끝나면 총 복귀). (6) 스탯 화면 표기 규칙 일원화(`StatDisplayFormat` - 총알 크기 x100,
  확률 %, 곱하기 효과 ±%). (7) "장전 속도"→"장전 시간", "검은 양복"/BlackSuit→"게마트리아"/Gematria 전면 교체(코드
  식별자 포함, 저장값 유지). 신규 6개: `ItemGlowSpawner`, `ItemGlowMarker`, `ItemPickupEvents`,
  `PlayerItemPickupPresenter`, `StatDisplayFormat`, `SpriteFlash.shader`(Assets/VFX/Shaders) / 수정 22개:
  `PlayerHackTool`, `PlayerWeaponMode`, `PlayerGrenadeThrower`, `PlayerShooting`, `PlayerAimAndAttack`,
  `AimTrajectoryIndicator`, `PlayerActiveItem`, `PlayerDisposableItem`, `PlayerFoodItem`, `TreasureChest`, `ItemPedestal`,
  `PlayerDeathHandler`, `PlayerStatDisplay`, `ItemInfoPanelView`, `PlayerStats`, `StatModifier`, `AmmoBoxDisplay`,
  `PlayerReloadIndicator`, `LevelBuilder`, `ChoiceLevelConfig`, `ChoiceLevelGenerator`, `RoomData` - 전부 로컬 실제 파일
  기준으로 Claude Project 문서에 최종 동기화 완료. 애니메이션 에셋(RootFixed FBX 5개, `UpperAvatarMask_NoRoot`) 추가.
- v1.42 (2026-10-03): 작업 장치를 ggumnu(`C:\Users\beens\Mob_archive`, git 동기화)로 옮겨 진행, 형빈 확인 완료.
  (1) HUD 패널 접기(`CollapsibleHUDPanel`, Tab 일괄) + 접힌 상태에서 버튼이 안 눌리던 버그 수정. (2) 미니맵 휠 확대/
  드래그/구역 제한/방 이동 시 초기화/"현재 위치" 버튼(부드러운 이동). (3) HUD 클릭 차단 `HUDClickBlocker`. (4) 슬롯
  교체 확인창(`SlotReplaceConfirmUI`, `confirmBeforeReplace`, 등급 색 이름, 일시정지 없이 조작만 차단, 피격 시 취소).
  (5) 테스트 보물상자(`ItemTestArenaBuilder.testChestItems`, 열린 상태 시작/재생성). (6) F 상호작용을 가장 가까운
  대상 하나로(`InteractionTargetSelector`, 설명칸도 따라감). (7) 아이템 9(방의 모든 살아 있는 상자 복제)/22(안 가본
  동아리방 우선)/27(보스방 상자까지 확장)/41(신규 패시브 - 해킹 적 피해 +50%)/45(신규 장신구 - 액티브 코스트 -1)/
  48(장신구 → 패시브). (8) 아이템 설명칸 능력치 없을 때 빈 틈 제거. (9) 남아 있던 BlackSuit 에셋 데이터 2곳 교체
  (`ChoiceLevelConfig.asset` variantId, `Reisa_Player.prefab` 저장 필드명). 신규 9개: `CollapsibleHUDPanel`,
  `HUDClickBlocker`, `SlotReplaceConfirmUI`, `InteractionTargetSelector`, `BossChestOpenEvents`, `Item41effect`,
  `EnemyDamageTakenModifier`, `Item45effect`, `ActiveCostModifier` / 수정 26개: `Minimapdisplay`, `Playerstatdisplay`,
  `PlayerSlotItemDisplay`, `Playerinventorydisplay`, `Playerhudmanager`, `Playeraimandattack`, `PlayerGrenadeThrower`,
  `PlayerHackTool`, `Itemdefinition`, `Treasurechest`, `Itempedestal`, `Levelportal`, `InteractPromptDisplay`,
  `Playeriteminfodisplay`, `Treasurechestinfodisplay`, `ItemTestArenaBuilder`, `Item27effect`,
  `Normalroomtreasurespawner`, `Item09effect`, `Item22effect`, `Levelbuilder`, `Enemyhealth`, `Item48effect`,
  `PlayerActiveItem`, `PlayerCostDisplay`, `ItemInfoPanelView` - 전부 로컬 실제 파일 기준으로 Claude Project 문서에 최종
  동기화 완료. 에셋 수정 2개(`ChoiceLevelConfig.asset`, `Reisa_Player.prefab`).
- v1.43 (2026-10-04): 블루아카 인게임풍 툰 셰이더/조명/배경 적용, 형빈 확인 완료. (1) 조명/후처리 기초(태양 빛 차가운
  흰색, 환경광 Color 모드, SSAO 끔, Vignette 끔, Color Adjustments/White Balance - 처음엔 너무 따뜻해서 "아침의 산뜻한"
  톤으로 재조정, ItemTestArena에도 동일 적용). (2) 배경 툰 셰이더 `Toon_Environment` - 처음 버전은 바닥 데칼이 사라짐 →
  DBuffer 데칼 받기 + Lit의 DepthNormals Pass 재사용으로 해결. (3) 캐릭터 툰 셰이더 `Toon_Character`(셀 셰이딩/림라이트/
  외곽선) - 플레이어는 FBX 내장 재질이라 `Player_Mat` 재질 5개를 만들어 FBX Remap, 외곽선은 플레이어 + 적. (4) 일괄 교체
  도구 `ToonShaderBatchConverter`(자동차 3종은 Extract 후 교체). (5) 배경 셰이더에도 외곽선 추가 + 도구에 배경 외곽선
  일괄 켜기/끄기 → 배경 전체 켬, 건물만 두께 2.5. (6) 화면 고정 배경 `ScreenBackground`(스카이박스 슬롯에서 화면 좌표로
  그림, 카메라 Background Type Skybox로 복귀) + 임시 이미지(세로 파랑→하늘색 그라데이션, 흰 마름모 패턴 타일).
  기존 스크립트 수정 없음. 신규 4개: `Toon_Environment.shader`, `Toon_Character.shader`, `ScreenBackground.shader`
  (`Assets/Shaders`), `ToonShaderBatchConverter.cs`(`Assets/Editor`) + 이미지 2개(`Assets/Background`) - 셰이더/스크립트는
  전부 로컬 실제 파일 기준으로 Claude Project 문서에 동기화 완료. 배경 이미지 재커밋 캐시 사고 재발(위 작업 방식 참고).
- v1.44 (2026-10-07): 플레이어 모델을 SD 유니티짱으로 교체하고 레이사도 병행 유지, 형빈 확인 완료. (1) 초상화 스프라이트
  자르기 + 초상화 테두리(`PlayerHUDManager`). (2) 애니메이션 FBX 15개 Humanoid 전환 + 레이사용 Generic 복사본/Override
  Controller/`Reisa_AvatarSource`(레이사가 Humanoid에서 공중에 뜨는 문제 해결). (3) `UnityChan_Player` 프리팹 신설(LevelBuilder
  칸으로 교체). (4) 상체 마스크 Combined(Body 끔)로 림보 자세 해결 + 몸통 Sync 레이어 `UpperBodyTorso`와
  `TorsoLayerWeightByState`로 수류탄/해킹/획득 때만 몸통 사용. (5) `HumanoidAimLookAt`(LookAt + 손 IK로 총 내리기 + 걷기 중 팔
  안정화 + 총구 자동 정렬, v3~v6 - 팔꿈치 내리기 기능은 오해로 만들었다가 삭제, 총을 소매 뼈에서 손 뼈로 이동). (6) 비조준 사격
  시 자동 조준(`PlayerAimAndAttack`). (7) `UpperBodyRecoil` Humanoid 뼈 대응. (8) `UpperBodyAngleCorrector`/미사용 FBX 2개/
  `TestController` 삭제. 신규 2개: `HumanoidAimLookAt`, `TorsoLayerWeightByState` / 수정 3개: `PlayerHUDManager`,
  `Playeraimandattack`, `UpperBodyRecoil` / 삭제 1개: `UpperBodyAngleCorrector` - 전부 Claude Project 문서에 즉시 동기화 완료.
  에셋: Generic FBX 15개 + Override Controller + 마스크 2개(Combined/Torso) 신규, Controller에 UpperBodyTorso 레이어 추가.
- v1.45 (2026-10-07): 시작/로딩 화면 + 포탈 이펙트 + HUD TextMeshPro 전환 + 스탯 변화 연출 + 탄환/수류탄 색 + 해킹 총알
  제거, 형빈 확인 완료. (1) Title 씬 + `TitleScreenUI`(게임 시작/설정(기능 없음)/나가기), Build Profiles Title 0/LevelBuilder 1,
  게임 오버 [아니오] → 시작 화면. (2) `LoadingScreen`(검은 배경 + 갸우뚱 아이콘, 최소 1초 + 착지 대기 0.5초 + 페이드 0.2초,
  게임 시작/다음 레벨/재시작/시작 화면 복귀, 로딩 중 조작 차단 - `PlayerMovement`는 이동 입력만 0). (3) 레벨 포탈을
  `vfx_Portal_01` 복제본 프리팹으로 교체(코드 수정 없음). (4) HUD 글자 흐림 → 에디터 Game 창 문제가 아님을 확인(빌드에서도
  흐림) 후 HUD 글자 14개 파일을 TextMeshPro로 교체(2단계로 나눠 진행, `HUDText`/`HUDFont.GetTMP()` 신설), 외곽선이 글자를
  덮던 문제는 Face Dilate 동시 적용 + 두께 변환 재계산으로 해결, F 안내 외곽선 인스펙터 칸 추가. (5) 스탯 패널 수치 변화 연출
  (유리 노랑/불리 빨강, `lowerIsBetter`). (6) 총알 노랑 Unlit 재질, 수류탄 `Grenade_Temp`(툰 어두운 초록) 연결 - 빌드에서
  수류탄이 핑크였던 원인(CreatePrimitive 기본 재질) 규명, 임시 구체에도 URP 재질 적용, 적 탄환 빛 번짐은 Bloom 정상 동작으로
  유지. (7) 해킹 효과 발동 순간 같은 범위 적 총알 제거(`EnemyProjectile.Active`/`ClearInRadius`). 신규 3개: `LoadingScreen`,
  `TitleScreenUI`, `HUDText` / 수정 19개: `Gameflowmanager`, `GameOverUI`, `Playermovement`, `HUDFont`, `Playerresourcedisplay`,
  `Ammoboxdisplay`, `Playerstatdisplay`, `PlayerCostDisplay`, `Minimapdisplay`, `BossHealthBarDisplay`, `ItemInfoPanelView`,
  `Treasurechestinfodisplay`, `InteractPromptDisplay`, `DamagePopupDisplay`, `SlotReplaceConfirmUI`, `Leveltransitionconfirmui`,
  `PlayerGrenadeThrower`, `PlayerHackTool`, `Enemyprojectile` - 전부 로컬 실제 파일 기준으로 Claude Project 문서에 최종 동기화
  완료. 에셋(형빈 에디터 작업): Title 씬, `LoadingScreen`/`LevelPortal`/`Grenade_Temp` 프리팹, `vfx_Portal_Level`,
  `Bullet_Player_Mat`/`Grenade_Mat`, TMP Essential Resources, `경기천년제목_Bold SDF` 글꼴 에셋.
