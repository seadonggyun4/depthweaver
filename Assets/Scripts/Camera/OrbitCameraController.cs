// Assets/Scripts/Camera/OrbitCameraController.cs
// 스크린 메시를 중심으로 궤도 회전하는 카메라 컨트롤러.
// 마우스 우클릭 드래그로 회전, 스크롤로 줌, 1~4키로 프리셋 전환.
// 야외 자연환경에 최적화된 프리셋과 거리 범위를 제공한다.
//
// Phase 5 확장: 자동 순항 모드, 시네마틱 스플라인 경로, 전환 애니메이션

using UnityEngine;

public class OrbitCameraController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 궤도 설정
    // ═══════════════════════════════════════════════════

    [Header("Orbit")]
    [Tooltip("궤도 중심 (미할당 시 ScreenMeshGenerator 자동 탐색)")]
    public Transform target;

    [Tooltip("초기 궤도 거리")]
    public float distance = 12f;

    [Tooltip("최소 줌 거리")]
    public float minDistance = 3f;

    [Tooltip("최대 줌 거리")]
    public float maxDistance = 35f;

    [Tooltip("마우스 회전 감도")]
    public float rotationSpeed = 5f;

    [Tooltip("스크롤 줌 감도")]
    public float zoomSpeed = 3f;

    [Tooltip("보간 속도 (부드러운 이동)")]
    public float smoothSpeed = 8f;

    // ═══════════════════════════════════════════════════
    // 각도 제한
    // ═══════════════════════════════════════════════════

    [Header("Angle Limits")]
    [Tooltip("수직 최소 각도 (아래쪽)")]
    public float minVerticalAngle = -5f;

    [Tooltip("수직 최대 각도 (위쪽)")]
    public float maxVerticalAngle = 70f;

    // ═══════════════════════════════════════════════════
    // 프리셋 카메라 위치 (야외 자연환경 최적화)
    // ═══════════════════════════════════════════════════

    [Header("Presets")]
    public CameraPreset[] presets = new CameraPreset[]
    {
        new CameraPreset("파노라마",     0f,  12f, 14f),
        new CameraPreset("측면뷰",     -50f,  10f, 11f),
        new CameraPreset("상공뷰",      15f,  45f, 18f),
        new CameraPreset("클로즈업",     5f,   8f,  6f)
    };

    [System.Serializable]
    public class CameraPreset
    {
        public string name;
        public float horizontalAngle;
        public float verticalAngle;
        public float distance;

        public CameraPreset(string name, float h, float v, float d)
        {
            this.name = name;
            horizontalAngle = h;
            verticalAngle = v;
            distance = d;
        }
    }

    // ═══════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════

    private float currentHAngle;
    private float currentVAngle = 12f;
    private float currentDist;

    private float targetHAngle;
    private float targetVAngle = 12f;
    private float targetDist;

    /// <summary>현재 활성 프리셋 인덱스 (-1이면 수동 조작 중)</summary>
    public int ActivePresetIndex { get; private set; } = -1;

    void Start()
    {
        // 타겟 자동 탐색
        if (target == null)
        {
            ScreenMeshGenerator screenMesh = FindObjectOfType<ScreenMeshGenerator>();
            if (screenMesh != null)
            {
                target = screenMesh.transform;
            }
            else
            {
                Debug.LogWarning("[UIShader] OrbitCamera: 궤도 타겟을 찾을 수 없습니다.");
            }
        }

        // 초기값 설정
        currentDist = distance;
        targetDist = distance;
        targetHAngle = currentHAngle;
        targetVAngle = currentVAngle;

        // 초기 위치 즉시 적용
        ApplyPosition(immediate: true);
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleMouseOrbit();
        HandleZoom();
        HandlePresetKeys();
        ApplyPosition(immediate: false);
    }

    // ═══════════════════════════════════════════════════
    // 입력 처리
    // ═══════════════════════════════════════════════════

    private void HandleMouseOrbit()
    {
        // 마우스 우클릭 드래그: 궤도 회전
        if (Input.GetMouseButton(1))
        {
            float deltaX = Input.GetAxis("Mouse X") * rotationSpeed;
            float deltaY = Input.GetAxis("Mouse Y") * rotationSpeed;

            targetHAngle += deltaX;
            targetVAngle -= deltaY;
            targetVAngle = Mathf.Clamp(targetVAngle, minVerticalAngle, maxVerticalAngle);

            ActivePresetIndex = -1; // 수동 조작으로 전환
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetDist -= scroll * zoomSpeed;
            targetDist = Mathf.Clamp(targetDist, minDistance, maxDistance);
            ActivePresetIndex = -1;
        }
    }

    private void HandlePresetKeys()
    {
        for (int i = 0; i < presets.Length && i < 4; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                ApplyPreset(i);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 위치 적용
    // ═══════════════════════════════════════════════════

    private void ApplyPosition(bool immediate)
    {
        float lerpFactor = immediate ? 1f : Time.deltaTime * smoothSpeed;

        currentHAngle = Mathf.Lerp(currentHAngle, targetHAngle, lerpFactor);
        currentVAngle = Mathf.Lerp(currentVAngle, targetVAngle, lerpFactor);
        currentDist = Mathf.Lerp(currentDist, targetDist, lerpFactor);

        // 구면 좌표 → 직교 좌표 변환
        float hRad = currentHAngle * Mathf.Deg2Rad;
        float vRad = currentVAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(hRad) * Mathf.Cos(vRad) * currentDist,
            Mathf.Sin(vRad) * currentDist,
            Mathf.Cos(hRad) * Mathf.Cos(vRad) * currentDist
        );

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }

    // ═══════════════════════════════════════════════════
    // 공개 API
    // ═══════════════════════════════════════════════════

    /// <summary>지정된 프리셋으로 카메라를 전환한다</summary>
    public void ApplyPreset(int index)
    {
        if (index < 0 || index >= presets.Length) return;

        CameraPreset preset = presets[index];
        targetHAngle = preset.horizontalAngle;
        targetVAngle = preset.verticalAngle;
        targetDist = preset.distance;
        ActivePresetIndex = index;

        Debug.Log($"[UIShader] 카메라 프리셋: {preset.name}");
    }

    /// <summary>카메라를 즉시 지정 위치로 이동한다 (보간 없음)</summary>
    public void SetPositionImmediate(float hAngle, float vAngle, float dist)
    {
        targetHAngle = hAngle;
        targetVAngle = vAngle;
        targetDist = dist;
        currentHAngle = hAngle;
        currentVAngle = vAngle;
        currentDist = dist;
        ActivePresetIndex = -1;

        if (target != null)
        {
            ApplyPosition(immediate: true);
        }
    }
}
