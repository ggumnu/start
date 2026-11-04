# 블아이작 (Mob_archive) - 핵심 상태 요약 (v1.58)

## 이 문서의 규칙 (항상 지킬 것)

- 이 문서는 **"지금 무엇이 맞는지"만** 담음. 설계 배경/버그 진단/세션 흐름은 적지 않음 - 그건 각 스크립트 상단 한국어 주석과
  아래 아카이브에 있음(필요할 때 `project_search`로 검색).
- 갱신은 형빈이 명시적으로 요청할 때만(세션 중간에 임의로 건드리지 않음). 갱신할 때는 기존 줄을 고치거나 한두 줄만 추가하고,
  지난 내용(예전 방식/버전별 경위)은 지움.
- **크기 상한 30,000자**: 넘으면 그 시점 문서를 새 아카이브로 그대로 보관하고, 이 문서를 다시 짧게 정리.
- **변경 로그는 버전당 한 줄**(무엇을 했는지만, 파일 목록/버그 경위 X).
- 확인 필요 항목은 `[N]` + 불릿 표기(마크다운 순서 목록 `1.` 금지 - 뷰어가 번호를 다시 매겨 번호 혼선 사고가 있었음).
  해결되면 지움. 반복 작업 목록은 별도.
- 아카이브는 한 번 만들면 절대 수정/삭제하지 않음.

## 아카이브 목록

- `claude/claude_Mob_archive_진행상황_통합요약_v1.26.md` - v1.4~v1.26 전체 히스토리(적 AI, 던전 생성, HUD, 아이템 카테고리, 레벨 전환,
  보물상자, 미니맵 등의 설계 배경 + 버그 진단).
- `claude/claude_Mob_archive_핵심상태_아카이브_v1.27-v1.45.md` - v1.27~v1.45 핵심 상태 문서 원본(제어 체계/수류탄·해킹/애니메이션 RootFixed·
  Generic 경위, 툰 셰이더 도입, TMP 전환 등).
- `claude/claude_Mob_archive_핵심상태_아카이브_v1.46-v1.52.md` - v1.46~v1.52 핵심 상태 문서 원본(상점 UI/흐르는 이름 세부, 특수방 세부,
  비주얼/애니메이션/HUD 세부 수치 - 이 문서에서 줄인 내용의 원문).
- `claude/claude_Mob_archive_핵심상태_아카이브_v1.53-v1.57.md` - v1.53~v1.57 핵심 상태 문서 원본(아이템별 확정값 전체, 상점 가격/진열 세부,
  슬롯 아이템 세부).

## 작업 방식

- 형빈은 3D 모델러(코딩 X), Claude가 C# 전량 담당. 답변/안내는 항상 한국어.
- 장치마다 작업 폴더가 다르고 git(`저장하기.bat`)으로 동기화 - 연결된 폴더는 세션마다 확인.
- 여러 채팅이 동시에 같은 프로젝트를 작업할 수 있음 - 기존 파일을 고치기 직전에 항상 다시 스테이징해 최신본 기준으로 편집
  (예전 outputs 사본과 `cmp`해서 다르면 다른 채팅이 고친 것 - 그 최신본을 기준으로).
- 장치 파일 작업: 스테이징 → 클라우드 편집 → `expectedMtimeMs`와 함께 커밋. 커밋할 때마다 outputs 아래 **새 하위 폴더** 사용 - 복사는 커밋과
  같은 단계에서 병렬로 하지 말고 먼저 끝낸 뒤 커밋, 커밋 후 다시 스테이징해 `cmp`(이미지는 픽셀 비교)로 확인(옛 내용이 커밋되는 사고가 여러 번
  있었음 - 확인에서 다르면 다시 커밋).
- "분석부터"/"계획부터"/"바로 진행하지 말고"라고 하면 코드 없이 분석/계획만.
- 착수 전: 기획 이해 + 코드 확인 결과 + 계획 + 정할 점 제시, 승인 후 코딩. 형빈이 미리 "짚을 점 없으면 바로 진행"이라 한 턴은 예외.
  기획 문구에 모순(예: "25% 감소"와 다른 예시 숫자)이 있으면 두 해석을 숫자로 보여주고 고르게 함.
- 코드는 항상 완성된 전체 파일로 전달. 기존 스크립트 수정은 "어느 파일의 무엇을" 먼저 밝히고 승인 - 파일 수 최소화보다 안정성 우선.
- 설계 전 실제 프리팹/코드/에셋을 먼저 확인. 프리팹 구성(콜라이더/태그)에 의존하는 판정은 피함. 이펙트 문제는 프리팹 YAML(파티클 수/수명/
  `moveWithTransform` 등)과 에셋 값을 직접 읽고 원인을 짚은 뒤 Inspector 수정 위치를 안내. 프리팹 참조가 이상하면 YAML에서 다른 guid를 가리키는
  참조(다른 프리팹 파일의 오브젝트)부터 확인. 수치가 이상하면 EnemyDefinition 에셋 값과 프리팹 컴포넌트 순서(Awake 순서)도 YAML로 확인.
- **플레이어 찾기는 `FindFirstObjectByType<PlayerHealth>()`(루트)** - "Player" 태그는 자식에도 붙어 있음(위치만 쓸 땐 태그 OK).
- 이름 변경 시 Assets 전체(.asset/.prefab/.unity)를 대소문자 무시로 검색. 직렬화 필드 이름을 바꿀 땐 `[FormerlySerializedAs]`.
  `[SerializeReference]` 효과 클래스는 클래스 이름으로 저장됨 - 이름을 바꿀 땐 `[MovedFrom]` + 에셋 강제 저장 후 보호 속성 제거.
  **스크립트 파일 이름 변경은 형빈이 Unity Project 창에서 F2로**(.meta 유지, 클래스명이 그대로면 에셋 연결 유지) - Claude는 내용만 고침.
- ScriptableObject 클래스는 파일 이름과 같은 파일에 따로 둠(다른 파일 안에 두면 에셋이 깨짐).
- 유니티 안내는 메뉴/버튼/인스펙터 필드명까지 클릭 단위로. 컴포넌트 찾기는 Hierarchy 검색 `t:컴포넌트이름`.
- 인코딩: 새 파일 = UTF-8(BOM) + CRLF. 기존 파일은 원래 인코딩/줄바꿈 보존 - CP949 남은 파일: `Playeraimandattack`/`Ipassiveitemeffect`/
  `Aimtrajectoryindicator`/`Statmodifier`/`Playerhitflash`/`Bossaicontroller`/`Roomobjectives`/`Enemystats`/`Realenemyspawner`/
  `Playerinventorydisplay`/`Playerhudmanager`/`Levelportal`/`Playeriteminfodisplay`/`Playerresourcedisplay`/`Ammoboxdisplay`/`AppearFromWhite` 등(고칠 때
  UTF-8 BOM으로 변환). UTF-8 무BOM + CRLF: `GameFlowManager`/`Treasurechest`/`Itempedestal`/`Normalroomtreasurespawner`/`PlayerDisposableItem`/
  `PlayerFoodItem`/`DisposableEffectContext`/`Treasurechestinfodisplay`/`HUDFont`/`Levelbuilder`/`Enemyhealth`/`Playerstats`/`Roomcontroller`.
  무BOM + LF: `PlayerResources`. `PlayerInventory` = BOM + CRLF. `Enemyradialattack` = BOM + LF.
  BOM 2개(그대로 보존): `ItemInfoPanelView`/`PlayerSlotItemDisplay`/`PlayerCostDisplay`. SD 스크립트/셰이더(v1.57)는 UTF-8 무BOM + LF.
- 요청 없는 버전 태그 주석/헤더 추가 금지. 기존 설계 주석은 유지.
- 스크립트 → 프로젝트 문서 동기화: 커밋할 때마다 즉시 `claude/<파일명>`에 + 채팅 끝 요청 시 장치 실제 파일 기준 재동기화(CP949는 UTF-8로).
  같은 스크립트의 옛 문서가 다른 경로(루트/대소문자 다른 이름)에 있으면 그 경로에 덮어쓰거나, 새로 만들었으면 옛 문서 삭제 여부를 형빈에게
  물어봄(`project_delete`로 Claude가 지울 수 있음). 장치 파일은 Claude가 못 지움 - 형빈에게 Project 창 삭제 안내.
- 토큰 절약: 폴더는 목록 먼저, 필요한 파일만 열기. 검증 로그는 요약만.
- 컴파일 사전 검사: mono `mcs` + Unity API stub(또는 `mcs --parse`). mono가 없고 dotnet SDK가 있으면 SDK 내장 Roslyn `csc.dll`로 구문 오류
  (`error CS1xxx`)만 검사(CP949 파일은 UTF-8 사본으로). 둘 다 없으면 눈 검토만 하고 형빈에게 알림.
- ScriptableObject 메뉴는 `MobArchive/<이름>`. 런타임 설정 에셋은 `Assets/Resources/<이름>` 자동 로드(없으면 기본값) 방식이 표준.
- 새 UI 글자는 TextMeshPro: `HUDText.AddTo(go, TextAnchor, wrap)`, 외곽선 `HUDText.SetOutline(text, 픽셀, 색)`(글자 크기 먼저).
- 월드 위치에 뜨는 아이콘/숫자 연출은 Screen Space Overlay 캔버스에 그림(모델에 안 가려짐).
- Inspector `OnGUI`/`PropertyDrawer` 안에서 에셋 검색을 매번 돌리지 말 것(렉) - static 캐시 + `EditorApplication.projectChanged`로 비우기.
- 빌드 함정: 런타임 `CreatePrimitive` 도형은 빌드에서 핑크 - URP 재질을 직접 입히거나 재질 붙은 프리팹 사용.
- 월드에 스프라이트 여러 장을 섞어 그릴 땐 SpriteRenderer 대신 MeshRenderer + 텍스처별 재질 + 스프라이트별 메시(`RabbitPopVfx` 방식).

## 프로젝트 개요

- Unity 6.3 LTS(URP) 3D 탑다운 로그라이트. 아이작 리펜턴스식 룸 진행/아이템 시너지 + 블루아카이브식 등각 카메라/캐릭터/아이템.
- 플레이어 모델: **신규 SD 캐릭터(`SD_Player`, 형빈 직접 모델링)**가 기본. SD 유니티짱(`Assets/UnityChan`, UCL 3.0)과 레이사(블아 SD)도 병행 유지.
  배포 X, GitHub 비공개, 포트폴리오는 영상/PNG만.
- 식별자: 샬레 = `Schale`, 청휘석 = `Pyroxenes`(표시는 한국어). 등급 `ItemRarity` = `Tier0`~`Tier3`. n.5 레벨 서쪽 선택방 = 게마트리아
  (`Gematria`, `gematriaChance`에 `[FormerlySerializedAs("blackSuitChance")]` 유지), 동쪽 = `Schale`. Variant ID는 에셋 데이터
  (`Appearancecontextdrawer`가 Config 에셋의 `variantId`를 읽음 - static 캐시). `ReloadTime` 표기 = "장전 시간"(작을수록 빠름).

## 확정 상태

### 게임 흐름

- 씬: `Title`(Build Profiles 0) + `LevelBuilder`(1). LevelBuilder 단독 Play도 동작.
- 전투 레벨 config는 `GameFlowManager.combatLevelConfigs`(Element 0 = 레벨 1)가 빌드 때마다 `LevelBuilder.config`에 덮어씀.
  레벨 = `CurrentLevelNumber` + `CurrentPhase`(Combat = n / Choice = n.5).
- `TitleScreenUI`: Canvas/배경/버튼 자동 생성. 게임 시작 = 로딩 + `gameSceneName` 로드, 설정 = 빈 `OnSettingsClicked()`, 나가기 = Quit.
- `LoadingScreen`(프리팹을 두 씬에 배치, `DontDestroyOnLoad`): 검은 배경 + 아이콘, order 1000, 최소 1초 + 착지 0.5초 + 페이드 0.2초(실제 시간),
  로딩 중 조작 차단. API: `RunLevelWork`, `LoadSceneWithLoading`, `StartSceneLoad`, `StartReloadActiveScene`, `CanLoadScene`, `IsShowing`.
- 사망: `PlayerHealth.Die()` → `OnDied`/`OnAnyPlayerDied`. `PlayerDeathHandler`(자동 부착)가 조작 끄고 총 모드 복귀. `GameOverUI`(order 100):
  즉시 `timeScale=0` + F 차단 → 2초 뒤 [예]/[아니오] 창(공용 팝업 스타일, F/Enter = 예, Esc = 아니오).
- 피격 알림 `PlayerHealth.OnDamageTaken(실제 피해)` - 사망/테스트 무적/무적 시간 확인을 통과해 체력이 깎인 뒤에만(78번). `IsInvincible` = 피격 무적 중.
- 다음 레벨 이동 확인창 `LevelTransitionConfirmUI`: 공용 팝업 스타일(둥근 모서리, 1920 기준 스케일), F/Enter = 예, Esc = 아니오.
- 게임 중단 화면 `PauseMenuUI`(설정 `Resources/PauseMenuSettings`): ESC → `timeScale 0` + 막 80% → [계속하기]/[그만하기]. Esc = 닫기(막이 걷힌 뒤 재개),
  Enter/[그만하기] = 로딩 + 타이틀. 다른 팝업/획득 연출/로딩/사망 중이면 안 열림.

### 비주얼 / 셰이더

- 조명/후처리(LevelBuilder/ItemTestArena 동일): Directional `F5F9FF` 1.6/Shadow 0.55/회전 (50,-30,0), 환경광 (200,218,240), Skybox
  `BG_ScreenSky`, SSAO 끔. Volume `SampleSceneProfile`: Neutral 톤맵, Bloom 1/0.25, Post Exposure 0.15/Saturation 8, Temperature -15.
- `MobArchive/Toon_Environment`(배경, 외곽선 Pass, DBuffer 데칼) / `MobArchive/Toon_Character`(셀 셰이딩, 림라이트, 외곽선) - 둘 다 URP/Lit
  프로퍼티 이름 호환(`_BaseMap`/`_BaseColor`/`_EmissionColor`/`_Surface`) - 새 셰이더도 이 이름 유지. SD 캐릭터 전용 셰이더 2개는 [SD 신규 캐릭터].
- 플레이어 재질 `Player_Mat/*`(FBX Remap), 적 재질 `Enemy_Mat`. 탄환 `Bullet_Player_Mat`(Unlit `FFE000`), 수류탄 `Grenade_Temp`.
- 일괄 교체 도구 `ToonShaderBatchConverter`(메뉴 `MobArchive/셰이더 일괄 교체`). 화면 고정 배경 `MobArchive/ScreenBackground`(임시 이미지).
- 세부 수치는 v1.46-v1.52 아카이브.

### 플레이어

- 관성 없는 CharacterController 이동, twin-stick 조준(우클릭 = 마우스 조준), 사거리 소멸 탄환, 무적 프레임 체력.
- 프리팹 3개(`Assets/Player/Character/Player`): `SD_Player` / `UnityChan_Player` / `Reisa_Player_Fixed` - **값을 바꿀 때는 세 프리팹 모두**.
  쓰는 프리팹은 `LevelBuilder`/`ItemTestArenaBuilder`의 Player Prefab 칸으로 교체.
- 비조준 자동 조준(`autoAimOnFire`): 좌클릭 → 조준 자세 → 발사 → 0.4초 뒤 복귀.
- `PlayerStats` 단일 소스: (기본 + 덧셈 합) x 곱셈 곱 → `statFloors`. 새 `StatType`은 **enum 맨 끝**에 + 매핑 추가.
  `RemoveModifiersFromSource`/`RemoveModifiersFromSourceExcept(id, 남길 스탯)`. 특수효과가 준 스탯은 출처 `PassiveEffectIds.EffectStatSourceId(id)`
  (= id + "_effect") - 기본 스탯과 따로 지워짐(잃어도 남는 "귀속" 스탯은 이 출처로). 잃으면 사라지는 실시간 스탯은 별도 출처(예: 76번 id + "_passiveCount").
- **공격력 하한선 = 고정값(Absolute) 1**(Player 프리팹의 `statFloors` 설정 - 코드 기본값 "런 시작 50%"가 아님). 다른 기본값에 공격력 등 아이템 효과만
  적용 = `PlayerStats.ApplyModifiersTo(StatType, 기본값)`(하한선 X - 87번 아군이 씀).
- 회복 배율 등록소 `HealBoostOverride`(101번 x2 - `Heal`/최대 체력 증가 회복에 곱함, 상점 최대 체력 구매 수량도 배율 반영).
- 사격 알림 `PlayerShooting.OnAmmoConsumed`(탄환 생성 전)/`OnAmmoRefunded`(실제 환급량)/`OnReloadStarted`, 즉시 장전 `TryInstantReload()`(장전 중이면 끝냄,
  110번 충전은 유지). 투명 `PlayerInvisibility`(112번 - `IsActive`/`Remaining01`, `_DitherFade` 프로퍼티가 있는 재질을 4x4 디더로 반투명 - SD 셰이더 2종에 있음).
- 시작 자원(수류탄/해킹툴/크레딧/청휘석)은 `PlayerResources`. 무기 모드 `PlayerWeaponMode` 1 총/2 수류탄/3 해킹 - 수류탄/해킹 모드는 0개여도
  들어갈 수 있고, 개수가 0이 되는 순간에만 총으로 자동 복귀.
- 수류탄 `PlayerGrenadeThrower`(곡사, 착지 폭발 + 자해 - `GrenadeSelfDamageOverride.IsBlocked`면 자해 건너뜀), 폭발 알림
  `OnGrenadeExploded(위치, 반경)`, 폭발 중심 = `Grenade.CurrentGroundPoint()`(77번 유도 `GrenadeHomingOverride`, 81번 소이탄
  `IncendiaryGrenadeOverride` → `IncendiaryFireZone`, 115번 크레모어 `ClaymoreOverride`). 해킹툴 `PlayerHackTool` - 공용 `ApplyHackArea(중심, 반경, 높이, 피해, 따라갈 대상)`/한 명
  `ApplyHackToEnemy(적)`, `OnHackDeployed`. 범위 이펙트 크기 = 설정 배율 x (실제 반경 / 기본 반경) `GetRadiusVfxFactor`. 스킬 스탯 `PlayerStats` 14/15번.
  수류탄 겉모습 등록소 `GrenadeVisualOverride`(우선순위 + 나중 것 우선). 세부는 v1.53-v1.57 아카이브.
- 아이템 획득 연출: `ItemPickupEvents.Raise` → `PlayerItemPickupPresenter`(연출 중 모든 행동 차단 `BlocksActions`). 여러 개를 순서대로 =
  `PassiveRerollUI.RaisePickupsInSequence`(56/80번).
- F 상호작용 = 가장 가까운 하나(`InteractionTargetSelector`) - 상자/동아리방 상자/포탈/상인/아이템이 올려진 보관 좌대. 팝업이 떠 있으면 대상 없음.
- 테스트: `PlayerTestProfile` → `ItemTestArenaBuilder.testProfile`, `testChestItems`로 아이템별 보물상자 자동 배치. 테스트 무적 중엔 피격 알림 없음.
  ItemTestArena는 방 클리어(`RoomController`)가 없음 - 방 클리어 아이템(72번)은 실제 레벨에서 테스트.

### 플레이어 애니메이션

- 애니메이션 FBX 15개(Mixamo, Humanoid), Controller `ReisaAnimatorController` 공유(SD/유니티짱 직접, 레이사는 Generic 복사본 + Override Controller).
- **지우면 안 됨**: `Reisa_AvatarSource.fbx`, `Animation/Generic/` 전체, `Animation` 원본 FBX, `Reisa_Fixed.fbx`, `Reisa_gun_Fixed.fbx`, `Assets/UnityChan`.
- 레이어: Locomotion / UpperBody / UpperBodyTorso(Humanoid만 - SD/유니티짱, `TorsoLayerWeightByState`). `HumanoidAimLookAt`(조준 IK), 반동 `UpperBodyRecoil`.
- RootFixed 사본 = 루트 커브를 상수로 고정한 FBX(상체 클립이 몸 전체를 흔드는 문제 해결용).

### SD 신규 캐릭터 (`Assets/Player/Character/Player/SD`)

- 알바디 `SD_AltBody_v02.fbx`(3ds Max 바이패드 `Bip001` - 척추 3/목 1/손가락 0, Humanoid, Scale Factor 1): `Body_Head`/`Body_Torso`/`Body_Hand_L·R`/
  `Body_Foot_L·R` + 얼굴 평면 `Body_Eye`/`Body_Mouth` - 조각마다 뼈 하나에 100% 스키닝(사지가 떨어져 떠다니는 각진 체형). 단독 프리팹
  `SD_AltBody_v02.prefab` = `SDCharacterSkin` + `SDPartAttacher` + `SDFaceController`.
- **`SD_Player`** = `UnityChan_Player` 복제 → `MeshRotationOffset` 아래 `SD_AltBody_v02.prefab`(Animator = `ReisaAnimatorController`, Root Motion 끔, Layer/
  Tag Player, `HumanoidAimLookAt`/`TorsoLayerWeightByState`), 총 `Reisa_gun_Fixed`는 `Bip001 R Hand`, 레이사 헤일로는 `Bip001 Head` 아래, 루트에
  `SDFaceGameEvents`. 루트의 Fire Point/Gun Model 등은 SD_Player 자신의 오브젝트를 가리켜야 함.
- 셰이더 `MobArchive/Toon_Character_SD`(Toon_Character 복사본 + 추가): 색 = 기본 텍스처 x (1 - 덮인 양 + Σ 마스크 채널 x 영역 색 R/G/B/A),
  `_WholeSkin`(머리/맨손 = 전부 피부색), `_SkinLinkR~A`(그 영역 = 피부색), 기본 텍스처 알파 0 = 피부(알파 잘라내기 끈 재질만). 텍스처엔 명암 X(셰이더 담당).
  얼굴 셰이더와 함께 `_DitherFade`(112번 투명 디더, 모든 Pass) 있음.
- 피부색 `SDCharacterSkin`(기본 `#FBEDE8`) - MaterialPropertyBlock으로 캐릭터마다 덮어씀(재질은 그대로), `SetSkinColor`, 파츠를 붙이면 `Apply()`.
- 재질 자동 설정 메뉴 `MobArchive/SD 캐릭터 재질 자동 설정`(`SDCharacterMaterialSetup`, 에디터): `SD/Textures`의 `T_SD_{부위}_Base`(sRGB, HQ) /
  `_Mask`(sRGB 끔, 압축 없음, 알파 투명도 끔) → `SD/Materials/M_SD_{부위}` 생성(처음만 Player_Body 툰 값 + 기본 영역 색) → SD 폴더 모든 FBX에 재질
  이름으로 Remap. 3ds Max 재질 이름 = `M_SD_{부위}`.
- 교체 파츠: `SDPartDefinition`(`MobArchive/SD 캐릭터/파츠 정의` - 슬롯 Hair/Hat/HairAccessory/Torso/Hands/Feet/Back + 파츠 FBX + 추가로 숨길 알바디) +
  `SDPartAttacher`(파츠 FBX의 본을 이름으로 캐릭터 본에 다시 연결, 캐릭터에 없는 전용 본 사슬은 부모 본 아래로 옮김, 슬롯별로 `Body_` 조각 숨김,
  시작 파츠는 Awake 순서 -100, `Equip`/`Unequip`/`GetEquipped`, Skeleton Root는 **비워둠**(자동 `Bip001`)). 파츠 FBX = 알바디와 같은 바이패드 +
  Skin, Rig **Generic**, Scale Factor 알바디와 같게.
- 지금 있는 것: 레이사 의상(토르소 R 외투 `#434E6B`/G 세일러 옷깃 `#F3F3F3`/B 상의·치마 `#CBD2DA`/A 리본·단추·선 `#30374B`, 목·배꼽 = 알파 피부;
  신발 R 본체 `#FBD5F8`/G 밑창·끈 `#F6F6F8`/B 포인트 `#4F96C8`/A 양말 `#F8F8FA`), 머리카락 `SDPart_Hair_Reisa01`(R 하늘 `#CFDDF6`/G 분홍 `#F3C8EB`/
  B 별 머리핀 `#B4A5EE`, 머리끈 구슬은 고정색). 텍스처는 Claude가 FBX를 직접 읽어 3D 디자인을 UV에 굽는 Python으로 생성 - 도구는 `claude/tools/sd_texture/`(README에 실행법).
- 얼굴: 시트 3x3 = 1800x1500(칸 하나 = 머리 정면 600x500, 1번 = 왼쪽 위), `SD/Textures/Face`. 눈 `T_SD_Eye_Reisa01`(색 바뀔 부분은 흰색/회색조) +
  `T_SD_Eye_Reisa01_Mask.tga`(32비트 알파, R 어두운 눈동자/G 밝은 눈동자/B 눈썹/A 속눈썹), 입 `T_SD_Mouth_Basic01`(마스크 없음). 시트 설정 =
  Alpha Is Transparency/Clamp/HQ. 재질 `M_SD_FaceEye`/`M_SD_FaceMouth` - 셰이더 `MobArchive/Toon_Face_SD`(칸 번호 `_Cell`, 반투명 섞기, 외곽선/
  그림자 드리우기 없음, 깊이 Offset, 머리와 같은 셀 셰이딩). 평면(3ds Max): 22.526 x 18.772, 중심 X 0/Z 36.366, 머리 앞면 Y -10.35 → 입 -10.45 / 눈 -10.5.
- 표정: 에셋 `SDFaceSet_Reisa01`(`SDFaceExpressionSet`, `MobArchive/SD 캐릭터/표정 세트`) - 기본 눈1/입1(깜빡임 O), 장전 3/1, 아이템 획득 4/4, 피격 5/5,
  사망 5/5, 우선순위 0/10/20/30/100. 깜빡임 = 눈 2번 칸, 2~5초마다 0.1초(깜빡임 허용 표정에서만). `SDFaceController`: `Hold`/`Release`/`Play(초)`/
  `ClearAll`, 머리 재질 명암 값 자동 복사, Test Mode(플레이 중 미리보기 - 평소 끔). `SDFaceGameEvents`(SD_Player 루트): 기존 스크립트 수정 없이
  `PlayerHealth.IsInvincible`(피격)/`PlayerItemPickupPresenter.IsPresenting`(획득)/`PlayerShooting.IsReloading`(장전)/`PlayerHealth.IsDead`(사망)를 읽어 Hold/Release.
- 피격 깜빡임(`PlayerHitFlash`)은 얼굴/머리카락 포함 전체가 함께 깜빡임(확인됨). 게임 도중 `Equip`한 파츠는 아직 깜빡임 목록에 안 들어감.

### 적 AI

- 근접 `EnemyAIController`(`IEnemyAttackBehavior`), 원거리 `EnemyRangedAIController` + `EnemyRangedAttack`, 보스 `BossAIController`.
- 적 탄환 = 코드 생성 구체 + `EnemyProjectile`(정적 목록 `Active`) - 한 발 = 한 번 명중(콜라이더 2개에 동시에 닿아도 1회).
- 적 수치: 공통 = `EnemyDefinition`(모든 적 Attack Power 1), 공격 방식별 = `attackConfig`(돌진/연속돌진 dashDamage, 사격 rangedDamage 0.5 등).
  접촉 트리거 `EnemyMeleeContactTrigger`는 공격 컴포넌트 `Initialize`에서 능력치를 받음(트리거를 아직 못 찾았으면 그 자리에서 찾음 - 프리팹 컴포넌트 순서 무관).
- **목표 정하기 `CombatTargeting`**: 적/아군 AI와 공격 컴포넌트는 Player 태그 대신 `Resolve`/`ResolveNow`/`CurrentTarget`/`TargetIsInvisiblePlayer`가 정한
  목표를 씀(`CombatTargetState` 자동 부착, 0.2초마다 재선택). 적 = 플레이어(112번 투명이면 제외)와 아군 중 가까운 쪽, 아군 = 가장 가까운 적(없으면
  플레이어 따라다니기). 투명 중엔 목표가 플레이어인 공격 "준비"만 취소(진행 중 돌진/연사는 끝까지). **새 적 AI/공격도 이 창구 사용**. 켜진 적 목록 `EnemyHealth.ActiveEnemies`.
- **87번 아군 `AllyFaction`**: 포섭 시 Enemy 태그 제거(태그로 대상을 고르는 플레이어 공격/효과는 자동 제외 - 태그를 안 보는 새 효과는 `AllyFaction.IsAlly`로 제외),
  방 클리어 목록에서 빠짐(`EnemyKillObjective.TryExclude` - 마지막 적이면 즉시 클리어), 상태 해제, 피해 x30 후 플레이어 공격력 효과 적용(하한 1, 매 프레임),
  체력 15당 0.5(가득 참), 피격 1초 무적 + 깜빡임, 하늘색 외곽선(`AllyOutline` 셰이더)/초록 체력바(맞을 때만)/하늘색 탄환. 아군↔적 접촉 피해 1초 간격
  (아군 돌진 중엔 안 받음), 아군 탄환은 적에게만. 아군 사망 = 처치 아님(그냥 사라짐), 아군이 죽인 적 = 플레이어 처치(처치 아이템 발동). 방 이동 =
  `AllyRoomTransfer`(LevelRoot 밑 `Allies (87)`로 옮김 → 방 입장 다음 프레임에 플레이어 곁 순간이동, 다음 층에서 LevelRoot와 함께 사라짐). 보스는 포섭 불가.
- 정적 등록소: `EnemyDetectRangeOverride`, `EnemySightOverride`, `EnemySpeedModifier`, `EnemyDamageTakenModifier`.
- 처치 이벤트 `EnemyHealth.OnAnyEnemyKilled(EnemyHealth, 위치)` - `Die()` 흐름 안 구독 코드는 try/catch로 감쌈.
- 제어 체계 `EnemyControlState`: 강력(수면/해킹) 1칸 + 일반(매료) 1칸, 강력 중엔 모든 AI 정지. 새 제어 = `EnemyControlType` 추가 + `GetTier()`.
  `TryApplyStrong`은 이미 걸린 같은 제어도 시간을 다시 채우고 true.
- 제어 지속 이펙트 `EnemyStatusVfx`(출처별 `EnemyStatusVfxSettings` - 해킹/31번 수면/38번 매료 `VFX_Charm_Status`), 잔여 입자 `StatusVfxParticleFader`,
  `ParticleSwayTilt`.
- 연출 `DissolveAndVanish`/`AppearFromWhite`. 전투방 재입장 시 적만 초기 상태로 재생성. 다른 방의 적은 방 내용물째 꺼져 있음
  (`FindObjectsByType` 기본값이면 지금 방 적 + 87번 아군이 잡힘).
- 넉백: `ForcedPush.Apply`가 적도 밈(켜진 NavMeshAgent면 `Move`, 꺼진 더미는 transform). 몸통 콜라이더로 훑어 Wall 태그 앞/NavMesh 가장자리에서
  정지, 한 걸음마다 `AfterStep` 콜백, `Stop()`. 밀리는 동안 `ForcedPush.IsBeingPushed`면 `EnemyAIController`/`EnemyRangedAIController`/
  `EnemyContactOnlyAttack`이 이동 판단을 쉼(충전 타이머는 공격 컴포넌트가 계속 진행). 돌진 중 판정 = `IDashDamageSource.IsDashing`.

### 레벨 생성

- `RoomGenerationConfig` → `LevelGraphGenerator` → `LevelBuilder.BuildRooms`, `RoomVisibilityManager`가 현재 방만 활성화. 방 = 한 변 `cellSize` 칸 하나.
  NavMesh는 방마다 런타임 베이크(방 Content의 `NavMeshSurface` - 방이 꺼지면 그 NavMesh도 사라짐). 층 이동 = `LevelRoot` 통째 삭제 후 재생성.
- 현재 방 찾기: `LevelBuilder.CurrentPlayerCell`/`HasPlayerCell`/`GeneratedRooms`(`RoomInstance`)/`GetRoomController(id)`. 방 클리어 이벤트
  `RoomController.OnAnyRoomCleared`(일반방/보스방만 발생), 방 입장 `OnAnyRoomEntered`(화면 전환 전에 발생 - 같은 방에서 다시 올 수 있음), `RequiresClear`
  (false = 처음부터 열린 방). `SlotItemDropper.IsInCombatFreeRoom()` = 전투 없는 방 판정 공용. 상자 열림 공용 알림 `ContainerOpenEvents.OnAnyContainerOpened`.
  이 레벨에 상점이 있는지 = `GeneratedRooms`에 `RoomCategory.Store` 방이 있는지(86번).
- 레벨 포탈 `LevelPortal` 프리팹. 바닥/벽 프리팹에 Wall 태그 장애물/투명 벽이 섞여 있음 - 태그로 방 벽 구분 불가.
- 런타임 방 추가 `TryAddExtraRoom(category)`(16번), 텔레포트 `TryTeleportPlayerToRandomRoom`(22/28/46번).
- 일반맵 보물상자 `NormalRoomTreasureSpawner`(자리 = NavMesh 샘플 + 겹침 검사), 보스방 보상 `ResolveBossRewardContent`.
- **레벨 내 아이템 중복 등장 방지**: `GameFlowManager`의 "이 레벨에서 나온 아이템" 기록 - 보물상자는 열 때, 상점은 진열할 때, 80번은 줄 때
  `MarkItemAssignedThisLevel`, 뽑을 때 `FilterOutAlreadyAssignedThisLevel`(상자/상점/48/56/80번 공통), 상점 갱신으로 사지 않고 사라진 아이템은
  `UnmarkItemAssignedThisLevel`로 기록에서 지움. 다음 레벨로 넘어가면 초기화(아래 1회 지급 기록과는 별개).
- **아이템 등장 가중치**: 아이템을 가중치로 뽑는 곳(일반 방 보물상자/`ItemPedestal`/상점/80번)은 `item.weight` 대신 `ItemWeightModifier.GetWeight(item)`
  사용 - 분류별 보정 등록소(더하기 합 → 곱하기, 에셋 weight 0 이하는 보정 안 함, 등장 위치는 안 바꿈). 권장 weight 등급별 100/80/60/40.
  48번 옛 가중치 뒤섞기(`NextItemPickWeightOverride`)는 지금 아무도 켜지 않음 - 켜지면 그 추첨엔 보정 없이 1~3.

### 특수방 (크레딧 더미 `credit_pile`만 제작됨)

- `RoomCategory.Special`(4) 하나로 통합(5, 6번 비어 있음). 종류 = 레이아웃 에셋 `SpecialRoomLayout`(`Assets/Config/SpecialRoom`), Level Config
  `specialLayouts`에 등록. 항상 막다른 방, 부모 = 시작/일반.
- 진입 조건(Free/Grenade/HackingTool/Health)은 레이아웃 `entryConditionPool`에서 방마다 추첨 → `RoomInstance.specialEntryCondition`. 공용 설정
  `SpecialRoomEntrySettings`. 수류탄/해킹툴 = 잠금 바리케이드 `SpecialRoomLock`(같은 도구 범위로 해제, 81번 불 구역으로는 안 열림), 체력 = 지날 때마다
  0.5 피해 + 가시 마커.
- 외형 `RoomVisualSet`(바닥 풀 + 벽 + 부모 쪽 입구 벽). 아이템 = Appearance Contexts에 Special + 레이아웃 ID(전용)/카테고리 전체(공용).
- 크레딧 더미: 보물상자 3개 + `PresetBasicItemChest`(2~5줄 각 25% 크레딧 +1). 10/26번 = 해당 잠금 자동 해제, 37번 = 체력 피해 막음(피격 판정 자체 없음).
- 세부(폴백 순서/마커 배치/9번 복제 조건)는 v1.46-v1.52 아카이브와 각 스크립트 주석.

### 상점 (편의점 `convenience_store`만 제작 중)

- 편의점 바닥 프리팹 안에 `ShopNPC`(Shop Config 연결) + 보관 좌대 `HoldingPedestal` 4개(리스트가 비면 같은 방에서 자동 탐색).
- 설정 `ShopConfig`(`Assets/Config/Level/ShopConfig_Store`): 소모품 칸(재고 무제한), 플레이어 아이템 후보 = `playerItemContexts`(기본 Store)에 맞는
  아이템, 개수 `min~maxPlayerItemCount`(1~3), 등급별 가격 범위 `rarityPrices`, 갱신 1 크레딧/최대 2회.
- 진열: 처음 열 때 한 번 뽑고 유지(최대 8칸). 플레이어 아이템은 가중치(`ItemWeightModifier`) 추첨, 가진 패시브/장착 슬롯 아이템/보관 좌대 아이템/
  이 레벨에서 나온 아이템 제외. 보물상자 추첨 코드/보정과 분리.
- 가격: 실제 가격 = `ShopNPC.GetUnitPrice(listing)`(플레이어 아이템 칸만 `ShopPriceModifier` 적용, 실시간). 레벨 한정 무료
  `ShopPriceModifier.RegisterLevelFree`(86번). 할인 표시/아이콘 교체 세부는 v1.53-v1.57 아카이브.
- 구매: 받을 수 있는지 전부 확인 후 재화 차감. 슬롯 아이템은 장착 + 원래 아이템은 빈 보관 좌대로(빈 좌대 없으면 불가), `confirmBeforeReplace`면 확인창.
  최대 체력 구매 수량 = 잃은 체력 ÷ (1개당 회복 x 101번 배율) 올림.
- 갱신: 플레이어 아이템 칸 전부 다시 뽑음. 이 상점에 한 번이라도 진열됐던 아이템은 이후 갱신에서 안 나옴(`everDisplayed`), 사지 않고 사라진 아이템은
  레벨 기록에서 지움. 후보 0이면 재화 안 쓰고 막음.
- 창 `ShopUI`(order 85): 카드 + 구매/갱신 팝업(공용 팝업 스타일), 흐르는 이름(`ShopUI.Marquee`), 카드 호버 = 아이템 설명칸. 창이 떠 있는 동안
  게임은 계속, 조작 컴포넌트만 잠시 끔(일회용 Q도 못 씀).
- 보관 좌대 `HoldingPedestal`: 보물상자 시스템에 등록 안 하는 범용 좌대(9번 복제 대상 아님). F = 슬롯 아이템 맞바꾸기/패시브 획득, 미니맵 "Item"
  마커. 슬롯 공용 도우미(`IsSlotCategory`/`GetEquippedSlotItem` - 맞바꿀 때 교체될 아이템을 돌려줌/`IsSlotItemEquipped`/`TryEquipSlotItem`).

### 팝업 / 공용 UI

- 공용 스타일 에셋 `PopupStyle`(`Resources/PopupStyle`, `MobArchive/Popup Style`): 버튼 평소 `8FE2FD`/호버 `65ADC7`/글자 `343365`/못 누를 때 반투명,
  검은 창 = 검정 90% + 모서리 16, 버튼 모서리 10. 모든 검은 팝업(교체 확인/버리기/상점 구매·갱신/게임 오버/레벨 이동/56번 창)과 흰 창 버튼이 씀 -
  **새 UI도 이 에셋 값 사용**. 둥근 사각형 공용 도우미 `UIRoundedBox`(코드 생성 9-Slice).
- 팝업 공통 패턴: 자동 생성 `DontDestroyOnLoad` 호스트, Overlay Canvas + CanvasScaler 1920x1080, `RectTransformUtility` 마우스 판정(EventSystem X),
  열린 동안 조작 컴포넌트를 끔, 피격/사망 시 닫힘, 여는 프레임 입력 무시. static `IsOpen` + `BlocksInteract`(열려 있거나 방금 닫힌 프레임) - 이 값을 `InteractionTargetSelector`/`HUDClickBlocker`/
  `PauseMenuUI`/`SlotItemDropUI`가 확인함. **새 팝업을 만들면 이 네 곳에 조건 추가**.
- 슬롯 교체 확인창 `SlotReplaceConfirmUI`(order 90): `confirmBeforeReplace` 아이템 교체 시 F 예/Esc 아니오.
- 사용 불가 이유 알림 창 = `PassiveRerollUI.ShowMessage(플레이어, 문구)`(56/80/86번 공용).
- **머리 위 아이콘 연출 범용** `CreditPopupDisplay`(Item54effect.cs 안, Overlay order 8 - 아이콘 + 발광 + 빛 입자, 떠오름 0.25/유지 0.35/사라짐 0.35초):
  `Show(EnemyHealth, 위치, 설정)`(54번) / `Show(대상, 발밑 위치, 설정, 대체 아이콘, 따라다님)`(72/117번/사용 연출 등 어디서나). 설정 묶음 `CreditPopupSettings`
  (아이콘/크기/높이 상한/발광 색·크기·맥동/입자 색·수·크기·속도·수명).

### 슬롯 아이템 (장신구 / 일회용 Q / 음식 E)

- 컴포넌트 `PlayerTrinket`/`PlayerDisposableItem`/`PlayerFoodItem`: 목록(먼저 얻은 것이 앞), `SlotCount` = 기본 1 + `AddExtraSlots(출처, 수)`/
  `RemoveExtraSlots`, `GetEquippedAt(i)`, `IsEquipped`, `TryUnequip`, `ReplacedOnAcquire`. 칸이 꽉 찼을 때 얻으면 가장 먼저 얻은 것이 교체되고
  (확인 대상이면 확인창) 순서가 당겨짐(A,B + C → B,C). 칸이 줄어 넘치면 `LateUpdate`가 나중에 얻은 것을 바닥 좌대로 떨어뜨림(`AppearFromWhite`).
  같은 음식 중복 장착은 막혀 있음(유지 확정).
- 일회용/음식: 항상 앞칸만 사용, 지연형이 대기 중(`IsPendingAt`, 앞칸만)이면 Q/E가 아무것도 안 씀. `CanUse` false = 아무 일도 없음(소모 X).
  지연형 끝 = `context.CompletePendingUse(id)`(소모) / `context.CancelPendingUse(id)`(대기만 풀림, 아이템 남음). 일회용 획득 훅 `IDisposableAcquireEffect`.
  "막히면 창 + 소모 X"인 일회용은 지연형으로 만들고 `BeginPending` 안에서 바로 완료/취소(56/80/86번).
- 사용 연출 `ItemUsePopup.Show(플레이어, 아이템)`(실제 소모 순간 + 85번 재현, 설정 `Resources/ItemUsePopupSettings`), 음식 소모 알림
  `PlayerFoodItem.OnFoodConsumed`, 직전 소모 기록 + 재현 `LastUsedFood`/`LastUsedDisposable` + `CreateReplayContext`(93/85번, 보조 아이콘
  `PlayerCostDisplay.SyncAuxIcon`), 슬롯 HUD `PlayerSlotItemDisplay`(종류마다 2칸, 잠긴 칸 작고 반투명) - 세부는 v1.53-v1.57 아카이브.
- 버리기: Ctrl 0.8초 꾹 → `SlotItemDropUI`(여러 개 선택, 확인 대상이면 재확인) → `SlotItemDropper`가 보물상자 자리 기준으로 방 안에 놓음(`HoldingPedestal` +
  `DroppedItemPedestal`, 설정 `Resources/SlotDropSettings`). 전투 중인 방/팝업 중/사망이면 안 열림.

### 아이템 시스템

- 패시브: `ItemDefinition.specialEffects`(`[SerializeReference] IPassiveItemEffect` - `[Serializable]` + 매개변수 없는 생성자, 에셋의 효과 인스턴스를
  공유하므로 런타임 필드는 `[NonSerialized]`), 훅 `OnProjectileSpawned`/`OnShotFired`/`Projectile.OnDespawning`. "보유 중" 상시 동작은 런타임 러너
  MonoBehaviour(10/26/37/54/64번 - 지울 땐 `DestroyImmediate`).
- **총알 피해 계산** `Projectile.Damage` = (기본 + 덧셈 보너스 `AddDamageBonusProvider`) x 곱셈 배율 `AddDamageMultiplierProvider` → 최소 피해 하한 →
  23번 추가 피해(곱하지 않음). `UnclampedDamage` = 하한/23번 전 값. **`OnDespawning` 구독자가 도는 동안 `Damage`는 충돌 순간 값으로 고정**(획득 순서와
  무관 - 튕김 뒤 피해로 4번 폭발이 터지지 않게). 튕김 횟수 `BounceCount`(전체)/`WallBounceCount`(`Redirect(.., reset: true, ..)`)/
  `EnemyBounceCount`(`RedirectFromEnemy(방향, 사거리 배율, 맞힌 적)` - 그 적은 다른 적/벽 전까지 통과). 35번 표시 `HasWallBounceEffect`.
- **총알 1발 = 적 1명**: `Projectile.OnTriggerEnter`는 소멸이 확정된(`hasDespawned`) 총알이면 무시 - 아이템 없는 총알은 겹친 적의 겹친 부분을 맞혀도
  한 명만 맞음(관통처럼 `CancelDespawn`한 총알만 계속 맞힘).
- **패시브 잃기** `IRemovablePassiveEffect.Remove`(새 패시브 효과는 반드시 구현 - 등록소 해제/구독 해제/러너 제거). `PlayerInventory.RemoveItem`/
  `ReplaceItems(목록, ignoreOneTimeGrantLimit)`/`TryAcquireItem(item, ignoreOneTimeGrantLimit)`. 잃을 때 기본 스탯은 최대 체력만 남고, 특수효과로 얻은
  스탯(출처 id + "_effect")/1회성 재화는 남음. 보유 패시브 변화 알림 `PlayerInventory.OnPassiveItemsChanged`(획득/잃기 직후, 교체는 다 끝난 뒤 1번) +
  `PassiveCount` - 나중에 만들 "패시브 잃기" 효과도 `RemoveItem`/`ReplaceItems`를 쓰면 자동으로 알림이 감.
- **1회 지급 규칙**(획득 시 주는 재화/스탯): `GameFlowManager.TryConsumeOneTimeGrant("{ID}_용도")`, 기록은 **게임 한 판 내내 유지**(`StartNewGame`에서만
  초기화). 보물상자/좌대(새로 뽑힌 것)/상점 구매/80번으로 **새 사본**을 얻으면 `GameFlowManager.AcquireItemCopy(item, 새 사본인지, 획득)`가 그 아이템의
  `{ID}_` 기록을 잠시 풀고 획득 → 다시 잠금 = 매번 지급. 맞바꿔 상자/좌대에 들어간 것, 보관 좌대·버린 좌대의 것은 새 사본이 아니라 다시 주워도 지급 X
  (교체로 무한 복제 방지). 48/56번은 `IgnoreOneTimeGrantLimit`로 항상 지급. GameFlowManager가 없는 곳(테스트 씬)은 경고만 남기고 지급.
- 액티브(Space) `IActiveItemEffect`/`IDelayedActiveItemEffect`(실제 소모 `GetEffectiveCost`), 사용 조건/쿨다운 표시 `IActiveItemUseRules`
  (`CanActivateNow`는 코스트 차감 전), 음식 `IFoodEffect`, 일회용 `IDisposableItemEffect`, 장신구 `ITrinketEffect`. 상태 있는 효과 재실행은 `JsonUtility` 복제
  (85번은 에셋 참조 필드를 리플렉션으로 같이 옮김).
- 구현된 아이템(정확한 목록은 `Assets/Script/Item`, 상세 기획은 각 `ItemNN effect.cs` 주석 - v1.57 이후 다른 채팅 진행분은 빠져 있을 수 있음): 패시브 2, 4, 7, 8, 10, 11, 18, 19, 21, 23, 24, 26, 30, 35,
  36, 37, 38, 39, 41, 43, 47, 54, 55, 57, 61, 64, 65, 66, 67, 68, 69, 72, 76, 77, 78, 79, 81, 101, 104, 105, 115, 117 / 음식 9, 13, 14, 27, 34, 40 /
  액티브 16, 29, 31, 85, 87, 93, 112 / 일회용 17, 22, 25, 28, 48, 56, 80, 86 / 장신구 20, 32, 45, 103.
- 아이템별 확정값(10~86번 수치/조합 규칙)은 v1.53-v1.57 아카이브의 [아이템 시스템] 확정값 목록 + 각 `ItemNN effect.cs` 주석.
- v1.58 확정값: 101번 회복 x2 / 103번 공격력 x1.3, 그 방에서 피격되면 x0.7(방 입장 시 초기화, 입장 순간·특수방 진입 피해는 무시) / 104번 쏜 탄 1발당
  공격력 +3(덧셈, 장전 시작 시 초기화, 환급분 차감) / 105번 처치 시 즉시 장전 / 112번 투명 5초(투명 중 재사용 X, 적은 대기·공격 준비 취소) /
  115번 크레모어(착지 설치 후 R로 그 방 것만 기폭, 공중 기폭 X, 획득 시 수류탄 +2 1회, 잃으면 전부 기폭, 층 이동 시 소멸) / 117번 상자를 처음 열 때
  체력 1 또는 코스트 1 회복 / 87번 위 [적 AI]의 아군 항목.
- 플레이어 총알 색: 등록소 `ProjectileColorOverride` + 재사용 효과 `BulletColorEffect`(우선순위 높은 색, 같으면 나중 것, 23번 색이 최우선). 38번 = `BA55D3`.
- ItemDefinition 에셋은 복제해서 만들어도 됨 - `Item Id` 변경 + ItemDatabase 등록 필수. 상점에 나오게 하려면 Appearance Contexts에 `Store`.
- 정적 등록소: `CreditGainBonusOverride`/`TreasureChanceBonusOverride`(곱연산), `SplitShotOverride`(19), `WallBounceOverride`(35),
  `RandomBulletDamageOverride`(23), `ProjectileColorOverride`, `GrenadeSelfDamageOverride`/`GrenadeVisualOverride`(57), `GrenadeHomingOverride`(77),
  `GrenadeHackOverride`(66 보유 여부), `IncendiaryGrenadeOverride`(81), `HealBoostOverride`(101), `ClaymoreOverride`(115 - 방별/전체 기폭),
  `ItemWeightModifier`/`ShopPriceModifier`(67 - 둘 다 분류별, 공용 계산
  `CategoryModifierSet`: 출처 이름 + 소유자, (기본 + 더하기 합) x 곱하기 곱. `ShopPriceModifier`엔 86번 레벨 한정 무료도). 각각 해제 메서드 있음.
- 확정값: 4번 반경 2.5, 19번 ±30도 2발(탄약 1). 7번 잠든 적은 피격으로 안 깸.
- 상자 공통: `ItemPedestal`(동아리방/특수방 받침대) 통과 가능, 범위 = 거리. 뚜껑 분리 도구 `MobArchive/보물상자 뚜껑 분리` + `ChestLidOpener`.
  밀어내기 공용 `ForcedPush.Apply`(플레이어 + 적 - [적 AI] 참고).
- 상자 위 빛 `ItemGlowSpawner`(씬에 하나, 등급 색 Tier0 흰/1 파랑/2 노랑/3 보라 - `tier0~3Color`, Extra Layer `Disabled Child Names`로 부품 끄기 -
  세부는 v1.53-v1.57 아카이브).

### 전투 연출

- `DamagePopupDisplay`(아군이 맞을 땐 숫자 X), `EnemyHealthBarDisplay`(보스 제외, 아군 = 초록), `CombatVfxSpawner`(총구/피격/벽/처치 슬롯).
- 이펙트 모양 조정 = 원본 프리팹을 `Assets/VFX/Custom/`에 복제해 그 프리팹을 직접 수정(스크립트엔 프리팹 칸 + 위치만). 원본 팩 재질은 수정 X.
  크기 칸이 필요하면 파티클 시작 크기/속도에 곱하는 방식(`ApplyVfxScale` - 4번/68번/81번 불꽃/수류탄 폭발 공통). 대상을 따라가는 반복 이펙트는 대상의
  자식이 아니라 따로 만들어 위치만 따라감(38번/78번) - 하트가 생긴 자리에 남게 하려면 그 파티클 Simulation Space World, 같이 움직이려면 Local.
- 탄환 꼬리 `BulletTrail`(설정 `Resources/BulletTrailSettings`, 템플릿 `Trail_Bullet`/`Trail_Grenade`) - 사라질 때 남은 꼬리를 LineRenderer로 넘겨
  둥글게 소멸, 꼬리 색 = 붙는 순간의 탄환 재질 색. Trail `SetPositions`는 쓰지 말 것.

### HUD

- `PlayerHUDManager`의 단일 Canvas(1920x1080) 위에 각 스크립트가 패널 생성, 라이브 트윅. 대부분 `PlayerHUD` 프리팹 안 - 값 바꾸면 Overrides → Apply All.
- 글자 = TMP, 글꼴 `Assets/Resources/Fonts/경기천년제목_Bold SDF`(**이름 바꾸면 `HUDFont.GetTMP()`가 못 찾음**). TMP 취소선/밑줄 굵기·위치는 글꼴
  에셋 값이라 바꾸면 모든 글자에 영향 - 따로 조절할 줄은 이미지로 그림.
- 구성: 초상화+체력 칸(2:1 스프라이트, `PlayerHeartDisplay` full/half/empty 스프라이트), 스탯 패널(`Playerstatdisplay`), 액티브+코스트(+보조 아이콘),
  우측 슬롯(`PlayerSlotItemDisplay`), 인벤토리, 자원(`PlayerResourceDisplay` - 수류탄/해킹툴/크레딧/청휘석 아이콘), 탄약, 미니맵, 보스 체력바, 설명칸,
  보물상자 정보(`TreasureChestInfoDisplay` - 체력/코스트 등 기본 아이템 아이콘), 장전 표시, F 안내, 툴팁(`ItemTooltipDisplay`).
- 스탯 표기는 `StatDisplayFormat` 한 곳, 새 스탯 한글 이름은 `ItemInfoPanelView.GetStatDisplayName`.
- 패널 접기 `CollapsibleHUDPanel`(Tab 일괄), 미니맵 휠 확대/드래그. 클릭 차단 `HUDClickBlocker.BlocksLeftClick`.

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
- [10] 복제로 만든 ItemDefinition은 `Item Id`를 새 번호로 + ItemDatabase 등록.
- [11] 총알 색 아이템 = Special Effects에 `BulletColorEffect`. 새 제어 지속 이펙트 = 프리팹 복제 + 출처 효과에 설정 칸 + `Attach`.
- [12] 새 패시브 효과는 `IRemovablePassiveEffect.Remove` 구현(48/56번으로 잃을 수 있음). 1회성 지급은 `TryConsumeOneTimeGrant` + `IgnoreOneTimeGrantLimit`
  예외 처리.
- [13] 아이템 테스트 씬(LevelBuilder 없음)에서 48/56/80번을 쓰려면 아이템 에셋 Disposable Effect의 `Item Database` 칸을 채움.
- [14] 새 팝업 UI = `PopupStyle` 값 사용 + `IsOpen`/`BlocksInteract`를 위 네 곳에 연결 + ESC 겹침(`PauseMenuUI.CanOpenNow`) 확인.
- [15] 새 아이템 획득 경로(상자/좌대/상점 같은 것)를 만들면: 새로 뽑힌 사본이면 `GameFlowManager.AcquireItemCopy(item, true, ...)`로 감싸고,
  가중치 추첨은 `ItemWeightModifier.GetWeight`, 상점식 가격은 `ShopNPC.GetUnitPrice` 사용. 새 수류탄 계열 자해/구역 피해 판정엔
  `GrenadeSelfDamageOverride.IsBlocked` 확인.
- [16] 스스로 이동하는 새 적 AI/공격 컴포넌트를 만들면 강력 제어 검사 옆에 `ForcedPush.IsBeingPushed(gameObject)` 검사도 추가(68번 넉백이 상쇄되지 않게).
  수류탄 폭발 위치를 쓰는 새 코드는 착지점(`LandingPoint`) 대신 `OnGrenadeExploded`의 위치를 사용(77번 유도로 달라짐).
- [17] 새 총알 피해 효과: 원래 피해에 더하는 건 `AddDamageBonusProvider`, 최종 피해에 곱하는 건 `AddDamageMultiplierProvider`. 튕김/관통처럼 `OnDespawning`에서
  방향을 바꾸는 효과는 `Redirect`(벽)/`RedirectFromEnemy`(적)를 써서 횟수가 따로 세지게.
- [18] 새 SD 파츠: 3ds Max에서 알바디 바이패드로 Skin(Figure Mode) + 재질 이름 `M_SD_{부위}` + 빈 더미 오브젝트 삭제 → FBX Rig Generic/Scale Factor
  알바디와 같게 → 텍스처 `T_SD_{부위}_Base`/`_Mask`를 `SD/Textures`에 → `MobArchive/SD 캐릭터 재질 자동 설정` → 파츠 정의 에셋에 FBX 넣고 Save Project.
  얼굴 시트 이름은 `_Base`로 끝내지 않음(마스크만 `_Mask`).
- [19] 몸에 붙이는 물건(총/헤일로/장신구)은 `Body_` 메시가 아니라 **`Bip001` 뼈** 아래에(메시 오브젝트는 A포즈 자리에 고정). 다른 프리팹에서 컴포넌트를
  Copy/Paste하면 오브젝트 참조 칸(Fire Point/Gun Model 등)이 원본 프리팹을 가리킴 - 붙여넣은 뒤 Hierarchy에서 다시 연결.
- [20] 적을 대상으로 하는 새 플레이어 효과(아이템/스킬)는 대상을 "Enemy" 태그로 고르거나 `AllyFaction.IsAlly`로 87번 아군을 제외. 새 적 공격/AI는
  `CombatTargeting`으로 목표를 정하고, 아군 쪽 피해(접촉/탄환)도 아군·적 양쪽으로 맞게 처리.

## 확인 필요 항목 (해결되면 지울 것)

- [34] `PlayerInventoryDisplay` 스크롤용 씬 `EventSystem` 존재 확인.
- [48] 행운(Luck) 스탯 - 필드만 존재, 확률 반영 미구현. 반영할 때 54번 처치 확률(25% 고정), 80번 개수 확률(1/2/3개 = 50/30/20, 보류 - 참조만)도 대상.
- [49] 미니맵 마커 아이콘 8종 + 포탈 아이콘 스프라이트 연결.
- [54] 등급 스프라이트 오른쪽 "태그 UI" 자리 남아 있음.
- [67] 무기 모드 표시 HUD 없음 - 필요하면 `PlayerWeaponMode.OnModeChanged` 구독.
- [73] `PlayerItemInfoDisplay.cs` 등급 이름 색 코드 기본값이 씬 값과 다름 - 고칠지 형빈 결정 대기.
- [77] 하드 엣지 모델 외곽선이 모서리에서 끊길 수 있음 - 거슬리면 부드러운 노멀 굽기 또는 후처리 외곽선.
- [78] (참조만) 특수방 진입 비용 변형 아이디어 - 수류탄 3개/해킹툴 2개 등, 레벨별 난이도로 검토.
- [79] 특수방 개수 규칙(Level1 Special 필수 1개 - 확인용)/레이아웃 가중치 조정 예정 + 테스트 레이아웃 정리 필요.
- [81] 비어 있는 방 번호 5, 6 - 나중에 새 방 종류에 사용.
- [82] (보류, 참조만) **이어하기**: 사망 시 저장 삭제, 저장 슬롯 1개, 강제 종료돼도 마지막 자동 저장으로 이어함, 전투 중이 아니면 지금 방으로 /
  전투 중이면 그 방에 들어가기 전으로. 저장 자리는 `PauseMenuUI.QuitToTitle()`에 표시됨.
- [84] 1회 지급 규칙이 "게임 한 판 + 새 사본마다"로 바뀜 - "레벨당 1회"라고 적힌 옛 주석/툴팁(인터페이스들, 10~66번 지급 아이템 등) 일괄 정리 남음.
  동작은 이미 새 규칙.
- [85] 67번 음식 가중치 보정값은 +50으로 적용 중 - 음식이 4개뿐이라 +80~+100도 검토 가능(일반방 1회 음식 확률 +50 = 13.8%, +100 = 18.1%).
  아이템 에셋 값만 바꾸면 됨.
- [86] 25번이 음식 대신 소모될 때 사용 연출(25번 아이콘)이 뜨는지, 85번으로 86번을 재현하면 "이미 사용" 창이 뜨는지 플레이 확인 필요.
  93번(직전 음식 재발동)에도 사용 연출을 넣을지 형빈 결정 대기.
- [87] **다음 작업 - SD 외형 아이템 시스템 + 외형 설정 창**: 외형 아이템은 가장 최근 것으로 바뀜, 설정 창에서 보유 외형을 슬롯별로 선택(아이템 효과는
  유지), 나중에 외형 고정 기능. 색 바꾸기(머리/옷/눈 - 눈은 어두운 눈동자 색을 고르면 밝은 색 자동 + 밝은 색도 따로 선택). 런타임 `Equip` 파츠를
  `PlayerHitFlash` 목록에 넣는 처리 포함. 이후 파츠 모델링 순서: 모자 → 머리 장신구 → 옷 변형 → 장갑 → 신발(무기는 당분간 소총만, 기존 총 사용).
- [88] (보류) `SDPartAttacher` Skeleton Root에 캐릭터 밖 오브젝트(FBX 원본 등)가 들어가면 무시하고 자동 탐색하는 실수 방지 장치.
- [89] `SD_Player`의 조준 보정(`HumanoidAimLookAt` Hand Offset 등)/FirePoint 높이/획득 아이콘 높이(Icon Height)/반동은 유니티짱 값 그대로 - 영상용으로
  어색하면 조정.
- [90] 87번 아군이 있는 전투방을 클리어 전에 나갔다 오면(보통 문이 잠겨 불가) 방 초기화로 포섭했던 적이 적으로 다시 생김 - 실제로 문제 되면 처리.
- [91] 장치에 빈 `Assets/Script/Item/AllyMarkerDisplay.cs`(87번 옛 머리 위 아이콘) 남아 있으면 형빈이 Project 창에서 삭제.

## 변경 로그 (버전당 한 줄)

- v1.4~v1.26: 카메라/적 AI/던전 생성/HUD/아이템 카테고리 기반 구축(상세는 v1.26 아카이브).
- v1.27~v1.45: 문서 구조 전환, HUD 확장, 수류탄/해킹/제어 체계, 사망/게임 오버, 애니메이션, 툰 셰이더, 유니티짱, 시작/로딩 화면 등(v1.27-v1.45 아카이브).
- v1.46~v1.52: 편의점 상점 + 보관 좌대, 특수방 통합 + 크레딧 더미, 흐르는 이름, 아이템 10/13/26/28/37/54, 수면/매료 지속 이펙트, 탄환 꼬리,
  아이템별 총알 색(v1.46-v1.52 아카이브).
- v1.53: 문서 정리(v1.46-v1.52 아카이브, 슬림 재작성) + 아이템 14/17/23/48(전체 교체)/55/56/61/79, 패시브 잃기 체계, 슬롯 2칸 확장 + 잠긴 칸 표시,
  Ctrl 슬롯 아이템 버리기, 공용 팝업 스타일, 게임 오버/레벨 이동 창 개편, ESC 게임 중단 화면, 레벨 내 중복 방지에 48/56/상점 갱신 규칙 반영.
- v1.54: 아이템 57(자해 무효 + 흰 수류탄/토끼 팝콘)/64/65/66(수류탄 해킹)/67(음식 가중치 + 할인), 1회 지급 규칙 개편(게임 한 판 기록 + 새 사본마다
  지급, 교체 복제 차단), 공용 해킹 처리 + 범위 비례 이펙트 크기, 분류별 가중치/상점 가격 보정 등록소 + 할인 가격 표시.
- v1.55: 아이템 68(총알 넉백 + 충돌 피해)/76(패시브 수 비례 공격력)/77(수류탄 착지 후 유도)/80(랜덤 패시브 1~3개)/85(직전 일회용 재현), 적 넉백
  (`ForcedPush` 적 지원 + AI 일시정지), 총알 1발 1적 판정, 보유 패시브 변화 알림, 일회용 직전 소모 기록/재현 컨텍스트, 보조 아이콘 호버 설명칸,
  93번 코스트 10, AI 컨트롤러 3종 UTF-8 변환.
- v1.56: 아이템 69(적 연쇄 튕김)/72(방 클리어 체력·코스트)/78(피격 시 공격력 + 하트)/81(소이탄 불 구역)/86(레벨 한정 상점 음식 무료), 35번 피해 곱연산
  전환, 총알 곱셈 피해/튕김 횟수 분리/소멸 처리 중 피해 고정, 머리 위 아이콘 연출 범용화 + 일회용·음식 사용 연출(등급 색), 한 명 해킹 처리,
  상점 레벨 한정 무료 + 가격 옆 아이콘 교체, 상자 빛 가운데 과발광 해결(Extra Layer 부품 끄기).
- v1.57: 신규 SD 캐릭터 - 알바디(바이패드 Humanoid) + 레이사 의상/머리카락 텍스처, SD 전용 셰이더 2종(색 영역 마스크 + 피부색 연동 / 얼굴 시트),
  재질 자동 설정 메뉴, 파츠 교체 구조, 표정 시스템(눈/입 시트 + 깜빡임 + 게임 상황 연결), `SD_Player` 프리팹으로 플레이어 교체.
- v1.58: 아이템 87(적 포섭 아군 - 목표 정하기 체계, 아군↔적 피해, 방 이동, 공격력 연동)/101/103/104/105/112(투명 + 디더)/115(크레모어)/117, 사격·장전 알림 +
  즉시 장전, 회복 배율, 무기 모드 0개 진입, 적 접촉 트리거 연결 순서 버그·적 탄환 중복 명중 수정, 공격력 하한선(고정 1) 문서화.
