using UnityEngine;

// [버린 아이템 좌대 - 비면 사라짐]
// 슬롯 아이템 버리기(SlotItemDropper)로 만들어진 좌대에 자동으로 붙습니다. 올려진 아이템을 플레이어가 그냥 가져가서(빈 슬롯이라
// 맞바꿀 아이템이 없어) 좌대가 비면, 빈 상자 모델이 바닥에 남지 않도록 좌대를 지웁니다. 맞바꿔서 다른 아이템이 올라가 있으면
// 그대로 남습니다. 상점의 보관 좌대에는 붙지 않으므로 상점 좌대는 비어도 그대로 있습니다.
[RequireComponent(typeof(HoldingPedestal))]
public class DroppedItemPedestal : MonoBehaviour
{
    private HoldingPedestal pedestal;
    private bool hadItem;

    void Awake()
    {
        pedestal = GetComponent<HoldingPedestal>();
    }

    void LateUpdate()
    {
        if (pedestal == null) return;

        if (!pedestal.IsEmpty)
        {
            hadItem = true;
            return;
        }

        // 아이템이 올라간 적이 있는데 지금 비었으면 = 가져감
        if (hadItem) Destroy(gameObject);
    }
}
