// Assets/Scripts/Core/UIShaderConfig.cs
// UIShader 전역 설정 ScriptableObject
// 모든 파이프라인 컴포넌트가 참조하는 단일 설정 소스

using UnityEngine;

[CreateAssetMenu(fileName = "UIShaderConfig", menuName = "UIShader/Config")]
public class UIShaderConfig : ScriptableObject
{
    // ═══════════════════════════════════════════════════
    // 스크린 설정 (CineShader 호환)
    // ═══════════════════════════════════════════════════

    [Header("Screen")]
    [Tooltip("CEF 및 텍스처 해상도 (CineShader 호환: 512)")]
    public int screenResolution = 512;

    [Tooltip("스크린 메시 월드 크기 (CineShader SCREEN_SIZE = 6)")]
    public float screenWorldSize = 6f;

    [Tooltip("스크린 메시 세그먼트 수 (CineShader SCREEN_SEGMENTS = 511)")]
    public int screenSegments = 511;

    [Tooltip("스크린 메시 위치")]
    public Vector3 screenPosition = new Vector3(0f, 3.5f, -2f);

    [Tooltip("스크린 메시 회전 (카메라를 향하도록)")]
    public Vector3 screenRotation = new Vector3(0f, 180f, 0f);

    // ═══════════════════════════════════════════════════
    // 변위 설정
    // ═══════════════════════════════════════════════════

    [Header("Displacement")]
    [Tooltip("깊이 맵 기반 정점 변위 스케일")]
    [Range(0f, 2f)]
    public float displacementScale = 0.5f;

    [Tooltip("변위 오프셋 (바이어스)")]
    [Range(-1f, 1f)]
    public float displacementBias = 0f;

    [Tooltip("경계 감쇠 범위 (메시 가장자리에서 변위를 부드럽게 줄임)")]
    [Range(0f, 0.2f)]
    public float edgeFalloff = 0.05f;

    [Tooltip("스크린 자체 발광 강도")]
    [Range(0f, 5f)]
    public float emissionIntensity = 0.3f;

    [Tooltip("깊이 맵 가우시안 블러 반복 횟수")]
    [Range(0, 10)]
    public int depthBlurIterations = 1;

    [Tooltip("블러 커널 크기")]
    [Range(1, 10)]
    public int depthBlurKernelSize = 5;

    // ═══════════════════════════════════════════════════
    // 영역광 설정
    // ═══════════════════════════════════════════════════

    [Header("Area Light")]
    [Tooltip("RectAreaLight 기본 강도 (루멘)")]
    public float lightIntensity = 5000f;

    [Tooltip("영역광 도달 범위 (미터)")]
    [Range(5f, 50f)]
    public float lightRange = 30f;

    [Tooltip("쿠키 밝기 승수")]
    [Range(0.1f, 10f)]
    public float cookieIntensityMultiplier = 1f;

    [Tooltip("투사 광 색상 선명도 증가")]
    [Range(0f, 2f)]
    public float cookieSaturationBoost = 1f;

    [Tooltip("비선형 밝기 매핑 커브")]
    public AnimationCurve cookieContrastCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // ═══════════════════════════════════════════════════
    // 자동 노출
    // ═══════════════════════════════════════════════════

    [Header("Auto Exposure")]
    [Tooltip("자동 노출 활성화: 페이지 밝기에 따라 광원 강도를 자동 조절")]
    public bool autoExposureEnabled = true;

    [Tooltip("밝은 페이지의 강도 승수 (최소, 과포화 방지)")]
    [Range(0.3f, 1.5f)]
    public float autoExposureMinMultiplier = 0.6f;

    [Tooltip("어두운 페이지의 강도 승수 (최대, 가시성 확보)")]
    [Range(0.5f, 3f)]
    public float autoExposureMaxMultiplier = 1.5f;

    [Tooltip("자동 노출 보간 속도 (높을수록 빠른 반응)")]
    [Range(0.5f, 10f)]
    public float autoExposureSmoothSpeed = 3f;

    // ═══════════════════════════════════════════════════
    // 사분면 다중 광원
    // ═══════════════════════════════════════════════════

    [Header("Quadrant Lights")]
    [Tooltip("2×2 사분면 다중 광원 활성화 (공간적 정확도 향상, 성능 비용 증가)")]
    public bool enableQuadrantLights = false;

    [Tooltip("사분면 광원 간 간격 (월드 유닛)")]
    [Range(0f, 0.2f)]
    public float quadrantLightSpacing = 0.05f;

    // ═══════════════════════════════════════════════════
    // CEF 설정 (Phase 1+)
    // ═══════════════════════════════════════════════════

    [Header("CEF (Phase 1+)")]
    [Tooltip("CEF 브라우저 초기 URL")]
    public string defaultURL = "https://mui.com";

    [Tooltip("마우스/키보드 입력 전달 활성화")]
    public bool enableInput = true;

    [Tooltip("CEF 렌더링 프레임레이트")]
    public int cefFrameRate = 60;

    // ═══════════════════════════════════════════════════
    // 자연환경 — 지형
    // ═══════════════════════════════════════════════════

    [Header("Terrain")]
    [Tooltip("지형 크기 (한 변의 길이, 유닛)")]
    public float terrainSize = 100f;

    [Tooltip("지형 메시 해상도 (한 변의 정점 수 - 1)")]
    [Range(32, 256)]
    public int terrainResolution = 128;

    [Tooltip("지형 최대 높이 (유닛)")]
    [Range(1f, 20f)]
    public float terrainMaxHeight = 5f;

    [Tooltip("Perlin 노이즈 스케일 (클수록 완만한 지형)")]
    [Range(10f, 200f)]
    public float terrainNoiseScale = 50f;

    [Tooltip("노이즈 옥타브 수 (많을수록 디테일 증가)")]
    [Range(1, 5)]
    public int terrainNoiseOctaves = 3;

    [Tooltip("지형 기본 색상 (잔디 초록)")]
    public Color terrainBaseColor = new Color(0.25f, 0.45f, 0.15f, 1f);

    [Tooltip("지형 스무스니스")]
    [Range(0f, 1f)]
    public float terrainSmoothness = 0.3f;

    // ═══════════════════════════════════════════════════
    // 자연환경 — 하늘 및 태양
    // ═══════════════════════════════════════════════════

    [Header("Sky & Sun")]
    [Tooltip("고정 노출값 (주간 야외: 10~13)")]
    [Range(6f, 16f)]
    public float skyExposure = 11f;

    [Tooltip("태양 강도 (럭스)")]
    public float sunIntensity = 50000f;

    [Tooltip("태양 색상")]
    public Color sunColor = new Color(1f, 0.96f, 0.84f, 1f);

    [Tooltip("태양 고도각 (0=수평, 90=정오)")]
    [Range(10f, 80f)]
    public float sunElevation = 45f;

    [Tooltip("태양 방위각")]
    [Range(-180f, 180f)]
    public float sunAzimuth = -30f;

    // ═══════════════════════════════════════════════════
    // 자연환경 — 초목
    // ═══════════════════════════════════════════════════

    [Header("Vegetation")]
    [Tooltip("잔디 총 개수 (밀도)")]
    [Range(1000, 50000)]
    public int grassDensity = 8000;

    [Tooltip("잔디 기본 높이 (유닛)")]
    [Range(0.1f, 1.5f)]
    public float grassHeight = 0.4f;

    [Tooltip("잔디 높이 변동폭")]
    [Range(0f, 0.5f)]
    public float grassHeightVariation = 0.15f;

    [Tooltip("잔디 색상")]
    public Color grassColor = new Color(0.3f, 0.55f, 0.2f, 1f);

    // ═══════════════════════════════════════════════════
    // 자연환경 — 대기
    // ═══════════════════════════════════════════════════

    [Header("Atmosphere")]
    [Tooltip("포그 평균 자유 경로 (클수록 옅은 안개)")]
    [Range(50f, 1000f)]
    public float fogDistance = 200f;

    [Tooltip("포그 최대 높이")]
    [Range(10f, 100f)]
    public float fogMaxHeight = 30f;

    [Tooltip("포그 색상")]
    public Color fogColor = new Color(0.8f, 0.85f, 0.9f, 1f);

    [Tooltip("볼류메트릭 포그 활성화 (성능 비용 있음)")]
    public bool enableVolumetricFog = false;

    [Tooltip("바람 세기")]
    [Range(0f, 2f)]
    public float windStrength = 0.3f;

    [Tooltip("바람 난류")]
    [Range(0f, 1f)]
    public float windTurbulence = 0.2f;

    [Tooltip("바람 방향 (각도)")]
    [Range(0f, 360f)]
    public float windDirection = 60f;

    // ═══════════════════════════════════════════════════
    // 성능 설정
    // ═══════════════════════════════════════════════════

    [Header("Performance")]
    [Tooltip("목표 프레임레이트")]
    public int targetFrameRate = 60;

    [Tooltip("Screen Space Reflection 활성화")]
    public bool enableSSR = true;

    [Tooltip("Screen Space Ambient Occlusion 활성화")]
    public bool enableSSAO = true;

    // ═══════════════════════════════════════════════════
    // 부트스트랩 설정 (Phase 6)
    // ═══════════════════════════════════════════════════

    [Header("Bootstrap (Phase 6)")]
    [Tooltip("시작 시 자동 데모 모드")]
    public bool autoStartDemo = true;

    [Tooltip("시작 시 자동 순항 모드")]
    public bool autoStartCruise = true;

    [Tooltip("시작 시 UI 오버레이 표시")]
    public bool showUIOnStart = true;

    // ═══════════════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════════════

    /// <summary>스크린 메시 정점 수 (segments+1)^2</summary>
    public int ScreenVertexCount => (screenSegments + 1) * (screenSegments + 1);

    /// <summary>스크린 메시 삼각형 수</summary>
    public int ScreenTriangleCount => screenSegments * screenSegments * 2;

    private void OnValidate()
    {
        screenResolution = Mathf.Max(64, screenResolution);
        screenWorldSize = Mathf.Max(0.1f, screenWorldSize);
        screenSegments = Mathf.Clamp(screenSegments, 1, 1023);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        lightRange = Mathf.Max(1f, lightRange);
        autoExposureMinMultiplier = Mathf.Max(0.1f, autoExposureMinMultiplier);
        autoExposureMaxMultiplier = Mathf.Max(autoExposureMinMultiplier, autoExposureMaxMultiplier);
        autoExposureSmoothSpeed = Mathf.Max(0.1f, autoExposureSmoothSpeed);
        cefFrameRate = Mathf.Clamp(cefFrameRate, 1, 120);
        targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 240);
        terrainSize = Mathf.Max(10f, terrainSize);
        terrainMaxHeight = Mathf.Max(0.5f, terrainMaxHeight);
        sunIntensity = Mathf.Max(0f, sunIntensity);
    }
}
