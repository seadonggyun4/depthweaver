// Assets/Scripts/Environment/NaturalEnvironmentBuilder.cs
// 자연환경 스튜디오의 모듈 오케스트레이터.
// IEnvironmentModule 구현체들을 순차적으로 실행하여 전체 환경을 구성한다.
// 에디터(StudioSceneSetupWizard)에서 직접 생성하거나,
// 런타임 MonoBehaviour에서 참조하여 사용할 수 있는 순수 C# 클래스.

using System.Collections.Generic;
using UnityEngine;

public class NaturalEnvironmentBuilder
{
    // 등록된 환경 모듈
    private List<IEnvironmentModule> modules = new List<IEnvironmentModule>();

    // 생성된 오브젝트 추적 (정리용)
    private List<GameObject> createdObjects = new List<GameObject>();

    // ═══════════════════════════════════════════════════
    // 환경 빌드
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 모든 환경 모듈을 실행하여 자연환경 스튜디오를 구성한다.
    /// 반환값: 생성된 모든 루트 GameObject 배열
    /// </summary>
    public GameObject[] BuildEnvironment(UIShaderConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[UIShader] NaturalEnvironmentBuilder: UIShaderConfig가 null입니다.");
            return new GameObject[0];
        }

        // 모듈 등록
        RegisterModules();

        createdObjects.Clear();

        // 각 모듈 순차 실행
        foreach (IEnvironmentModule module in modules)
        {
            Debug.Log($"[UIShader] 환경 모듈 빌드 시작: {module.ModuleName}");

            GameObject[] objects = module.Build(config);
            if (objects != null)
            {
                createdObjects.AddRange(objects);
            }

            Debug.Log($"[UIShader] 환경 모듈 빌드 완료: {module.ModuleName} ({(objects != null ? objects.Length : 0)}개 오브젝트)");
        }

        Debug.Log($"[UIShader] 자연환경 스튜디오 빌드 완료: 총 {createdObjects.Count}개 오브젝트");
        return createdObjects.ToArray();
    }

    /// <summary>
    /// 테스트 오브젝트(바위 등)를 생성한다. 환경과 별도로 관리됨.
    /// </summary>
    public GameObject[] BuildTestObjects(UIShaderConfig config)
    {
        if (config == null) return new GameObject[0];

        List<GameObject> testObjects = new List<GameObject>();

        // 1. 큰 바위 — 스크린 좌측 전방
        testObjects.Add(CreateRockObject(
            "TestRock_Large",
            config.screenPosition + new Vector3(-3.5f, -1.5f, 2.5f),
            new Vector3(2f, 1.5f, 1.8f),
            new Color(0.4f, 0.38f, 0.35f)
        ));

        // 2. 작은 바위 — 스크린 우측 전방
        testObjects.Add(CreateRockObject(
            "TestRock_Small",
            config.screenPosition + new Vector3(3f, -2f, 3f),
            new Vector3(1f, 0.8f, 1.2f),
            new Color(0.45f, 0.42f, 0.38f)
        ));

        // 3. 돌기둥 — 스크린 중앙 전방
        testObjects.Add(CreateRockObject(
            "TestPillar",
            config.screenPosition + new Vector3(0f, -1.5f, 4f),
            new Vector3(0.6f, 2.5f, 0.6f),
            new Color(0.5f, 0.48f, 0.44f)
        ));

        // 4. 넓적한 바위 — 바닥 근처
        testObjects.Add(CreateRockObject(
            "TestFlatRock",
            config.screenPosition + new Vector3(1.5f, -2.8f, 1.5f),
            new Vector3(2.5f, 0.4f, 1.8f),
            new Color(0.38f, 0.36f, 0.33f)
        ));

        Debug.Log($"[UIShader] 테스트 오브젝트 {testObjects.Count}개 생성 완료");
        return testObjects.ToArray();
    }

    // ═══════════════════════════════════════════════════
    // 조회 및 정리
    // ═══════════════════════════════════════════════════

    /// <summary>등록된 환경 모듈 목록을 반환한다</summary>
    public IReadOnlyList<IEnvironmentModule> GetModules()
    {
        return modules.AsReadOnly();
    }

    /// <summary>생성된 모든 환경 오브젝트를 제거한다</summary>
    public void ClearEnvironment()
    {
        foreach (IEnvironmentModule module in modules)
        {
            module.Cleanup();
        }
        modules.Clear();

        foreach (GameObject obj in createdObjects)
        {
            if (obj != null) Object.DestroyImmediate(obj);
        }
        createdObjects.Clear();

        Debug.Log("[UIShader] 자연환경 초기화됨");
    }

    // ═══════════════════════════════════════════════════
    // 내부 구현
    // ═══════════════════════════════════════════════════

    /// <summary>환경 모듈을 빌드 순서에 따라 등록한다</summary>
    private void RegisterModules()
    {
        modules.Clear();

        // 순서 중요: 지형 → 하늘 → 태양 → 잔디 → 대기
        // (잔디가 지형 높이를 참조하므로 지형이 먼저)
        modules.Add(new ProceduralTerrainModule());
        modules.Add(new HDRPSkyModule());
        modules.Add(new SunLightModule());
        modules.Add(new ProceduralGrassModule());
        modules.Add(new AtmosphereModule());
    }

    /// <summary>바위/돌 테스트 오브젝트를 생성한다</summary>
    private GameObject CreateRockObject(string name, Vector3 position, Vector3 scale, Color color)
    {
        // 구(Sphere)를 비균일 스케일로 바위 형태에 근사
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = name;
        rock.transform.position = position;
        rock.transform.localScale = scale;
        // 약간의 랜덤 회전으로 자연스러움 부여
        rock.transform.rotation = Quaternion.Euler(
            Random.Range(-15f, 15f),
            Random.Range(0f, 360f),
            Random.Range(-10f, 10f)
        );

        Renderer renderer = rock.GetComponent<Renderer>();
        Material mat = CreateHDRPLitMaterial("M_" + name);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.15f); // 거친 돌 표면
        mat.SetColor("_BaseColor", color);
        renderer.material = mat;

        return rock;
    }

    /// <summary>HDRP/Lit 셰이더로 새 머티리얼을 생성한다</summary>
    private Material CreateHDRPLitMaterial(string materialName)
    {
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null)
        {
            Debug.LogWarning($"[UIShader] HDRP/Lit 셰이더를 찾을 수 없습니다. Standard 폴백 사용.");
            hdrpLit = Shader.Find("Standard");
        }

        Material mat = new Material(hdrpLit);
        mat.name = materialName;
        return mat;
    }
}
