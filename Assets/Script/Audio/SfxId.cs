using UnityEngine;

// 효과음 종류입니다. SoundLibrary 에셋이 종류마다 한 칸씩 자동으로 만들고, GameAudio.Play(SfxId.XXX)로 부릅니다.
// [주의] 에셋에 숫자로 저장되므로 기존 값의 숫자는 바꾸지 말고, 새 종류는 빈 번호에 추가합니다.
public enum SfxId
{
    [InspectorName("없음")] None = 0,

    [InspectorName("UI - 진행 버튼(예/계속 등)")] UIConfirm = 1,
    [InspectorName("UI - 취소 버튼(아니오/뒤로/나가기 등)")] UICancel = 2,
    [InspectorName("UI - 버튼 호버")] UIHover = 3,
    [InspectorName("UI - 위험 버튼(재시작/그만하기)")] UIDanger = 4,
    [InspectorName("UI - ESC 중단 화면 열기")] PauseOpen = 5,
    [InspectorName("UI - 게임 오버 창 등장")] GameOverShow = 6,
    [InspectorName("UI - R 꾹 눌러 재시작")] HoldRestart = 7,

    [InspectorName("보물상자 열기")] ChestOpen = 10,
    [InspectorName("아이템 획득(재화/플레이어 아이템)")] ItemPickup = 11,
    [InspectorName("상점 구매 완료")] ShopPurchase = 12,
    [InspectorName("상점 구매 실패(재화 부족)")] ShopPurchaseFail = 13,
    [InspectorName("아이템 효과 발동(머리 위 아이콘/액티브 사용)")] ItemActivate = 14,
    [InspectorName("아이템 효과 실패 알림(빨간 아이콘 - 39/103/111번)")] ItemFail = 15,
    [InspectorName("사용 불가(코스트 부족/지금 못 씀/수류탄·해킹툴 0개)")] ActionFail = 16,

    [InspectorName("걸음")] Footstep = 20,
    [InspectorName("74번 돌진")] Dash = 21,

    [InspectorName("사격")] GunShot = 30,
    [InspectorName("빈 탄창 클릭")] GunEmpty = 31,
    [InspectorName("장전 시작")] ReloadStart = 32,
    [InspectorName("무기 모드 전환(1/2/3)")] WeaponSwitch = 33,
    [InspectorName("장전 완료")] ReloadComplete = 34,
    [InspectorName("플레이어 총알 벽 충돌")] BulletHitWall = 35,

    [InspectorName("수류탄 준비(핀 뽑기 - 우클릭 조준 시작)")] GrenadePinPull = 40,
    [InspectorName("수류탄 투척")] GrenadeThrow = 41,
    [InspectorName("수류탄 폭발")] GrenadeExplode = 42,
    [InspectorName("해킹툴 전개")] HackDeploy = 43,
    [InspectorName("118번 포격 낙하")] ArtilleryFall = 44,
    [InspectorName("115번 크레모어 설치")] ClaymoreInstall = 45,

    [InspectorName("81번 소이탄 연소(반복)")] IncendiaryBurn = 50,
    [InspectorName("90번 물결")] RoomWave = 51,
    [InspectorName("110번 충전")] ChargeShotCharge = 52,
    [InspectorName("110번 강화 총알 발사")] ChargeShotFire = 53,

    [InspectorName("플레이어 피격")] PlayerHit = 60,
    [InspectorName("보호막으로 막음(보호막 1개 깨짐)")] ShieldBlock = 61,
    [InspectorName("플레이어 사망")] PlayerDeath = 62,
    [InspectorName("체력 회복")] PlayerHeal = 63,
    [InspectorName("보호막 획득")] ShieldGain = 64,

    [InspectorName("적 피격(총알 명중)")] EnemyHit = 70,
    [InspectorName("적 처치")] EnemyKill = 71,
    [InspectorName("적 공격 예고(돌진 충전/보스 포격 조준)")] EnemyAttackWarn = 72,
    [InspectorName("2레벨 보스 포격 폭발")] BossArtilleryExplode = 73,
    [InspectorName("2레벨 보스 소환 신호 물결")] BossSummonWave = 74,
    [InspectorName("적 사격(적 총알 발사)")] EnemyShot = 75,
    [InspectorName("보스 보호막 피격")] BossShieldHit = 76,
    [InspectorName("보스 보호막 파괴")] BossShieldBreak = 77,
    [InspectorName("보스 그로기 진입")] BossGroggy = 78,
    [InspectorName("보스 처치")] BossKill = 79,

    [InspectorName("방 클리어(바리케이드 열림)")] RoomClear = 80,
    [InspectorName("특수방 잠금 해제")] SpecialRoomUnlock = 81,
    [InspectorName("헬기 로터(반복)")] HeliRotor = 82,
    [InspectorName("전투 시작(바리케이드 닫힘)")] CombatStart = 83,
    [InspectorName("보스방 입장 경고(BGM이 잠깐 끊김)")] BossRoomAlert = 84,
    [InspectorName("소환된 적 나타남")] SummonAppear = 85,

    [InspectorName("입장 컷씬 '짜잔' 포즈")] IntroPose = 90,

    [InspectorName("액티브 16번(방 추가)")] Active16 = 100,
    [InspectorName("액티브 29번(방 전체 피해)")] Active29 = 101,
    [InspectorName("액티브 31번(방 전체 수면)")] Active31 = 102,
    [InspectorName("액티브 85번(직전 일회용 재현)")] Active85 = 103,
    [InspectorName("액티브 87번(적 포섭)")] Active87 = 104,
    [InspectorName("액티브 93번(직전 음식 재현)")] Active93 = 105,
    [InspectorName("액티브 106번(상자 도박)")] Active106 = 106,
    [InspectorName("액티브 112번(투명)")] Active112 = 107,
}
