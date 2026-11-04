// 액티브 아이템이 스페이스 바로 "사용"됐을 때 실제로 어떤 효과를 일으키는지를 표현하는
// 인터페이스입니다. IPassiveItemEffect와 짝을 이루는 구조이지만, 호출 시점이 다릅니다.
//
// - IPassiveItemEffect.Apply()는 아이템을 "획득"하는 순간 딱 한 번 호출됩니다.
// - IActiveItemEffect.Activate()는 그 아이템을 장착하고 있는 동안, 플레이어가 스페이스 바를
//   누르고 코스트를 지불하는 데 성공할 때마다(=여러 번) 호출됩니다. 코스트가 부족해서
//   사용에 실패한 경우에는 호출되지 않습니다(PlayerActiveItem.TryUse() 참고).
//
// [새 구현체를 만들 때 지켜야 할 것 - IPassiveItemEffect와 동일한 규칙]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것.
// - 매개변수 없는 생성자를 가질 것(필드 + 기본값만으로 구성) - ActiveItemEffectDrawer.cs가
//   인스펙터 드롭다운에 그 타입을 보여주려면 필요합니다.
public interface IActiveItemEffect
{
    // 스페이스 바 사용이 코스트 지불에 성공했을 때 정확히 1번 호출됩니다.
    // context에는 이 효과가 필요로 할 만한 참조(PlayerStats/PlayerHealth/PlayerResources/
    // 플레이어 GameObject)가 담겨 있습니다.
    void Activate(ActiveEffectContext context);
}
