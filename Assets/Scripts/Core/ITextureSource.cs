// Assets/Scripts/Core/ITextureSource.cs
// 텍스처 공급자 인터페이스 — UIShader 아키텍처의 핵심 추상화
//
// Phase 0: StaticTextureSource (정적 PNG)
// Phase 1: CEFTextureSource (실시간 웹 페이지)
// Phase N: 임의의 텍스처 소스 확장 가능
//
// TexturePipelineManager는 이 인터페이스만 참조하므로,
// 새로운 텍스처 소스 추가 시 파이프라인 코드 변경이 불필요하다.

using System;
using UnityEngine;

/// <summary>
/// 텍스처 갱신 이벤트 인자.
/// 색상 텍스처는 매 프레임, 깊이 텍스처는 변경 시에만 전달된다.
/// </summary>
public class TextureUpdateEventArgs : EventArgs
{
    /// <summary>웹 페이지 색상 텍스처 (RGB)</summary>
    public Texture2D ColorTexture { get; }

    /// <summary>깊이 맵 텍스처 (그레이스케일). 변경이 없으면 null</summary>
    public Texture2D DepthTexture { get; }

    /// <summary>
    /// 깊이 텍스처 변경 여부.
    /// true: 깊이 텍스처를 GPU에 재업로드해야 함
    /// false: 이전 깊이 텍스처를 그대로 사용
    /// Phase 2의 MutationObserver와 직결되는 최적화 플래그.
    /// </summary>
    public bool DepthChanged { get; }

    public TextureUpdateEventArgs(Texture2D colorTexture, Texture2D depthTexture, bool depthChanged)
    {
        ColorTexture = colorTexture;
        DepthTexture = depthTexture;
        DepthChanged = depthChanged;
    }
}

/// <summary>
/// 텍스처 공급자 인터페이스.
/// 색상 텍스처와 깊이 텍스처를 이벤트 기반으로 소비자에게 전달한다.
/// </summary>
public interface ITextureSource
{
    /// <summary>
    /// 텍스처 갱신 이벤트.
    /// 색상 텍스처가 변경될 때마다 발생한다.
    /// 깊이 변경은 DepthChanged 플래그로 구분한다.
    /// </summary>
    event EventHandler<TextureUpdateEventArgs> OnTextureUpdated;

    /// <summary>현재 색상 텍스처 (직접 접근용)</summary>
    Texture2D ColorTexture { get; }

    /// <summary>현재 깊이 텍스처 (직접 접근용)</summary>
    Texture2D DepthTexture { get; }

    /// <summary>텍스처 소스 준비 완료 여부</summary>
    bool IsReady { get; }

    /// <summary>설정을 기반으로 텍스처 소스를 초기화한다</summary>
    void Initialize(UIShaderConfig config);

    /// <summary>텍스처 소스를 정리하고 리소스를 해제한다</summary>
    void Shutdown();
}
