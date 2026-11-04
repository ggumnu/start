using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// [실시간 플레이어 초상화 - SD 플레이어]
// 화면 왼쪽 위 초상화 자리에 지금 움직이는 실제 플레이어 모델의 얼굴을 실시간으로 보여줍니다.
//
// [원리 - 카메라 두 대를 겹쳐 그리기]
// 초상화 전용 그림판(Render Texture) 하나에 카메라 두 대가 차례로 그립니다(URP 카메라 스택).
// 1) 배경 카메라(먼저): 하늘 그라데이션만 그립니다(형빈 확정 B안 - 방향 전환이 빠를 때 건물이 휙휙 지나가 어지러워서 배경 물체는
//    모두 뺌). 그라데이션은 세상 기준 위/아래라, 고개를 숙이거나 들면 화면 안에서 살짝 움직여 은은한 움직임만 남습니다
//    (PortraitSkyGradient 셰이더 - 하늘(Skybox)로 그림).
//    Show Background Scenery를 켜면 예전처럼 지형까지 보입니다(A안 - 적(아군 포함), 총알(플레이어/적), 수류탄, 파티클/트레일/선
//    이펙트, 보호막 연출은 이 카메라가 그리는 순간에만 잠깐 숨김 - Renderer.forceRenderingOff, 메인 화면에는 아무 영향 없음).
// 2) 플레이어 카메라(나중): 플레이어 모델만 그리고, 그리기 전에 깊이 정보를 지워서 배경 위에 항상 맨 앞으로 그려집니다.
//    그래서 카메라와 얼굴 사이에 적/벽/이펙트/보호막이 들어와도 얼굴은 절대 가려지지 않습니다.
// 완성된 그림판은 PlayerHUDManager의 초상화 자리(흰 테두리 안)를 꽉 채워 표시됩니다.
//
// [플레이어만 골라 그리는 방법 - 레이어]
// 플레이어 모델의 렌더러(몸/머리카락/얼굴/총 등 - 콜라이더가 없는 것만)를 portraitLayerName 레이어(기본 "Player" - 이미 있는
// 레이어)로 옮기고, 플레이어 카메라는 이 레이어만, 배경 카메라는 이 레이어를 뺀 나머지를 그립니다. 같은 레이어에 플레이어 모델이
// 아닌 렌더러(다른 무언가)가 있으면 플레이어 카메라가 그리는 순간 잠깐 숨깁니다. 메인 카메라는 이 레이어도 보도록 자동으로 맞춥니다.
// 나중에 붙는 외형 파츠(SDPartAttacher)도 잡히도록 0.5초마다 다시 모읍니다.
//
// [얼굴에 고정 - 형빈 확정]
// 카메라는 머리 뼈(Humanoid Head)에 완전히 붙어서 얼굴과 수평하게 마주봅니다. 공격/이동으로 고개를 숙이거나 들거나 기울여도
// 카메라가 똑같이 따라 움직이므로, 초상화 속 얼굴은 항상 같은 자리/같은 크기로 정면을 보고 그 뒤의 배경이 움직입니다
// (몸이 들썩이는 박진감은 배경이 흔들리며 흘러가는 것으로 표현됨). 얼굴 방향은 시작 자세의 모델 정면을 머리 뼈에 붙여 둔 것입니다.
//
// [초상화 전용 보정 - 메인 화면은 그대로]
// - 피격 점멸: 초상화에서는 깜빡이지 않고 피격 표정만 보입니다(hideHitFlashInPortrait - PlayerHitFlash.ShowNormalForPortrait).
// - 조명: 초상화를 그리는 동안에만 SD 캐릭터 셰이더(Toon_Character_SD/Toon_Face_SD)가 태양 대신 카메라 쪽에서 비추는 빛으로
//   명암을 나눠, 고개를 숙여도 얼굴이 그늘지지 않습니다(카툰 명암/입체감 유지, 머리카락 그림자 제거 - portraitLight*).
//
// [붙이는 법]
// SD 플레이어 프리팹 루트(PlayerHealth 등이 붙은 오브젝트)에 Add Component로 붙입니다. 값은 Play 중에 바꾸면 바로 반영됩니다
// (그림판 크기만 다음 Play부터). 초상화 표시 크기 자체는 PlayerHUDManager의 Portrait Size입니다.
[DefaultExecutionOrder(1000)] // 애니메이션/조준 IK/반동(LateUpdate)이 끝난 뒤의 머리 위치를 쓰도록 맨 마지막에 실행
[DisallowMultipleComponent]
public class PlayerPortraitCamera : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("얼굴 기준이 될 머리 오브젝트입니다. 비워두면 Humanoid 아바타의 Head 뼈를 자동으로 찾습니다.")]
    public Transform headOverride;
    [Tooltip("플레이어 모델을 옮길 레이어 이름입니다(기본 \"Player\" - 이미 있는 레이어). 플레이어 카메라는 이 레이어만 그립니다.")]
    public string portraitLayerName = "Player";

    [Header("그림판")]
    [Tooltip("초상화 그림판 해상도(정사각형 픽셀)입니다. 초상화를 크게 띄울수록 올려주세요. 다음 Play부터 반영됩니다.")]
    [Min(64)] public int textureSize = 512;
    [Tooltip("하늘 그라데이션을 끄거나 그라데이션 셰이더를 찾지 못했을 때 배경에 칠할 단색입니다.")]
    public Color backgroundColor = new Color(0.55f, 0.8f, 0.95f, 1f);

    [Header("배경 (하늘 그라데이션)")]
    [Tooltip("켜면 예전처럼 플레이어 뒤의 지형/건물까지 보입니다. 끄면(기본) 플레이어와 하늘만 보입니다.")]
    public bool showBackgroundScenery = false;
    [Tooltip("켜면 하늘을 위아래 그라데이션으로 그립니다(고개를 숙이거나 들면 살짝 움직임). 끄면 Background Color 단색입니다.")]
    public bool useGradientSky = true;
    [Tooltip("하늘 그라데이션 셰이더입니다. Assets/Shaders/PortraitSkyGradient.shader를 넣으세요(빌드에 포함되도록). " +
             "비우면 이름으로 찾아 쓰고, 그래도 없으면 Background Color 단색으로 칠합니다.")]
    public Shader skyShader;
    [Tooltip("하늘 위쪽 색입니다.")]
    public Color skyTopColor = new Color(0.40f, 0.70f, 0.94f, 1f);
    [Tooltip("하늘 아래쪽 색입니다.")]
    public Color skyBottomColor = new Color(0.80f, 0.92f, 0.99f, 1f);
    [Tooltip("그라데이션의 촘촘함입니다. 클수록 초상화 안에서 위아래 색 차이가 크고, 고개를 움직일 때 그라데이션이 크게 움직입니다.")]
    [Min(0f)] public float skyGradientScale = 2.5f;
    [Tooltip("그라데이션 중심을 위(+)/아래(-)로 옮깁니다. +로 올리면 아래쪽 연한 색이 더 많이 보입니다.")]
    [Range(-1f, 1f)] public float skyGradientOffset = 0f;
    [Tooltip("켜면 배경 카메라에도 후처리(Bloom 등)를 적용합니다.")]
    public bool postProcessing = false;

    [Header("구도 (얼굴에 고정 - 얼굴은 가만히, 배경이 움직임)")]
    [Tooltip("얼굴에서 카메라까지의 거리입니다. 클수록 얼굴이 작게(몸이 더 많이) 보입니다.")]
    [Min(0.01f)] public float distance = 0.9f;
    [Tooltip("카메라 화각(도)입니다. 작을수록 원근감이 줄고 얼굴이 크게 보입니다.")]
    [Range(5f, 90f)] public float fieldOfView = 30f;
    [Tooltip("카메라가 바라보는 얼굴 중심 = 머리 뼈 위치 + 이 값입니다. 얼굴 기준 방향이라 고개를 숙여도 같이 움직입니다 " +
             "(x = 얼굴 오른쪽, y = 얼굴 위쪽, z = 얼굴 앞쪽). 얼굴 중심이 화면 가운데 오도록 맞춰주세요.")]
    public Vector3 faceOffset = new Vector3(0f, 0.05f, 0f);
    [Tooltip("얼굴 정면에서 위/아래로 돌려 찍는 각도입니다(0 = 수평하게 정면, 양수 = 위에서 내려다봄).")]
    [Range(-60f, 60f)] public float tiltAngle = 0f;
    [Tooltip("얼굴 정면에서 좌/우로 돌려 찍는 각도입니다(0 = 정면).")]
    [Range(-90f, 90f)] public float turnAngle = 0f;
    [Tooltip("카메라 앞쪽 클리핑 거리입니다. 얼굴이 잘려 보이면 낮추세요.")]
    [Min(0.001f)] public float nearClip = 0.02f;

    [Header("초상화 조명 (초상화에서만 - 메인 화면은 그대로)")]
    [Tooltip("초상화에서 태양 대신 카메라 쪽 빛을 얼마나 쓸지입니다(0 = 실제 태양 그대로, 1 = 항상 카메라 쪽에서 비춤). " +
             "카툰 명암(입체감)은 유지되고, 머리카락 등이 드리우는 그림자는 이 비율만큼 사라집니다. SD 캐릭터 셰이더에만 적용됩니다.")]
    [Range(0f, 1f)] public float portraitLightAmount = 1f;
    [Tooltip("초상화 빛이 카메라보다 얼마나 위에서 내려오는지(도)입니다. 0이면 카메라 정면에서, 클수록 위에서 비춰 아래쪽에 명암이 생깁니다.")]
    [Range(-60f, 80f)] public float portraitLightElevation = 25f;
    [Tooltip("초상화 빛이 좌우로 얼마나 비껴서 오는지(도)입니다. 0이면 정면, 값을 주면 한쪽 볼에 명암이 생겨 입체감이 커집니다.")]
    [Range(-80f, 80f)] public float portraitLightSide = 20f;

    [Header("초상화 점멸")]
    [Tooltip("켜면 초상화에서는 피격 점멸(깜빡임)을 숨기고 피격 표정만 보여줍니다. 메인 화면은 그대로 깜빡입니다.")]
    public bool hideHitFlashInPortrait = true;

    private const float RescanInterval = 0.5f;

    private Animator animator;
    private Transform head;
    private Vector3 headLocalForward = Vector3.forward;
    private Vector3 headLocalUp = Vector3.up;

    private static readonly int PortraitLightAmountId = Shader.PropertyToID("_SDPortraitLightAmount");
    private static readonly int PortraitLightDirId = Shader.PropertyToID("_SDPortraitLightDir");
    private PlayerHitFlash hitFlash;
    private bool flashSuspended;
    private int portraitLayer = -1;

    private RenderTexture renderTexture;
    private GameObject rig;
    private Camera backgroundCamera;
    private Skybox backgroundSkybox;
    private Material skyMaterial;
    private Shader skyShaderInUse;
    private bool skyWarned;
    private Camera playerCamera;
    private RawImage portraitImage;
    private PlayerHUDManager hud;

    private readonly HashSet<Renderer> ownedRenderers = new HashSet<Renderer>();
    private float nextRescanTime;

    // 배경 카메라에서 숨길지 판단한 결과(렌더러마다 한 번만 판단)
    private readonly Dictionary<Renderer, bool> hideInBackgroundCache = new Dictionary<Renderer, bool>();
    private float nextCachePurgeTime;
    private readonly List<Renderer> hideForBackground = new List<Renderer>();
    private readonly List<Renderer> hideForPlayer = new List<Renderer>();
    private readonly List<Renderer> currentlyHidden = new List<Renderer>();

    void Start()
    {
        portraitLayer = LayerMask.NameToLayer(portraitLayerName);
        if (portraitLayer < 0)
        {
            Debug.LogWarning($"PlayerPortraitCamera: '{portraitLayerName}' 레이어가 없습니다. Project Settings > Tags and Layers에서 추가하거나 " +
                             "Portrait Layer Name을 있는 레이어 이름으로 바꿔주세요. 실시간 초상화를 끕니다.");
            enabled = false;
            return;
        }

        FindHead();
        BuildRig();
        RescanOwnedRenderers();

        RenderPipelineManager.beginCameraRendering += HandleBeginCamera;
        RenderPipelineManager.endCameraRendering += HandleEndCamera;
    }

    void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCamera;
        RenderPipelineManager.endCameraRendering -= HandleEndCamera;
        RestoreHidden();
        Shader.SetGlobalFloat(PortraitLightAmountId, 0f);

        if (portraitImage != null) Destroy(portraitImage.gameObject);
        if (rig != null) Destroy(rig);
        if (skyMaterial != null) Destroy(skyMaterial);
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    // ---------------- 준비 ----------------

    private void FindHead()
    {
        animator = GetComponentInChildren<Animator>();
        head = headOverride;
        if (head == null && animator != null && animator.isHuman)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        if (head == null)
        {
            Debug.LogWarning("PlayerPortraitCamera: 머리 뼈를 찾지 못해 플레이어 위치 기준으로 찍습니다. Head Override 칸에 머리 오브젝트를 넣어주세요.");
            head = transform;
        }

        // 지금(시작 자세) 모델 정면을 머리 뼈 기준 방향으로 기억해 두면, 이후 머리가 돌아간 방향을 알 수 있습니다.
        Transform modelRoot = animator != null ? animator.transform : transform;
        headLocalForward = Quaternion.Inverse(head.rotation) * FlatForward(modelRoot);
        headLocalUp = Quaternion.Inverse(head.rotation) * Vector3.up;
    }

    private void BuildRig()
    {
        renderTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
        {
            name = "PlayerPortraitRT",
            antiAliasing = 1,
        };
        renderTexture.Create();

        rig = new GameObject("PlayerPortraitRig");

        // 1) 배경 카메라(기본 카메라) - 그림판에 먼저 그림
        var bgGO = new GameObject("PortraitBackgroundCamera");
        bgGO.transform.SetParent(rig.transform, false);
        backgroundCamera = bgGO.AddComponent<Camera>();
        backgroundCamera.targetTexture = renderTexture;
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.depth = -100f;
        backgroundSkybox = bgGO.AddComponent<Skybox>(); // 이 카메라만 쓰는 하늘(그라데이션) - 메인 화면 하늘은 그대로
        var bgData = backgroundCamera.GetUniversalAdditionalCameraData();
        bgData.renderType = CameraRenderType.Base;
        bgData.renderShadows = true;

        // 2) 플레이어 카메라(겹쳐 그리는 카메라) - 깊이를 지우고 플레이어만 맨 앞에 그림
        var plGO = new GameObject("PortraitPlayerCamera");
        plGO.transform.SetParent(bgGO.transform, false);
        playerCamera = plGO.AddComponent<Camera>();
        var plData = playerCamera.GetUniversalAdditionalCameraData();
        plData.renderType = CameraRenderType.Overlay;
        // (Overlay 카메라는 URP가 항상 그리기 전에 깊이를 지웁니다 - 그래서 플레이어가 배경 위 맨 앞에 그려짐. 따로 설정할 값 없음)
        plData.renderPostProcessing = false;
        bgData.cameraStack.Add(playerCamera);

        // 메인 카메라가 플레이어 레이어를 보도록 보장합니다(원래 보고 있었다면 변화 없음).
        if (Camera.main != null) Camera.main.cullingMask |= 1 << portraitLayer;

        ApplyCameraSettings();
    }

    private void ApplyCameraSettings()
    {
        int playerMask = 1 << portraitLayer;
        int uiLayer = LayerMask.NameToLayer("UI");
        int bgMask = ~playerMask;
        if (uiLayer >= 0) bgMask &= ~(1 << uiLayer);
        if (!showBackgroundScenery) bgMask = 0; // B안: 배경 물체는 하나도 그리지 않고 하늘만

        backgroundCamera.cullingMask = bgMask;
        backgroundCamera.backgroundColor = backgroundColor;
        ApplySky();
        backgroundCamera.fieldOfView = fieldOfView;
        backgroundCamera.nearClipPlane = nearClip;
        backgroundCamera.GetUniversalAdditionalCameraData().renderPostProcessing = postProcessing;

        playerCamera.cullingMask = playerMask;
        playerCamera.fieldOfView = fieldOfView;
        playerCamera.nearClipPlane = nearClip;
    }

    // 하늘 그라데이션: 배경 카메라만 쓰는 하늘(Skybox 컴포넌트)에 그라데이션 머티리얼을 넣습니다. 끄거나 셰이더가 없으면 단색.
    private void ApplySky()
    {
        Shader shader = null;
        if (useGradientSky)
        {
            shader = skyShader != null ? skyShader : Shader.Find("MobArchive/PortraitSkyGradient");
            if (shader == null && !skyWarned)
            {
                skyWarned = true; // 경고는 한 번만
                Debug.LogWarning("PlayerPortraitCamera: 하늘 그라데이션 셰이더(MobArchive/PortraitSkyGradient)를 찾지 못해 Background Color 단색으로 칠합니다. " +
                                 "Sky Shader 칸에 PortraitSkyGradient.shader를 넣어주세요.");
            }
        }

        if (shader == null)
        {
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            if (backgroundSkybox != null) backgroundSkybox.material = null;
            return;
        }

        if (skyMaterial == null || skyShaderInUse != shader)
        {
            if (skyMaterial != null) Destroy(skyMaterial);
            skyMaterial = new Material(shader) { name = "PortraitSky", hideFlags = HideFlags.HideAndDontSave };
            skyShaderInUse = shader;
        }

        skyMaterial.SetColor("_TopColor", skyTopColor);
        skyMaterial.SetColor("_BottomColor", skyBottomColor);
        skyMaterial.SetFloat("_Scale", skyGradientScale);
        skyMaterial.SetFloat("_Offset", skyGradientOffset);

        if (backgroundSkybox != null && backgroundSkybox.material != skyMaterial) backgroundSkybox.material = skyMaterial;
        backgroundCamera.clearFlags = CameraClearFlags.Skybox;
    }

    // 플레이어 모델의 렌더러(콜라이더가 없는 메시/스프라이트 - 파티클/트레일/선 제외)를 모아 초상화 레이어로 옮깁니다.
    private void RescanOwnedRenderers()
    {
        nextRescanTime = Time.time + RescanInterval;
        ownedRenderers.Clear();

        Transform modelRoot = animator != null ? animator.transform : transform;
        foreach (var rend in modelRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer) continue;
            if (rend.GetComponent<Collider>() != null) continue; // 충돌 판정이 바뀌지 않도록 콜라이더가 있는 오브젝트는 건드리지 않음

            ownedRenderers.Add(rend);
            if (rend.gameObject.layer != portraitLayer) rend.gameObject.layer = portraitLayer;
        }
    }

    // ---------------- 매 프레임 ----------------

    void LateUpdate()
    {
        if (rig == null) return;

        if (Time.time >= nextRescanTime) RescanOwnedRenderers();
        EnsurePortraitImage();
        ApplyCameraSettings();
        UpdateCameraPose();
        CollectHideLists();
    }

    // 초상화 자리(PlayerHUDManager.PortraitRect)를 꽉 채우는 RawImage를 만들어 그림판을 띄웁니다. HUD가 새로 만들어지면 다시 붙입니다.
    private void EnsurePortraitImage()
    {
        if (portraitImage != null) return;

        if (hud == null) hud = Object.FindFirstObjectByType<PlayerHUDManager>();
        if (hud == null || hud.PortraitRect == null) return;

        var go = new GameObject("LivePortrait");
        go.transform.SetParent(hud.PortraitRect, false);
        go.transform.SetAsFirstSibling(); // 흰 테두리보다 아래

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        portraitImage = go.AddComponent<RawImage>();
        portraitImage.texture = renderTexture;
        portraitImage.raycastTarget = false;
    }

    // 카메라를 얼굴에 완전히 고정합니다: 머리 뼈가 숙이거나 들거나 기울어도 카메라가 똑같이 따라 움직여서, 초상화 안의 얼굴은
    // 항상 같은 자리/같은 크기로 정면을 보고 그 뒤 배경만 움직입니다(형빈 확정). 지연/흔들림 없이 매 프레임 바로 붙습니다.
    private void UpdateCameraPose()
    {
        if (head == null) return;

        // 얼굴 기준 방향(시작 자세의 모델 정면/위쪽을 머리 뼈에 붙여 둔 것) - 고개를 숙이면 같이 숙여짐
        Vector3 faceForward = head.rotation * headLocalForward;
        Vector3 faceUp = head.rotation * headLocalUp;
        Quaternion faceRotation = Quaternion.LookRotation(faceForward, faceUp);

        Vector3 faceCenter = head.position + faceRotation * faceOffset;

        // 얼굴 정면(+ 위아래/좌우 각도)에서 얼굴 중심을 수평하게 마주봅니다.
        Quaternion around = faceRotation * Quaternion.Euler(-tiltAngle, turnAngle, 0f);
        Vector3 cameraPos = faceCenter + around * Vector3.forward * distance;

        Transform camT = backgroundCamera.transform;
        camT.position = cameraPos;
        camT.rotation = Quaternion.LookRotation(faceCenter - cameraPos, around * Vector3.up);
    }

    // ---------------- 카메라별로 잠깐 숨기기 ----------------

    // 이번 프레임에 배경 카메라/플레이어 카메라에서 숨길 렌더러를 모읍니다.
    private void CollectHideLists()
    {
        hideForBackground.Clear();
        hideForPlayer.Clear();

        if (Time.time >= nextCachePurgeTime)
        {
            nextCachePurgeTime = Time.time + 5f;
            var dead = new List<Renderer>();
            foreach (var key in hideInBackgroundCache.Keys) if (key == null) dead.Add(key);
            foreach (var key in dead) hideInBackgroundCache.Remove(key);
        }

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            if (rend.gameObject.layer == portraitLayer)
            {
                // 초상화 레이어에 있지만 플레이어 모델이 아닌 것 -> 플레이어 카메라에서 숨김(배경 카메라는 원래 이 레이어를 안 그림)
                if (!ownedRenderers.Contains(rend)) hideForPlayer.Add(rend);
                continue;
            }

            if (!showBackgroundScenery) continue; // 배경 카메라가 아무것도 그리지 않으므로 숨길 필요 없음

            if (!hideInBackgroundCache.TryGetValue(rend, out bool hide))
            {
                hide = ShouldHideInBackground(rend);
                hideInBackgroundCache[rend] = hide;
            }
            if (hide) hideForBackground.Add(rend);
        }
    }

    // A안(지형만): 적/아군, 총알, 수류탄, 파티클/트레일/선, 보호막 연출을 배경에서 뺍니다.
    private static bool ShouldHideInBackground(Renderer rend)
    {
        if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer) return true;
        if (rend.GetComponentInParent<EnemyHealth>() != null) return true;
        if (rend.GetComponentInParent<EnemyProjectile>() != null) return true;
        if (rend.GetComponentInParent<Projectile>() != null) return true;
        if (rend.GetComponentInParent<Grenade>() != null) return true;

        var mat = rend.sharedMaterial;
        if (mat != null && mat.shader != null && mat.shader.name == "MobArchive/ShieldBreakIcosa") return true;
        return false;
    }

    private void HandleBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == null) return;
        if (cam == backgroundCamera)
        {
            HideAll(hideForBackground);
        }
        else if (cam == playerCamera)
        {
            HideAll(hideForPlayer);

            // 초상화에서는 피격 점멸을 숨깁니다(점멸이 꺼 둔 렌더러를 이 카메라가 그리는 동안만 켬).
            if (hideHitFlashInPortrait)
            {
                if (hitFlash == null) hitFlash = GetComponent<PlayerHitFlash>();
                if (hitFlash != null)
                {
                    hitFlash.ShowNormalForPortrait();
                    flashSuspended = true;
                }
            }

            // 초상화 조명 켜기 - 이 카메라의 그리기 명령 앞에 넣어 순서를 보장합니다.
            SetPortraitLighting(context, portraitLightAmount, ComputePortraitLightDir());
        }
    }

    private void HandleEndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == backgroundCamera || cam == playerCamera) RestoreHidden();

        if (cam == playerCamera)
        {
            if (flashSuspended && hitFlash != null) hitFlash.RestoreAfterPortrait();
            flashSuspended = false;

            // 초상화 조명 끄기(메인 화면은 실제 태양 그대로)
            SetPortraitLighting(context, 0f, Vector3.up);
        }
    }

    // 초상화 빛 방향(얼굴에서 빛 쪽으로 향하는 방향): 카메라 쪽에서 portraitLightElevation만큼 위, portraitLightSide만큼 옆.
    private Vector3 ComputePortraitLightDir()
    {
        Transform camT = backgroundCamera.transform;
        Vector3 towardCamera = -camT.forward;
        Quaternion offset = Quaternion.AngleAxis(portraitLightSide, camT.up) * Quaternion.AngleAxis(-portraitLightElevation, camT.right);
        return (offset * towardCamera).normalized;
    }

    private static void SetPortraitLighting(ScriptableRenderContext context, float amount, Vector3 dir)
    {
        var cmd = CommandBufferPool.Get("PortraitLighting");
        cmd.SetGlobalFloat(PortraitLightAmountId, amount);
        cmd.SetGlobalVector(PortraitLightDirId, new Vector4(dir.x, dir.y, dir.z, 0f));
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        // 혹시 명령 버퍼 밖에서 그려지는 경우도 같은 값이 되도록 즉시 값도 맞춥니다.
        Shader.SetGlobalFloat(PortraitLightAmountId, amount);
        Shader.SetGlobalVector(PortraitLightDirId, new Vector4(dir.x, dir.y, dir.z, 0f));
    }

    private void HideAll(List<Renderer> list)
    {
        foreach (var rend in list)
        {
            if (rend == null || rend.forceRenderingOff) continue; // 원래 꺼져 있던 것은 건드리지 않음
            rend.forceRenderingOff = true;
            currentlyHidden.Add(rend);
        }
    }

    // 이 컴포넌트가 숨긴 것만 원래대로 되돌립니다(메인 화면에는 항상 보임).
    private void RestoreHidden()
    {
        foreach (var rend in currentlyHidden)
        {
            if (rend != null) rend.forceRenderingOff = false;
        }
        currentlyHidden.Clear();
    }

    // ---------------- 도우미 ----------------

    private static Vector3 FlatForward(Transform t)
    {
        return Flatten(t.forward, Vector3.forward);
    }

    private static Vector3 Flatten(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
    }
}
