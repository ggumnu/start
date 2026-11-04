using System.Collections.Generic;
using UnityEngine;

// 존재하다가 사라지는 개체(처치된 적, 다 비운 보물상자, 다 가져간 동아리방 보물상자, 방 클리어로
// 열리는 바리케이드)가 갑자기 뚝 끊기듯 사라지는 대신, 흰색으로 완전히 물든 뒤 서서히
// 투명해지면서 사라지도록 만드는 공용 컴포넌트입니다. 원래는 여기저기서
// Destroy(gameObject)를 직접 호출하던 지점들(EnemyHealth.Die(), TreasureChest/
// ItemPedestal의 다 먹은 이후 처리, RoomController.UnlockGates())에서 이 컴포넌트를
// 붙이는 방식으로 대체해서 사용합니다.
//
// [판정 즉시, 행동도 즉시 정지 - 핵심 요구사항]
// BeginDissolve()가 호출되는 즉시 대상의 모든 Collider(자식 포함)를 비활성화합니다.
// 그래야 "이미 죽은 적이 총알을 막거나, 이미 죽은 적이 플레이어에게 닿는" 문제가
// 생기지 않습니다. 여기에 더해, 적이 사라지는 도중에도 계속 움직이거나 공격 모션을
// 취하는 문제를 막기 위해 자기 자신(DissolveAndVanish)만 남기고 그 오브젝트에 붙은
// 모든 Behaviour(AI/공격 스크립트, NavMeshAgent, Animator 등)를 즉시 꺼버립니다.
// Rigidbody가 있다면 속도를 0으로 만들고 Kinematic으로 전환해 관성으로 미끄러지는
// 것도 막습니다. 시각적으로 사라지는 것(색상/투명도 변화, 최종 Destroy/비활성화)만
// duration초에 걸쳐 서서히 진행됩니다.
//
// [Opaque 머티리얼 -> 런타임 Transparent 전환]
// 이 프로젝트의 3D 모델 머티리얼(URP Lit)은 기본적으로 Surface = Opaque라, 알파값을
// 아무리 바꿔도 화면에 눈에 띄는 효과가 나타나지 않습니다(GPU가 알파를 무시함). 그래서
// 페이드를 시작하는 순간에 그 머티리얼 인스턴스를 Transparent 모드로 전환합니다.
// renderer.materials로 처음 접근하는 순간 자동으로 인스턴스가 생겨나므로, 같은
// 프리팹에서 나온 다른(아직 살아있는) 개체의 머티리얼에는 영향이 없습니다.
//
// [흰색으로 "완전히" 물들었다가 사라지는 2단계 타이밍]
// 처음 버전은 색상->흰색 보간과 알파->0 보간을 처음부터 끝까지 같은 비율(t)로
// 동시에 진행시켰습니다. 그러면 색이 절반쯤 하얘졌을 때 이미 절반쯤 투명해져 있어서
// "완전한 흰색"이 되는 순간이 아예 없이 그냥 투명해지기만 하는 것처럼 보였습니다.
// 그래서 duration을 두 구간으로 나눴습니다: 앞의 whitenPortion 비율 동안은 알파를
// 원래 값으로 유지한 채 색상만 흰색으로 보간하고(완전한 흰색 도달), 남은 구간에서는
// 색상을 흰색으로 고정한 채 알파만 0으로 보간합니다(그 흰색 상태 그대로 투명해짐).
//
// [조명 때문에 흰색이 흐릿해 보이는 문제 - Emission 보정]
// URP Lit 셰이더에서 _BaseColor/_Color는 표면 알베도(기본 색)일 뿐이라, 최종 화면에
// 보이는 색은 여전히 씬의 조명(그림자, 각도에 따른 명암 등)의 영향을 받습니다. 그래서
// _BaseColor만 흰색으로 바꿔도 그림자가 진 부분은 여전히 어둡게 보여 "흰색으로 확
// 물든다"는 느낌이 잘 안 살 수 있습니다(적 피격 시 히트플래시 색을 흰색으로 하면 잘
// 안 보이는 것과 같은 원인입니다). 이를 보정하기 위해 머티리얼이 _EmissionColor를
// 지원하면 흰색 구간 동안 자체발광 값도 같이 끌어올립니다 - 자체발광은 조명의 영향을
// 받지 않고 그대로 더해지므로, 그림자 진 부분까지 포함해 훨씬 확실하게 흰색으로
// 보이게 됩니다.
//
// [스프라이트(SpriteRenderer)는 대상에서 제외]
// TreasureChest/ItemPedestal 위에 떠 있는 아이템 아이콘은 별도 요청에 따라 본체와
// 별개로 즉시 사라져야 하므로, 그 호출부에서 아이콘 오브젝트를 먼저 즉시
// Destroy하고 이 컴포넌트는 본체(3D 메쉬)에만 붙습니다. 혹시 호출 순서상 아이콘이
// 아직 자식으로 붙어있는 채로 이 컴포넌트가 시작되더라도, SpriteRenderer는
// 아예 페이드 대상에서 제외하므로 안전합니다.
//
// [물드는 색 - 98번 추가]
// 기본은 흰색입니다. Begin(대상, 끝 동작, 색)으로 부르면 그 색으로 물들며 사라집니다(98번 즉시 처치 = 빨강). 자체발광도 그 색 기준으로
// 끌어올립니다. 색을 지정하지 않는 기존 호출은 전부 예전처럼 흰색입니다.
public class DissolveAndVanish : MonoBehaviour
{
    public enum EndAction { Destroy, Deactivate }

    [Tooltip("페이드가 끝난 뒤 오브젝트를 완전히 삭제(Destroy)할지, 꺼두기만" +
             "(SetActive(false)) 할지 정합니다. 바리케이드처럼 같은 계층 구조를 계속 " +
             "유지해야 하는 경우 Deactivate를 사용하세요.")]
    public EndAction endAction = EndAction.Destroy;

    [Tooltip("사라짐이 시작된 순간부터 완전히 끝날 때까지 걸리는 시간(초)입니다.")]
    public float duration = 0.5f;

    [Range(0.05f, 0.95f)]
    [Tooltip("duration 중 앞부분 몇 %를 '완전한 흰색으로 물드는' 구간에 쓸지 정합니다. " +
             "예를 들어 0.35면 앞 35% 동안 색만 흰색으로 바뀌고(투명도는 그대로), " +
             "남은 65% 동안 그 흰색 상태에서 서서히 투명해집니다.")]
    public float whitenPortion = 0.35f;

    [Tooltip("흰색 구간 동안 자체발광(Emission) 값을 얼마나 끌어올릴지 정합니다. " +
             "조명/그림자 영향을 받지 않는 밝기이므로, 값을 키울수록 그림자 진 부분까지 " +
             "확실하게 흰색으로 보입니다. 머티리얼이 Emission을 지원하지 않으면 무시됩니다.")]
    public float emissionBoost = 1.5f;

    [Tooltip("사라짐이 시작되는 순간, 이 오브젝트(와 자식)에 붙어있는 다른 모든 " +
             "동작 스크립트/컴포넌트(AI, 공격, NavMeshAgent 등)를 즉시 꺼서 그 자리에 " +
             "멈추도록 할지 정합니다. 적이 죽은 뒤에도 계속 움직이거나 공격 모션을 " +
             "취하는 문제를 막기 위한 옵션이며, 기본적으로 켜져 있습니다.")]
    public bool freezeBehavioursOnBegin = true;

    [Tooltip("물드는 색입니다(기본 흰색). 98번 즉시 처치는 코드가 빨간색으로 지정합니다.")]
    public Color tintColor = Color.white;

    private struct RendererEntry
    {
        public Material[] materials;
        public Color[] originalColors;
        public bool[] usesBaseColor;
        public bool[] usesColor;
        public bool[] usesEmission;
        public Color[] originalEmission;
    }

    private readonly List<RendererEntry> rendererEntries = new List<RendererEntry>();
    private float elapsed = 0f;
    private bool isDissolving = false;

    // 이미 이 컴포넌트가 붙어있으면 그걸 그대로 재사용(프리팹에 미리 붙여놓고
    // duration이나 whitenPortion 등을 개별 조정해도 계속 그 값을 존중), 없으면
    // 기본값으로 새로 붙입니다. 호출부에서 매번 이 메서드 하나만 부르면 되도록 한
    // 정적 헬퍼입니다.
    public static DissolveAndVanish Begin(GameObject target, EndAction endAction)
    {
        var dissolve = target.GetComponent<DissolveAndVanish>();
        if (dissolve == null)
        {
            dissolve = target.AddComponent<DissolveAndVanish>();
        }
        dissolve.endAction = endAction;
        dissolve.BeginDissolve();
        return dissolve;
    }

    // [98번] 지정한 색으로 물들며 사라집니다(그 외에는 위 Begin과 같음).
    public static DissolveAndVanish Begin(GameObject target, EndAction endAction, Color tint)
    {
        var dissolve = target.GetComponent<DissolveAndVanish>();
        if (dissolve == null)
        {
            dissolve = target.AddComponent<DissolveAndVanish>();
        }
        dissolve.endAction = endAction;
        dissolve.tintColor = tint;
        dissolve.BeginDissolve();
        return dissolve;
    }

    // 사라짐을 시작합니다. 대상의 Collider는 이 호출 즉시 전부 비활성화됩니다.
    // freezeBehavioursOnBegin이 켜져 있으면 다른 동작 스크립트들도 즉시 멈춥니다.
    // 이미 진행 중이면 아무 일도 하지 않습니다(중복 호출 방지).
    public void BeginDissolve()
    {
        if (isDissolving) return;
        isDissolving = true;

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        if (freezeBehavioursOnBegin)
        {
            FreezeBehaviours();
        }

        CacheRenderers();
    }

    // 이 컴포넌트 자신을 제외한 나머지 Behaviour(MonoBehaviour뿐 아니라 Animator,
    // NavMeshAgent, AudioSource 등 UnityEngine 기본 컴포넌트 포함)를 전부 꺼서,
    // 사라지는 도중에는 어떤 스크립트도 더 이상 움직이거나 판단을 내리지 못하게
    // 합니다. Renderer/Collider/Transform은 Behaviour가 아니라서 영향받지 않고,
    // 그래서 시각적인 페이드는 계속 정상적으로 진행됩니다.
    // [38번 패시브 작업 중 발견 - 널 항목으로 인한 NullReferenceException 방지]
    // GetComponentsInChildren<Behaviour>()는 스크립트 참조가 끊긴(누락된) 컴포넌트가
    // 자식 어딘가에 하나라도 있으면 그 자리에 null을 채워 반환합니다. 이전에는 이 null을
    // 걸러내지 않아서, 그런 프리팹의 적이 죽을 때 behaviour.enabled = false에서
    // NullReferenceException이 났습니다. 이 예외가 BeginDissolve()를 중간에 끊어버리면
    // 그 뒤에 와야 할 CacheRenderers() 호출이 아예 실행되지 않아 흰색 페이드 연출이
    // 통째로 사라지고(렌더러가 하나도 캐시되지 않으니 Update()에서 색을 바꿀 대상이
    // 없음), 또한 아직 꺼지지 못한 AI/공격 스크립트(NavMeshAgent 포함)가 죽은 뒤에도
    // 계속 움직이려다 별도의 NavMeshAgent 오류까지 냈습니다. null 항목을 건너뛰도록
    // 방어 코드를 추가해 이 컴포넌트가 끝까지 정상적으로 실행되도록 했습니다.
    private void FreezeBehaviours()
    {
        foreach (var behaviour in GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || behaviour == this) continue;
            behaviour.enabled = false;
        }

        // NavMeshAgent 등으로 이미 붙어있던 속도(관성) 때문에 죽은 자리에서 계속
        // 미끄러지듯 밀리는 것까지 막기 위해, Rigidbody가 있다면 속도를 0으로
        // 만들고 물리 영향을 받지 않도록 Kinematic으로 전환합니다.
        //
        // [이미 Kinematic인 Rigidbody는 속도 대입을 건너뜀 - 경고 로그 방지]
        // 이 프로젝트의 적(Enemy) 프리팹들은 NavMeshAgent가 위치를 직접 관리하므로
        // Rigidbody.isKinematic이 애초에 true로 설정되어 있습니다(EnemySeparation.cs
        // 주석 참고). 이미 Kinematic인 Rigidbody에 linearVelocity/angularVelocity를
        // 대입하는 것은 물리적으로 아무 효과가 없는 동작인데도, Unity가 "Setting
        // linear/angular velocity of a kinematic body is not supported" 경고를
        // 매번 띄웁니다. 실제 동작에는 영향이 없었지만(콘솔만 시끄러웠음), non-kinematic
        // 이었던 Rigidbody(=아직 물리 영향을 받던 것)에 대해서만 속도를 0으로 만들고,
        // 이미 Kinematic인 것은 대입 자체를 건너뛰어 경고를 없앴습니다. isKinematic을
        // true로 맞추는 것은 두 경우 모두 그대로 유지합니다.
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }
    }

    private void CacheRenderers()
    {
        rendererEntries.Clear();

        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            // 아이템 아이콘 등에 쓰이는 스프라이트(SpriteRenderer)는 별도로 처리되므로 제외합니다.
            if (rend is SpriteRenderer) continue;

            var materials = rend.materials; // 처음 접근할 때 자동으로 인스턴스화됨
            var originalColors = new Color[materials.Length];
            var usesBaseColor = new bool[materials.Length];
            var usesColor = new bool[materials.Length];
            var usesEmission = new bool[materials.Length];
            var originalEmission = new Color[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                usesBaseColor[i] = mat.HasProperty("_BaseColor");
                usesColor[i] = mat.HasProperty("_Color");
                usesEmission[i] = mat.HasProperty("_EmissionColor");

                if (usesEmission[i])
                {
                    originalEmission[i] = mat.GetColor("_EmissionColor");
                    mat.EnableKeyword("_EMISSION");
                }

                if (!usesBaseColor[i] && !usesColor[i])
                {
                    continue; // 색을 바꿀 방법이 없는 셰이더
                }

                originalColors[i] = usesBaseColor[i] ? mat.GetColor("_BaseColor") : mat.GetColor("_Color");
                MakeTransparent(mat);
            }

            rendererEntries.Add(new RendererEntry
            {
                materials = materials,
                originalColors = originalColors,
                usesBaseColor = usesBaseColor,
                usesColor = usesColor,
                usesEmission = usesEmission,
                originalEmission = originalEmission,
            });
        }
    }

    // URP Lit/Simple Lit 계열의 기본 Opaque 머티리얼을 런타임에 Transparent 모드로
    // 전환하는 헬퍼입니다. Opaque가 아니거나 URP Lit 계열이 아니면(=_Surface 프로퍼티가
    // 없으면) 손대지 않습니다.
    private void MakeTransparent(Material mat)
    {
        if (!mat.HasProperty("_Surface")) return;
        if (Mathf.Approximately(mat.GetFloat("_Surface"), 1f)) return; // 이미 Transparent

        mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
        mat.SetFloat("_Blend", 0f);   // 0 = Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void Update()
    {
        if (!isDissolving) return;

        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        // 앞 구간(whiten) 동안은 색만 흰색으로 보간(알파는 원래 값 유지),
        // 뒤 구간에서는 색이 흰색으로 고정된 채 알파만 0으로 보간합니다.
        float whiten = whitenPortion;
        float colorT = whiten > 0f ? Mathf.Clamp01(t / whiten) : 1f;
        float alphaT = whiten < 1f ? Mathf.Clamp01((t - whiten) / (1f - whiten)) : (t >= 1f ? 1f : 0f);

        foreach (var entry in rendererEntries)
        {
            for (int i = 0; i < entry.materials.Length; i++)
            {
                if (entry.usesBaseColor[i] || entry.usesColor[i])
                {
                    Color c = Color.Lerp(entry.originalColors[i], tintColor, colorT);
                    c.a = Mathf.Lerp(entry.originalColors[i].a, 0f, alphaT);

                    if (entry.usesBaseColor[i]) entry.materials[i].SetColor("_BaseColor", c);
                    if (entry.usesColor[i]) entry.materials[i].SetColor("_Color", c);
                }

                if (entry.usesEmission[i])
                {
                    Color e = Color.Lerp(entry.originalEmission[i], tintColor * emissionBoost, colorT);
                    entry.materials[i].SetColor("_EmissionColor", e);
                }
            }
        }

        if (t >= 1f)
        {
            Finish();
        }
    }

    private void Finish()
    {
        isDissolving = false;

        if (endAction == EndAction.Destroy)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
