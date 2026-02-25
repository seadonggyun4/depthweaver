// Assets/Scripts/Rendering/ScreenMeshGenerator.cs
// 511x511 세그먼트로 분할된 고해상도 평면 메시를 절차적으로 생성한다.
// 262,144개 정점을 가진 이 메시는 깊이 맵에 의해 변위되어
// 웹 페이지 UI 요소들이 물리적으로 돌출되는 2.5D 표면을 형성한다.
//
// Phase 3 확장: LOD 시스템 (511→255→63), 탄젠트 벡터, MeshCollider

using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ScreenMeshGenerator : MonoBehaviour
{
    [SerializeField] private UIShaderConfig config;

    private Mesh generatedMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    /// <summary>생성된 메시에 대한 읽기 전용 접근 (Phase 3 LOD용)</summary>
    public Mesh CurrentMesh => generatedMesh;

    /// <summary>현재 세그먼트 수</summary>
    public int CurrentSegments { get; private set; }

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void Start()
    {
        GenerateScreenMesh();
    }

    /// <summary>
    /// 설정에 따라 스크린 메시를 (재)생성한다.
    /// 외부에서 호출하여 해상도를 변경할 수 있다.
    /// </summary>
    public void GenerateScreenMesh()
    {
        int segments = config != null ? config.screenSegments : 511;
        float size = config != null ? config.screenWorldSize : 6f;
        GenerateScreenMesh(segments, size);
    }

    /// <summary>
    /// 지정된 파라미터로 스크린 메시를 생성한다.
    /// Phase 3의 LOD 전환 시 이 오버로드를 사용한다.
    /// </summary>
    public void GenerateScreenMesh(int segments, float size)
    {
        CurrentSegments = segments;

        int vertsPerSide = segments + 1;
        int totalVerts = vertsPerSide * vertsPerSide;
        int totalTriangles = segments * segments * 2;
        int totalIndices = totalTriangles * 3;

        // 기존 메시가 있으면 해제
        if (generatedMesh != null)
        {
            generatedMesh.Clear();
        }
        else
        {
            generatedMesh = new Mesh();
        }

        generatedMesh.name = $"ScreenMesh_{segments}x{segments}";

        // 262,144 정점은 16-bit 인덱스 한계(65,535)를 초과하므로 32-bit 필수
        generatedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        Vector3[] normals = new Vector3[totalVerts];
        int[] triangles = new int[totalIndices];

        float halfSize = size * 0.5f;
        float step = size / segments;

        // ─── 정점 생성 ───
        // XY 평면에 배치, 법선은 +Z 방향 (카메라를 향함)
        // UV 좌표계: 좌하단 (0,0) ~ 우상단 (1,1)
        for (int y = 0; y < vertsPerSide; y++)
        {
            for (int x = 0; x < vertsPerSide; x++)
            {
                int idx = y * vertsPerSide + x;

                float posX = -halfSize + x * step;
                float posY = halfSize - y * step;

                vertices[idx] = new Vector3(posX, posY, 0f);

                uvs[idx] = new Vector2(
                    (float)x / segments,
                    1f - (float)y / segments
                );

                normals[idx] = Vector3.forward;
            }
        }

        // ─── 삼각형 인덱스 생성 ───
        // 각 사각형을 두 삼각형으로 분할 (시계 방향, +Z가 앞면)
        int triIdx = 0;
        for (int y = 0; y < segments; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                int topLeft = y * vertsPerSide + x;
                int topRight = topLeft + 1;
                int bottomLeft = (y + 1) * vertsPerSide + x;
                int bottomRight = bottomLeft + 1;

                // 삼각형 1: 좌상 → 좌하 → 우상
                triangles[triIdx++] = topLeft;
                triangles[triIdx++] = bottomLeft;
                triangles[triIdx++] = topRight;

                // 삼각형 2: 우상 → 좌하 → 우하
                triangles[triIdx++] = topRight;
                triangles[triIdx++] = bottomLeft;
                triangles[triIdx++] = bottomRight;
            }
        }

        // ─── 메시 할당 ───
        generatedMesh.vertices = vertices;
        generatedMesh.uv = uvs;
        generatedMesh.normals = normals;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateBounds();

        // 탄젠트 계산 (법선 맵 및 셰이더 호환성을 위해)
        generatedMesh.RecalculateTangents();

        meshFilter.mesh = generatedMesh;

        // MeshCollider가 있으면 갱신 (Phase 1 입력 레이캐스트용)
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.sharedMesh = generatedMesh;
        }

        Debug.Log($"[UIShader] ScreenMesh 생성 완료: " +
                  $"{totalVerts:N0} 정점, {totalTriangles:N0} 삼각형, " +
                  $"{segments}x{segments} 세그먼트, {size}x{size} 유닛");
    }

    /// <summary>
    /// MeshCollider를 추가하고 현재 메시를 할당한다.
    /// Phase 1의 CEFInputForwarder가 레이캐스트에 사용한다.
    /// </summary>
    public MeshCollider EnsureMeshCollider()
    {
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<MeshCollider>();
        }
        if (generatedMesh != null)
        {
            collider.sharedMesh = generatedMesh;
        }
        return collider;
    }
}
