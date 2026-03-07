// Assets/Scripts/CEF/CEFInputHandler.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 입력 전달 핸들러
// ══════════════════════════════════════════════════════════════════════
//
// Unity 입력 시스템의 마우스/키보드 이벤트를 CEF 브라우저에 전달한다.
// 스크린 메시에 대한 레이캐스트로 마우스 좌표를 계산한다.
//
// 설계 원칙:
//   - IBrowserBackend만 참조 (구체적 백엔드에 비의존)
//   - 스크린 호버 상태에서만 입력을 캡처하여 카메라 제어와 공존
//   - UIShaderConfig.enableInput으로 전체 입력 전달 토글

using UnityEngine;

public class CEFInputHandler : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 인스펙터 참조
    // ═══════════════════════════════════════════════════

    [Header("Core")]
    [SerializeField] private CEFTextureSource textureSource;
    [SerializeField] private UIShaderConfig config;

    [Header("Scene")]
    [SerializeField] private ScreenMeshGenerator screenMesh;
    [SerializeField] private Camera mainCamera;

    // ═══════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════

    private IBrowserBackend backend;
    private int resolution;
    private bool isHoveringScreen;
    private Vector2 lastCEFMousePos;
    private MeshCollider screenCollider;

    /// <summary>스크린 메시 위에 마우스가 올라가 있는지 여부</summary>
    public bool IsHoveringScreen => isHoveringScreen;

    // ═══════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Start()
    {
        resolution = config != null ? config.screenResolution : 512;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // MeshCollider 확인 (레이캐스트에 필요)
        if (screenMesh != null)
        {
            screenCollider = screenMesh.GetComponent<MeshCollider>();

            if (screenCollider == null)
            {
                // 다음 프레임에 설정 (메시 생성 후)
                StartCoroutine(SetupColliderNextFrame());
            }
        }
    }

    void Update()
    {
        // 백엔드 지연 바인딩
        if (backend == null && textureSource != null)
        {
            backend = textureSource.Backend;
        }

        if (backend == null || !backend.IsInitialized) return;
        if (config != null && !config.enableInput) return;

        HandleMouseInput();
        HandleKeyboardInput();
        HandleScrollInput();
    }

    // ═══════════════════════════════════════════════════
    // 마우스 입력
    // ═══════════════════════════════════════════════════

    private void HandleMouseInput()
    {
        if (mainCamera == null || screenMesh == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider != null && hit.collider.gameObject == screenMesh.gameObject)
            {
                isHoveringScreen = true;

                // UV 좌표 → CEF 픽셀 좌표 변환
                // Unity UV: Y축 하→상, CEF: Y축 상→하
                Vector2 uv = hit.textureCoord;
                int cefX = Mathf.Clamp(Mathf.RoundToInt(uv.x * resolution), 0, resolution - 1);
                int cefY = Mathf.Clamp(Mathf.RoundToInt((1f - uv.y) * resolution), 0, resolution - 1);

                lastCEFMousePos = new Vector2(cefX, cefY);

                // 마우스 이동
                backend.SendMouseMove(cefX, cefY);

                // 마우스 클릭
                ProcessMouseButton(cefX, cefY, 0, MouseButton.Left);
                ProcessMouseButton(cefX, cefY, 1, MouseButton.Right);
                ProcessMouseButton(cefX, cefY, 2, MouseButton.Middle);

                return;
            }
        }

        isHoveringScreen = false;
    }

    private void ProcessMouseButton(int cefX, int cefY, int unityButton, MouseButton cefButton)
    {
        if (Input.GetMouseButtonDown(unityButton))
            backend.SendMouseDown(cefX, cefY, cefButton);
        if (Input.GetMouseButtonUp(unityButton))
            backend.SendMouseUp(cefX, cefY, cefButton);
    }

    // ═══════════════════════════════════════════════════
    // 키보드 입력
    // ═══════════════════════════════════════════════════

    private void HandleKeyboardInput()
    {
        if (!isHoveringScreen) return;

        KeyModifiers modifiers = GetCurrentModifiers();

        // 키 다운 이벤트
        if (Input.anyKeyDown)
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(key))
                {
                    int winKey = UnityKeyToWindowsKey(key);
                    if (winKey != 0)
                    {
                        backend.SendKeyDown(winKey, modifiers);
                    }
                }
            }
        }

        // 키 업 이벤트
        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyUp(key))
            {
                int winKey = UnityKeyToWindowsKey(key);
                if (winKey != 0)
                {
                    backend.SendKeyUp(winKey, modifiers);
                }
            }
        }

        // 문자 입력 (텍스트 필드용)
        foreach (char c in Input.inputString)
        {
            backend.SendChar(c);
        }
    }

    // ═══════════════════════════════════════════════════
    // 스크롤 입력
    // ═══════════════════════════════════════════════════

    private void HandleScrollInput()
    {
        if (!isHoveringScreen) return;

        float scrollY = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            int cefX = (int)lastCEFMousePos.x;
            int cefY = (int)lastCEFMousePos.y;
            int deltaY = Mathf.RoundToInt(scrollY * 120); // 120 = 표준 마우스 휠 단위

            backend.SendMouseWheel(cefX, cefY, 0, deltaY);
        }
    }

    // ═══════════════════════════════════════════════════
    // 키 매핑
    // ═══════════════════════════════════════════════════

    private KeyModifiers GetCurrentModifiers()
    {
        KeyModifiers mod = KeyModifiers.None;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            mod |= KeyModifiers.Shift;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            mod |= KeyModifiers.Control;
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            mod |= KeyModifiers.Alt;
        if (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
            mod |= KeyModifiers.Command;

        return mod;
    }

    /// <summary>
    /// Unity KeyCode를 Windows 가상 키 코드로 변환한다.
    /// CEF는 Windows 가상 키 코드를 내부적으로 사용한다.
    /// </summary>
    private int UnityKeyToWindowsKey(KeyCode key)
    {
        switch (key)
        {
            // 제어 키
            case KeyCode.Backspace: return 0x08;
            case KeyCode.Tab: return 0x09;
            case KeyCode.Return: return 0x0D;
            case KeyCode.Escape: return 0x1B;
            case KeyCode.Space: return 0x20;
            case KeyCode.Delete: return 0x2E;
            case KeyCode.Insert: return 0x2D;

            // 방향 키
            case KeyCode.LeftArrow: return 0x25;
            case KeyCode.UpArrow: return 0x26;
            case KeyCode.RightArrow: return 0x27;
            case KeyCode.DownArrow: return 0x28;

            // 네비게이션 키
            case KeyCode.Home: return 0x24;
            case KeyCode.End: return 0x23;
            case KeyCode.PageUp: return 0x21;
            case KeyCode.PageDown: return 0x22;

            default: break;
        }

        // A-Z (0x41 ~ 0x5A)
        if (key >= KeyCode.A && key <= KeyCode.Z)
            return 0x41 + (key - KeyCode.A);

        // 0-9 (0x30 ~ 0x39)
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            return 0x30 + (key - KeyCode.Alpha0);

        // F1-F12 (0x70 ~ 0x7B)
        if (key >= KeyCode.F1 && key <= KeyCode.F12)
            return 0x70 + (key - KeyCode.F1);

        // 넘패드 0-9 (0x60 ~ 0x69)
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            return 0x60 + (key - KeyCode.Keypad0);

        return 0; // 매핑 없음
    }

    // ═══════════════════════════════════════════════════
    // MeshCollider 설정
    // ═══════════════════════════════════════════════════

    private System.Collections.IEnumerator SetupColliderNextFrame()
    {
        yield return null; // 메시 생성 대기

        if (screenMesh == null) yield break;

        var meshFilter = screenMesh.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null) yield break;

        screenCollider = screenMesh.gameObject.AddComponent<MeshCollider>();
        screenCollider.sharedMesh = meshFilter.mesh;
    }
}
