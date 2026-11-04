using UnityEngine;

// 패시브 아이템 2번 - "움직이면 지나간 자리에 발자국이 남고, 발자국 위의 적은 이동 속도가
// 50% 감소한다. 발자국은 일정 시간이 지나면 서서히 사라지고, 방을 나가면 초기화된다."
//
// [이 파일의 역할 - 설정값만 들고 있음]
// 이 클래스는 인스펙터에서 조절하는 값(발자국 스프라이트, 크기, 간격, 감속 배율 등)만
// 담고 있고, 실제 동작은 전부 FootprintTrail(플레이어에 붙는 부품)이 합니다.
// 아이템을 획득하면 Apply()가 딱 한 번 호출되어 플레이어에 FootprintTrail을 붙이고 이
// 설정을 넘겨줍니다. FootprintTrail은 이 객체의 값을 매번 직접 읽으므로, Play 중에
// 인스펙터 값을 바꾸면 곧바로 반영됩니다(이미 놓인 발자국의 모양/색은 그대로이고, 새로
// 놓이는 발자국부터 바뀝니다. 판정 반경/감속 배율/간격/최대 개수는 즉시 반영).
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 FootprintTrailEffect를 고르면
// 아래 필드들이 나타납니다. 스프라이트는 Texture Type을 "Sprite (2D and UI)"로 가져온
// 이미지를 Left Foot Sprite 칸에 넣으세요(비워두면 임시 타원 모양으로 표시됩니다).
[System.Serializable]
public class FootprintTrailEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("발자국 모양")]
    [Tooltip("왼발 발자국 스프라이트입니다. 이미지의 '위쪽'이 걸어가는 방향을 가리키도록 " +
             "그려주세요. 비워두면 임시 타원 모양이 표시됩니다.")]
    public Sprite leftFootSprite;
    [Tooltip("오른발 발자국 스프라이트입니다. 비워두면 왼발 스프라이트를 좌우 반전해서 씁니다.")]
    public Sprite rightFootSprite;
    [Tooltip("발자국 색/투명도입니다. 흰색이면 스프라이트 원본 색 그대로 표시됩니다.")]
    public Color footprintColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("발자국의 긴 쪽 변 길이(월드 단위)입니다. 스프라이트 원본 픽셀 크기와 무관하게 " +
             "이 값에 맞춰 크기가 조절됩니다.")]
    public float footprintSize = 0.4f;

    [Header("발자국 배치")]
    [Tooltip("플레이어가 이 거리(월드 단위)만큼 움직일 때마다 발자국이 하나 놓입니다.")]
    public float stepDistance = 0.5f;
    [Tooltip("진행 방향 기준 좌우 발이 중심선에서 벌어진 거리입니다(0이면 일직선).")]
    public float lateralOffset = 0.12f;
    [Tooltip("플레이어 발 높이에서 얼마나 띄워서 놓을지입니다. 발자국이 바닥에 파묻혀 안 보이거나 " +
             "깜빡거리면(Z-fighting) 이 값을 조금씩 올려주세요. Play 중에도 새로 놓이는 " +
             "발자국부터 바로 반영됩니다.")]
    public float groundOffset = 0.03f;
    [Tooltip("동시에 존재할 수 있는 발자국 최대 개수입니다. 넘치면 가장 오래된 것부터 " +
             "재활용됩니다(그 자리의 감속도 함께 사라집니다).")]
    public int maxFootprints = 400;

    [Header("발자국 수명")]
    [Tooltip("발자국이 놓인 뒤 원래 모습 그대로 유지되는 시간(초)입니다. 이 시간이 지나면 " +
             "서서히 투명해지기 시작합니다. 0 이하로 두면 사라지지 않습니다(방을 나갈 때까지 유지).")]
    public float lifetime = 5f;
    [Tooltip("수명이 끝난 뒤 완전히 투명해져서 사라지기까지 걸리는 시간(초)입니다. " +
             "이 시간 동안에도 발자국 위의 적은 계속 감속되고, 완전히 사라지는 순간 감속도 함께 풀립니다. " +
             "0으로 두면 서서히 사라지지 않고 수명이 끝나는 즉시 사라집니다.")]
    public float fadeDuration = 1f;

    [Header("적 감속")]
    [Tooltip("발자국 위에 있는 적의 이동 속도 배율입니다(0.5 = 50% 감소). 발자국이 여러 개 " +
             "겹쳐도 중첩되지 않고 이 값으로 고정됩니다.")]
    public float slowMultiplier = 0.5f;
    [Tooltip("발자국 중심에서 이 거리(월드 단위) 안에 적이 있으면 '경로 위'로 판정합니다. " +
             "발자국 사이에 빈틈이 없도록 Step Distance의 절반보다 충분히 크게 두세요.")]
    public float slowRadius = 0.45f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var trail = context.PlayerObject.GetComponent<FootprintTrail>();
        if (trail == null) trail = context.PlayerObject.AddComponent<FootprintTrail>();

        trail.Configure(this, sourceItemId);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    // 발자국 컴포넌트를 즉시 떼어냅니다 - 떼는 순간 남아 있던 발자국과 적 감속도 함께 정리됩니다(FootprintTrail.OnDestroy).
    // 같은 프레임에 다시 얻을 수 있으므로 Destroy가 아니라 DestroyImmediate를 씁니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var trail = context.PlayerObject.GetComponent<FootprintTrail>();
        if (trail != null) Object.DestroyImmediate(trail);
    }
}
