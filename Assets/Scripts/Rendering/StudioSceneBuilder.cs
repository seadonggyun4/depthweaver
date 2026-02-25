// Assets/Scripts/Rendering/StudioSceneBuilder.cs
// 스튜디오 환경 요소를 절차적으로 생성한다.
// - 반사성 PBR 바닥
// - 반원기둥 사이클로라마 배경막
// - 조명 검증용 테스트 오브젝트 (구, 큐브, 실린더 등)
// - 약한 앰비언트 디렉셔널 라이트
//
// StudioSceneSetupWizard에서 에디터 메뉴로 호출하거나,
// UIShaderBootstrap에서 런타임에 호출할 수 있다.

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class StudioSceneBuilder : MonoBehaviour
{
    [SerializeField] private UIShaderConfig config;

    // 생성된 오브젝트 참조 (정리용)
    private GameObject floorObject;
    private GameObject backdropObject;
    private GameObject ambientLightObject;
    private GameObject[] testObjects;

    // ═══════════════════════════════════════════════════
    // 전체 스튜디오 빌드
    // ═══════════════════════════════════════════════════

    /// <summary>스튜디오 환경의 모든 요소를 생성한다</summary>
    public void BuildFullStudio()
    {
        if (config == null)
        {
            Debug.LogError("[UIShader] StudioSceneBuilder: UIShaderConfig가 할당되지 않았습니다.");
            return;
        }

        floorObject = CreateFloor();
        backdropObject = CreateBackdrop();
        ambientLightObject = CreateAmbientLight();
        testObjects = CreateTestObjects();

        Debug.Log("[UIShader] 스튜디오 환경 빌드 완료");
    }

    // ═══════════════════════════════════════════════════
    // 바닥
    // ═══════════════════════════════════════════════════

    /// <summary>반사성 PBR 바닥을 생성한다</summary>
    public GameObject CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "StudioFloor";
        floor.transform.position = Vector3.zero;

        // Plane 기본 크기 10x10. Scale로 조정.
        float scaleFactor = config.floorSize / 10f;
        floor.transform.localScale = new Vector3(scaleFactor, 1f, scaleFactor);

        // PBR 머티리얼 설정
        Renderer renderer = floor.GetComponent<Renderer>();
        Material mat = CreateHDRPLitMaterial("M_StudioFloor");
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", config.floorSmoothness);
        mat.SetColor("_BaseColor", config.floorColor);
        renderer.material = mat;

        Debug.Log($"[UIShader] 바닥 생성: {config.floorSize}x{config.floorSize} 유닛, " +
                  $"smoothness={config.floorSmoothness}");

        return floor;
    }

    // ═══════════════════════════════════════════════════
    // 배경막 (사이클로라마)
    // ═══════════════════════════════════════════════════

    /// <summary>반원기둥형 사이클로라마 배경막을 생성한다</summary>
    public GameObject CreateBackdrop()
    {
        GameObject backdrop = new GameObject("StudioBackdrop");
        backdrop.transform.position = new Vector3(0f, 0f, config.screenPosition.z - 3f);

        MeshFilter mf = backdrop.AddComponent<MeshFilter>();
        MeshRenderer mr = backdrop.AddComponent<MeshRenderer>();

        Mesh mesh = GenerateCycloramaMesh(
            config.backdropRadius,
            config.backdropHeight,
            64, // 곡면 분할 수
            config.backdropArcDegrees
        );
        mf.mesh = mesh;

        Material mat = CreateHDRPLitMaterial("M_StudioBackdrop");
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", config.backdropSmoothness);
        mat.SetColor("_BaseColor", config.backdropColor);
        mr.material = mat;

        Debug.Log($"[UIShader] 배경막 생성: 반경={config.backdropRadius}, " +
                  $"높이={config.backdropHeight}, 호={config.backdropArcDegrees}도");

        return backdrop;
    }

    /// <summary>반원기둥 사이클로라마 메시를 절차적으로 생성한다</summary>
    private Mesh GenerateCycloramaMesh(float radius, float height, int segments, float arcDegrees)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CycloramaMesh";

        int vertCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        int[] triangles = new int[segments * 6];

        float arcRad = arcDegrees * Mathf.Deg2Rad;
        // 뒤쪽 중앙에서 시작하여 좌우로 호를 그림
        float startAngle = -arcRad * 0.5f + Mathf.PI;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = startAngle + t * arcRad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // 안쪽을 향하는 법선
            float nx = -Mathf.Cos(angle);
            float nz = -Mathf.Sin(angle);

            // 하단 정점
            int bottomIdx = i * 2;
            vertices[bottomIdx] = new Vector3(x, 0f, z);
            uvs[bottomIdx] = new Vector2(t, 0f);
            normals[bottomIdx] = new Vector3(nx, 0f, nz);

            // 상단 정점
            int topIdx = i * 2 + 1;
            vertices[topIdx] = new Vector3(x, height, z);
            uvs[topIdx] = new Vector2(t, 1f);
            normals[topIdx] = new Vector3(nx, 0f, nz);
        }

        // 삼각형 인덱스 (내면을 앞면으로)
        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            int bl = i * 2;
            int tl = i * 2 + 1;
            int br = (i + 1) * 2;
            int tr = (i + 1) * 2 + 1;

            // 삼각형 1
            triangles[triIdx++] = bl;
            triangles[triIdx++] = tl;
            triangles[triIdx++] = br;

            // 삼각형 2
            triangles[triIdx++] = br;
            triangles[triIdx++] = tl;
            triangles[triIdx++] = tr;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }

    // ═══════════════════════════════════════════════════
    // 테스트 오브젝트
    // ═══════════════════════════════════════════════════

    /// <summary>영역광 조명 효과 검증용 테스트 오브젝트를 생성한다</summary>
    public GameObject[] CreateTestObjects()
    {
        GameObject[] objects = new GameObject[4];

        // 1. 구 (금속성) — 스크린 좌측 전방
        objects[0] = CreateTestPrimitive(
            "TestSphere", PrimitiveType.Sphere,
            new Vector3(-2.5f, 1f, -2f),
            Vector3.one * 1.5f,
            metallic: 0.8f, smoothness: 0.6f,
            color: Color.white
        );

        // 2. 정육면체 (무광 백색) — 스크린 우측 전방
        objects[1] = CreateTestPrimitive(
            "TestCube", PrimitiveType.Cube,
            new Vector3(2.5f, 0.75f, -2f),
            Vector3.one * 1.5f,
            metallic: 0f, smoothness: 0.3f,
            color: new Color(0.9f, 0.9f, 0.9f)
        );

        // 3. 원기둥 (크롬 표면) — 스크린 중앙 전방
        objects[2] = CreateTestPrimitive(
            "TestCylinder", PrimitiveType.Cylinder,
            new Vector3(0f, 1f, -1.5f),
            new Vector3(0.5f, 2f, 0.5f),
            metallic: 0.9f, smoothness: 0.95f,
            color: Color.white
        );

        // 4. 평판 큐브 (반사 바닥 효과 검증) — 바닥 근처
        objects[3] = CreateTestPrimitive(
            "TestFlatCube", PrimitiveType.Cube,
            new Vector3(0f, 0.1f, -3f),
            new Vector3(3f, 0.2f, 2f),
            metallic: 0f, smoothness: 0.5f,
            color: new Color(0.7f, 0.7f, 0.7f)
        );

        Debug.Log("[UIShader] 테스트 오브젝트 4개 생성 완료");
        return objects;
    }

    private GameObject CreateTestPrimitive(
        string name, PrimitiveType type,
        Vector3 position, Vector3 scale,
        float metallic, float smoothness, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;

        Renderer renderer = obj.GetComponent<Renderer>();
        Material mat = CreateHDRPLitMaterial("M_" + name);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetColor("_BaseColor", color);
        renderer.material = mat;

        return obj;
    }

    // ═══════════════════════════════════════════════════
    // 앰비언트 라이트
    // ═══════════════════════════════════════════════════

    /// <summary>매우 약한 앰비언트 디렉셔널 라이트를 생성한다</summary>
    public GameObject CreateAmbientLight()
    {
        GameObject lightObj = new GameObject("AmbientLight");
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.8f, 0.85f, 1f); // 약간 푸른 톤
        light.shadows = LightShadows.None;

        HDAdditionalLightData hdLight = lightObj.AddComponent<HDAdditionalLightData>();
        hdLight.lightUnit = LightUnit.Lux;
        hdLight.intensity = 50f; // 매우 약하게 — 스크린 영역광이 주 광원

        Debug.Log("[UIShader] 앰비언트 라이트 생성: 50 lux");
        return lightObj;
    }

    // ═══════════════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════════════

    /// <summary>HDRP/Lit 셰이더로 새 머티리얼을 생성한다</summary>
    private Material CreateHDRPLitMaterial(string materialName)
    {
        // HDRP/Lit 셰이더 탐색
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

    /// <summary>생성된 모든 스튜디오 오브젝트를 제거한다</summary>
    public void ClearStudio()
    {
        if (floorObject != null) DestroyImmediate(floorObject);
        if (backdropObject != null) DestroyImmediate(backdropObject);
        if (ambientLightObject != null) DestroyImmediate(ambientLightObject);

        if (testObjects != null)
        {
            foreach (var obj in testObjects)
            {
                if (obj != null) DestroyImmediate(obj);
            }
        }

        Debug.Log("[UIShader] 스튜디오 환경 초기화됨");
    }
}
