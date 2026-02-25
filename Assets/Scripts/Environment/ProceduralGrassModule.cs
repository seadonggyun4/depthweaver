// Assets/Scripts/Environment/ProceduralGrassModule.cs
// GPU 인스턴싱 기반 절차적 잔디 시스템.
// 지형 표면 위에 잔디 블레이드를 대량 배치하며,
// Graphics.DrawMeshInstanced로 효율적으로 렌더링한다.
// 스크린 직하 영역은 잔디 배치에서 제외한다.

using System.Collections.Generic;
using UnityEngine;

public class ProceduralGrassModule : IEnvironmentModule
{
    public string ModuleName => "ProceduralGrass";

    private GameObject grassRendererObject;
    private GrassRenderer grassRenderer;

    public GameObject[] Build(UIShaderConfig config)
    {
        grassRendererObject = new GameObject("GrassRenderer");
        grassRenderer = grassRendererObject.AddComponent<GrassRenderer>();
        grassRenderer.Initialize(config);

        Debug.Log($"[UIShader] 잔디 시스템 초기화: 밀도={config.grassDensity}");

        return new GameObject[] { grassRendererObject };
    }

    public void Cleanup()
    {
        if (grassRendererObject != null)
        {
            Object.DestroyImmediate(grassRendererObject);
        }
    }
}

/// <summary>
/// 매 프레임 GPU 인스턴싱으로 잔디를 렌더링하는 MonoBehaviour.
/// Graphics.DrawMeshInstanced는 1회 호출당 최대 1023개 인스턴스를 처리한다.
/// </summary>
public class GrassRenderer : MonoBehaviour
{
    private Mesh grassBladeMesh;
    private Material grassMaterial;
    private List<Matrix4x4[]> instanceBatches = new List<Matrix4x4[]>();
    private bool isInitialized;

    /// <summary>잔디 시스템을 초기화하고 인스턴스 데이터를 생성한다</summary>
    public void Initialize(UIShaderConfig config)
    {
        grassBladeMesh = CreateGrassBladeMesh();
        grassMaterial = CreateGrassMaterial(config);
        GenerateGrassInstances(config);
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        // 배치별로 GPU 인스턴싱 렌더링
        foreach (Matrix4x4[] batch in instanceBatches)
        {
            Graphics.DrawMeshInstanced(
                grassBladeMesh,
                0,
                grassMaterial,
                batch,
                batch.Length
            );
        }
    }

    // ═══════════════════════════════════════════════════
    // 잔디 블레이드 메시 생성
    // ═══════════════════════════════════════════════════

    /// <summary>단일 잔디 블레이드 메시 (삼각형 2장, 4 정점)</summary>
    private Mesh CreateGrassBladeMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "GrassBlade";

        // 잔디 블레이드: 아래가 넓고 위가 뾰족한 형태
        float width = 0.06f;
        float height = 0.4f;

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-width * 0.5f, 0f, 0f),       // 좌하
            new Vector3( width * 0.5f, 0f, 0f),       // 우하
            new Vector3(-width * 0.25f, height * 0.6f, 0f), // 좌중
            new Vector3( width * 0.25f, height * 0.6f, 0f), // 우중
            new Vector3(0f, height, 0.02f),            // 상단 (약간 앞으로 휘어짐)
        };

        Vector3[] normals = new Vector3[]
        {
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back,
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.25f, 0.6f),
            new Vector2(0.75f, 0.6f),
            new Vector2(0.5f, 1f),
        };

        int[] triangles = new int[]
        {
            0, 2, 1,   // 하단 삼각형 좌
            1, 2, 3,   // 하단 삼각형 우
            2, 4, 3,   // 상단 삼각형
        };

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    // ═══════════════════════════════════════════════════
    // 잔디 인스턴스 생성
    // ═══════════════════════════════════════════════════

    /// <summary>지형 위에 잔디 인스턴스를 분포시킨다</summary>
    private void GenerateGrassInstances(UIShaderConfig config)
    {
        instanceBatches.Clear();

        float terrainSize = config.terrainSize;
        float halfSize = terrainSize * 0.5f;
        int density = config.grassDensity;
        float grassHeight = config.grassHeight;
        float grassHeightVariation = config.grassHeightVariation;

        // 스크린 위치 (잔디 배치 제외 영역)
        Vector3 screenPos = config.screenPosition;
        float screenExclusionRadius = config.screenWorldSize * 0.7f;

        List<Matrix4x4> allInstances = new List<Matrix4x4>();

        // 지형 영역 내에 균일 분포
        float spacing = terrainSize / Mathf.Sqrt(density);

        for (float x = -halfSize + spacing; x < halfSize - spacing; x += spacing)
        {
            for (float z = -halfSize + spacing; z < halfSize - spacing; z += spacing)
            {
                // 약간의 랜덤 오프셋 (균일 격자 방지)
                float offsetX = x + Random.Range(-spacing * 0.4f, spacing * 0.4f);
                float offsetZ = z + Random.Range(-spacing * 0.4f, spacing * 0.4f);

                // 스크린 직하 영역 제외
                float distToScreen = Vector2.Distance(
                    new Vector2(offsetX, offsetZ),
                    new Vector2(screenPos.x, screenPos.z)
                );
                if (distToScreen < screenExclusionRadius) continue;

                // 지형 높이 샘플링
                float terrainY = ProceduralTerrainModule.SampleTerrainHeight(offsetX, offsetZ, config);

                // 랜덤 회전 및 스케일
                float rotation = Random.Range(0f, 360f);
                float scale = grassHeight + Random.Range(-grassHeightVariation, grassHeightVariation);

                Matrix4x4 matrix = Matrix4x4.TRS(
                    new Vector3(offsetX, terrainY, offsetZ),
                    Quaternion.Euler(0f, rotation, Random.Range(-5f, 5f)),
                    new Vector3(scale, scale, scale)
                );

                allInstances.Add(matrix);
            }
        }

        // 1023개씩 배치 분할 (DrawMeshInstanced 제한)
        const int batchSize = 1023;
        for (int i = 0; i < allInstances.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, allInstances.Count - i);
            Matrix4x4[] batch = new Matrix4x4[count];
            allInstances.CopyTo(i, batch, 0, count);
            instanceBatches.Add(batch);
        }

        Debug.Log($"[UIShader] 잔디 인스턴스 생성: {allInstances.Count}개 ({instanceBatches.Count} 배치)");
    }

    // ═══════════════════════════════════════════════════
    // 머티리얼
    // ═══════════════════════════════════════════════════

    private Material CreateGrassMaterial(UIShaderConfig config)
    {
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null)
        {
            hdrpLit = Shader.Find("Standard");
        }

        Material mat = new Material(hdrpLit);
        mat.name = "M_Grass";
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.2f);
        mat.SetColor("_BaseColor", config.grassColor);

        // 양면 렌더링 (잔디 블레이드 뒷면도 보이도록)
        mat.SetFloat("_DoubleSidedEnable", 1f);
        mat.SetFloat("_CullMode", 0f); // Off

        // GPU 인스턴싱 활성화
        mat.enableInstancing = true;

        return mat;
    }
}
