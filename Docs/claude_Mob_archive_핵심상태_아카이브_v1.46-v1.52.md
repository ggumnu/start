# 블아이작 (Mob_archive) - 핵심 상태 요약 (v1.52)

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

- `claude/claude_Mob_archive_진행상황_통합요약_v1.26.md` - v1.4~v1.26 전체 히스토리(적 AI, 던전 생성, HUD 1~7단계, 아이템
  카테고리 신설, 레벨 전환, 보물상자, 미니맵 등의 설계 배경 + 버그 진단).
- `claude/claude_Mob_archive_핵심상태_아카이브_v1.27-v1.45.md` - v1.27~v1.45 핵심 상태 문서 원본(세션별 상세 변경 로그, 제어 체계/
  수류탄·해킹/애니메이션 RootFixed·Generic 경위, 툰 셰이더 도입, TMP 전환, 삭제된 확인 항목 원문 등).

## 작업 방식

- 형빈은 3D 모델러(코딩 X), Claude가 C# 전량 담당. 답변/안내는 항상 한국어.
- 장치마다 작업 폴더가 다르고 git(`저장하기.bat`)으로 동기화 - 연결된 폴더는 세션마다 확인.
- 여러 채팅이 동시에 같은 프로젝트를 작업할 수 있음 - 기존 파일을 고치기 직전에 항상 다시 스테이징해 최신본 기준으로 편집.
- 장치 파일 작업: 스테이징 → 클라우드 편집 → `expectedMtimeMs`와 함께 커밋. 커밋할 때마다 outputs 아래 **새 하위 폴더** 사용,
  커밋 후 다시 스테이징해 `cmp`로 내용 확인 + 폴더 재조회로 `-1` 중복 파일 확인(텍스트/이미지 모두 - 캐시/OneDrive 되돌림 사고 있었음).
- "분석부터"/"바로 진행하지 말고"라고 하면 코드 없이 분석/설계만.
- 착수 전: 기획 이해 + 코드 확인 결과 + 계획 제시, 결정할 게 있으면 **같은 턴에 질문 도구**, 승인 후 코딩. 형빈이 미리
  "없으면 바로 진행"이라 승인한 턴은 예외. 큰 기능은 단계로 나눠 단계마다 에디터 테스트 후 다음 단계.
- 코드는 항상 완성된 전체 파일로 전달. 기존 스크립트 수정은 "어느 파일의 무엇을" 먼저 밝히고 승인 - 파일 수 최소화보다 안정성 우선.
- 설계 전 실제 프리팹/코드/에셋을 먼저 확인. 프리팹 구성(콜라이더/태그)에 의존하는 판정은 피함.
- **플레이어 찾기는 `FindFirstObjectByType<PlayerHealth>()`(루트)** - "Player" 태그는 플레이어 프리팹 자식(`FirePoint`/
  `MeshRotationOffset`/모델)에도 붙어 있어 태그로 찾으면 자식이 잡혀 `PlayerResources` 등을 못 찾음(위치만 쓸 땐 태그 OK).
- 이름 변경 시 Assets 전체(.asset/.prefab/.unity)를 대소문자 무시로 함께 검색(에셋 문자열/직렬화 필드 이름이 남음).
  직렬화 필드 이름을 바꿀 땐 `[FormerlySerializedAs]`로 기존 값 보존. `[SerializeReference]` 효과 클래스 이름을 바꿀 땐
  `[MovedFrom]` + 에셋 강제 저장 후 보호 속성 제거(31번에서 검증).
- 유니티 안내는 메뉴/버튼/인스펙터 필드명까지 클릭 단위로. 컴포넌트 찾기는 Hierarchy 검색 `t:컴포넌트이름`.
- 인코딩: 새 파일 = UTF-8(BOM) + CRLF. 기존 파일은 원래 인코딩/줄바꿈 보존 - CP949: `Playeraimandattack`/`Playershooting`/
  `Projectile`/`Ipassiveitemeffect`/`Aimtrajectoryindicator`/`Statmodifier`/`Playerhitflash`/AI 컨트롤러 3종/`Playerinventorydisplay`/
  `Playerhudmanager`/`Levelportal`/`Playeriteminfodisplay`/`Playerresourcedisplay`/`Ammoboxdisplay`/`Leveltransitionconfirmui`/
  `AppearFromWhite` 등(고칠 때 UTF-8 BOM으로 변환 - `Minimapdisplay`/`Appearancecontext(drawer)`/`Playermovement`/`Roomdata`는 이미
  변환됨). `PlayerResources` = UTF-8 무BOM + LF. `Gameflowmanager`/`Treasurechestinfodisplay`/`HUDFont`/`Levelbuilder` = UTF-8 무BOM +
  CRLF. `ItemInfoPanelView` = BOM 2개(그대로 보존).
- 요청 없는 버전 태그 주석/헤더 추가 금지. 기존 설계 주석은 유지.
- 스크립트 → 프로젝트 문서 동기화: 바꿀 때마다 즉시 + 세션 끝 요청 시 로컬 실제 파일 기준 재동기화(CP949는 UTF-8로 변환).
- Claude는 장치 파일을 지울 수 없음 - 안 쓰는 파일은 형빈에게 Project 창 삭제 안내.
- 토큰 절약: 폴더는 목록 먼저, 필요한 파일만 열기. 검증 로그는 요약만.
- 컴파일 사전 검사: mono `mcs` + Unity API stub(`default` 리터럴/`??=`는 mcs만 못 씀 - 그대로 둠, 제네릭 지역 함수는 private static으로).
- ScriptableObject 메뉴는 `MobArchive/<이름>`.
- 새 UI 글자는 TextMeshPro: `HUDText.AddTo(go, TextAnchor, wrap)`, 외곽선 `HUDText.SetOutline(text, 픽셀, 색)`(글자 크기 먼저).
- 런타임 UI 틀/버튼은 스프라이트 칸 + 비었을 때 코드로 만든 흰 둥근 사각형(9-Slice, `pixelsPerUnitMultiplier = 32/반지름`)에 색을
  입히는 방식(상점 창/교체 확인창/아이템 설명칸 공통). 스프라이트를 넣으면 원래 색 그대로(버튼은 Hover/Disabled Tint만 곱함).
- 월드 위치에 뜨는 아이콘/숫자 연출은 Screen Space Overlay 캔버스에 그림(모델에 안 가려짐 - `DamagePopupDisplay`/54번 크레딧 팝업).
- Inspector `OnGUI`/`PropertyDrawer` 안에서 `AssetDatabase.FindAssets` 같은 에셋 검색을 매번 돌리지 말 것(렉) - static 캐시 +
  `EditorApplication.projectChanged`로 비우기.
- 빌드 함정: 런타임 `CreatePrimitive` 도형은 빌드에서 핑크 - URP 재질을 직접 입히거나 재질 붙은 프리팹 사용.

## 프로젝트 개요

- Unity 6.3 LTS(URP) 3D 탑다운 로그라이트. 아이작 리펜턴스식 룸 진행/아이템 시너지 + 블루아카이브식 등각 카메라/캐릭터/아이템.
- 플레이어 모델: 기본 SD 유니티짱(`Assets/UnityChan`, UCL 3.0), 레이사(블아 SD)도 병행 유지. 배포 X, GitHub 비공개, 포트폴리오는
  영상/PNG만.
- 식별자: 샬레 = `Schale`, 청휘석 = `Pyroxenes`(표시는 한국어). 등급 `ItemRarity` = `Tier0`~`Tier3`. n.5 레벨 서쪽 선택방 =
  게마트리아(`Gematria`, `gematriaChance`에 `[FormerlySerializedAs("blackSuitChance")]` 유지), 동쪽 = `Schale`. Variant ID는 에셋 데이터
  (`Appearancecontextdrawer`가 Config 에셋의 `variantId`를 읽음 - 카테고리별 static 캐시, 1초 경과 또는 프로젝트 변경 시 다시 검색).
  `ReloadTime` 표기 = "장전 시간"(작을수록 빠름).

## 확정 상태

### 게임 흐름

- 씬: `Title`(Build Profiles 0) + `LevelBuilder`(1). LevelBuilder 단독 Play도 동작.
- 전투 레벨 config는 `GameFlowManager.combatLevelConfigs`(Element 0 = 레벨 1)가 빌드 때마다 `LevelBuilder.config`에 덮어씀 - 테스트
  config는 GameFlowManager 쪽 칸에 넣어야 적용됨.
- `TitleScreenUI`: Canvas/배경/버튼 자동 생성(라이브 트윅). 게임 시작 = 로딩 + `gameSceneName` 로드, 설정 = 빈
  `OnSettingsClicked()`, 나가기 = `Application.Quit()`.
- `LoadingScreen`(프리팹을 두 씬에 배치, `DontDestroyOnLoad` 싱글턴): 검은 배경 + 갸우뚱 아이콘, sortingOrder 1000, 최소 1초 +
  착지 대기 0.5초 + 페이드 0.2초(실제 시간). 로딩 중 조작 차단(`PlayerMovement`는 `BlocksPlayerInput`이면 이동 입력만 0).
  API: `RunLevelWork`, `LoadSceneWithLoading`, `StartSceneLoad`, `StartReloadActiveScene`, `CanLoadScene`, `IsShowing`. 씬에 없으면 기존 동작.
  뜨는 때: 게임 시작/다음 레벨(`GameFlowManager.AdvanceToNextPhase`)/게임 오버 [예] 재시작/[아니오] 시작 화면.
- 사망: `PlayerHealth.Die()` → `OnDied`/`OnAnyPlayerDied`. `PlayerDeathHandler`(자동 부착)가 조작 전부 끄고 총 모드 복귀, Animator
  UnscaledTime + `IsDead`. `GameOverUI`(sortingOrder 100): 즉시 `timeScale=0` + F 상호작용 차단 → `showDelay` 2초 뒤 [예]/[아니오].

### 비주얼 / 셰이더

- 조명/후처리(LevelBuilder/ItemTestArena 동일): Directional `F5F9FF` 1.6/Shadow 0.55/회전 (50,-30,0), 환경광 Color (200,218,240),
  Reflections 0, Skybox `BG_ScreenSky`, SSAO 끔. Volume `SampleSceneProfile`(두 씬 공유): Neutral 톤맵, Bloom threshold 1/0.25,
  Vignette 끔, Post Exposure 0.15/Saturation 8, Temperature -15. Forward+, DBuffer 데칼.
- `GameCamera` 프리팹: Background Skybox, Post Processing 켬.
- `MobArchive/Toon_Environment`(배경): 2단 명암, 색 그림자, 빛은 색만 반영(밝기는 `_Brightness`), DBuffer 데칼, 외곽선 Pass
  (배경 전체 켬, `Building_Mat 1~4`는 2.5), 노멀 세기 0.4.
- `MobArchive/Toon_Character`(플레이어/적): 셀 셰이딩, `_ShadeAmount`(얼굴/눈입 0), 림라이트, 외곽선(화면 픽셀 기준 2).
- 두 툰 셰이더는 URP/Lit과 프로퍼티 이름 호환(`_BaseMap`/`_BaseColor`/`_EmissionColor`/`_Surface` 등) - 피격 색/투명 전환 코드가
  그대로 동작. 새 셰이더도 이 이름 유지.
- 플레이어 재질: `Player_Mat/Player_Body·EyeMouth·Face·Hair·Weapon`(FBX Remap). 적 재질은 `Enemy_Mat` 별도.
- 탄환/수류탄: `Bullet_Temp` = `Bullet_Player_Mat`(Unlit 노랑 `FFE000`, 아이템 총알 색은 아래 [아이템 시스템]), 수류탄 `Grenade_Temp`
  프리팹(`Grenade_Mat` 툰 `1F4D1F`), 적 탄환 = 코드 생성 Unlit 빨강. 빛 번짐은 Bloom 정상 동작(유지 확정).
- 일괄 교체 도구 `ToonShaderBatchConverter`(메뉴 `MobArchive/셰이더 일괄 교체`): URP/Lit 재질 → 툰, FBX 내장 재질은 Extract 후.
- 화면 고정 배경 `MobArchive/ScreenBackground`(`BG_ScreenSky`): 화면 좌표로 그림, 배경 이미지 cover + 패턴 타일. 지금은 임시
  `BG_SkyGradient.png` + `BG_DiamondPattern.png` - 최종 이미지는 재질의 이미지만 교체.
- `SpriteFlash.shader`와 아이콘/이펙트/UI 셰이더는 툰 적용 대상 아님.

### 플레이어

- 관성 없는 CharacterController 이동, twin-stick 조준(우클릭 = 마우스 조준), 사거리 소멸 탄환, 무적 프레임 체력.
- 프리팹 2개(`Assets/Player/Character/Player`): `UnityChan_Player` / `Reisa_Player_Fixed` - LevelBuilder(테스트는
  ItemTestArenaBuilder) 플레이어 칸으로 교체. 로직 컴포넌트 동일 - **값을 바꿀 때는 두 프리팹 모두**.
  유니티짱 구조: 루트 → `MeshRotationOffset`(Y -20) → `SD_unitychan_humanoid`(0.6, `HumanoidAimLookAt`/`TorsoLayerWeightByState`),
  총 `Reisa_gun_Fixed`는 `Character1_RightHand` 아래 + `MuzzleDirection`.
- 비조준 자동 조준(`PlayerAimAndAttack.autoAimOnFire`): 좌클릭 → 조준 자세(`AimBlend` 1 + 0.08초, 최대 0.4초) → 발사 → 0.4초 뒤 복귀.
  캐릭터는 이동 방향을 봄, 조준선은 우클릭 때만.
- `PlayerStats` 단일 소스: (기본 + 덧셈 합) x 곱셈 곱 → `statFloors`(기본 Ratio 0.5). 새 `StatType`은 **enum 맨 끝**에 추가 +
  `GetBaseFieldValue`/`SetBaseFieldValue` 매핑. `EnsureAllStatFloors()`가 하한선 줄 자동 보충.
- 연사는 발수만큼 탄약 소모(부족하면 중단). 시작 자원(수류탄/해킹툴/크레딧/청휘석)은 `PlayerResources`.
- `AimTrajectoryIndicator`(방향 최대 2 x 튕김 구간 배열, `LineRenderers`), `PlayerHitFlash`(0.08초/흰색) - 플레이어 자식 표시용
  오브젝트는 Start에서 만들면 점멸 대상에서 자동 제외.
- 무기 모드 `PlayerWeaponMode`: 1 총/2 수류탄/3 해킹(자원 0이면 불가, 다 쓰면 총 복귀), Animator Int `WeaponMode`, `gunModel` 숨김
  (수류탄 켬/해킹 끔 - 오브젝트 SetActive). 투척/해킹 동작 중엔 전환 보류, 손 떠난/발동 뒤엔 1/2/3으로 끊고 전환 가능.
- 수류탄 `PlayerGrenadeThrower`: 우클릭 조준 + 좌클릭, 곡사(`flightTime` 0.6/`arcHeight` 2), 착지 즉시 폭발(원기둥, 적당 1회) + 자해,
  방 밖 지점은 칸 중심 ± (cellSize/2 - 1)로 잘라냄. 클릭 시 소모 → `releaseDelay` 뒤 발사 → `throwDuration`까지 연타 금지.
  프리팹 칸이 비면 URP Unlit 구체(`fallbackSphereColor`).
- 해킹툴 `PlayerHackTool`: 좌클릭 즉시(적 없어도 소모) → `deployDelay` 0.2 뒤 발동 → `deployDuration` 1. 반경 내 적 해킹 데미지 +
  강력 제어, 같은 원기둥 안 적 총알 제거(`clearEnemyBullets`, `EnemyProjectile.ClearInRadius`, 이펙트 `bulletClearVfxPrefab`).
- 스킬 스탯(`PlayerStats` 14/15번 그룹): 수류탄 데미지 10/자해 1/반경 2.5/사거리 8, 해킹 데미지 5/범위 3/지속 2초.
- 장전: 총 모드가 아니게 되면 취소. `CurrentReloadDuration`이 UI/애니메이션 공통 기준.
- 아이템 획득 연출: 상자/보관 좌대/상점에서 플레이어 아이템을 얻으면 `ItemPickupEvents.Raise` → `PlayerItemPickupPresenter`(획득 클립 +
  머리 위 아이콘 플래시, 연출 중 모든 행동 차단 `BlocksActions`, 끝나면 총 모드).
- F 상호작용 = 가장 가까운 하나(`InteractionTargetSelector`, XZ 거리) - 상자/동아리방 상자/포탈/상인(`ShopNPC`)/아이템이 올려진
  보관 좌대(`HoldingPedestal`) 중에서 고르고, F 안내/설명칸 모두 이를 따름. 상점 창이 떠 있으면 F 대상 없음.
- 슬롯 교체 확인창 `SlotReplaceConfirmUI`(자동 생성, order 90): 장착 중 아이템의 `confirmBeforeReplace`가 켜져 있으면 F 예/Esc 아니오,
  게임은 안 멈추고 조작만 차단, 피격 시 아니오, `BlocksInteract`. 보물상자/보관 좌대/상점 구매 모두 이 창을 씀.
- 테스트: `PlayerTestProfile`(`Assets/Player/Character/Player`) → `ItemTestArenaBuilder.testProfile`(스탯/자원/무적/자원 무한 -
  `[NonSerialized]`, 자원 값은 각 칸 체크박스를 켜야 적용, 자원 무한은 크레딧 제외). `testChestItems`로 아이템별 진짜 보물상자 자동 배치.

### 플레이어 애니메이션

- 애니메이션 FBX 15개(`Assets/Player/Character/Animation`, Mixamo) = Humanoid, Controller `ReisaAnimatorController`를 두 캐릭터가 공유.
  레이사는 Generic 복사본(`Animation/Generic/*_Generic.fbx`, 아바타 원본 `Reisa_AvatarSource.fbx`) +
  `ReisaAnimatorOverride_Generic` + Avatar `Reisa_FixedAvatar`.
- **지우면 안 됨**: `Reisa_AvatarSource.fbx`, `Animation/Generic/` 전체, `Animation` 원본 FBX, `Reisa_Fixed.fbx`, `Reisa_gun_Fixed.fbx`,
  `Assets/UnityChan`. `Reisa_Player.prefab`/`Reisa.fbx`/`Reisa_gun.fbx`는 적 프리팹용으로 보류.
- 레이어: Locomotion(전신, Death 포함, IK) / UpperBody(마스크 `Generic/UpperAvatarMask_Combined`, Body 끔, IK) / UpperBodyTorso
  (Sync, 마스크 `TorsoAvatarMask`, Weight 0 - `TorsoLayerWeightByState`가 수류탄/해킹/획득 중에만 올림, 유니티짱만).
- 파라미터: IsAiming/MoveX/MoveZ/Speed/IsDead/WeaponMode/Throw·Hack·ItemPickup(Trigger)/ReloadSpeed/IsReloading/AimBlend/IsPickingUp.
- `HumanoidAimLookAt`(유니티짱): 조준 중에만 LookAt + 손 IK(총 내리기/팔 안정화) + 총구 정렬, 장전/투척/해킹/획득/사망 때 꺼짐.
- 사격 반동 = 코드 `UpperBodyRecoil`(`OnShotFired` 구독, Humanoid는 Chest 뼈, 레이사는 `Bip001 Spine1`, 프리팹 값 (-12,0,3)/0.5).
- 장전: Reloading 블렌드 트리(AimBlend 0/1 = NonAim/Aim RootFixed 사본), 속도 = 클립 길이 ÷ 장전 시간(`PlayerShooting.reloadClip`).
- RootFixed 사본(`Hack_RootFixed`, `Hack_Idle_RootFixed`, `Reloading_RootFixed_Aim/NonAim`, `ItemPickup_RootFixed`) = 루트 커브를
  상수로 고정한 FBX(상체 클립이 몸 전체를 흔드는 문제 해결용).

### 적 AI

- 근접 `EnemyAIController`(공격은 `IEnemyAttackBehavior` - 돌진/접촉형), 원거리 `EnemyRangedAIController`(engage 6/chase 5/
  retreat 4/후퇴 0.5초, entryAttackDelay 0.5), `EnemyRangedAttack`(연사/샷건). 보스 `BossAIController`는 인스펙터에 직접 입력.
- 적 탄환 = 코드 생성 구체 + `EnemyProjectile`(정적 목록 `Active`).
- `EnemyContactOnlyAttack`: 1.4/0.4초/1바퀴/0.6초. `RealEnemySpawner.minPlayerDistance` 3. 발광 보정값은 기본값 확정.
- 정적 등록소(등록 없으면 원래 동작과 동일): `EnemyDetectRangeOverride`, `EnemySightOverride`, `EnemySpeedModifier`,
  `EnemyDamageTakenModifier`.
- 처치 이벤트: `EnemyHealth.OnAnyEnemyDied` → `OnAnyEnemyKilled(EnemyHealth, 위치)`(보스 포함 모든 처치, `DissolveAndVanish` 직전) -
  `Die()` 흐름 안에서 도는 구독 코드는 try/catch로 감쌈(예외가 사망 처리를 끊지 않게).
- 연출: `DissolveAndVanish`/`AppearFromWhite`(애니메이션 끝나면 자기 컴포넌트 제거), 충전 색 유지 `IEnemyColorOverride`.
- 제어 체계 `EnemyControlState`: 강력(수면 `Sleep`/해킹 `Hack`) 1칸 + 일반(매료 `Charm`) 1칸, 같은 등급은 교체, 강력이 걸리면 매료 종료,
  강력 중엔 모든 AI 완전 정지(보스 포함). 색은 여기서만 칠함. 새 제어 = `EnemyControlType` 추가 + `GetTier()`. 매료 고유 동작은
  `EnemyCharmState`(`TryApplyCharm`은 실제로 걸렸는지 bool 반환 - 사망/면역/이미 매료/강력 제어 중이면 false).
- 제어 지속 이펙트 `EnemyStatusVfx`(제어를 건 쪽이 `Attach` → 적에게 자동 부착, `EnemyControlState`는 읽기만): 제어가 끝나거나/다른
  강력 제어로 바뀌거나/사망·소멸하면 꺼짐(새 입자 중지 + `fadeOutTime`), 같은 제어 재적용은 이어감, 적 자식이 아니라 위치만 따라감.
  설정 묶음 `EnemyStatusVfxSettings`(프리팹/기준 위치 발밑·몸통·머리 위/오프셋/사라짐 시간/`fadeOutRemainingParticles` - 켜면 남은
  입자도 `StatusVfxParticleFader`가 사라짐 시간 동안 투명하게 지움)는 출처별로 둠:
  - 해킹 `PlayerHackTool.hackStatusVfx` = `VFX/Custom/VFX_Hack_Status`.
  - 수면 31번 `sleepStatusVfx` = `VFX_Sleep_Status`(흰 방울 `Bubble_Sleep_White.png`/`Mat_SleepBubble`, 시작 지연/크기/수명만 무작위,
    깨면 0.25초 잔여 페이드).
  - 매료 38번 `charmStatusVfx` = `VFX_Charm_Status`(꽉 찬 분홍 하트 `Heart_Charm_Solid.png`/`Mat_CharmHeart`, 0.25초 잔여 페이드) +
    `ParticleSwayTilt`(입자를 화면 좌우 이동 방향으로 기울임 - 붙인 프리팹만 적용, Rotation 모듈 끄고 Start Rotation 0).
- 전투방 이탈 후 재입장 시 적만 초기 상태로 재생성(`RealEnemySpawner` + `RoomController`).

### 레벨 생성

- `RoomGenerationConfig`(`roomCountRules`) → `LevelGraphGenerator`(그리드 트리, 특수방은 막다른 방, 시드 없음) → `LevelBuilder.BuildRooms`.
  `RoomVisibilityManager`가 현재 방만 활성화. 방 = 한 변 `cellSize`인 칸 하나. `cellTransitionMargin` 0/`roomEntryPushDistance` 0.1/
  NavMesh Agent Radius 0.3.
- 레벨 포탈: `LevelBuilder.levelPortalPrefab` = `LevelPortal` 프리팹(루트 Capsule 트리거 + 자식 `vfx_Portal_Level`), F 기준점은 바닥.
- 바닥/벽 프리팹 안에 Wall 태그 장애물/투명 제한 벽이 섞여 있음 - 태그/콜라이더로 방 벽을 구분할 수 없음.
- 방 클리어 이벤트 `RoomController.OnAnyRoomCleared`, 클리어 조건 유무 `RequiresClear`(시작방 등은 false = 처음부터 열린 방).
  이 이벤트 구독 코드도 try/catch로 감쌈.
- 런타임 방 추가 `CanAddExtraRoom(category)`/`TryAddExtraRoom(category)`(16번 - 제한 없이 연쇄, n.5 레벨 불가). 부모 = 보스방/특수방/
  전투 중인 방 제외, 동아리방은 `LevelBuilder.extraClubParentCategories`(시작/일반/동아리), 특수방은 Level Config 규칙, 편의점은 제한 없음.
  16번은 지금 붙일 자리가 있는 종류만 가중치 추첨(없으면 사용 불가, 코스트 소모 X). 부모 방 벽 교체: 옛 벽 즉시 삭제 → 새 벽(종류 무관)+
  잠금 바리케이드는 `AppearFromWhite`로 떠오름(플레이어가 그 방에 있을 때만) + 새 벽 앞의 플레이어는 `ForcedPush`로 짧은 넉백
  (`extraRoomWallPushMargin` 0.3/`Duration` 0.18).
  텔레포트 `TryTeleportPlayerToRandomRoom(preferUnvisited, categories)`(22번 동아리방/28번 상점/46번).
- 이웃방 파사드(`neighborFacingWallSets`)는 자기 벽 세트가 없는 방(사실상 일반방)에만 적용 - 예외로 특수방 입구 벽은 시작방에도 적용.
- 일반맵 보물상자 `NormalRoomTreasureSpawner`(적 수 비례 확률, 기본 묶음/패시브 가중치, 상자는 "Chest" 레이어로 플레이어 통과).
  보스방 보상 `ResolveBossRewardContent`(`BossChestOpenEvents` 먼저). 미니맵은 `LevelBuildVersion` 변경 감지.
- 3D 아이템 아이콘: 스케일 0.05, 각도 (55,-45,0).

### 특수방 (크레딧 더미 `credit_pile`만 제작됨)

- 예전 소비방/거래방/신비방은 `RoomCategory.Special`(4) 하나로 통합(5, 6번 비어 있음, Normal 7/Choice 8 고정). 종류 = 레이아웃 에셋
  `SpecialRoomLayout`(`MobArchive/Special Room Layout`, 폴더 `Assets/Config/SpecialRoom`), Level Config의 `specialLayouts`(레이아웃 +
  가중치)에 등록. layoutId = 그 방의 variantId(미니맵 variant 아이콘/아이템 등장 위치에 사용). Hierarchy 이름 `Room_N_Special_<ID>_<조건>`.
- 배치(`LevelGraphGenerator.AttachSpecialRooms`): 항상 막다른 방, 부모 = `specialRoomParentCategories`(시작/일반 - 동아리방 X), 부모를 먼저
  고른 뒤 빈 옆칸. 지금 Level1은 Special min1/max1. 미니맵은 방문 후에도 특수방 아이콘 유지(`keepIconAfterVisitCategories` Store/Special).
- 진입 조건 `SpecialRoomEntryCondition`(Free/Grenade/HackingTool/Health): 레이아웃의 `entryConditionPool`(조건 + 가중치, 비면 고정
  `entryCondition`)에서 방마다 뽑아 `RoomInstance.specialEntryCondition`에 저장 - 모든 판정은 이 값 기준. 동작 값은 공용 에셋
  `SpecialRoomEntrySettings`(LevelBuilder 칸).
  - 수류탄/해킹툴: 부모 방 입구 벽에 잠금 바리케이드(`SpecialRoomLock` - 정적 목록 `All`, `Condition`/`ParentRoom`/`IsLocked`/`Unlock()`) -
    전투 바리케이드보다 방 안쪽, 방 클리어로 안 열림, 같은 조건 도구의 폭발/해킹 범위(바닥 평면 거리)에 걸리면 해제(흰색 소멸 + 조건별
    `unlockVfxPrefab` 연쇄/단발 `SpecialRoomUnlockVfx`). 모델은 조건별 `lockBarricadePrefab`(입구 벽의 `WallSidePrefab.specialLockBarricade`
    자리/방향에 세움, 그 오브젝트는 꺼짐) → 없으면 벽 프리팹의 바리케이드 → 없으면 조건 색 임시 큐브.
  - 체력 소모: 부모 방↔특수방을 지날 때마다 일반 피격 0.5(`SpecialRoomHealthHazard`, 무적/21번 적용, 사망 가능, 막기 아이템용
    `SpecialRoomHealthDamageOverride.Block(출처ID, 주인)`). 부모 방 입구 벽에는 표시용 가시 마커 `SpecialRoomEntrySettings.healthMarkerPrefab`
    (잠금 바리케이드와 같은 자리/방향 - `LevelBuilder.InstantiateAtSpecialLockSlot` 공용, 콜라이더 전부 끔 = 통과 가능, 항상 남아 있음 -
    37번을 가져도 그대로). 칸이 비면 마커 없음.
- 외형: `RoomVisualSet`(`MobArchive/Room Visual Set`, 레이아웃의 `visualSet`) = 바닥 풀(방 내용물은 바닥 프리팹 안에 배치) + 이 방의 벽 +
  부모 방 쪽 입구 벽(`parentFacingWallPools`). 칸별 폴백: 외형 세트 → Room Wall Sets(variantId → Special) / Neighbor Facing Wall Sets
  (특수방 항목) → 기본. 채운 칸에서 빠진 방향은 임시 큐브 + 빌드 시 경고.
- 아이템: 아이템 에셋 Appearance Contexts에 Special + 레이아웃 ID(전용) / Special + 카테고리 전체(공용, 레이아웃의
  `includeSharedSpecialItems`가 켜졌을 때만). 레이아웃 Inspector 아래에 후보 미리보기 + 진입 조건 확률. `spawnItemPedestal`이면 방 가운데
  받침대(`isClubPedestal` false - 27번 등 동아리방 전용 효과 X).
- 9번 음식: 레이아웃 `allowChestDuplication`이 켜진 특수방만 - 크레딧 상자는 같은 설정의 크레딧 상자로(`SpawnDuplicatePresetChest`, 다른
  상자와 `duplicateChestMinSpacing` 이상), 받침대는 특수방 받침대로.
- 크레딧 더미: 진입 조건 그냥/수류탄/해킹툴 각 1, 바닥 프리팹 `Floor_CreditPile` 안 보물상자 3개 + `PresetBasicItemChest`(열 때 2~5줄
  각 25%, 줄마다 "크레딧 +1" = 실제 획득 단위라 30번 보너스도 줄마다, 48번 가중치 뒤섞기 적용, 통과 가능, 복제용 `duplicatePrefab`).
- 특수방 관련 아이템: 10번(수류탄)/26번(해킹툴) 보유 중엔 그 조건 잠금을 도구 없이 해제 - 부모 방이 클리어됐거나 `RequiresClear`가
  false(시작방)일 때, 플레이어가 그 방에 있으면(잠금이 보이는 상태, 로딩/`AppearFromWhite` 중 제외) `unlockDelay` 0.3초 뒤 일반 해제와
  같은 연출로 열림, 부모 방 클리어 순간엔 바로 열림. 37번 보유 중엔 체력 소모 특수방 피해를 완전히 막음(피격 판정 자체 없음).

### 상점 (편의점 `convenience_store`만 제작 중)

- 배치: 편의점 바닥 프리팹(LevelBuilder Room Wall Sets → Store → Floor Pool) 안에 상인 빈 오브젝트 + `ShopNPC`(Shop Config 연결,
  임시 캡슐 자식) + 주변 4방향 보관 좌대 `HoldingPedestal` 4개. 좌대는 `ShopNPC.holdingPedestals` 순서, 비우면 같은 방(RoomController
  아래, 없으면 같은 부모 아래) 자동 탐색 - 테스트 씬에선 NPC와 좌대를 같은 부모 아래 두거나 리스트에 직접 넣음.
- 설정 `ShopConfig`(`MobArchive/Shop Config`, 에셋 `Assets/Config/Level/ShopConfig_Store`): 소모품 칸 목록(종류/표시 이름/아이콘/재화/
  1회 가격/1회 지급량, 재고 무제한), 플레이어 아이템 후보 = `itemDatabase` 중 `playerItemContexts`(기본 Store 전체)에 맞는 아이템(분류 무관),
  개수 `min~maxPlayerItemCount`(기본 1~3, 열 때/갱신 때마다 무작위), 등급별 가격 `rarityPrices`(`minPrice~maxPrice` 무작위, max가 min
  미만이면 min 고정, 기존 `price`는 `FormerlySerializedAs`로 min에), 갱신 비용 1 크레딧/최대 2회.
- 진열: 처음 열 때 한 번 뽑고 유지. 소모품 먼저, 플레이어 아이템 뒤(최대 8칸 = 4x2). 플레이어 아이템은 가중치(weight) 추첨, 이미 가진
  패시브/장착 중 슬롯 아이템/보관 좌대의 아이템/이 레벨에서 이미 나온 아이템 제외(`GameFlowManager.FilterOutAlreadyAssignedThisLevel`,
  진열 즉시 `MarkItemAssignedThisLevel`). 보물상자 추첨 코드/보정(48번/27번)과 분리.
- 구매(`ShopNPC.CheckPurchase/TryPurchase` - 받을 수 있는지 전부 확인 후 재화 차감): 소모품은 수량 선택(현재 체력은 깎인 만큼 올림까지,
  초과분은 Heal이 잘라냄), 플레이어 아이템은 1개 고정. 패시브 = 인벤토리, 슬롯 아이템 = 장착 + 원래 아이템은 첫 빈 보관 좌대로(빈 좌대
  없으면 구매 불가), 교체 대상이 `confirmBeforeReplace`면 교체 확인창 [예]일 때만 구매. 산 카드는 반투명 검은 덮개(누를 수 없음).
- 갱신(`CheckRefresh/TryRefresh`): 확인 팝업 "1 크레딧으로 갱신하시겠습니까?", 플레이어 아이템 칸만 전부(팔린 칸 포함) 다시 뽑음 -
  지금 진열 중이던 아이템 제외, 후보가 0이면 재화 안 쓰고 막음. 횟수 소진 시 [갱신하기] 버튼 어둡게 비활성.
- 창 `ShopUI`(자동 생성, order 85 - 교체 확인창 90/게임 오버 100보다 아래): 반투명 흰 틀 + 카드(이름 전부 검정, 아이콘 틀 = 소모품 기본/
  플레이어 아이템 등급별 스프라이트·색, 묶음 "x5", 가격표 틀 전체가 구매 버튼) + [갱신하기]/[나가기](Esc). 구매 팝업(반투명 검정,
  "OO을(를) 구매하시겠습니까?" - 플레이어 아이템 이름만 등급 색(설명칸 색을 따름, 을/를은 흰색), -/수량/+, 합계 가격 부족 시 빨강 +
  버튼 비활성, 다른 이유는 빨간 안내 문구)과 갱신 팝업은 모달. 모든 틀/버튼/재화 아이콘에 스프라이트 칸 + 위치 오프셋(재화 아이콘/
  가격 칸/가격 숫자/갱신 안내 문구) 노출. 재화 아이콘이 비면 `PlayerResourceDisplay`의 아이콘을 빌림.
- 흐르는 이름(`ShopUI.Marquee`, 카드 이름 + 구매 팝업 제목 첫 줄 공용): 글자 크기 고정, 칸보다 길 때만 멈춤(1초) → 왼쪽으로 흐름(40px/s)
  → 끝에서 멈춤(1초) → 페이드 아웃/인(각 0.25초, 구역 `CanvasGroup`)으로 처음 위치 → 반복. 칸 밖은 `RectMask2D`로 잘림(Softness 8).
  이름 칸마다 프레임당 한 번만 진행(자기 `lastStepFrame`).
- 창이 떠 있는 동안: 게임은 계속, 이동/공격/장전/무기 전환/Space·Q·E 컴포넌트를 잠시 끔(교체 확인창과 같은 방식), 좌클릭은
  `HUDClickBlocker`, F는 `InteractionTargetSelector`가 막음. 사망 시 창만 닫음.
- 카드 아이콘 틀(아이콘 포함) 호버 = `ItemTooltipDisplay`가 아이템 설명칸을 커서 위에 띄움(그때만 정렬 순서를 상점 창 위로).
- 보관 좌대 `HoldingPedestal`: 보물상자 시스템에 등록하지 않는 범용 좌대(9번 복제/48번 등 대상 아님). F = 슬롯 아이템 맞바꾸기
  (교체 확인창 연동)/패시브 획득, 아이콘 + 상자와 같은 빛, 미니맵 "Item" 마커, 설명칸 표시. 슬롯 공용 도우미
  (`IsSlotCategory`/`GetEquippedSlotItem`/`TryEquipSlotItem`)가 이 클래스에 static으로 있음.

### 아이템 시스템

- 패시브: `ItemDefinition.specialEffects`(`[SerializeReference] IPassiveItemEffect` - 효과 클래스는 `[Serializable]` + 매개변수 없는
  생성자), 훅 `OnProjectileSpawned`(분열탄에도 발생)/`OnShotFired`(사격 틱)/`Projectile.OnDespawning`, 데미지 보너스 등록소
  `Projectile.AddDamageBonusProvider`(자동 합산). "보유 중" 상시 동작은 플레이어에 붙는 런타임 러너 MonoBehaviour(레벨 넘어가도 유지,
  씬 재로드 시 소멸 - 10/26/37/54번).
- 액티브(Space): `activeCost` + `IActiveItemEffect`, 지연형 `IDelayedActiveItemEffect`. 실제 소모 = `GetEffectiveCost`
  (`ActiveCostModifier`, 최소 1).
- 음식(E) `IFoodEffect`/`PlayerFoodItem`(`LastUsedFood`), 일회용(Q) `IDisposableItemEffect`, 장신구 `ITrinketEffect`.
  `confirmBeforeReplace` 체크박스.
- 상태 있는 효과를 재실행할 땐 `JsonUtility`로 복제(원본 공유 인스턴스 재사용 X).
- 구현된 아이템(정확한 목록은 `Assets/Script/Item`): 패시브 2, 4, 7, 8, 10, 11, 18, 19, 21, 24, 26, 30, 35, 36, 37, 38, 39, 41, 43, 48, 54 /
  음식 9, 13, 27, 34 / 액티브 16, 29, 31, 93 / 일회용 22, 25, 28 / 장신구 20, 32, 45. 상세 기획은 각 `ItemNN effect.cs` 주석.
  예전 13번 액티브(직전 음식 재현)는 93번으로 이동(`Item93effect.cs`, 에셋 ID/이름도 93), `Item13effect.cs`는 새 13번 음식.
- 31번 = 방 전체 수면(`RoomWideSleepEffect`, 옛 기절 - 이름 변경/보호 속성 제거 완료): 잠든 적 노란색 + 사용 순간 이펙트 + 수면 방울.
- 확정값(이번 추가분): 10번 획득 시 수류탄 2/26번 해킹툴 2/37번 수류탄 1 + 해킹툴 1(모두 레벨당 1회), 13번 음식 획득 시 공격 속도 +0.3
  (레벨당 1회) + 사용 시 공격 속도 0.1당 공격력 0.5(0.1 미만 버림), 28번 상점 텔레포트(안 가본 상점 우선, 상점 없는 레벨은 사용 불가 -
  `RoomCategory.Store` 전체라 편의점/카페 variant 모두 대상), 54번 획득 시 크레딧 5(레벨당 1회) + 처치마다 25%(행운 미반영, 고정)로 크레딧 2.
- 레벨당 1회 지급 = `GameFlowManager.TryConsumeOneTimeGrant(키)`(키 = `{아이템ID}_용도`). GameFlowManager가 없는 곳(아이템 테스트 씬)은
  경고만 남기고 지급.
- 54번 크레딧 팝업 `CreditPopupDisplay`(Item54effect.cs 안, 자동 생성 Overlay 캔버스 order 8): 적 머리 위(콜라이더/렌더러 높이,
  발밑 기준 화면 140px 상한 - 보스도 하늘로 안 뜸)에 아이콘이 떠오름 0.25초 → 유지 0.35초 → 가라앉으며 사라짐 0.35초. 뒤에 노란 빛
  (`glowSprite`, 비면 코드 생성 원형, x2.2 + 맥동) + 반짝이 8개. 아이콘이 비면 `PlayerResourceDisplay.creditIcon`.
- 플레이어 총알 색(아이작식): 정적 등록소 `ProjectileColorOverride` + 재사용 패시브 효과 `BulletColorEffect`(아이템 에셋 Special Effects에
  추가, `bulletColor`/`priority`). 우선순위 높은 색, 같으면 나중에 얻은 색. `Projectile.Launch`에서 그 총알 렌더러에만
  MaterialPropertyBlock으로 색을 덮어씀(공유 재질 그대로, 분열탄 포함, 적 총알 무관), 꼬리도 따름. 38번 = `BA55D3`(매료 색).
  장신구가 쓰면 뺄 때 `RemoveColor` 필요.
- ItemDefinition 에셋은 복제해서 새로 만들어도 됨(복제본도 문제없음) - 복제 후 `Item Id` 변경 + ItemDatabase 등록만 하면 됨(중복 ID 경고는
  없음 - 직접 주의).
- 상점에 나오게 하려면 아이템 에셋 Appearance Contexts에 `Store`(또는 상점 Shop Config의 등장 위치와 같은 값) 추가.
- 등장 위치 매칭 `ItemDatabase.MatchesRoom`: variantId 일치 항목 또는 카테고리 전체 항목(`GetItemsForRoom`/`GetAllCategoryItemsForRoom`).
- 보물상자 공통: 동아리방/특수방 받침대(`ItemPedestal`)도 이제 통과 가능(`walkThrough`, 시작 시 콜라이더 전부 트리거) + 범위는 거리
  (`interactRadius` 1) - 트리거 크기와 무관.
- 상자 뚜껑: 메뉴 `MobArchive/보물상자 뚜껑 분리`(`ChestLidSplitTool`)가 FBX 메시를 높이 절반에서 몸통/뚜껑으로 나눠
  `Object/Prop/LidSplit/`에 저장 + 프리팹에 `Lid` 자식과 `ChestLidOpener`를 추가(DroneBox 1/2/3_Box 적용됨, 원본 FBX는 그대로).
  `ChestLidOpener`는 상자의 `IOpenableContainer.IsOpened`(`TreasureChest`/`ItemPedestal` 구현)만 보고 열림 - 북쪽(+Z) 경첩 =
  뚜껑 본체 모서리(걸쇠 제외), 130°/0.35초/튕김 1.2, `rotateWithChest`(기본 켬) - 상자를 Y축으로 돌려 놓아도 경첩이 따라 돎.
- 밀어내기 공용 `ForcedPush.Apply(대상, 방향, 거리, 시간, 무시할 콜라이더)` - 지금은 CharacterController(플레이어)만, 밀리는 동안
  `PlayerMovement` 이동 입력 0. 적 넉백은 NavMeshAgent 방식 추가 필요.
- 정적 등록소: `CreditGainBonusOverride`/`TreasureChanceBonusOverride`(곱연산), `SplitShotOverride`(19번), `WallBounceOverride`(35번),
  `ProjectileColorOverride`(총알 색). 장신구 1회 지급은 `GameFlowManager.TryConsumeOneTimeGrant`(레벨당 1회).
- 확정값: 4번 반경 2.5/배율 1, 35번 사거리 x0.75 복리 + 튕길 때마다 데미지 +25%(`BounceCount`), 19번 ±30도 2발(탄약 1).
  `Projectile.DistanceTraveled` = 튕겨도 안 리셋되는 누적 거리(11번 기준). 7번: 잠든 적은 피격으로 안 깨어남(확정).
- 35번 실제 파일명 `Item35effect.cs`(문서명 `WallBounceEffect.cs`).
- 상자 위 아이템 빛 `ItemGlowSpawner`(씬에 하나, 등급 색 Tier0 흰/1 파랑/2 노랑/3 보라).

### 전투 연출

- `DamagePopupDisplay`(`EnemyHealth.OnAnyEnemyDamaged`), `EnemyHealthBarDisplay`(보스 제외, 처치 Pop).
- `CombatVfxSpawner`(씬에 하나): 총구 2/플레이어 피격 2/적 피격/벽 충돌 2/처치 폭발 8슬롯. 총구 이펙트는 firePoint 자식.
  크기 배율은 `ParticleSystem.main` 크기/속도 배율을 직접 곱함(수류탄/해킹 이펙트도 같은 방식).
- 이펙트 모양 조정 = 원본 이펙트 프리팹을 `Assets/VFX/Custom/`에 복제해 그 프리팹을 직접 수정(색/크기/분사율 등). 스크립트에는
  프리팹 칸 + 위치/오프셋 정도만 둠(인스펙터 상세 설정 칸/전용 이펙트 Definition 방식은 쓰지 않기로 확정). 원본 팩 재질은 수정 X.
- 탄환 꼬리 `BulletTrail`(별똥별 꼬리): 설정 에셋 `Assets/Resources/BulletTrailSettings`(`MobArchive/Bullet Trail Settings`, 자동 로드,
  없으면 꼬리 없음) + 템플릿 프리팹 `Trail_Bullet`(Time 0.3)/`Trail_Grenade` = Trail Renderer 하나, 재질
  `Mat_BulletTrail`(URP Particles/Unlit 투명, Render Face Both). 꼬리 모양은 템플릿에서 직접 조절, Width 1 = 탄환 지름(크기 자동 곱).
  플레이어/적 총알은 탄환 색(덮어쓴 총알 색 우선), 수류탄은 흰색. 부착 = `Projectile`/`EnemyProjectile`/`Grenade`의 `Launch`.
  탄환이 사라지면 남은 꼬리를 자식 LineRenderer로 넘겨 그 자리에 고정 + 앞 끝을 반원으로 둥글게 → `bulletReleaseFadeTime`/
  `grenadeReleaseFadeTime` 동안 투명·가늘어지며 소멸. Trail `SetPositions`는 점 시간이 0이 돼 즉시 사라지므로 쓰지 말 것.

### HUD

- `PlayerHUDManager`의 단일 Canvas(1920x1080) 위에 각 스크립트가 패널 생성, 라이브 트윅. 대부분 씬의 `PlayerHUD` 프리팹 안 -
  인스펙터 값을 바꾸면 Overrides → Apply All.
- 글자 = TMP, 글꼴 에셋 `Assets/Resources/Fonts/경기천년제목_Bold SDF`(**이름 바꾸면 `HUDFont.GetTMP()`가 못 찾음**).
  외곽선 = Face Dilate 동시 적용, 픽셀 → TMP 값 변환(최대 0.5, 더 두껍게는 글꼴 에셋 Padding). 조절: F 안내
  `InteractPromptDisplay.interactPromptOutlineThickness/Color`, 보스 이름 `BossHealthBarDisplay.nameOutlineThickness` +
  `BossPortrait.nameOutlineColor`, 데미지 `DamagePopupDisplay.outlineThickness/Color`.
- 체력 칸: 스프라이트 `UI_Sprite/UIIcon/UI_PlayerHealth_Full/Half/Zero`(보스 체력바 재활용, 152x76 = 2:1, 흰 테두리 8px,
  Half = 왼쪽 초록 + 오른쪽 빈 칸), `PlayerHeartDisplay.heartWidth/heartHeight`(기본 64x32, 비율 유지, 라이브 트윅).
- 구성: 초상화+하트(테두리 `portraitBorderColor/Thickness`), 좌하단 스탯 패널, 좌상단 액티브+코스트(`PlayerCostDisplay`, 추가 보조
  아이콘은 `SyncAuxIcon()` 분기로 어느 액티브든 사용 가능 - 지금은 93번), 우측 슬롯(`PlayerSlotItemDisplay`), 인벤토리
  (320x600/5열/64/8), 자원 카운터, 탄약 박스, 미니맵, 보스 체력바, 설명칸, 장전 표시(`PlayerReloadIndicator`, order 5),
  F 안내(Overlay, order 10, 문구 열기/획득하기/다음 레벨/거래하기), 툴팁(`ItemTooltipDisplay`, order 20 - 그리기는 `ItemInfoPanelView`,
  스타일은 `PlayerItemInfoDisplay`).
- 아이템 설명칸/툴팁 배경: `backgroundSprite`가 비면 `panelColor` 단색을 `panelCornerRadius`(기본 16, 0 = 각진 사각형) 둥근 사각형으로.
- 스탯 패널 `Playerstatdisplay`: `rowsPerColumn` 7, `isSkillStat` 스탯은 따로 오른쪽 열, 호버 툴팁. 수치 변화 연출(유리 노랑/불리
  빨강, 1.4배 0.08초 → 0.4초 복귀, `lowerIsBetter`, `flashOnChange`). 긴 숫자: `hideDecimalAtOrAbove`(100) 이상은 소수점 숨김(표시만 -
  실제 계산은 소수점 그대로), `autoShrinkValueText`가 켜지면 숫자가 실제 남는 폭(`valueTextWidth` + 음수 `columnSpacing` 반영)을 넘을 때
  글자를 `minValueFontSize`(14)까지 줄임.
- 스탯 표기는 `StatDisplayFormat` 한 곳(총알 크기 x100, 확률 %, 곱하기 효과 ±%). 새 스탯 한글 이름은
  `ItemInfoPanelView.GetStatDisplayName`.
- 패널 접기 `CollapsibleHUDPanel`(Tab 일괄). 미니맵 휠 확대(0.4~2)/드래그/"현재 위치" 버튼.
- 미니맵 방문 표시는 `ShowAsVisited()`로 거름(`RoomInstance.visited` 값 자체는 그대로 - 22번 등이 씀): 클리어 못 하고 벗어난
  일반방/보스방 = 미방문 표시(보스방은 보스 아이콘 다시), `keepIconAfterVisitCategories`(기본 Store/Special) = 방문 후에도 방 종류 아이콘 유지
  (포탈이 있으면 포탈 우선, 남은 아이템 4격자는 함께).
- 클릭 차단 `HUDClickBlocker.BlocksLeftClick`(접기 버튼/미니맵/교체 확인창/상점 창) - 클릭형 HUD가 생기면 여기에 조건 추가.

## 반복 작업 (새 에셋/씬을 만들 때마다 확인)

- [1] Wall 태그 콜라이더는 새 프리팹도 Box/Convex 체크리스트 적용.
- [2] 새 원거리 적 프리팹에 `EnemyRangedAIController`가 붙어 있는지 확인.
- [3] 상체 동작이 몸 전체를 흔들거나 가라앉게 하면 클립 루트 커브 문제 - RootFixed 사본을 Humanoid 원본 + `Animation/Generic`
  Generic 복사본(레이사용) 둘 다 만들고 Override Controller에도 연결.
- [4] 교체 확인창이 필요한 슬롯 아이템은 아이템 에셋의 `Confirm Before Replace` 체크.
- [5] 새 모델/재질은 `MobArchive/셰이더 일괄 교체`로 툰 적용(FBX 내장 재질은 먼저 Extract). 새 씬은 조명/환경광/Skybox/Global
  Volume을 위 [비주얼] 값으로.
- [6] 포트폴리오 영상/이미지에 유니티짱이 나오면 UCL 3.0 라이선스 로고/크레딧 표기.
- [7] 새 게임 씬은 `LoadingScreen` 프리팹 배치 + Build Profiles 등록(Title 0번 유지). 설정 화면이 생기면
  `TitleScreenUI.OnSettingsClicked()`에 연결.
- [8] 새 상점 바닥 프리팹(카페 등)은 `ShopNPC`(Shop Config 연결) + `HoldingPedestal` 4개 배치. 상점별 상품을 나누려면 Shop Config의
  등장 위치와 아이템 에셋 Appearance Contexts를 같은 variantId로 맞춤.
- [9] 새 특수방 레이아웃: 레이아웃 에셋(고유 layoutId, 진입 조건 후보, 아이템 규칙, 9번 허용 여부) + Room Visual Set(내용물은 바닥 프리팹 안)
  + **레벨마다** Level Config의 Special Layouts에 등록(config마다 목록이 따로) + 필요하면 미니맵 variant 아이콘.
- [10] 복제로 만든 ItemDefinition 에셋은 `Item Id`를 반드시 새 번호로 바꾸고 ItemDatabase에 등록(테스트용 복제본은 `NN_1` 이름으로 두고
  나중에 삭제).
- [11] 총알 색 아이템 = Special Effects에 `BulletColorEffect`. 새 제어 지속 이펙트 = 프리팹 복제 + 출처 효과에 설정 칸 + `Attach`.

## 확인 필요 항목 (해결되면 지울 것)

- [20] 캐릭터 정식 모델 교체 예정 - FBX Materials 탭에서 `Player_Mat`으로 Remap, Humanoid면 `UnityChan_Player` 구조를 따름.
  유니티짱은 Unity Toon Shader 재질이라 `Toon_Character` 규칙과 다를 수 있음.
- [34] `PlayerInventoryDisplay` 스크롤용 씬 `EventSystem` 존재 확인.
- [48] 행운(Luck) 스탯 - `PlayerStats.luck` 필드만 존재, 확률 반영 미구현. 반영할 때 54번 처치 확률(지금 25% 고정)도 대상.
- [49] 미니맵 마커 아이콘 8종 + 포탈 아이콘 스프라이트 연결.
- [54] 등급 스프라이트 오른쪽 "태그 UI" 자리 남아 있음.
- [67] 무기 모드 표시 HUD 없음 - 필요하면 `PlayerWeaponMode.OnModeChanged` 구독.
- [73] `PlayerItemInfoDisplay.cs` 등급 이름 색 코드 기본값(Tier2 보라/Tier3 금색)이 씬 값과 다름 - 고칠지 형빈 결정 대기.
- [77] 하드 엣지 모델 외곽선이 모서리에서 끊길 수 있음 - 거슬리면 (a) 부드러운 노멀 굽기 도구 또는 (b) 후처리 외곽선.
- [78] (참조만) 특수방 진입 비용 변형 아이디어 - 수류탄 3개/해킹툴 2개 소모 등. 1레벨에는 과해서 보류, 나중에 레벨별 난이도로 검토.
- [79] 특수방 개수 규칙(지금 Level1 Special 필수 1개 - 로직 확인용)과 레이아웃 가중치 조정 예정. 지금은 레이아웃이 크레딧 더미뿐 +
  테스트 레이아웃(Test_Free/Grenade/Hack/Health, Layout_ItemSingle) 정리 필요.
- [81] 비어 있는 방 번호 5, 6 - 나중에 새 방 종류를 만들 때 사용.

## 변경 로그 (버전당 한 줄)

- v1.4~v1.26: 카메라/적 AI/던전 생성/HUD/아이템 카테고리 기반 구축(상세는 v1.26 아카이브).
- v1.27~v1.33: 문서 구조 전환 + 확인 필요 항목 정리(`[N]` 표기 도입), 코드 변경은 거의 없음.
- v1.34: F 안내 화면공간 전환, 장전 표시, 아이템 호버 툴팁.
- v1.35: 아이템 18/20/24/25/29/30/32 구현.
- v1.36: 38번 매료 + 적 사망 처리 버그 수정.
- v1.37: 피해 팝업/적 체력바/스탯 툴팁/전투 VFX 8종.
- v1.38: 19번 양갈래 + 35번 벽 튕김 개편.
- v1.39: 수류탄/해킹툴/무기 모드/제어 2단계 체계/스킬 스탯.
- v1.40: 사망/게임 오버, 수류탄·장전·사망 애니메이션, 코드 반동, 테스트 프로필.
- v1.41: 해킹/장전/획득 애니메이션(RootFixed), 상자 아이템 빛, 획득 연출, 스탯 표기 일원화, 게마트리아 용어 교체.
- v1.42: HUD 접기/미니맵 확대/클릭 차단/교체 확인창/가장 가까운 F 대상, 아이템 9/22/27/41/45/48.
- v1.43: 툰 셰이더 2종 + 조명/후처리 + 화면 고정 배경 + 일괄 교체 도구.
- v1.44: SD 유니티짱 교체(레이사 병행), Humanoid 전환, 조준 IK, 비조준 자동 조준.
- v1.45: 시작/로딩 화면, 포탈 이펙트, HUD TextMeshPro 전환, 스탯 변화 연출, 탄환/수류탄 색, 해킹 총알 제거.
- v1.46: 문서 정리 - v1.27~v1.45 원본 아카이브, 슬림 재작성, 크기 상한 30k, 반복 작업 목록 분리, 확인 항목 정리.
- v1.47: 편의점 상점(상인/진열/구매 팝업/갱신/카드 툴팁/등급별 가격 범위/1~3개 무작위) + 보관 좌대, 미니맵 방문 표시 예외, 설명칸 둥근 배경.
- v1.48: 특수방 통합(레이아웃/진입 조건 4종/잠금 바리케이드/체력 피해/외형 세트/레이아웃별 아이템) + 크레딧 더미, 9번·16번 특수방 대응, 동아리방 상자 통과, 경첩 회전 대응, ForcedPush.
- v1.49: 상점 흐르는 이름(카드 이름/구매 팝업 제목, 페이드 반복) + 구매 팝업 이름 등급 색.
- v1.50: 문서 반영 누락분 - 체력 칸 2:1 스프라이트 + 가로/세로 크기, 보물상자 뚜껑 분리 도구 + 열림 연출(`IOpenableContainer`), 해킹 지속
  이펙트(`EnemyStatusVfx`) + 이펙트는 프리팹 복제 수정 방식 확정. 프로젝트 문서 스크립트를 로컬 기준으로 재동기화.
- v1.51: 아이템 10/26/37/54(패시브)/13(음식 개편, 옛 13번 → 93번)/28(일회용), 체력 소모 특수방 가시 마커, `OnAnyEnemyKilled`, 스탯 패널 긴 숫자
  처리(소수점 숨김/자동 축소), AppearanceContextDrawer 렉 수정(캐시).
- v1.52: 31번 기절 → 수면 이름 변경, 수면 방울/매료 하트 지속 이펙트(잔여 입자 페이드, 하트 기울임), 탄환 꼬리(둥근 소멸),
  아이템별 플레이어 총알 색(38번 보라).
