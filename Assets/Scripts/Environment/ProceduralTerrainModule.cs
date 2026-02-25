// Assets/Scripts/Environment/ProceduralTerrainModule.cs
// Perlin 노이즈 기반 절차적 구릉 지형 생성 모듈.
// 다중 옥타브 노이즈로 자연스러운 완만한 언덕을 생성하며,
// HDRP/Lit 초록색 잔디 머티리얼을 적용한다.

using UnityEngine;

public class ProceduralTerrainModule : IEnvironmentModule
{
    public string ModuleName => "ProceduralTerrain";

    private GameObject terrainObject;

    public GameObject[] Build(UIShaderConfig config)
    {
        terrainObject = new GameObject("Terrain");

        MeshFilter mf = terrainObject.AddComponent<MeshFilter>();
        MeshRenderer mr = terrainObject.AddComponent<MeshRenderer>();
        MeshCollider mc = terrainObject.AddComponent<MeshCollider>();

        // 절차적 지형 메시 생성
        Mesh mesh = GenerateTerrainMesh(config);
        mf.mesh = mesh;
        mc.sharedMesh = mesh;

        // 잔디 머티리얼 적용
        Material mat = CreateTerrainMaterial(config);
        mr.material = mat;

        terrainObject.transform.position = Vector3.zero;

        Debug.Log($"[UIShader] 지형 생성: {config.terrainResolution}x{config.terrainResolution} 정점, " +
                  $"{config.terrainSize}x{config.terrainSize} 유닛");

        return new GameObject[] { terrainObject };
    }

    public void Cleanup()
    {
        if (terrainObject != null)
        {
            Object.DestroyImmediate(terrainObject);
        }
    }

    // ═══════════════════════════════════════════════════
    // 지형 메시 생성
    // ═══════════════════════════════════════════════════

    private Mesh GenerateTerrainMesh(UIShaderConfig config)
    {
        int resolution = config.terrainResolution;
        float size = config.terrainSize;
        float maxHeight = config.terrainMaxHeight;
        float noiseScale = config.terrainNoiseScale;
        int octaves = config.terrainNoiseOctaves;

        Mesh mesh = new Mesh();
        mesh.name = "TerrainMesh";

        // 해상도가 높으면 32-bit 인덱스 사용
        if (resolution > 254)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        int vertsPerSide = resolution + 1;
        int totalVerts = vertsPerSide * vertsPerSide;
        int totalTris = resolution * resolution * 2;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        Vector3[] normals = new Vector3[totalVerts];
        int[] triangles = new int[totalTris * 3];

        float halfSize = size * 0.5f;
        float step = size / resolution;

        // 노이즈 시드 (일관성을 위해 고정)
        float seedX = 42.7f;
        float seedZ = 73.1f;

        // 정점 생성
        for (int z = 0; z < vertsPerSide; z++)
        {
            for (int x = 0; x < vertsPerSide; x++)
            {
                int idx = z * vertsPerSide + x;

                float worldX = -halfSize + x * step;
                float worldZ = -halfSize + z * step;

                // 다중 옥타브 Perlin 노이즈로 높이 계산
                float height = SampleMultiOctaveNoise(
                    worldX + seedX, worldZ + seedZ,
                    noiseScale, octaves, maxHeight
                );

                vertices[idx] = new Vector3(worldX, height, worldZ);
                uvs[idx] = new Vector2((float)x / resolution, (float)z / resolution);
                normals[idx] = Vector3.up; // 추후 RecalculateNormals로 보정
            }
        }

        // 삼각형 인덱스 생성
        int triIdx = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int topLeft = z * vertsPerSide + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * vertsPerSide + x;
                int bottomRight = bottomLeft + 1;

                // 삼각형 1
                triangles[triIdx++] = topLeft;
                triangles[triIdx++] = bottomLeft;
                triangles[triIdx++] = topRight;

                // 삼각형 2
                triangles[triIdx++] = topRight;
                triangles[triIdx++] = bottomLeft;
                triangles[triIdx++] = bottomRight;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    // ═══════════════════════════════════════════════════
    // Perlin 노이즈 유틸리티
    // ═══════════════════════════════════════════════════

    /// <summary>다중 옥타브 Perlin 노이즈를 샘플링한다</summary>
    private float SampleMultiOctaveNoise(float x, float z, float baseScale, int octaves, float maxHeight)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float height = 0f;
        float maxAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x / baseScale * frequency;
            float sampleZ = z / baseScale * frequency;

            // Mathf.PerlinNoise는 0~1 반환, -0.5~0.5로 변환하여 양방향 변동
            float noiseValue = Mathf.PerlinNoise(sampleX, sampleZ) - 0.5f;
            height += noiseValue * amplitude;

            maxAmplitude += amplitude;
            amplitude *= 0.5f;   // 옥타브마다 진폭 감쇠
            frequency *= 2f;     // 옥타브마다 주파수 증가
        }

        // 정규화 후 maxHeight 적용
        height = (height / maxAmplitude + 0.5f) * maxHeight;

        return height;
    }

    // ═══════════════════════════════════════════════════
    // 머티리얼
    // ═══════════════════════════════════════════════════

    private Material CreateTerrainMaterial(UIShaderConfig config)
    {
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null)
        {
            hdrpLit = Shader.Find("Standard");
        }

        Material mat = new Material(hdrpLit);
        mat.name = "M_NaturalTerrain";
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", config.terrainSmoothness);
        mat.SetColor("_BaseColor", config.terrainBaseColor);

        return mat;
    }

    // ═══════════════════════════════════════════════════
    // 공개 유틸리티
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 지정된 (x, z) 월드 좌표에서의 지형 높이를 반환한다.
    /// 잔디 배치, 오브젝트 배치 등에 사용된다.
    /// </summary>
    public static float SampleTerrainHeight(float worldX, float worldZ, UIShaderConfig config)
    {
        float seedX = 42.7f;
        float seedZ = 73.1f;

        float amplitude = 1f;
        float frequency = 1f;
        float height = 0f;
        float maxAmplitude = 0f;

        for (int i = 0; i < config.terrainNoiseOctaves; i++)
        {
            float sampleX = (worldX + seedX) / config.terrainNoiseScale * frequency;
            float sampleZ = (worldZ + seedZ) / config.terrainNoiseScale * frequency;

            float noiseValue = Mathf.PerlinNoise(sampleX, sampleZ) - 0.5f;
            height += noiseValue * amplitude;

            maxAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        height = (height / maxAmplitude + 0.5f) * config.terrainMaxHeight;
        return height;
    }
}
