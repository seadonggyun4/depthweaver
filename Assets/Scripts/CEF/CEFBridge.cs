// Assets/Scripts/CEF/CEFBridge.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 고수준 브릿지
// ══════════════════════════════════════════════════════════════════════
//
// CEFTextureSource의 IBrowserBackend를 감싸는 고수준 파사드.
// URL 로드, JS 주입, 이벤트 브릿지 등 비즈니스 레벨 API를 제공한다.
//
// UIShaderOverlay.OnURLRequested, DemoAutoPlay.OnLoadURL 이벤트의
// 수신자로서, BootstrapManager.ConnectEvents()에서 연결된다.
//
// Phase 2 연동:
//   - depth-extractor.js 자동 주입 (InjectDepthExtractor)
//   - 깊이 가중치 실시간 갱신 (UpdateDepthWeights)

using System;
using UnityEngine;

public class CEFBridge : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 인스펙터 참조
    // ═══════════════════════════════════════════════════

    [Header("Core")]
    [Tooltip("CEF 텍스처 소스 (백엔드 접근)")]
    [SerializeField] private CEFTextureSource textureSource;

    [Header("JavaScript Injection")]
    [Tooltip("페이지 로드 시 depth-extractor.js 자동 주입")]
    [SerializeField] private bool autoInjectDepthExtractor = true;

    [Tooltip("depth-extractor.js 파일 (TextAsset)")]
    [SerializeField] private TextAsset depthExtractorScript;

    // ═══════════════════════════════════════════════════
    // 이벤트
    // ═══════════════════════════════════════════════════

    /// <summary>URL 로드 시작 시 발생</summary>
    public event Action<string> OnURLLoadStarted;

    /// <summary>페이지 로드 완료 시 발생</summary>
    public event Action<string> OnPageLoaded;

    /// <summary>깊이 추출기 주입 완료 시 발생 (Phase 2)</summary>
    public event Action OnDepthExtractorInjected;

    // ═══════════════════════════════════════════════════
    // 공개 속성
    // ═══════════════════════════════════════════════════

    /// <summary>CEF 초기화 완료 여부</summary>
    public bool IsInitialized => Backend != null && Backend.IsInitialized;

    /// <summary>현재 페이지 로딩 완료 여부</summary>
    public bool IsPageLoaded => Backend != null && Backend.IsPageLoaded;

    /// <summary>현재 URL</summary>
    public string CurrentURL => Backend != null ? Backend.CurrentURL : string.Empty;

    /// <summary>활성 브라우저 백엔드</summary>
    public IBrowserBackend Backend => textureSource != null ? textureSource.Backend : null;

    // ═══════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════

    void OnEnable()
    {
        // 텍스처 소스의 백엔드 이벤트 연결
        if (textureSource != null && textureSource.Backend != null)
        {
            SubscribeBackendEvents();
        }
    }

    void OnDisable()
    {
        UnsubscribeBackendEvents();
    }

    void Start()
    {
        if (textureSource == null)
        {
            Debug.LogError("[UIShader] CEFBridge: CEFTextureSource가 할당되지 않았습니다.");
            return;
        }

        // 백엔드가 아직 초기화 안됐을 수 있으므로, 지연 구독 설정
        if (Backend != null)
        {
            SubscribeBackendEvents();
        }
    }

    // ═══════════════════════════════════════════════════
    // 공개 API — URL 탐색
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// URL을 로드한다.
    /// UIShaderOverlay.OnURLRequested, DemoAutoPlay.OnLoadURL의 핸들러.
    /// </summary>
    public void LoadURL(string url)
    {
        if (Backend == null || !Backend.IsInitialized)
        {
            Debug.LogWarning($"[UIShader] CEFBridge: 백엔드 미초기화, URL 로드 무시: {url}");
            return;
        }

        OnURLLoadStarted?.Invoke(url);
        Backend.LoadURL(url);
    }

    /// <summary>브라우저 뒤로 가기</summary>
    public void GoBack()
    {
        Backend?.GoBack();
    }

    /// <summary>브라우저 앞으로 가기</summary>
    public void GoForward()
    {
        Backend?.GoForward();
    }

    /// <summary>페이지 새로고침</summary>
    public void Reload()
    {
        Backend?.Reload();
    }

    // ═══════════════════════════════════════════════════
    // 공개 API — JavaScript
    // ═══════════════════════════════════════════════════

    /// <summary>JavaScript를 실행한다 (결과 없음)</summary>
    public void ExecuteJavaScript(string script)
    {
        Backend?.ExecuteJavaScript(script);
    }

    /// <summary>JavaScript를 실행하고 결과를 콜백으로 반환한다</summary>
    public void EvaluateJavaScript(string script, Action<bool, string> callback)
    {
        if (Backend == null)
        {
            callback?.Invoke(false, "Backend not available");
            return;
        }

        Backend.EvaluateJavaScript(script, callback);
    }

    /// <summary>
    /// depth-extractor.js를 현재 페이지에 주입한다.
    /// Phase 2의 깊이 추출 파이프라인 활성화.
    /// </summary>
    public void InjectDepthExtractor()
    {
        if (Backend == null || !Backend.IsInitialized)
        {
            Debug.LogWarning("[UIShader] CEFBridge: 백엔드 미초기화, 깊이 추출기 주입 무시");
            return;
        }

        string script = GetDepthExtractorScript();
        if (string.IsNullOrEmpty(script))
        {
            Debug.LogWarning("[UIShader] CEFBridge: depth-extractor.js가 없습니다.");
            return;
        }

        Backend.ExecuteJavaScript(script);
        OnDepthExtractorInjected?.Invoke();
        Debug.Log("[UIShader] depth-extractor.js 주입 완료");
    }

    /// <summary>
    /// 깊이 추출 가중치를 실시간으로 갱신한다.
    /// Phase 2 DepthWeightPreset → JS 가중치 동기화.
    /// </summary>
    public void UpdateDepthWeights(float w1, float w2, float w3, float w4, float w5, float w6)
    {
        string script = $"if(window.__depthWeaver){{" +
                        $"window.__depthWeaver.updateWeights({{" +
                        $"domDepth:{w1:F3},stackContext:{w2:F3}," +
                        $"boxShadow:{w3:F3},transformZ:{w4:F3}," +
                        $"opacity:{w5:F3},position:{w6:F3}" +
                        $"}})}}";

        Backend?.ExecuteJavaScript(script);
    }

    // ═══════════════════════════════════════════════════
    // 내부 이벤트 관리
    // ═══════════════════════════════════════════════════

    private void SubscribeBackendEvents()
    {
        if (Backend == null) return;

        Backend.OnLoadCompleted += HandlePageLoadCompleted;
        Backend.OnLoadError += HandlePageLoadError;
    }

    private void UnsubscribeBackendEvents()
    {
        if (Backend == null) return;

        Backend.OnLoadCompleted -= HandlePageLoadCompleted;
        Backend.OnLoadError -= HandlePageLoadError;
    }

    private void HandlePageLoadCompleted(object sender, PageLoadEventArgs args)
    {
        if (!args.IsMainFrame) return;

        OnPageLoaded?.Invoke(args.URL);

        // 자동 깊이 추출기 주입
        if (autoInjectDepthExtractor)
        {
            InjectDepthExtractor();
        }
    }

    private void HandlePageLoadError(object sender, PageLoadEventArgs args)
    {
        Debug.LogWarning($"[UIShader] 페이지 로드 오류: {args.ErrorMessage} (URL: {args.URL})");
    }

    // ═══════════════════════════════════════════════════
    // 내부 헬퍼
    // ═══════════════════════════════════════════════════

    private string GetDepthExtractorScript()
    {
        // TextAsset 할당된 경우 사용
        if (depthExtractorScript != null)
        {
            return depthExtractorScript.text;
        }

        // Phase 2 구현 전 스텁
        return "console.log('[UIShader] depth-extractor.js injection point (Phase 2)');";
    }
}
