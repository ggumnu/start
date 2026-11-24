// 보스 체력바(BossHealthBarDisplay) 오른쪽에 그로기 게이지(흰 테두리 + 회색 원 + 시계 방향으로 차오르는 노란 도넛)를 보이게 하는 보스용 약속입니다.
// 보스 루트에 붙은 컴포넌트가 이걸 구현하면 체력바가 알아서 찾아 게이지를 그립니다(지금은 3레벨 보스 BossLv3AIController - 비콘 하나 해제당 한 칸).
// 체력바 쪽이 보스 종류를 몰라도 되게 인터페이스로 뺐습니다(2레벨 보스 BossShield 바처럼 보스마다 따로 분기하지 않음).
public interface IBossGroggyGauge
{
    // 지금 게이지를 보일지(보스가 죽었거나 게이지를 쓰지 않는 페이즈면 false).
    bool ShowsGroggyGauge { get; }

    // 0~1 - 차오른 양(1이면 가득 = 그로기). 체력바가 이 값을 향해 부드럽게 따라갑니다.
    float GroggyGaugeRatio { get; }
}
