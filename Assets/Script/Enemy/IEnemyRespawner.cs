using System.Collections.Generic;
using UnityEngine;

// 방 하나의 적을 "처음 스폰됐을 때와 똑같이" 다시 만들 수 있는 스폰 담당자를 표현하는 인터페이스입니다.
// 플레이어가 클리어하지 않은 전투방을 벗어났다가(텔레포트 등) 다시 들어올 때, RoomController가
// 방을 처음 상태로 되돌리기 위해 사용합니다(RealEnemySpawner가 구현). IEnemySpawner를 바꾸지
// 않고 별도 인터페이스로 둔 이유는, 이 기능이 없는 스폰 담당자(DummyEnemySpawner 등)가 그대로
// 동작해야 하기 때문입니다 - 이 인터페이스를 구현하지 않으면 그 방은 그냥 초기화되지 않습니다.
public interface IEnemyRespawner
{
    // room의 처음 스폰 기록대로 적을 새로 만들어 돌려줍니다(수/종류/위치가 처음과 같고 체력은
    // 가득 찬 상태). 기존에 남아있는 적을 지우는 것은 호출한 쪽의 책임입니다. 이 방에 대한 스폰
    // 기록이 없으면 null을 돌려줍니다.
    List<EnemyHealth> RespawnEnemies(RoomInstance room, Transform parent);
}
