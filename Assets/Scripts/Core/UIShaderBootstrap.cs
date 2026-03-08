// Assets/Scripts/Core/UIShaderBootstrap.cs
// UIShader 시스템 진입점.
// 성능 설정을 적용하고, 모든 컴포넌트의 초기화 순서를 보장한다.
// 씬의 Managers 오브젝트에 부착한다.

using UnityEngine;

public class UIShaderBootstrap : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private UIShaderConfig config;

    [Header("CEF")]
    [SerializeField] private CEFBridge cefBridge;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool logPerformanceStats = false;

    // 성능 측정
    private float fps;
    private float fpsAccumulator;
    private int fpsFrameCount;
    private float fpsUpdateTimer;
    private const float FPS_UPDATE_INTERVAL = 0.5f;

    // 참조
    private TexturePipelineManager pipelineManager;

    void Awake()
    {
        if (config == null)
        {
            Debug.LogError("[UIShader] Bootstrap: UIShaderConfig가 할당되지 않았습니다.");
            return;
        }

        ApplyPerformanceSettings();
    }

    void Start()
    {
        pipelineManager = FindFirstObjectByType<TexturePipelineManager>();

        // CEFBridge 이벤트 연결
        if (cefBridge != null)
        {
            var overlay = FindFirstObjectByType<UIShaderOverlay>();
            if (overlay != null)
                overlay.OnURLRequested += cefBridge.LoadURL;

            var autoPlay = FindFirstObjectByType<DemoAutoPlay>();
            if (autoPlay != null)
                autoPlay.OnLoadURL += cefBridge.LoadURL;

            Debug.Log("[UIShader] CEFBridge 이벤트 연결 완료");
        }

        Debug.Log("[UIShader] ═══════════════════════════════════════════");
        Debug.Log("[UIShader] UIShader 시스템 초기화 완료");
        Debug.Log($"[UIShader]   스크린 해상도: {config.screenResolution}x{config.screenResolution}");
        Debug.Log($"[UIShader]   메시 세그먼트: {config.screenSegments}x{config.screenSegments}");
        Debug.Log($"[UIShader]   정점 수: {config.ScreenVertexCount:N0}");
        Debug.Log($"[UIShader]   변위 스케일: {config.displacementScale}");
        Debug.Log($"[UIShader]   광원 강도: {config.lightIntensity} lm");
        Debug.Log($"[UIShader]   목표 FPS: {config.targetFrameRate}");
        Debug.Log("[UIShader] ═══════════════════════════════════════════");
    }

    void Update()
    {
        UpdateFPS();

        if (logPerformanceStats)
        {
            LogPerformanceStats();
        }
    }

    // ═══════════════════════════════════════════════════
    // 성능 설정
    // ═══════════════════════════════════════════════════

    private void ApplyPerformanceSettings()
    {
        Application.targetFrameRate = config.targetFrameRate;
        QualitySettings.vSyncCount = 0; // VSync 비활성화 (프로파일링 정확성)

        Debug.Log($"[UIShader] 성능 설정 적용: targetFPS={config.targetFrameRate}, VSync=Off");
    }

    // ═══════════════════════════════════════════════════
    // FPS 측정
    // ═══════════════════════════════════════════════════

    private void UpdateFPS()
    {
        fpsUpdateTimer -= Time.unscaledDeltaTime;
        fpsAccumulator += Time.unscaledDeltaTime;
        fpsFrameCount++;

        if (fpsUpdateTimer <= 0f)
        {
            fps = fpsFrameCount / fpsAccumulator;
            fpsUpdateTimer = FPS_UPDATE_INTERVAL;
            fpsAccumulator = 0f;
            fpsFrameCount = 0;
        }
    }

    private float logTimer;
    private void LogPerformanceStats()
    {
        logTimer -= Time.unscaledDeltaTime;
        if (logTimer <= 0f)
        {
            logTimer = 5f; // 5초마다 출력
            Debug.Log($"[UIShader] Performance: {fps:F1} FPS, " +
                      $"FrameTime={Time.unscaledDeltaTime * 1000f:F2}ms" +
                      (pipelineManager != null
                          ? $", ColorUpdates/s={pipelineManager.ColorUpdatesPerSecond}, " +
                            $"DepthUpdates/s={pipelineManager.DepthUpdatesPerSecond}"
                          : ""));
        }
    }

    // ═══════════════════════════════════════════════════
    // 디버그 오버레이
    // ═══════════════════════════════════════════════════

    void OnGUI()
    {
        if (!showDebugOverlay) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 250));
        GUILayout.BeginVertical("box");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14
        };
        GUILayout.Label("UIShader Debug", titleStyle);

        GUILayout.Label($"FPS: {fps:F1}");
        GUILayout.Label($"Frame Time: {Time.unscaledDeltaTime * 1000f:F2} ms");

        if (pipelineManager != null)
        {
            GUILayout.Label($"Pipeline Active: {pipelineManager.IsActive}");
            GUILayout.Label($"Color Updates/s: {pipelineManager.ColorUpdatesPerSecond}");
            GUILayout.Label($"Depth Updates/s: {pipelineManager.DepthUpdatesPerSecond}");
        }

        if (config != null)
        {
            GUILayout.Label($"Displacement Scale: {config.displacementScale:F2}");
            GUILayout.Label($"Light Intensity: {config.lightIntensity:F0} lm");
        }

        OrbitCameraController cam = FindFirstObjectByType<OrbitCameraController>();
        if (cam != null && cam.ActivePresetIndex >= 0 && cam.ActivePresetIndex < cam.presets.Length)
        {
            GUILayout.Label($"Camera: {cam.presets[cam.ActivePresetIndex].name}");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
