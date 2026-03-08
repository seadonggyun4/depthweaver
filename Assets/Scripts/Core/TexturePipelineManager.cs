// Assets/Scripts/Core/TexturePipelineManager.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver — 텍스처 파이프라인 오케스트레이터
// ══════════════════════════════════════════════════════════════════════
//
// ITextureSource로부터 텍스처 갱신 이벤트를 수신하여
// 스크린 메시 머티리얼, 영역광 쿠키, 사분면 광원에 분배한다.
//
// 핵심 설계: 이 클래스는 텍스처 소스의 구체적 구현을 알지 못한다.
// Phase 0의 StaticTextureSource든 Phase 1의 CEFTextureSource든
// ITextureSource 인터페이스만 참조하므로, 소스 교체 시 이 코드는 변경되지 않는다.
//
// Phase 4 확장:
//   - QuadrantLightSystem 연동 (사분면 광원 모드 전환)
//   - 메인/사분면 광원 자동 전환 오케스트레이션

using System;
using UnityEngine;

public class TexturePipelineManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 인스펙터 참조
    // ═══════════════════════════════════════════════════

    [Header("Configuration")]
    [SerializeField] private UIShaderConfig config;

    [Header("Scene Components")]
    [Tooltip("스크린 메시 (머티리얼에 텍스처 할당)")]
    [SerializeField] private ScreenMeshGenerator screenMesh;

    [Tooltip("영역광 컨트롤러 (쿠키 텍스처 갱신)")]
    [SerializeField] private ScreenLightController screenLight;

    [Tooltip("사분면 다중 광원 시스템 (선택적, config에서 활성/비활성)")]
    [SerializeField] private QuadrantLightSystem quadrantLightSystem;

    [Tooltip("깊이 텍스처 후처리 (선택적, 가우시안 블러 적용)")]
    [SerializeField] private DepthTextureProcessor depthProcessor;

    [Header("Texture Source")]
    [Tooltip("ITextureSource를 구현하는 MonoBehaviour를 할당.\n" +
             "Phase 0: StaticTextureSource\n" +
             "Phase 1: CEFTextureSource")]
    [SerializeField] private MonoBehaviour textureSourceComponent;

    // ═══════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════

    private ITextureSource textureSource;
    private Material screenMaterial;
    private bool isActive;

    // 셰이더 프로퍼티 ID 캐싱 (문자열 해싱 비용 절감)
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int DepthTexId = Shader.PropertyToID("_DepthTex");
    private static readonly int DisplacementScaleId = Shader.PropertyToID("_DisplacementScale");
    private static readonly int DisplacementBiasId = Shader.PropertyToID("_DisplacementBias");
    private static readonly int EdgeFalloffId = Shader.PropertyToID("_EdgeFalloff");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");

    // 성능 측정
    private int colorUpdateCount;
    private int depthUpdateCount;

    // 광원 모드 추적
    private bool lastQuadrantMode;

    /// <summary>마지막 1초간 색상 텍스처 갱신 횟수</summary>
    public int ColorUpdatesPerSecond { get; private set; }

    /// <summary>마지막 1초간 깊이 텍스처 갱신 횟수</summary>
    public int DepthUpdatesPerSecond { get; private set; }

    /// <summary>파이프라인 활성 여부</summary>
    public bool IsActive => isActive;

    // ═══════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        ValidateReferences();
    }

    void Start()
    {
        SetupPipeline();
    }

    void Update()
    {
        if (!isActive || config == null) return;

        // 변위 파라미터의 실시간 변경을 반영
        if (screenMaterial != null)
        {
            screenMaterial.SetFloat(DisplacementScaleId, config.displacementScale);
            screenMaterial.SetFloat(DisplacementBiasId, config.displacementBias);
            screenMaterial.SetFloat(EdgeFalloffId, config.edgeFalloff);
            screenMaterial.SetFloat(EmissionIntensityId, config.emissionIntensity);
        }

        // ─── 광원 모드 오케스트레이션 ───
        UpdateLightMode();
    }

    void OnDestroy()
    {
        DisconnectTextureSource();
    }

    // ═══════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════

    private void ValidateReferences()
    {
        if (config == null)
            Debug.LogError("[UIShader] TexturePipelineManager: UIShaderConfig가 할당되지 않았습니다.");

        if (screenMesh == null)
            Debug.LogError("[UIShader] TexturePipelineManager: ScreenMeshGenerator가 할당되지 않았습니다.");

        if (screenLight == null)
            Debug.LogError("[UIShader] TexturePipelineManager: ScreenLightController가 할당되지 않았습니다.");

        // QuadrantLightSystem은 선택적 (null 허용)
        if (quadrantLightSystem == null)
            Debug.Log("[UIShader] TexturePipelineManager: QuadrantLightSystem 미할당 (단일 광원 모드)");

        if (textureSourceComponent == null)
        {
            Debug.LogError("[UIShader] TexturePipelineManager: textureSourceComponent가 할당되지 않았습니다.");
            return;
        }

        textureSource = textureSourceComponent as ITextureSource;
        if (textureSource == null)
        {
            Debug.LogError($"[UIShader] TexturePipelineManager: " +
                         $"{textureSourceComponent.GetType().Name}이 ITextureSource를 구현하지 않습니다.");
        }
    }

    private void SetupPipeline()
    {
        if (textureSource == null || screenMesh == null || screenLight == null || config == null)
        {
            Debug.LogError("[UIShader] TexturePipelineManager: 필수 참조가 누락되어 파이프라인을 시작할 수 없습니다.");
            return;
        }

        // 스크린 메시 머티리얼 획득
        MeshRenderer renderer = screenMesh.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogError("[UIShader] TexturePipelineManager: ScreenMesh에 MeshRenderer가 없습니다.");
            return;
        }
        screenMaterial = renderer.material;

        // 초기 셰이더 파라미터 설정
        screenMaterial.SetFloat(DisplacementScaleId, config.displacementScale);
        screenMaterial.SetFloat(DisplacementBiasId, config.displacementBias);
        screenMaterial.SetFloat(EdgeFalloffId, config.edgeFalloff);
        screenMaterial.SetFloat(EmissionIntensityId, config.emissionIntensity);

        // 텍스처 소스 연결
        ConnectTextureSource(textureSource);

        // 성능 측정 시작
        InvokeRepeating(nameof(UpdatePerformanceCounters), 1f, 1f);

        Debug.Log("[UIShader] TexturePipelineManager 파이프라인 시작");
    }

    // ═══════════════════════════════════════════════════
    // 텍스처 소스 관리
    // ═══════════════════════════════════════════════════

    private void ConnectTextureSource(ITextureSource source)
    {
        source.Initialize(config);
        source.OnTextureUpdated += HandleTextureUpdate;
        isActive = true;

        Debug.Log($"[UIShader] 텍스처 소스 연결: {source.GetType().Name}");
    }

    private void DisconnectTextureSource()
    {
        if (textureSource != null)
        {
            textureSource.OnTextureUpdated -= HandleTextureUpdate;
            textureSource.Shutdown();
            isActive = false;
        }
    }

    /// <summary>
    /// 런타임에서 텍스처 소스를 교체한다.
    /// Phase 1 전환 시: pipeline.SetTextureSource(cefTextureSource);
    /// </summary>
    public void SetTextureSource(ITextureSource newSource)
    {
        if (newSource == null)
        {
            Debug.LogError("[UIShader] TexturePipelineManager: null 텍스처 소스를 설정할 수 없습니다.");
            return;
        }

        DisconnectTextureSource();
        textureSource = newSource;
        ConnectTextureSource(textureSource);

        Debug.Log($"[UIShader] 텍스처 소스 교체: {newSource.GetType().Name}");
    }

    // ═══════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═══════════════════════════════════════════════════

    private void HandleTextureUpdate(object sender, TextureUpdateEventArgs args)
    {
        if (screenMaterial == null) return;

        // ─── 색상 텍스처 → 스크린 메시 알베도 + 영역광 쿠키 ───
        if (args.ColorTexture != null)
        {
            // 스크린 메시에 색상 텍스처 적용
            screenMaterial.SetTexture(MainTexId, args.ColorTexture);

            // GPU 쿠키 후처리 + 메인 쿠키 갱신
            screenLight.UpdateCookie(args.ColorTexture);

            // 사분면 광원 모드: 후처리된 쿠키를 크롭하여 분배
            if (quadrantLightSystem != null && quadrantLightSystem.IsEnabled)
            {
                RenderTexture processedCookie = screenLight.ProcessedCookieTexture;
                if (processedCookie != null)
                    quadrantLightSystem.UpdateQuadrantCookies(processedCookie);
            }

            colorUpdateCount++;
        }

        // ─── 깊이 텍스처 → (블러 후처리) → 변위 맵 (변경 시에만) ───
        if (args.DepthChanged && args.DepthTexture != null)
        {
            Texture depthOutput = args.DepthTexture;

            // Phase 2: DepthTextureProcessor를 통해 가우시안 블러 적용
            if (depthProcessor != null && depthProcessor.IsInitialized)
            {
                var blurredRT = depthProcessor.ApplyBlur(args.DepthTexture);
                if (blurredRT != null)
                    depthOutput = blurredRT;
            }

            screenMaterial.SetTexture(DepthTexId, depthOutput);
            depthUpdateCount++;
        }
    }

    // ═══════════════════════════════════════════════════
    // 광원 모드 오케스트레이션
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 메인 광원 ↔ 사분면 광원 모드 전환을 관리한다.
    /// 이중 조명을 방지하고, 자동 노출 강도를 사분면 광원에 동기화한다.
    /// </summary>
    private void UpdateLightMode()
    {
        if (quadrantLightSystem == null) return;

        bool quadrantMode = config.enableQuadrantLights && quadrantLightSystem.IsEnabled;

        // 모드 전환 감지
        if (quadrantMode != lastQuadrantMode)
        {
            screenLight.SetMainLightActive(!quadrantMode);
            lastQuadrantMode = quadrantMode;

            Debug.Log($"[UIShader] 광원 모드 전환: {(quadrantMode ? "사분면 (4광원)" : "단일 (메인)")}");
        }

        // 사분면 모드: 자동 노출 강도 동기화
        if (quadrantMode)
        {
            quadrantLightSystem.SyncIntensity(screenLight.CurrentIntensity);
        }
    }

    // ═══════════════════════════════════════════════════
    // 성능 측정
    // ═══════════════════════════════════════════════════

    private void UpdatePerformanceCounters()
    {
        ColorUpdatesPerSecond = colorUpdateCount;
        DepthUpdatesPerSecond = depthUpdateCount;
        colorUpdateCount = 0;
        depthUpdateCount = 0;
    }
}
