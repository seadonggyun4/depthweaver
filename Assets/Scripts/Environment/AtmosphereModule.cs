// Assets/Scripts/Environment/AtmosphereModule.cs
// 대기 효과 모듈: 볼류메트릭 포그와 바람 존을 생성한다.
// 은은한 안개로 원거리 깊이감을 부여하고,
// 바람 존으로 잔디/나뭇잎 흔들림 효과의 기반을 제공한다.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class AtmosphereModule : IEnvironmentModule
{
    public string ModuleName => "Atmosphere";

    private GameObject fogVolumeObject;
    private GameObject windZoneObject;

    public GameObject[] Build(UIShaderConfig config)
    {
        var objects = new System.Collections.Generic.List<GameObject>();

        // ─── 포그 볼륨 ───
        fogVolumeObject = CreateFogVolume(config);
        objects.Add(fogVolumeObject);

        // ─── 바람 존 ───
        windZoneObject = CreateWindZone(config);
        objects.Add(windZoneObject);

        Debug.Log($"[UIShader] 대기 효과 생성: 포그 거리={config.fogDistance}, 바람={config.windStrength}");

        return objects.ToArray();
    }

    public void Cleanup()
    {
        if (fogVolumeObject != null)
        {
            Volume volume = fogVolumeObject.GetComponent<Volume>();
            if (volume != null && volume.profile != null)
            {
                Object.DestroyImmediate(volume.profile);
            }
            Object.DestroyImmediate(fogVolumeObject);
        }

        if (windZoneObject != null)
        {
            Object.DestroyImmediate(windZoneObject);
        }
    }

    // ═══════════════════════════════════════════════════
    // 포그
    // ═══════════════════════════════════════════════════

    private GameObject CreateFogVolume(UIShaderConfig config)
    {
        GameObject obj = new GameObject("AtmosphereVolume");

        Volume volume = obj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1; // 하늘 볼륨(0)보다 높은 우선순위

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Atmosphere_Profile";

        // ─── Fog 설정 ───
        Fog fog = profile.Add<Fog>();
        fog.enabled.Override(true);

        // Mean Free Path: 빛이 산란 없이 이동하는 평균 거리 (클수록 옅은 안개)
        fog.meanFreePath.Override(config.fogDistance);

        // 포그 높이 범위
        fog.baseHeight.Override(-5f);
        fog.maximumHeight.Override(config.fogMaxHeight);

        // 포그 색상 (약간 푸른 톤)
        fog.albedo.Override(config.fogColor);

        // 볼류메트릭 포그 (선택적, 성능 비용 있음)
        fog.enableVolumetricFog.Override(config.enableVolumetricFog);

        volume.profile = profile;

        return obj;
    }

    // ═══════════════════════════════════════════════════
    // 바람
    // ═══════════════════════════════════════════════════

    private GameObject CreateWindZone(UIShaderConfig config)
    {
        GameObject obj = new GameObject("WindZone");

        WindZone wind = obj.AddComponent<WindZone>();
        wind.windMain = config.windStrength;
        wind.windTurbulence = config.windTurbulence;
        wind.mode = WindZoneMode.Directional;

        // 바람 방향 (측면에서 불어옴)
        obj.transform.rotation = Quaternion.Euler(0f, config.windDirection, 0f);

        return obj;
    }
}
