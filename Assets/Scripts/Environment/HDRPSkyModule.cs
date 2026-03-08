// Assets/Scripts/Environment/HDRPSkyModule.cs
// HDRP Volume을 통한 하늘 및 노출 설정 모듈.
// Physically Based Sky로 사실적인 대기 산란을 구현하고,
// 적절한 노출값으로 주간 야외 환경을 표현한다.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class HDRPSkyModule : IEnvironmentModule
{
    public string ModuleName => "HDRPSky";

    private GameObject volumeObject;

    public GameObject[] Build(UIShaderConfig config)
    {
        volumeObject = new GameObject("SkyVolume");

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0; // 기본 우선순위

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "NaturalSky_Profile";

        // ─── Visual Environment (하늘 유형 선택) ───
        VisualEnvironment visualEnv = profile.Add<VisualEnvironment>();
        // PhysicallyBasedSky의 HDRP 등록 ID = 4
        visualEnv.skyType.Override(4);
        visualEnv.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

        // ─── Physically Based Sky (대기 산란) ───
        // 기본값이 지구형 대기에 최적화되어 있으므로 대부분 기본값 사용
        PhysicallyBasedSky pbSky = profile.Add<PhysicallyBasedSky>();
        // 추가 커스터마이징이 필요하면 여기서 설정
        // pbSky.groundTint.Override(new Color(0.25f, 0.45f, 0.15f)); // 지면 반사색

        // ─── Exposure (노출) ───
        Exposure exposure = profile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(config.skyExposure);

        // ─── White Balance (화이트 밸런스) ───
        WhiteBalance whiteBalance = profile.Add<WhiteBalance>();
        whiteBalance.temperature.Override(0f); // 중립 (6500K)

        // ─── Indirect Lighting Controller (간접광) ───
        // Unity 6.x: 기본값(1.0) 사용 — 별도 오버라이드 불필요
        profile.Add<IndirectLightingController>();

        volume.profile = profile;

        Debug.Log($"[UIShader] HDRP 하늘 설정: PhysicallyBasedSky, 노출={config.skyExposure}");

        return new GameObject[] { volumeObject };
    }

    public void Cleanup()
    {
        if (volumeObject != null)
        {
            // VolumeProfile 에셋 정리
            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume != null && volume.profile != null)
            {
                Object.DestroyImmediate(volume.profile);
            }
            Object.DestroyImmediate(volumeObject);
        }
    }
}
