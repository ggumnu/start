# 블아이작 (Mob_archive) - 핵심 상태 요약 (v1.61)

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
- 프로젝트 지식: `claude/claude_Mob_archive_핵심상태_아카이브_v1.59.md` - 이 문서에서 줄인 v1.58/v1.59 아이템 확정값 원문.

## 작업 방식

- 형빈은 3D 모델러(코딩 X), Claude가 C# 전량 담당. 답변/안내는 항상 한국어.
- 장치마다 작업 폴더가 다르고 git(`저장하기.bat`)으로 동기화 - 연결된 폴더는 세션마다 확인.
- **스크립트의 원본은 장치 폴더**. 프로젝트 지식엔 스크립트 사본을 두지 않음(이 문서 + v1.59 아카이브 + SD 텍스처 도구만) - 코드는 매번 장치에서 읽음.
- 여러 채팅이 동시에 같은 프로젝트를 작업할 수 있음 - 기존 파일을 고치기 직전에 항상 다시 스테이징해 최신본 기준으로 편집
  (예전 outputs 사본과 `cmp`해서 다르면 다른 채팅이 고친 것). 형빈이 "새 내용이 안 보인다"고 하면 장치 파일부터 다시 받아 확인.
  이 문서도 갱신 직전에 다시 읽어 다른 채팅의 갱신 위에 합침.
- 장치 파일 작업: 스테이징 → 클라우드 편집 → `expectedMtimeMs`와 함께 커밋. 커밋할 때마다 outputs 아래 **새 하위 폴더** 사용(같은 경로를 다시 쓰면
  옛 내용이 커밋됨) - 복사를 먼저 끝낸 뒤 커밋, 커밋 후 다시 스테이징해 `cmp`(이미지는 픽셀 비교)로 확인.
- "분석부터"/"계획부터"/"바로 진행하지 말고"라고 하면 코드 없이 분석/계획만. "가능할까?"도 계획 + 정할 점으로 답함.
- 착수 전: 기획 이해 + 코드 확인 결과 + 계획 + 정할 점 제시, 승인 후 코딩. 형빈이 미리 "짚을 점 없으면 바로 진행"이라 한 턴은 예외.
  기획 문구에 모순이 있으면 두 해석을 숫자로 보여주고 고르게 함. 화면 꾸미기는 코드 전에 같은 계산으로 미리보기 이미지를 만들어 보여줌.
- 코드는 항상 완성된 전체 파일로 전달. 기존 스크립트 수정은 "어느 파일의 무엇을" 먼저 밝히고 승인 - 파일 수 최소화보다 안정성 우선.
- 설계 전 실제 프리팹/코드/에셋을 먼저 확인. 프리팹 구성(콜라이더/태그)에 의존하는 판정은 피함. 이펙트 문제는 프리팹 YAML과 에셋 값을 직접 읽고
  원인을 짚은 뒤 Inspector 수정 위치를 안내. 수치가 이상하면 EnemyDefinition 에셋 값과 프리팹 컴포넌트 순서(Awake 순서)도 YAML로 확인.
  모델 치수 문제는 FBX(바이너리)를 직접 파싱해 꼭짓점/노드 변환을 확인.
- **플레이어 찾기는 `FindFirstObjectByType<PlayerHealth>()`(루트)** - "Player" 태그는 자식에도 붙어 있음(위치만 쓸 땐 태그 OK).
- 이름 변경 시 Assets 전체(.asset/.prefab/.unity)를 대소문자 무시로 검색. 직렬화 필드 이름을 바꿀 땐 `[FormerlySerializedAs]`.
  `[SerializeReference]` 효과 클래스는 클래스 이름으로 저장됨 - 이름을 바꿀 땐 `[MovedFrom]` + 에셋 강제 저장 후 보호 속성 제거. 기존 효과 클래스에 새 칸을
  추가하면 옛 에셋은 그 칸이 0/빈 값일 수 있음 - 코드에서 빈 값 대비 또는 메뉴 도구로 채움(ScriptableObject 설정 에셋은 새 칸에 코드 기본값이 들어감).
  **스크립트 파일 이름 변경은 형빈이 Unity Project 창에서 F2로**(.meta 유지) - Claude는 내용만 고침.
- ScriptableObject 클래스는 파일 이름과 같은 파일에 따로 둠(다른 파일 안에 두면 에셋이 깨짐).
- 유니티 안내는 메뉴/버튼/인스펙터 필드명까지 클릭 단위로. 컴포넌트 찾기는 Hierarchy 검색 `t:컴포넌트이름`. 재질/프리팹/설정 에셋 준비가 필요하면
  안내 대신 메뉴 도구/자동 생성(`MobArchive/VFX/...` - 91번 오한/110번 굴절, `OutlineWidthSettings`).
- 인코딩: 새 파일 = UTF-8(BOM) + CRLF. 기존 파일은 원래 인코딩/줄바꿈 보존 - CP949 남은 파일: `Playeraimandattack`/`Ipassiveitemeffect`/
  `Statmodifier`/`Bossaicontroller`/`Roomobjectives`/`Enemystats`/`Realenemyspawner`/`Playerinventorydisplay`/`Levelportal`/
  `Playerresourcedisplay`/`AppearFromWhite` 등(고칠 때 UTF-8 BOM으로 변환). UTF-8 무BOM + CRLF: `GameFlowManager`/`Treasurechest`/`Itempedestal`/
  `Normalroomtreasurespawner`/`PlayerDisposableItem`/`PlayerFoodItem`/`PlayerActiveItem`/`PlayerTrinket`/`DisposableEffectContext`/`Treasurechestinfodisplay`/
  `HUDFont`/`Levelbuilder`/`Enemyhealth`/`Playerstats`/`Playerhealth`/`Roomcontroller`/`Toon_Environment.shader`. 무BOM + LF: `PlayerResources`.
  `Enemyradialattack` = BOM + LF. BOM 2개(그대로 보존): `ItemInfoPanelView`/`PlayerSlotItemDisplay`/`PlayerCostDisplay`. SD 스크립트/셰이더(v1.57)는 UTF-8 무BOM + LF.
- 요청 없는 버전 태그 주석 금지.
- **주석 정책(스크립트를 고칠 때만 그 파일에 적용 - 일괄 정리 X)**: 주석은 "왜"만. 남김 = 파일 맨 위 역할 요약 1~3줄, 버그 방지 경고, 다른 스크립트와의 연결,
  `[Tooltip]`/`[Header]`(길면 한 줄로). 지움 = 코드를 그대로 풀어쓴 설명, C#/Unity 기초 설명, 주석 처리된 옛 코드/버전 이력. 긴 기획/결정 경위는
  장치 `Docs\설계노트.md`로 옮김(이미 이 문서/아카이브에 있는 내용은 다시 쓰지 않음, 커지면 그때 시스템별로 분리). 주석 언어는 한국어.
  결과 보고 때 기능 수정 / 주석 정리 / 설계노트 이동을 나눠 알림(툴팁 문구 변경은 따로).
- 토큰 절약: 폴더는 목록 먼저, 필요한 파일만 열기.
- 컴파일 사전 검사: mono `mcs` + Unity API stub, 없으면 dotnet SDK의 Roslyn `csc.dll`로 구문 오류만, 둘 다 없으면 괄호 짝 + 눈 검토 후 형빈에게 알림.
- ScriptableObject 메뉴는 `MobArchive/<이름>`. 런타임 설정 에셋은 `Assets/Resources/<이름>` 자동 로드(없으면 기본값) 방식이 표준.
- 새 UI 글자는 TextMeshPro: `HUDText.AddTo(go, TextAnchor, wrap)`, 외곽선 `HUDText.SetOutline(text, 픽셀, 색)`. 작은 숫자의 흰 테두리는 흰 글자 8개를
  사방으로 깔기(110/113번 표시, 무기 모드 자원 표시). 같은 Canvas 안에서는 나중 자식이 위 - 항상 위여야 하는 UI는 필요할 때 `SetAsLastSibling`.
- 월드 위치에 뜨는 아이콘/숫자 연출은 Overlay 캔버스에 그림.
- Inspector `OnGUI`/`PropertyDrawer` 안에서 에셋 검색을 매번 돌리지 말 것(렉) - static 캐시 + `EditorApplication.projectChanged`로 비우기.
- 빌드 함정: 런타임 `CreatePrimitive` 도형/`Shader.Find` 재질은 빌드에서 핑크·누락 가능 - 재질/셰이더 칸을 두고 에셋을 넣음(비우면 `Shader.Find` 대체).
- 월드에 스프라이트 여러 장을 섞어 그릴 땐 SpriteRenderer 대신 MeshRenderer + 텍스처별 재질 + 스프라이트별 메시(`RabbitPopVfx` 방식).
- URP 함정: Overlay 카메라 `clearDepth`는 읽기 전용(Overlay는 항상 깊이를 지움). 카메라 하나에서만 숨기기 = `beginCameraRendering`/`endCameraRendering`에서
  `Renderer.forceRenderingOff`. 카메라별 셰이더 전역값 = CommandBuffer `SetGlobal*` + `context.ExecuteCommandBuffer`. 카메라 하나만 다른 하늘 = `Skybox` 컴포넌트.
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
  레벨 = `CurrentLevelNumber` + `CurrentPhase`(Combat = n / Choice = n.5).
- `TitleScreenUI`: Canvas/배경/버튼 자동 생성. 버튼 위에서부터 게임 시작(로딩 + `gameSceneName`) / 도감(`ItemCodexUI.Open`) / 설정(빈 `OnSettingsClicked()`) /
  나가기. 묶음 중심(`buttonsCenterOffset`) = 도감과 설정 사이. 도감이 떠 있는 동안 버튼 입력 무시(`ItemCodexUI.BlocksInput`).
- `LoadingScreen`(프리팹을 두 씬에 배치, `DontDestroyOnLoad`, order 1000, 페이드 없이 바로 검게 뜸, 로딩 중 조작 차단) - `RunLevelWork`/`LoadSceneWithLoading`/
  `StartReloadActiveScene`/`IsShowing` 등.
- 사망: `PlayerHealth.Die()` → `OnDied`/`OnAnyPlayerDied` → `GameOverUI`(order 100, 2초 뒤 [예]/[아니오]).
- 피격 알림 `PlayerHealth.OnDamageTaken(실제 피해)` - 체력이 깎인 뒤에만(보호막으로 막은 피격은 알림 X - 피격 반응 아이템/얼굴/점멸 없음).
  `IsHitInvincible` = 피격 무적 중(점멸/얼굴/적 피격 이펙트 판정은 이 값과 `WillShieldBlockNextHit` 사용).
- 레벨 이동 확인창 `LevelTransitionConfirmUI`.
- **게임 중단 화면 `PauseMenuUI`**(설정 `Resources/PauseMenuSettings`, order 95): ESC → `timeScale 0` + 막 80% → 가로 한 줄 [계속하기] [도감] [재시작하기]
  [그만하기](뒤 두 개 = `PopupStyle` 위험 버튼 빨강 + 흰 글자). 도감 = 중단 화면 위에 `ItemCodexUI.Open(order 96)`(도감만 닫히고 돌아옴). 재시작/그만하기 =
  검은 확인창(실행 버튼 빨강, ESC/[취소하기] = 확인창만 닫힘) → 로딩 + 지금 씬 다시 불러오기(게임 오버 [예]와 같음) / 타이틀. Enter 동작 없음.
  다른 팝업/획득 연출/로딩/사망/도감 중이면 안 열림. 씬을 떠나는 처리는 `LeaveScene()`(이어하기 저장 자리 표시).
- **R 꾹 누르기 재시작 `HoldRestart`**(자동 생성, 설정 `Resources/HoldRestartSettings`, order 99): R 2초 → 경고 없이 로딩 + 지금 씬 다시 불러오기(테스트 씬도).
  막 알파 = 진행도^2(처음엔 천천히), 떼면 0.3초에 걸쳐 밝아짐, 다시 누르면 남은 진행도에서 이어감, 재시작 후엔 R을 뗐다 눌러야 다시 셈. 게임은 계속 흐름
  (R의 원래 동작 장전/115번 기폭도 그대로). 팝업/중단 화면/도감/획득 연출/로딩/사망/`timeScale 0`이면 안 셈(누르는 중이면 취소).

### 아이템 도감

- `ItemCodexUI`(시작 화면 + 중단 화면에서 엶, 그 씬과 함께 사라짐, 설정 `Resources/ItemCodexSettings`): 불투명 흰 막 페이드인 → 모서리 삼각형 무늬 →
  "아이템 도감" 제목 + 연회색 둥근 패널(한 줄 10칸, 6줄 보이고 휠 스크롤 - `Input.mouseScrollDelta` 직접 처리) + [돌아가기]가 ESC 중단 화면과 같은
  곡선/시간으로 떠오름, 닫을 때 가라앉으며 사라짐. ESC/[돌아가기]로 닫힘(클릭은 다 떠오른 뒤).
- 목록 = `ItemCodexSettings.itemDatabase`의 전 아이템, 분류 무관 ID 숫자 순서(숫자 아닌 ID는 뒤, 빈/중복 ID 제외). 얻은 적 있으면 원래 색 + 호버 시 커서 위
  설명칸(`ItemInfoPanelView`, 모양 = `hudPrefab` 안 `PlayerItemInfoDisplay.GetStyle()`), 없으면 반투명(0.35) + 설명칸 X.
- 기록 `ItemCodexRecord`(정적, `persistentDataPath/item_codex.json`, 처음 얻는 순간 저장, 재시작/게임 종료해도 유지): 인벤토리/슬롯에 실제로 들어오는 다섯 곳
  (`PlayerInventory.TryAcquireItem`/`ReplaceItems`, `PlayerActiveItem`/`PlayerTrinket`/`PlayerDisposableItem`/`PlayerFoodItem.TryAcquireItem`)에서
  `MarkAcquired` - 어떤 경로로 얻든 기록. 테스트 씬(`ItemTestArenaBuilder`가 있는 씬)은 기록 X. 초기화 = 메뉴 `MobArchive/아이템 도감 기록 초기화` 또는
  ItemCodexSettings 에셋 ⋮ 메뉴.
- 배경 무늬 셰이더 `MobArchive/UICodexCornerPattern`(텍스처 없이 정삼각형 격자 한 변 145px, 삼각형별 랜덤 진하기, 왼쪽 위/오른쪽 아래 대각선 820px 페이드,
  최대 0.75, 삼각형별 은은한 반짝임). 값은 ItemCodexSettings "배경 무늬"(Pattern Shader 칸 연결됨 - 빌드 포함용).

### 비주얼 / 셰이더

- 조명/후처리/Volume 값은 LevelBuilder/ItemTestArena 동일(수치는 v1.46-v1.52 아카이브).
- `MobArchive/Toon_Environment`(배경, 외곽선 Pass, DBuffer 데칼) / `MobArchive/Toon_Character`(셀 셰이딩, 림라이트, 외곽선) - 둘 다 URP/Lit
  프로퍼티 이름 호환(`_BaseMap`/`_BaseColor`/`_EmissionColor`/`_Surface`) - 새 셰이더도 이 이름 유지. SD 캐릭터 전용 셰이더 2개는 [SD 신규 캐릭터].
- **배경 외곽선 보정**(Toon_Environment 외곽선 Pass만, 실행 비용 없음): 에디터 `OutlineSmoothNormalBaker`(AssetPostprocessor)가 `Assets/Object` 아래 모델을
  임포트할 때 UV4에 xyz = 외곽선 전용 노멀(**볼록한 모서리만** 평균 - 하드 엣지 끊김 방지, 오목한 곳(창문 음각)은 원래 노멀), w = 모델 크기(가로·세로·높이 평균)를 저장.
  셰이더가 w x 오브젝트 스케일을 기준 크기와 비교해 작은 모델은 비례해서 얇게(최소 비율까지). 설정 `Resources/OutlineWidthSettings`(없으면 자동 생성,
  기준 = `Car_Cyan_Wall` 프리팹 크기, `minWidthFactor` 0.4, 전역값이라 바꾸면 즉시 반영). 이미 있던 모델/계산 변경 후엔 메뉴 `MobArchive/외곽선/배경 모델 외곽선 노멀 다시 굽기`. UV4를 이미 쓰는 메시는 건너뜀(콘솔 경고).
- 화면 굴절 셰이더 `MobArchive/RoomWave`(90번)/`MobArchive/HitRipple`(110번, `Assets/Shaders`) - URP 설정의 Opaque Texture 필요.
- 플레이어 재질 `Player_Mat/*`(FBX Remap), 적 재질 `Enemy_Mat`. 탄환 `Bullet_Player_Mat`(Unlit `FFE000`), 수류탄 `Grenade_Temp`.
- 일괄 교체 도구 `ToonShaderBatchConverter`(메뉴 `MobArchive/셰이더 일괄 교체`). 화면 고정 배경 `MobArchive/ScreenBackground`(임시 이미지).

### 플레이어

- 관성 없는 CharacterController 이동, twin-stick 조준(우클릭 = 마우스 조준), 사거리 소멸 탄환, 무적 프레임 체력.
- 프리팹 3개(`Assets/Player/Character/Player`): `SD_Player` / `UnityChan_Player` / `Reisa_Player_Fixed` - **값을 바꿀 때는 세 프리팹 모두**.
  쓰는 프리팹은 `LevelBuilder`/`ItemTestArenaBuilder`의 Player Prefab 칸으로 교체.
- 비조준 자동 조준 `autoAimOnFire`(좌클릭 → 조준 → 발사 → 0.4초 뒤 복귀).
- `PlayerStats` 단일 소스: (기본 + 덧셈 합) x 곱셈 곱 → `statFloors`(최종 값에만). 새 `StatType`은 **enum 맨 끝**에 + 매핑 추가.
  `RemoveModifiersFromSource`/`RemoveModifiersFromSourceExcept(id, 남길 스탯)`. 특수효과가 준 스탯은 출처 `PassiveEffectIds.EffectStatSourceId(id)`
  (= id + "_effect", 잃어도 남는 "귀속" 스탯). 잃으면 사라지는 실시간 스탯은 별도 출처(예: 76번 id + "_passiveCount").
- **공격력 하한선 = 고정값(Absolute) 1**(Player 프리팹 `statFloors`). 하한선 없이 아이템 효과만 적용 = `PlayerStats.ApplyModifiersTo(StatType, 기본값)`(87번 아군).
- **최대 체력 상한 `maxHealthCap` 10**(`Compute()` 끝에서 자름, 넘친 값은 `RawMaxHealth`로 기억 - 나중에 깎여도 반영). 상한에 막힌 증가분은 그만큼 회복
  (`OnMaxHealthIncreaseCapped`). 테스트 프로필 `noMaxHealthCap`/`noShieldCap`(기본 켬)으로 테스트 씬만 상한 해제(표시는 10칸까지).
- **보호막**(아이작 소울하트): `PlayerHealth.CurrentShield`/`MaxShield`(5)/`AddShield(수, 획득 연출 여부)`/`SetShield`, 알림 `OnShieldChanged`/`OnShieldBroken`/
  `OnShieldGained(얻은 수, 연출)`. 피해량과 무관하게 피격 1번을 통째로 막음(자해 수류탄/체력 비용 방 포함) - 무적 시간은 주되 피격 알림 X. 레벨 이동/방 클리어와
  무관하게 맞기 전까지 유지, 꽉 차면 초과분 버림. 연출 `PlayerShieldBreakVfx`(자동 부착, 코드 생성 깎은 정이십면체 + `MobArchive/ShieldBreakIcosa` - 외곽선 없이
  면마다 흰/파랑/투명 반짝임 + 발광, 깨질 때 0.6→1.25 전개 후 축소·페이드 0.55초, 얻을 때 `PlayAcquire` 0.5초).
- 회복 배율 등록소 `HealBoostOverride`(101번 x2).
- 사격 알림 `PlayerShooting.OnAmmoConsumed`/`OnAmmoRefunded`/`OnReloadStarted`, 즉시 장전 `TryInstantReload()`. 투명 `PlayerInvisibility`(112번, `_DitherFade`).
- 74번 돌진 `PlayerDash`(`IsDodging`/`BlocksPlayerInput`) - 회피는 피해원마다 확인. 액티브 사용 규칙 `IActiveItemUseRules`, 디버프 면역 `PlayerDebuffImmunity`(94번).
- 시작 자원은 `PlayerResources`. 무기 모드 `PlayerWeaponMode` 1 총/2 수류탄/3 해킹 - 0개여도 들어갈 수 있고, 0이 되는 순간에만 총으로 자동 복귀.
  수류탄/해킹 모드 자원 표시: 플레이어 오른쪽 아래 `WeaponModeResourceIndicator`(아이콘 + 흰 테두리 검은 숫자, 0개면 아이콘만 반투명) + 탄약 상자 뒤에서
  왼쪽으로 미끄러져 나오는 보조 상자(`AmmoBoxDisplay` - 수류탄/해킹 위치 따로, 회색).
- 수류탄 `PlayerGrenadeThrower`(자해 - `GrenadeSelfDamageOverride.IsBlocked`), 폭발 알림 `OnGrenadeExploded(위치, 반경)`, 폭발 중심 = `Grenade.CurrentGroundPoint()`
  (77/81/92/115번 등록소). **118번 포격 `ArtilleryStrike`는 수류탄 폭발 처리를 따로 구현**(반복 작업 [21]).
  해킹툴 `PlayerHackTool` - `ApplyHackArea`/`ApplyHackToEnemy`, 알림 `OnHackDeployed`(효과 발동) / `OnHackToolSpent`(1개 실제 소모 순간 - 58번).
  범위 이펙트 크기 `GetRadiusVfxFactor`. 수류탄 겉모습 등록소 `GrenadeVisualOverride`. 세부는 v1.53-v1.57 아카이브.
- 아이템 획득 연출: `ItemPickupEvents.Raise` → `PlayerItemPickupPresenter`(연출 중 행동 차단 `BlocksActions`). 여러 개 = `PassiveRerollUI.RaisePickupsInSequence`.
- F 상호작용 = 가장 가까운 하나(`InteractionTargetSelector`). 팝업이 떠 있으면 대상 없음.
- 테스트: `PlayerTestProfile` → `ItemTestArenaBuilder.testProfile`/`testChestItems`(시작 보호막 `startingShield`, `refillShieldNow`). ItemTestArena엔 방 클리어가 없음 -
  39/72/111번은 실제 레벨에서 테스트.

### 플레이어 애니메이션

- 애니메이션 FBX 15개(Mixamo, Humanoid), Controller `ReisaAnimatorController` 공유(SD/유니티짱 직접, 레이사는 Generic 복사본 + Override Controller).
- **지우면 안 됨**: `Reisa_AvatarSource.fbx`, `Animation/Generic/` 전체, `Animation` 원본 FBX, `Reisa_Fixed.fbx`, `Reisa_gun_Fixed.fbx`, `Assets/UnityChan`.
- 레이어 Locomotion/UpperBody/UpperBodyTorso(Humanoid만), 조준 IK `HumanoidAimLookAt`, 반동 `UpperBodyRecoil`, RootFixed 사본 = 루트 커브 고정 FBX.

### SD 신규 캐릭터 (`Assets/Player/Character/Player/SD`) - 세부는 v1.58 아카이브

- 알바디 `SD_AltBody_v02.fbx`(바이패드 `Bip001`, Humanoid) + `SDCharacterSkin`/`SDPartAttacher`/`SDFaceController`. **`SD_Player`** = `UnityChan_Player` 복제 +
  알바디(총 `Bip001 R Hand`, 헤일로 `Bip001 Head`, 루트에 `SDFaceGameEvents`). 셰이더 `MobArchive/Toon_Character_SD`/`MobArchive/Toon_Face_SD`(둘 다 `_DitherFade`,
  초상화 조명 전역값 `_SDPortraitLightAmount`/`_SDPortraitLightDir` - 평소 0). 조준 보정/FirePoint 높이/획득 아이콘 높이/반동은 SD에 맞게 조정됨.
- 재질 자동 설정 `MobArchive/SD 캐릭터 재질 자동 설정`, 파츠 `SDPartDefinition` + `SDPartAttacher`(`Equip`/`Unequip`), 표정 `SDFaceExpressionSet`. 게임 도중
  `Equip`한 파츠는 아직 피격 깜빡임 목록에 안 들어감.
- **실시간 초상화 `PlayerPortraitCamera`**(SD_Player 루트, `PlayerHUDManager.PortraitRect`에 RawImage): 그림판 512 + 카메라 2대(URP 스택). 배경 카메라 = 물체 없이
  하늘 그라데이션(`MobArchive/PortraitSkyGradient`, 이 카메라만의 `Skybox` - 세상 기준 위아래라 고개를 숙이고 들 때만 살짝 움직임, `Sky Shader` 칸 연결됨),
  `showBackgroundScenery` 켜면 예전처럼 지형까지. 플레이어 카메라 = Player 레이어만(모델 렌더러를 Player 레이어로 옮김, 0.5초마다 다시 모음), 항상 맨 앞.
  카메라는 머리 뼈에 고정(얼굴 정면, 얼굴은 가만히). 초상화에서만 피격 점멸 숨김(`PlayerHitFlash.ShowNormalForPortrait`) + 카메라 쪽 조명.

### 적 AI

- 근접 `EnemyAIController`, 원거리 `EnemyRangedAIController` + `EnemyRangedAttack`, 보스 `BossAIController`.
- 적 탄환 = 코드 생성 구체 + `EnemyProjectile`(정적 목록 `Active`) - 한 발 = 한 번 명중.
- 적 수치: 공통 = `EnemyDefinition`(모든 적 Attack Power 1), 공격 방식별 = `attackConfig`. 접촉 트리거 `EnemyMeleeContactTrigger`는 공격 컴포넌트 `Initialize`에서
  능력치를 받음.
- **목표 정하기 `CombatTargeting`**: 적/아군 AI와 공격 컴포넌트는 Player 태그 대신 `Resolve`/`ResolveNow`/`CurrentTarget`/`TargetIsInvisiblePlayer`로 목표를 정함
  (적 = 플레이어(투명 제외)와 아군 중 가까운 쪽, 아군 = 가장 가까운 적). **새 적 AI/공격도 이 창구 사용**. 켜진 적 목록 `EnemyHealth.ActiveEnemies`.
- **87번 아군 `AllyFaction`**(세부는 v1.59 아카이브): Enemy 태그 제거(태그를 안 보는 새 효과는 `AllyFaction.IsAlly`로 제외), 방 클리어 목록에서 빠짐,
  아군이 죽인 적 = 플레이어 처치, 방 이동 `AllyRoomTransfer`, 보스는 포섭 불가.
- 정적 등록소: `EnemyDetectRangeOverride`, `EnemySightOverride`, `EnemySpeedModifier`, `EnemyDamageTakenModifier`(41번), `EnemyAttackSpeedModifier`(91번).
- 처치 이벤트 `EnemyHealth.OnAnyEnemyKilled(적, 위치)` - 구독 코드는 try/catch로 감쌈.
- 제어 체계 `EnemyControlState`: 강력(수면/해킹) 1칸 + 일반(매료) 1칸, 강력 중엔 모든 AI 정지. 새 제어 = `EnemyControlType` 추가 + `GetTier()`.
  몸 색 우선순위 = 공격 충전 > 수면·해킹 > 매료 > 상태 색(`SetStatusTint`) > 원래 색. 짧은 발광 `FlashGlow`(90번).
- 적 상태: 오한 `EnemyChillState`(91번), 지속 피해 `EnemyDotState`(102+109), 슈가 중첩 `EnemySugarStack`(113번). 제어 지속 이펙트 `EnemyStatusVfx`.
- 전투방 재입장 시 적만 초기 상태로 재생성. 다른 방의 적은 방 내용물째 꺼져 있음(`FindObjectsByType` 기본값이면 지금 방 적 + 87번 아군).
- 넉백 `ForcedPush.Apply`(적도 밈) - 밀리는 동안 `ForcedPush.IsBeingPushed`면 AI가 이동 판단을 쉼. 돌진 중 판정 = `IDashDamageSource.IsDashing`.

### 레벨 생성

- `RoomGenerationConfig` → `LevelGraphGenerator` → `LevelBuilder.BuildRooms`, `RoomVisibilityManager`가 현재 방만 활성화. 방 = 한 변 `cellSize` 칸 하나.
  NavMesh는 방마다 런타임 베이크. 층 이동 = `LevelRoot` 통째 삭제 후 재생성. 방은 런타임 생성이라 정적 배칭 없음.
- 현재 방: `LevelBuilder.CurrentPlayerCell`/`GeneratedRooms`/`GetRoomController(id)`, 위치가 속한 방 = `ClaymoreOverride.RoomOf`/`WallPierceOverride.RoomOf`.
  방 클리어 `RoomController.OnAnyRoomCleared`(일반방/보스방만), 방 입장 `OnAnyRoomEntered`, `RequiresClear`. 전투 없는 방 = `SlotItemDropper.IsInCombatFreeRoom()`.
  상자 열림 공용 알림 `ContainerOpenEvents.OnAnyContainerOpened`(TreasureChest/ItemPedestal 처음 열 때만, 보관 좌대 X - 97번).
- 레벨 포탈 `LevelPortal`. Wall 태그로 방 벽 구분 불가(120번은 칸 경계로 판정). 런타임 방 추가 `TryAddExtraRoom`(16번), 텔레포트 `TryTeleportPlayerToRandomRoom`.
- 일반맵 보물상자 `NormalRoomTreasureSpawner`(내용물은 처음 열 때 정함 - 116번 `NormalChestItemGuaranteeOverride`면 아이템), 보스방 보상 `ResolveBossRewardContent`.
- **레벨 내 아이템 중복 등장 방지**: `GameFlowManager` `MarkItemAssignedThisLevel`/`FilterOutAlreadyAssignedThisLevel`/`UnmarkItemAssignedThisLevel`(상자/상점/48/56/62/80번
  공통, 다음 레벨에서 초기화). ID로 찾기 `ResolveItemById`.
- **아이템 등장 가중치**: 가중치로 뽑는 곳은 `ItemWeightModifier.GetWeight(item)` 사용(분류별 보정, 권장 weight 등급별 100/80/60/40).

### 특수방 (크레딧 더미 `credit_pile`만 제작됨) - 세부는 v1.46-v1.52 아카이브

- `RoomCategory.Special`(4) 하나로 통합(5, 6번 비어 있음). 종류 = 레이아웃 에셋 `SpecialRoomLayout` → Level Config `specialLayouts`. 항상 막다른 방.
- 진입 조건(Free/Grenade/HackingTool/Health) 추첨. 잠금 `SpecialRoomLock`(같은 도구 범위로 해제 - 118번 O, 81번 X), 체력 = 지날 때마다 0.5 피해(보호막이 먼저 막음).
  10/26번 = 잠금 자동 해제, 37번 = 체력 피해 막음.

### 상점 (편의점 `convenience_store`만 제작 중) - 세부는 v1.53-v1.57 아카이브

- 바닥 프리팹 안 `ShopNPC`(Shop Config) + 보관 좌대 `HoldingPedestal` 4개. 진열은 처음 열 때 뽑고 유지, 갱신 1 크레딧/최대 2회.
  가격 = `ShopNPC.GetUnitPrice(listing)`(`ShopPriceModifier`, 86번 `RegisterLevelFree`). 창 `ShopUI`(order 85). 보관 좌대 = 범용 좌대(9/106번 대상 아님).
- 상점 창 등급 이름 색(`ShopUI` popupTier 색)은 일부러 어둡게 - HUD/상자 빛 등급 색과 통일하지 않음.

### 팝업 / 공용 UI

- 공용 스타일 에셋 `PopupStyle`(`Resources/PopupStyle`) - 버튼 평소/호버/글자 색 + **위험 버튼**(`dangerButtonColor` `E5484D`/호버 `B8383C`/글자 흰색,
  `GetDangerButtonColor`) - 되돌릴 수 없는 동작 버튼에 사용. 모든 검은 팝업/흰 창 버튼이 씀, **새 UI도 이 에셋 값 사용**. 둥근 사각형 `UIRoundedBox`.
- 팝업 공통 패턴: 자동 생성 `DontDestroyOnLoad` 호스트, Overlay Canvas + CanvasScaler 1920x1080, `RectTransformUtility` 마우스 판정(EventSystem에 의존 X), 열린 동안 조작
  컴포넌트를 끔, 여는 프레임 입력 무시. static `IsOpen` + `BlocksInteract`(열려 있거나 방금 닫힌 프레임) - `InteractionTargetSelector`/`HUDClickBlocker`/
  `PauseMenuUI`/`SlotItemDropUI`/`HoldRestart`가 확인함. **새 팝업을 만들면 이 다섯 곳에 조건 추가**.
- 연출이 있는 전체 화면 창(ESC 중단 화면/도감)은 실제 시간 기준 + 같은 곡선(smoothstep) - 떠오름 `riseDistance` 30, 닫힘 0.35초.
- 슬롯 교체 확인창 `SlotReplaceConfirmUI`(order 90). 사용 불가 이유 알림 창 = `PassiveRerollUI.ShowMessage(플레이어, 문구)`.
- **머리 위 아이콘 연출 범용** `CreditPopupDisplay`(Item54effect.cs 안, order 8): `Show(대상, 발밑 위치, 설정, 대체 아이콘, 따라다님, onStarted)` - 대상별 줄 세우기,
  방 입장 시 건너뜀, 획득 연출과 순서 조정. 성공/실패 알림 공용 `HeadAlertRoomRules`(39/103/111번).
- 스탯 패널 강제 연출 `PlayerStatDisplay.RequestFlash(스탯, 유리한지)`.

### 슬롯 아이템 (장신구 / 일회용 Q / 음식 E) - 세부는 v1.53-v1.57 아카이브

- `PlayerTrinket`/`PlayerDisposableItem`/`PlayerFoodItem`: 먼저 얻은 것이 앞, `SlotCount` = 1 + `AddExtraSlots`, 꽉 차면 가장 먼저 얻은 것이 교체.
- 일회용/음식은 앞칸만, `CanUse` false = 소모 X, 지연형 = `IsPendingAt`/`CompletePendingUse`/`CancelPendingUse`. 사용 연출 `ItemUsePopup.Show`(실제 소모 순간 +
  85/93번 재현 때 재현한 아이템 아이콘 - 즉시형은 사용 순간, 지연형은 발동 순간, 대기 취소면 X), 직전 소모 재현 `LastUsedFood`/`LastUsedDisposable`(93/85번),
  버리기 Ctrl 0.8초 → `SlotItemDropUI`.

### 아이템 시스템

- 패시브: `ItemDefinition.specialEffects`(`[SerializeReference] IPassiveItemEffect` - `[Serializable]` + 매개변수 없는 생성자, 에셋의 효과 인스턴스를
  공유하므로 런타임 필드는 `[NonSerialized]`, `Apply`는 먼저 이전 구독 해제), 훅 `OnProjectileSpawned`/`OnShotFired`/`Projectile.OnDespawning`.
  "보유 중" 상시 동작은 런타임 러너 MonoBehaviour(지울 땐 `DestroyImmediate`). 정적 알림을 구독하는 효과는 플레이어가 사라지면 스스로 구독 정리(97번).
- **총알 피해 계산** `Projectile.Damage` = (기본 + `AddDamageBonusProvider`) x `AddDamageMultiplierProvider` → 최소 피해 하한 → 23번 추가 피해. `OnDespawning`
  구독자가 도는 동안 `Damage`는 충돌 순간 값으로 고정. 튕김 횟수 `BounceCount`/`WallBounceCount`/`EnemyBounceCount`. 명중 피해 = (`Damage` + `PlayerBulletBonusDamage`)
  → `EnemyHealth.TakeDamage`에서 받는 피해 배율을 맨 마지막에 곱함.
- **총알 1발 = 적 1명**(소멸 확정 총알은 무시, 관통처럼 `CancelDespawn`한 총알만 계속 맞힘). 명중 알림 `OnPlayerBulletHitEnemy`/`OnPlayerBulletHitEnemyDetailed`.
- **관통**: 적 = `SetEnemyPiercing`(99번), 벽 = `SetWallPiercing`(120번), 둘 다 = `SetPiercing`(110번). 벽 관통 총알은 처음 있던 방의 칸을 벗어나면 소멸.
- **상극** `ItemDefinition.incompatibleItemIds` + `ItemCompatibility`(99↔69, 120↔35). 시너지 감시 `SynergyWatcher`(102+109). 확률 판정 창구 `LuckOdds.Roll`.
- **패시브 잃기** `IRemovablePassiveEffect.Remove`(새 패시브 효과는 반드시 구현). `PlayerInventory.RemoveItem`/`ReplaceItems`/`TryAcquireItem(item, ignoreOneTimeGrantLimit)`.
  잃을 때 기본 스탯은 최대 체력만 남고, 특수효과로 얻은 스탯/1회성 재화/보호막은 남음. 보유 패시브 변화 알림 `OnPassiveItemsChanged` + `PassiveCount`.
- **1회 지급 규칙**: `GameFlowManager.TryConsumeOneTimeGrant("{ID}_용도")`(게임 한 판 내내 유지, `StartNewGame`에서만 초기화). 새 사본 획득은
  `GameFlowManager.AcquireItemCopy(item, 새 사본인지, 획득)` = 매번 지급, 맞바꾼/버린 것을 다시 주우면 지급 X. 48/56번은 `IgnoreOneTimeGrantLimit`로 항상 지급.
  GameFlowManager가 없는 곳(테스트 씬)은 경고만 남기고 지급. "레벨당 1회"라고 적힌 옛 주석/툴팁이 남아 있을 수 있음(반복 작업 [25]).
- 액티브(Space) `IActiveItemEffect`/`IDelayedActiveItemEffect`, `IActiveItemUseRules`, 음식 `IFoodEffect`, 일회용 `IDisposableItemEffect`, 장신구 `ITrinketEffect`.
  상태 있는 효과 재실행은 `JsonUtility` 복제.
- 구현된 아이템(정확한 목록은 `Assets/Script/Item`, 상세 기획은 각 `ItemNN effect.cs` 주석 - 다른 채팅 진행분은 그 채팅 정리 때 반영): 패시브 2, 4, 7, 8, 10, 11, 18, 19, 21, 23, 24, 26, 30, 35,
  36, 37, 38, 39, 41, 43, 47, 54, 55, 57, 58, 61, 64, 65, 66, 67, 68, 69, 72, 76, 77, 78, 79, 81, 91, 94, 97, 99, 101, 102, 104, 105, 109, 111, 113, 115, 117, 118, 120, 137 /
  음식 9, 13, 14, 27, 34, 40 / 액티브 16, 29, 31, 74, 85, 87, 93, 106, 112 / 일회용 17, 22, 25, 28, 48, 56, 62, 80, 86 / 장신구 20, 32, 45, 103, 110, 116.
  (4번 폭발 효과 클래스 `ExplodeOnImpactEffect`는 `Item04effect.cs` 안에 있음.)
- 아이템별 확정값: 10~86번은 v1.53-v1.57 아카이브, 87~137번은 v1.59 아카이브 + 각 `ItemNN effect.cs` 주석.
- v1.60 확정값: 58번 패시브 - 획득 시 해킹툴 2(1회 지급 `{id}_hack_grant`), 보유 중 해킹툴 1개 소모 순간 보호막 +1(연출은 해킹 발동 순간), 잃어도 보호막 유지 /
  97번 패시브 - 보유 중 정당한 상자(일반/동아리방/보스방/특수방 받침대/9번 복제)를 처음 열 때 보호막 +1(보관 좌대 X, 얻은 뒤 연 상자부터).
- v1.61 확정값: 67번 음식 가중치 보정 +100(에셋 값). 93번 사용 시 재현한 음식 아이콘을 머리 위에 띄움(85번과 같은 방식).
- 플레이어 총알 색: 등록소 `ProjectileColorOverride` + `BulletColorEffect`(23번 색이 최우선). 38번 = `BA55D3`, 102+109 = 초록.
- ItemDefinition 에셋은 복제해서 만들어도 됨 - `Item Id` 변경 + ItemDatabase 등록 필수(도감도 ItemDatabase 기준). 상점에 나오게 하려면 Appearance Contexts에 `Store`.
- 정적 등록소: `CreditGainBonusOverride`/`TreasureChanceBonusOverride`, `SplitShotOverride`(19), `SpreadShotOverride`(137), `WallBounceOverride`(35),
  `WallPierceOverride`(120), `RandomBulletDamageOverride`(23), `ProjectileColorOverride`, `PlayerBulletBonusDamage`(113), `GrenadeSelfDamageOverride`/
  `GrenadeVisualOverride`(57), `GrenadeHomingOverride`(77), `GrenadeHackOverride`(66), `IncendiaryGrenadeOverride`(81), `GrenadeSplitOverride`(92),
  `ChargeShotOverride`(110), `HealBoostOverride`(101), `ClaymoreOverride`(115), `NormalChestItemGuaranteeOverride`(116), `ItemWeightModifier`/`ShopPriceModifier`
  (67 - 분류별, 공용 계산 `CategoryModifierSet`). 각각 해제 메서드 있음.
- 확정값: 4번 반경 2.5, 19번 ±30도 2발(탄약 1). 7번 잠든 적은 피격으로 안 깸.
- 상자 공통: `ItemPedestal` 통과 가능, 범위 = 거리. 뚜껑 분리 도구 `MobArchive/보물상자 뚜껑 분리` + `ChestLidOpener`. 상자 위 빛 `ItemGlowSpawner`(등급 색).
- 등급 색(HUD 설명칸 이름/상자 빛/사용 연출) = Tier0 흰 / Tier1 청록 / Tier2 노랑 / Tier3 자홍(`PlayerItemInfoDisplay` 코드 기본값도 같음).

### 전투 연출

- `DamagePopupDisplay`(아군이 맞을 땐 숫자 X), `EnemyHealthBarDisplay`(보스 제외, 아군 = 초록), `CombatVfxSpawner`(총구/피격/벽/처치 슬롯 - 보호막이 막을 피격은
  적 피격 이펙트 X).
- 이펙트 모양 조정 = 원본 프리팹을 `Assets/VFX/Custom/`에 복제해 직접 수정. 원본 팩 재질은 수정 X. 크기 칸 = 파티클 시작 크기/속도에 곱함(`ApplyVfxScale`).
  대상을 따라가는 반복 이펙트는 따로 만들어 위치만 따라감(38번/78번).
- 110번 관통 충전 이펙트 칸 `piercingImplosionPrefab`, 피격 굴절 `ChargedHitRipple`(메뉴 `MobArchive/VFX/110번 피격 굴절 재질 만들기`). 91번 메뉴 `MobArchive/VFX/91번 오한 이펙트 만들기`.
- 탄환 꼬리 `BulletTrail`(설정 `Resources/BulletTrailSettings`) - Trail `SetPositions`는 쓰지 말 것.

### HUD

- `PlayerHUDManager`의 단일 Canvas(1920x1080) 위에 각 스크립트가 패널 생성, 라이브 트윅. 대부분 `PlayerHUD` 프리팹(`Assets/Config/Level/PlayerHUD.prefab`) 안 -
  값 바꾸면 Overrides → Apply All. 도감/테스트 씬도 같은 HUD 프리팹 사용.
- 글자 = TMP, 글꼴 `Assets/Resources/Fonts/경기천년제목_Bold SDF`(**이름 바꾸면 `HUDFont.GetTMP()`가 못 찾음**).
- 구성: 초상화(실시간 - [SD 신규 캐릭터]) + 체력 칸, 스탯(`Playerstatdisplay`), 액티브+코스트, 우측 슬롯, 인벤토리(넘치면 휠 스크롤), 자원(`PlayerResourceDisplay`), 탄약(+ 무기 모드 보조 상자), 미니맵, 보스 체력바, 설명칸, 보물상자 정보, 장전 표시, F 안내, 툴팁(`ItemTooltipDisplay`).
- 체력 칸 `PlayerHeartDisplay`: 항상 한 줄, 5칸(`fullSizeSlotCount`)까지 원래 크기, 넘으면 전체 폭·간격 그대로 칸 폭만 줄임(10칸 = 꽉 참), 최대 10칸. 하트 스프라이트
  9-slice(Border 8, `heartBorderThickness`). 보호막 바 = 하트 위 하늘색(`UI_PlayerShield_Full` (183,233,251)) 바 + 개수만큼 흰 칸선, 항상 하트보다 위에 그려짐.
- 스탯 표기는 `StatDisplayFormat` 한 곳, 새 스탯 한글 이름은 `ItemInfoPanelView.GetStatDisplayName`. 패널 접기 `CollapsibleHUDPanel`(Tab), 클릭 차단 `HUDClickBlocker`.

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
  (`PauseMenuUI.CanOpenNow`) 확인.
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

## 확인 필요 항목 (해결되면 지울 것)

- [96] 외곽선 2차 보정(볼록 모서리만 + 크기별 두께) 확인 - 컴파일 후 `MobArchive/외곽선/배경 모델 외곽선 노멀 다시 굽기` 실행, 작은 소품 두께/창문 음각 확인,
  `OutlineWidthSettings` 기준 크기·최소 비율 조절. 음각에 선이 남으면 외곽선 깊이 밀기(Z Offset) 조절칸 추가 검토.

## 할 일 / 보류 (나중에 만들 것, 참조 메모)

- [48] 행운(Luck) 스탯 - 필드만 존재, 확률 반영 미구현. 반영 창구 = `LuckOdds`(지금 106번만 거침). 54번 처치 확률(25%), 80번 개수 확률(50/30/20)도 이 창구로 옮김.
- [49] 미니맵 마커 아이콘 8종 + 포탈 아이콘 스프라이트 연결.
- [54] 등급 스프라이트 오른쪽 "태그 UI" 자리 남아 있음.
- [78] (참조만) 특수방 진입 비용 변형(수류탄 3개 등) 레벨별 난이도로 검토.
- [79] 특수방 개수 규칙(Level1 Special 필수 1개 - 확인용)/레이아웃 가중치 조정 예정 + 테스트 레이아웃 정리 필요.
- [81] 비어 있는 방 번호 5, 6 - 나중에 새 방 종류에 사용.
- [82] (보류, 참조만) 이어하기(저장 슬롯 1개, 사망 시 삭제) - 세부는 v1.58 아카이브, 저장 자리는 `PauseMenuUI.LeaveScene()`(그만하기만).
- [87] **다음 작업 후보 - SD 외형 아이템 시스템 + 외형 설정 창**(가장 최근 외형으로 바뀜, 설정 창에서 슬롯별 선택, 머리/옷/눈 색 바꾸기, 런타임 `Equip` 파츠를
  `PlayerHitFlash` 목록에 추가). 이후 파츠 모델링: 모자 → 머리 장신구 → 옷 변형 → 장갑 → 신발. 세부는 v1.58 아카이브.
- [88] (보류) `SDPartAttacher` Skeleton Root에 캐릭터 밖 오브젝트가 들어가면 무시하고 자동 탐색하는 실수 방지 장치.
- [94] 시작 화면 [설정] 버튼은 아직 빈 기능.
- [95] (형빈 모델링) 골목 끝 바닥 틈 - `Field_Player`(바닥면)가 `Field_Upper`와 별개 메시 + 1.091632 스케일 미적용이라 맞닿은 모서리에 1픽셀 미만 틈
  (좌표는 일치). Reset XForm 후 재수출 예정(재질이 달라 Attach X). 급하면 `Field_Player_Wall`에 바닥 1~2cm 아래 받침판(Quad + 아스팔트 재질).

## 변경 로그 (버전당 한 줄)

- v1.4~v1.57: 기반 구축 ~ 신규 SD 캐릭터(각 버전 구간 아카이브 참고).
- v1.58: 아이템 87(적 포섭 아군)/101/103/104/105/112/115/117, 사격·장전 알림 + 즉시 장전, 회복 배율, 무기 모드 0개 진입, 적 접촉 트리거·적 탄환 버그 수정.
- v1.59: 아이템 62/74/91/94/99/102+109/106/111/113/116/118/120/137, 머리 위 알림 줄 세우기 + 성공/실패 알림, 110번 굴절 셰이더, 적 상태 색/공격 속도 등록소,
  스탯 강제 연출.
- v1.60: 무기 모드 자원 표시, 최대 체력 상한 10 + 한 줄 체력 칸, 보호막 + 아이템 58/97, 실시간 초상화, 아이템 도감, ESC 중단 화면 도감/재시작, R 꾹 누르기 재시작.
- v1.61: 프로젝트 지식 정리(스크립트 사본 전부 제거, 아카이브 v1.26~v1.58 → 장치 Docs 폴더), 주석 정책 + 설계노트, 확인 필요/할 일·보류 분리, 93번 사용 연출,
  등급 이름 색 기본값 통일, 배경 외곽선 보정(볼록 모서리 노멀 굽기 + 크기별 두께).
