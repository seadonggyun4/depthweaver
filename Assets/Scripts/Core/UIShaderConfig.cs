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

    [Tooltip("쿠키 밝기 승수")]
    [Range(0.1f, 10f)]
    public float cookieIntensityMultiplier = 1f;

    [Tooltip("투사 광 색상 선명도 증가")]
    [Range(0f, 2f)]
    public float cookieSaturationBoost = 1f;

    [Tooltip("비선형 밝기 매핑 커브")]
    public AnimationCurve cookieContrastCurve = AnimationCurve.Linear(0, 0, 1, 1);

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
    // 스튜디오 환경 설정
    // ═══════════════════════════════════════════════════

    [Header("Studio Environment")]
    [Tooltip("스크린 메시 위치")]
    public Vector3 screenPosition = new Vector3(0f, 3f, -5f);

    [Tooltip("스크린 메시 회전 (카메라를 향하도록)")]
    public Vector3 screenRotation = new Vector3(0f, 180f, 0f);

    [Tooltip("바닥 크기 (한 변의 길이)")]
    public float floorSize = 20f;

    [Tooltip("바닥 스무스니스 (반사도)")]
    [Range(0f, 1f)]
    public float floorSmoothness = 0.85f;

    [Tooltip("바닥 색상")]
    public Color floorColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    [Tooltip("배경막 반경")]
    public float backdropRadius = 12f;

    [Tooltip("배경막 높이")]
    public float backdropHeight = 10f;

    [Tooltip("배경막 호각도")]
    public float backdropArcDegrees = 180f;

    [Tooltip("배경막 스무스니스")]
    [Range(0f, 1f)]
    public float backdropSmoothness = 0.1f;

    [Tooltip("배경막 색상")]
    public Color backdropColor = new Color(0.15f, 0.15f, 0.15f, 1f);

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
        cefFrameRate = Mathf.Clamp(cefFrameRate, 1, 120);
        targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 240);
    }
}
