// Assets/Scripts/Core/StaticTextureSource.cs
// Phase 0 텍스처 소스 구현: 정적 PNG 텍스처를 제공한다.
// CEF 통합(Phase 1) 이전에 전체 파이프라인을 종단간 검증하기 위한 용도.
//
// ITextureSource 인터페이스를 구현하므로, TexturePipelineManager는
// 이 클래스와 향후의 CEFTextureSource를 구분하지 않고 동일하게 처리한다.

using System;
using UnityEngine;

public class StaticTextureSource : MonoBehaviour, ITextureSource
{
    // ═══════════════════════════════════════════════════
    // 인스펙터 할당 필드
    // ═══════════════════════════════════════════════════

    [Header("Test Textures")]
    [Tooltip("512x512 웹페이지 스크린샷 PNG (sRGB)")]
    [SerializeField] private Texture2D testColorTexture;

    [Tooltip("512x512 그레이스케일 깊이 맵 PNG (Linear, sRGB Off)")]
    [SerializeField] private Texture2D testDepthTexture;

    [Header("Runtime Control")]
    [Tooltip("활성화 시 매 프레임 텍스처 이벤트를 발생시킨다 (CEF 시뮬레이션)")]
    [SerializeField] private bool continuousUpdate = false;

    // ═══════════════════════════════════════════════════
    // ITextureSource 구현
    // ═══════════════════════════════════════════════════

    public event EventHandler<TextureUpdateEventArgs> OnTextureUpdated;

    public Texture2D ColorTexture => testColorTexture;
    public Texture2D DepthTexture => testDepthTexture;
    public bool IsReady => testColorTexture != null;

    private bool hasEmittedInitial;
    private UIShaderConfig cachedConfig;

    public void Initialize(UIShaderConfig config)
    {
        cachedConfig = config;
        hasEmittedInitial = false;

        if (testColorTexture != null)
        {
            Debug.Log($"[UIShader] StaticTextureSource 초기화: " +
                      $"색상 {testColorTexture.width}x{testColorTexture.height}, " +
                      $"깊이 {(testDepthTexture != null ? $"{testDepthTexture.width}x{testDepthTexture.height}" : "없음")}");
        }
        else
        {
            Debug.LogWarning("[UIShader] StaticTextureSource: 색상 텍스처가 할당되지 않았습니다. " +
                           "Inspector에서 testColorTexture를 할당하세요.");
        }
    }

    public void Shutdown()
    {
        hasEmittedInitial = false;
    }

    // ═══════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Update()
    {
        if (!IsReady) return;

        if (!hasEmittedInitial)
        {
            // 첫 프레임: 색상 + 깊이 모두 전달
            hasEmittedInitial = true;
            EmitTextureUpdate(depthChanged: true);
        }
        else if (continuousUpdate)
        {
            // 연속 갱신 모드: 매 프레임 색상만 전달 (깊이 변경 없음)
            // CEF의 동작을 시뮬레이션하여 성능 프로파일링에 활용
            EmitTextureUpdate(depthChanged: false);
        }
    }

    // ═══════════════════════════════════════════════════
    // 내부 메서드
    // ═══════════════════════════════════════════════════

    private void EmitTextureUpdate(bool depthChanged)
    {
        OnTextureUpdated?.Invoke(this, new TextureUpdateEventArgs(
            testColorTexture,
            depthChanged ? testDepthTexture : null,
            depthChanged
        ));
    }

    // ═══════════════════════════════════════════════════
    // 공개 API
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 런타임에서 텍스처를 교체한다.
    /// Inspector에서 텍스처를 변경한 후 호출하거나,
    /// 코드에서 직접 텍스처를 설정할 때 사용한다.
    /// </summary>
    public void SetTextures(Texture2D color, Texture2D depth)
    {
        testColorTexture = color;
        testDepthTexture = depth;
        hasEmittedInitial = false; // 다음 Update에서 재전달

        Debug.Log("[UIShader] StaticTextureSource: 텍스처 교체됨");
    }

    /// <summary>
    /// 텍스처 변경을 강제로 알린다.
    /// Inspector에서 텍스처를 교체한 후 호출.
    /// </summary>
    public void NotifyTextureChanged()
    {
        hasEmittedInitial = false;
    }
}
