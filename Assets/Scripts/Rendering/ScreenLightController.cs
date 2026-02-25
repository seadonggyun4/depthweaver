// Assets/Scripts/Rendering/ScreenLightController.cs
// HDRP RectAreaLight를 스크린 메시와 동기화하여 관리한다.
// 웹 페이지 색상 텍스처를 영역광 쿠키로 사용하여
// 주변 3D 오브젝트에 유색 간접 조명을 투사한다.
//
// Phase 4 확장: 자동 노출, 채도 부스트, 사분면 다중 광원

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Light))]
public class ScreenLightController : MonoBehaviour
{
    [SerializeField] private UIShaderConfig config;

    private Light areaLight;
    private HDAdditionalLightData hdLightData;
    private Texture2D cookieTexture;
    private bool isConfigured;

    /// <summary>쿠키 텍스처에 직접 접근 (디버그/테스트용)</summary>
    public Texture2D CookieTexture => cookieTexture;

    /// <summary>광원 구성 완료 여부</summary>
    public bool IsConfigured => isConfigured;

    void Awake()
    {
        areaLight = GetComponent<Light>();
        hdLightData = GetComponent<HDAdditionalLightData>();

        if (hdLightData == null)
        {
            hdLightData = gameObject.AddComponent<HDAdditionalLightData>();
        }
    }

    void Start()
    {
        ConfigureAreaLight();
    }

    /// <summary>
    /// RectAreaLight를 UIShaderConfig 사양에 맞게 구성한다.
    /// </summary>
    private void ConfigureAreaLight()
    {
        if (config == null)
        {
            Debug.LogError("[UIShader] ScreenLightController: UIShaderConfig가 할당되지 않았습니다.");
            return;
        }

        // ─── 광원 유형: Area (Rectangle) ───
        areaLight.type = LightType.Area;
        hdLightData.SetAreaLightSize(new Vector2(
            config.screenWorldSize,
            config.screenWorldSize
        ));

        // ─── 강도 설정 ───
        hdLightData.lightUnit = LightUnit.Lumen;
        hdLightData.intensity = config.lightIntensity;

        // ─── 색상: 흰색 (쿠키가 색상을 제어) ───
        areaLight.color = Color.white;

        // ─── 범위: 충분히 넓게 ───
        areaLight.range = 30f;

        // ─── 그림자 설정 ───
        areaLight.shadows = LightShadows.Soft;
        hdLightData.shadowResolution.level = 1; // Medium

        // ─── 쿠키 텍스처 초기화 ───
        InitializeCookieTexture();

        isConfigured = true;

        Debug.Log($"[UIShader] RectAreaLight 구성 완료: " +
                  $"{config.screenWorldSize}x{config.screenWorldSize} 유닛, " +
                  $"{config.lightIntensity} 루멘");
    }

    /// <summary>
    /// 쿠키 텍스처를 생성하고 초기값(순백색)으로 채운다.
    /// </summary>
    private void InitializeCookieTexture()
    {
        int res = config.screenResolution;

        cookieTexture = new Texture2D(res, res, TextureFormat.RGBA32, true);
        cookieTexture.filterMode = FilterMode.Trilinear;
        cookieTexture.wrapMode = TextureWrapMode.Clamp;
        cookieTexture.name = "UIShader_CookieTexture";

        // 초기: 순백색으로 채움
        Color32[] whitePixels = new Color32[res * res];
        Color32 white = new Color32(255, 255, 255, 255);
        for (int i = 0; i < whitePixels.Length; i++)
        {
            whitePixels[i] = white;
        }
        cookieTexture.SetPixels32(whitePixels);
        cookieTexture.Apply(true); // 밉맵 생성

        // HDRP 영역광 쿠키 할당
        hdLightData.SetCookie(cookieTexture);
    }

    /// <summary>
    /// 외부에서 호출: 웹 페이지 색상 텍스처로 쿠키를 갱신한다.
    /// TexturePipelineManager가 ITextureSource 이벤트 수신 시 호출.
    /// </summary>
    public void UpdateCookie(Texture2D sourceTexture)
    {
        if (sourceTexture == null || cookieTexture == null) return;

        // 소스 텍스처 → 쿠키 텍스처로 GPU 복사
        if (sourceTexture.width == cookieTexture.width &&
            sourceTexture.height == cookieTexture.height)
        {
            Graphics.CopyTexture(sourceTexture, cookieTexture);
        }
        else
        {
            // 해상도 불일치 시 CPU 기반 복사 (폴백)
            RenderTexture rt = RenderTexture.GetTemporary(
                cookieTexture.width, cookieTexture.height, 0, RenderTextureFormat.ARGB32
            );
            Graphics.Blit(sourceTexture, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            cookieTexture.ReadPixels(new Rect(0, 0, cookieTexture.width, cookieTexture.height), 0, 0);
            cookieTexture.Apply(true); // 밉맵 재생성
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }

        // 밉맵 재생성 (거리 기반 블러링: 근거리=선명, 원거리=흐릿)
        cookieTexture.Apply(true);
        hdLightData.SetCookie(cookieTexture);
    }

    /// <summary>
    /// 광원 강도를 갱신한다.
    /// </summary>
    public void SetIntensity(float lumens)
    {
        if (hdLightData != null)
        {
            hdLightData.intensity = lumens;
        }
    }

    /// <summary>
    /// 런타임에서 config 변경을 반영한다.
    /// </summary>
    public void ApplyConfigChanges()
    {
        if (config == null || hdLightData == null) return;

        hdLightData.SetAreaLightSize(new Vector2(
            config.screenWorldSize,
            config.screenWorldSize
        ));
        hdLightData.intensity = config.lightIntensity;
    }
}
