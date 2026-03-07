// Assets/Scripts/CEF/CEFTextureSource.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 텍스처 소스
// ══════════════════════════════════════════════════════════════════════
//
// IBrowserBackend를 감싸 ITextureSource를 구현한다.
// 기존 TexturePipelineManager에 무변경 연결:
//   Inspector에서 textureSourceComponent를 이 컴포넌트로 교체하면 된다.
//
// 이중 텍스처 버퍼링:
//   - 색상 텍스처 2장을 핑퐁하여 GPU 스톨을 방지
//   - 깊이 텍스처는 Phase 2의 depth-extractor.js 출력을 수신하는 별도 경로
//
// GCHandle 고정 버퍼:
//   - pixelBuffer를 GC 이동 불가로 고정하여 네이티브 복사 시 안정성 확보
//
// 전송 모드 (TextureTransferMode):
//   - Standard: LoadRawTextureData + Apply (기저선, ~8ms)
//   - DoubleBuffered: 핑퐁 텍스처 (GPU 스톨 방지, ~5ms)
//   - NativePointer: 네이티브 텍스처 포인터 직접 기록 (~2ms, 플랫폼 의존)

using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>텍스처 전송 모드</summary>
public enum TextureTransferMode
{
    /// <summary>LoadRawTextureData + Apply (기저선, 안정적)</summary>
    Standard,

    /// <summary>이중 버퍼링으로 GPU 스톨 방지</summary>
    DoubleBuffered,

    /// <summary>네이티브 텍스처 포인터 직접 기록 (최적, 플랫폼 의존)</summary>
    NativePointer
}

public class CEFTextureSource : MonoBehaviour, ITextureSource
{
    // ═══════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════

    [Header("Transfer Mode")]
    [Tooltip("텍스처 전송 방식.\n" +
             "Standard: 안정적 (기저선)\n" +
             "DoubleBuffered: GPU 스톨 방지\n" +
             "NativePointer: 최적 (플랫폼 의존)")]
    [SerializeField] private TextureTransferMode transferMode = TextureTransferMode.DoubleBuffered;

    // ═══════════════════════════════════════════════════
    // ITextureSource 구현
    // ═══════════════════════════════════════════════════

    public event EventHandler<TextureUpdateEventArgs> OnTextureUpdated;

    public Texture2D ColorTexture { get; private set; }
    public Texture2D DepthTexture { get; private set; }
    public bool IsReady => backend != null && backend.IsInitialized;

    // ═══════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════

    private IBrowserBackend backend;
    private UIShaderConfig config;
    private int resolution;

    // 픽셀 버퍼 (GC 고정)
    private byte[] pixelBuffer;
    private GCHandle pixelBufferHandle;

    // 이중 버퍼링
    private Texture2D[] colorTextures;
    private int currentTexIndex;

    // 깊이 텍스처 (Phase 2 연동)
    private bool depthDirty;

    // 성능 측정
    private float lastTransferTimeMs;

    /// <summary>마지막 프레임 전송 소요 시간 (밀리초)</summary>
    public float TransferTimeMs => lastTransferTimeMs;

    /// <summary>현재 활성 백엔드 참조 (외부 접근용)</summary>
    public IBrowserBackend Backend => backend;

    // ═══════════════════════════════════════════════════
    // ITextureSource 인터페이스
    // ═══════════════════════════════════════════════════

    public void Initialize(UIShaderConfig shaderConfig)
    {
        config = shaderConfig;
        resolution = config != null ? config.screenResolution : 512;
        int frameRate = config != null ? config.cefFrameRate : 60;

        // 백엔드 생성 및 초기화
        backend = new CEFNativeBackend();

        if (!backend.Initialize(resolution, resolution, frameRate))
        {
            Debug.LogError("[UIShader] CEFTextureSource: 백엔드 초기화 실패");
            backend = null;
            return;
        }

        // 텍스처 생성
        CreateTextures();

        // 픽셀 버퍼 할당 및 GC 고정
        int bufferSize = resolution * resolution * 4; // BGRA32
        pixelBuffer = new byte[bufferSize];
        pixelBufferHandle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);

        // 백엔드 이벤트 연결
        backend.OnLoadCompleted += OnPageLoadCompleted;

        // 기본 URL 로드
        if (config != null && !string.IsNullOrEmpty(config.defaultURL))
        {
            backend.LoadURL(config.defaultURL);
        }

        Debug.Log($"[UIShader] CEFTextureSource 초기화 완료: {resolution}x{resolution}, " +
                  $"전송 모드: {transferMode}, 백엔드: {backend.BackendName}");
    }

    public void Shutdown()
    {
        // 이벤트 연결 해제
        if (backend != null)
        {
            backend.OnLoadCompleted -= OnPageLoadCompleted;
        }

        // GC 핸들 해제
        if (pixelBufferHandle.IsAllocated)
        {
            pixelBufferHandle.Free();
        }

        // 백엔드 종료
        backend?.Dispose();
        backend = null;

        // 텍스처 정리
        DestroyTextures();

        Debug.Log("[UIShader] CEFTextureSource 종료 완료");
    }

    // ═══════════════════════════════════════════════════
    // 백엔드 교체 (확장성)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 런타임에서 브라우저 백엔드를 교체한다.
    /// CefSharp → Vuplex 전환 등에 활용.
    /// </summary>
    public void SetBackend(IBrowserBackend newBackend)
    {
        if (newBackend == null)
        {
            Debug.LogError("[UIShader] CEFTextureSource: null 백엔드를 설정할 수 없습니다.");
            return;
        }

        // 기존 백엔드 정리
        if (backend != null)
        {
            backend.OnLoadCompleted -= OnPageLoadCompleted;
            backend.Dispose();
        }

        backend = newBackend;
        backend.OnLoadCompleted += OnPageLoadCompleted;

        Debug.Log($"[UIShader] 백엔드 교체: {newBackend.BackendName}");
    }

    // ═══════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Update()
    {
        if (backend == null || !backend.IsInitialized) return;

        // 백엔드 틱 (CEF 메시지 루프)
        backend.Tick();

        // 새 프레임 확인 및 텍스처 전송
        if (backend.HasNewFrame())
        {
            float startTime = Time.realtimeSinceStartup;
            TransferFrame();
            lastTransferTimeMs = (Time.realtimeSinceStartup - startTime) * 1000f;

            // 이벤트 발신
            EmitTextureUpdate();
        }
    }

    void OnDestroy()
    {
        Shutdown();
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    // ═══════════════════════════════════════════════════
    // 텍스처 전송
    // ═══════════════════════════════════════════════════

    private void TransferFrame()
    {
        switch (transferMode)
        {
            case TextureTransferMode.Standard:
                TransferStandard();
                break;
            case TextureTransferMode.DoubleBuffered:
                TransferDoubleBuffered();
                break;
            case TextureTransferMode.NativePointer:
                TransferNativePointer();
                break;
        }
    }

    /// <summary>기저선 전송: LoadRawTextureData + Apply</summary>
    private void TransferStandard()
    {
        backend.CopyPixelBuffer(pixelBufferHandle.AddrOfPinnedObject(), out int w, out int h);

        if (w != resolution || h != resolution) return;

        ColorTexture.LoadRawTextureData(pixelBuffer);
        ColorTexture.Apply(false); // false = 밉맵 미생성
    }

    /// <summary>이중 버퍼링: 핑퐁 텍스처로 GPU 스톨 방지</summary>
    private void TransferDoubleBuffered()
    {
        int writeIndex = 1 - currentTexIndex;

        backend.CopyPixelBuffer(pixelBufferHandle.AddrOfPinnedObject(), out int w, out int h);

        if (w != resolution || h != resolution) return;

        colorTextures[writeIndex].LoadRawTextureData(pixelBuffer);
        colorTextures[writeIndex].Apply(false);

        // 버퍼 스왑
        currentTexIndex = writeIndex;
        ColorTexture = colorTextures[currentTexIndex];
    }

    /// <summary>네이티브 텍스처 포인터: GPU 직접 기록 (최적)</summary>
    private void TransferNativePointer()
    {
        IntPtr texturePtr = ColorTexture.GetNativeTexturePtr();
        if (texturePtr == IntPtr.Zero)
        {
            // 폴백: 표준 전송
            TransferStandard();
            return;
        }

        NativePluginInterop.CEF_CopyToNativeTexture(texturePtr, resolution, resolution);
    }

    // ═══════════════════════════════════════════════════
    // 깊이 텍스처 (Phase 2 연동)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 깊이 텍스처 데이터를 수신한다.
    /// Phase 2의 depth-extractor.js → JS 브릿지 → 이 메서드 경로.
    /// </summary>
    public void UpdateDepthTexture(byte[] depthData, int width, int height)
    {
        if (DepthTexture == null || width != resolution || height != resolution) return;

        DepthTexture.LoadRawTextureData(depthData);
        DepthTexture.Apply(false);
        depthDirty = true;
    }

    /// <summary>
    /// 깊이 텍스처를 Texture2D로 직접 설정한다.
    /// Phase 2의 DepthTextureProcessor 출력 (블러 처리 후) 수신용.
    /// </summary>
    public void SetDepthTexture(Texture2D processedDepth)
    {
        DepthTexture = processedDepth;
        depthDirty = true;
    }

    // ═══════════════════════════════════════════════════
    // 이벤트 발신
    // ═══════════════════════════════════════════════════

    private void EmitTextureUpdate()
    {
        OnTextureUpdated?.Invoke(this, new TextureUpdateEventArgs(
            ColorTexture,
            depthDirty ? DepthTexture : null,
            depthDirty
        ));

        depthDirty = false;
    }

    // ═══════════════════════════════════════════════════
    // 텍스처 생명주기
    // ═══════════════════════════════════════════════════

    private void CreateTextures()
    {
        // 이중 버퍼링용 색상 텍스처
        colorTextures = new Texture2D[2];
        for (int i = 0; i < 2; i++)
        {
            colorTextures[i] = new Texture2D(resolution, resolution, TextureFormat.BGRA32, false);
            colorTextures[i].filterMode = FilterMode.Bilinear;
            colorTextures[i].wrapMode = TextureWrapMode.Clamp;
            colorTextures[i].name = $"CEF_Color_{i}";
        }

        currentTexIndex = 0;
        ColorTexture = colorTextures[0];

        // 깊이 텍스처 (Phase 2에서 활용, 초기엔 검정)
        DepthTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false);
        DepthTexture.filterMode = FilterMode.Bilinear;
        DepthTexture.wrapMode = TextureWrapMode.Clamp;
        DepthTexture.name = "CEF_Depth";
    }

    private void DestroyTextures()
    {
        if (colorTextures != null)
        {
            for (int i = 0; i < colorTextures.Length; i++)
            {
                if (colorTextures[i] != null)
                    Destroy(colorTextures[i]);
            }
            colorTextures = null;
        }

        if (DepthTexture != null)
        {
            Destroy(DepthTexture);
            DepthTexture = null;
        }

        ColorTexture = null;
    }

    // ═══════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═══════════════════════════════════════════════════

    private void OnPageLoadCompleted(object sender, PageLoadEventArgs args)
    {
        if (!args.IsMainFrame) return;

        Debug.Log($"[UIShader] 페이지 로드 완료: {args.URL}");

        // Phase 2: 여기서 depth-extractor.js를 자동 주입
        // backend.ExecuteJavaScript(depthExtractorScript);
    }
}
