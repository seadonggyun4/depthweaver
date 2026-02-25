// Assets/Scripts/Environment/SunLightModule.cs
// 주간 태양광을 시뮬레이션하는 디렉셔널 라이트 모듈.
// 자연스러운 오후 시간대 조명을 구현하며,
// 소프트 섀도우를 적용하여 사실적인 그림자를 생성한다.

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class SunLightModule : IEnvironmentModule
{
    public string ModuleName => "SunLight";

    private GameObject sunObject;

    public GameObject[] Build(UIShaderConfig config)
    {
        sunObject = new GameObject("SunLight");

        // 오후 시간대 태양 위치 (45도 고도, 약간 서쪽)
        sunObject.transform.rotation = Quaternion.Euler(
            config.sunElevation,
            config.sunAzimuth,
            0f
        );

        // Light 컴포넌트
        Light light = sunObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = config.sunColor;
        light.shadows = LightShadows.Soft;

        // HDRP 추가 설정
        HDAdditionalLightData hdLight = sunObject.AddComponent<HDAdditionalLightData>();
        hdLight.lightUnit = LightUnit.Lux;
        hdLight.intensity = config.sunIntensity;

        // 그림자 품질
        hdLight.shadowResolution.level = 2; // Medium-High

        Debug.Log($"[UIShader] 태양광 생성: {config.sunIntensity} lux, " +
                  $"고도={config.sunElevation}°, 방위={config.sunAzimuth}°");

        return new GameObject[] { sunObject };
    }

    public void Cleanup()
    {
        if (sunObject != null)
        {
            Object.DestroyImmediate(sunObject);
        }
    }
}
