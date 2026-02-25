// Assets/Editor/StudioSceneSetupWizard.cs
// 메뉴: UIShader > Build Studio Scene
// 원클릭으로 전체 스튜디오 씬을 자동 구성한다.
// 스크린 메시, 영역광, 환경, 카메라, 매니저를 모두 생성하고 연결한다.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class StudioSceneSetupWizard
{
    [MenuItem("UIShader/Build Studio Scene", false, 20)]
    public static void BuildStudioScene()
    {
        // ─── UIShaderConfig 에셋 탐색 ───
        UIShaderConfig config = FindOrCreateConfig();
        if (config == null)
        {
            Debug.LogError("[UIShader] UIShaderConfig를 찾을 수 없습니다. " +
                         "Assets/ScriptableObjects/에 생성하세요: Create > UIShader > Config");
            return;
        }

        // ─── 기존 UIShader 오브젝트 정리 ───
        CleanupExistingObjects();

        // ═══════════════════════════════════════════════
        // 1. 환경 생성
        // ═══════════════════════════════════════════════

        GameObject environmentRoot = new GameObject("--- Environment ---");

        // StudioSceneBuilder를 임시로 생성하여 환경 구축
        GameObject builderObj = new GameObject("_TempBuilder");
        StudioSceneBuilder builder = builderObj.AddComponent<StudioSceneBuilder>();

        // config 필드를 SerializedObject로 할당
        SerializedObject builderSO = new SerializedObject(builder);
        builderSO.FindProperty("config").objectReferenceValue = config;
        builderSO.ApplyModifiedPropertiesWithoutUndo();

        // 환경 요소 생성
        builder.BuildFullStudio();

        // 생성된 오브젝트를 Environment 루트 아래로 이동
        MoveToParent("StudioFloor", environmentRoot);
        MoveToParent("StudioBackdrop", environmentRoot);
        MoveToParent("AmbientLight", environmentRoot);

        // 테스트 오브젝트 그룹
        GameObject testRoot = new GameObject("--- Test Objects ---");
        MoveToParent("TestSphere", testRoot);
        MoveToParent("TestCube", testRoot);
        MoveToParent("TestCylinder", testRoot);
        MoveToParent("TestFlatCube", testRoot);

        // 임시 빌더 제거
        Object.DestroyImmediate(builderObj);

        // ═══════════════════════════════════════════════
        // 2. 스크린 생성
        // ═══════════════════════════════════════════════

        GameObject screenRoot = new GameObject("--- Screen ---");

        // 스크린 메시
        GameObject screenMeshObj = new GameObject("ScreenMesh");
        screenMeshObj.transform.position = config.screenPosition;
        screenMeshObj.transform.eulerAngles = config.screenRotation;
        screenMeshObj.transform.parent = screenRoot.transform;

        MeshFilter mf = screenMeshObj.AddComponent<MeshFilter>();
        MeshRenderer mr = screenMeshObj.AddComponent<MeshRenderer>();

        ScreenMeshGenerator meshGen = screenMeshObj.AddComponent<ScreenMeshGenerator>();
        SerializedObject meshGenSO = new SerializedObject(meshGen);
        meshGenSO.FindProperty("config").objectReferenceValue = config;
        meshGenSO.ApplyModifiedPropertiesWithoutUndo();

        // 변위 셰이더 머티리얼 생성 및 할당
        Shader displacementShader = Shader.Find("UIShader/DisplacedScreen_Simple");
        Material screenMat;
        if (displacementShader != null)
        {
            screenMat = new Material(displacementShader);
            screenMat.name = "M_ScreenSurface";
        }
        else
        {
            Debug.LogWarning("[UIShader] UIShader/DisplacedScreen_Simple 셰이더를 찾을 수 없습니다. " +
                           "Unlit/Color 폴백 사용.");
            screenMat = new Material(Shader.Find("Unlit/Color"));
            screenMat.name = "M_ScreenSurface_Fallback";
        }
        mr.material = screenMat;

        // MeshCollider 추가 (Phase 1 입력 레이캐스트용)
        screenMeshObj.AddComponent<MeshCollider>();

        // 영역광
        GameObject lightObj = new GameObject("ScreenAreaLight");
        lightObj.transform.position = config.screenPosition + new Vector3(0f, 0f, 0.01f);
        lightObj.transform.eulerAngles = config.screenRotation;
        lightObj.transform.parent = screenRoot.transform;

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Area;

        ScreenLightController lightCtrl = lightObj.AddComponent<ScreenLightController>();
        SerializedObject lightSO = new SerializedObject(lightCtrl);
        lightSO.FindProperty("config").objectReferenceValue = config;
        lightSO.ApplyModifiedPropertiesWithoutUndo();

        // ═══════════════════════════════════════════════
        // 3. 카메라 설정
        // ═══════════════════════════════════════════════

        GameObject cameraRoot = new GameObject("--- Camera ---");

        // 메인 카메라 (기존 있으면 재활용)
        Camera mainCam = Camera.main;
        GameObject camObj;
        if (mainCam != null)
        {
            camObj = mainCam.gameObject;
        }
        else
        {
            camObj = new GameObject("MainCamera");
            camObj.tag = "MainCamera";
            camObj.AddComponent<Camera>();
            camObj.AddComponent<HDAdditionalCameraData>();
        }
        camObj.transform.parent = cameraRoot.transform;

        OrbitCameraController orbitCam = camObj.GetComponent<OrbitCameraController>();
        if (orbitCam == null)
        {
            orbitCam = camObj.AddComponent<OrbitCameraController>();
        }
        orbitCam.target = screenMeshObj.transform;

        // ═══════════════════════════════════════════════
        // 4. 매니저 설정
        // ═══════════════════════════════════════════════

        GameObject managersRoot = new GameObject("--- Managers ---");

        // TexturePipelineManager
        GameObject pipelineObj = new GameObject("PipelineManager");
        pipelineObj.transform.parent = managersRoot.transform;

        TexturePipelineManager pipeline = pipelineObj.AddComponent<TexturePipelineManager>();

        // StaticTextureSource (같은 오브젝트에 추가)
        StaticTextureSource staticSource = pipelineObj.AddComponent<StaticTextureSource>();

        // 참조 연결
        SerializedObject pipelineSO = new SerializedObject(pipeline);
        pipelineSO.FindProperty("config").objectReferenceValue = config;
        pipelineSO.FindProperty("screenMesh").objectReferenceValue = meshGen;
        pipelineSO.FindProperty("screenLight").objectReferenceValue = lightCtrl;
        pipelineSO.FindProperty("textureSourceComponent").objectReferenceValue = staticSource;
        pipelineSO.ApplyModifiedPropertiesWithoutUndo();

        // Bootstrap
        GameObject bootstrapObj = new GameObject("Bootstrap");
        bootstrapObj.transform.parent = managersRoot.transform;

        UIShaderBootstrap bootstrap = bootstrapObj.AddComponent<UIShaderBootstrap>();
        SerializedObject bootstrapSO = new SerializedObject(bootstrap);
        bootstrapSO.FindProperty("config").objectReferenceValue = config;
        bootstrapSO.ApplyModifiedPropertiesWithoutUndo();

        // ═══════════════════════════════════════════════
        // 5. 씬 마무리
        // ═══════════════════════════════════════════════

        // 씬 dirty 플래그 설정 (저장 필요 알림)
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[UIShader] ═══════════════════════════════════════════");
        Debug.Log("[UIShader] 스튜디오 씬 빌드 완료!");
        Debug.Log("[UIShader] ═══════════════════════════════════════════");
        Debug.Log("[UIShader] 다음 단계:");
        Debug.Log("  1. StaticTextureSource에 테스트 텍스처를 할당하세요");
        Debug.Log("     - testColorTexture: 512x512 웹페이지 스크린샷 (sRGB)");
        Debug.Log("     - testDepthTexture: 512x512 그레이스케일 깊이 맵 (sRGB Off)");
        Debug.Log("  2. Play 모드를 실행하여 파이프라인을 검증하세요");
        Debug.Log("  3. 마우스 우클릭 드래그: 카메라 회전");
        Debug.Log("  4. 스크롤: 줌");
        Debug.Log("  5. 1~4키: 카메라 프리셋");
    }

    // ═══════════════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════════════

    private static UIShaderConfig FindOrCreateConfig()
    {
        // ScriptableObjects 폴더에서 기존 에셋 탐색
        string[] guids = AssetDatabase.FindAssets("t:UIShaderConfig");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            UIShaderConfig existing = AssetDatabase.LoadAssetAtPath<UIShaderConfig>(path);
            if (existing != null)
            {
                Debug.Log($"[UIShader] 기존 UIShaderConfig 사용: {path}");
                return existing;
            }
        }

        // 없으면 자동 생성
        string createPath = "Assets/ScriptableObjects/UIShaderConfig.asset";
        UIShaderConfig newConfig = ScriptableObject.CreateInstance<UIShaderConfig>();
        AssetDatabase.CreateAsset(newConfig, createPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[UIShader] UIShaderConfig 자동 생성: {createPath}");
        return newConfig;
    }

    private static void CleanupExistingObjects()
    {
        // UIShader 관련 루트 오브젝트 제거
        string[] rootNames = {
            "--- Environment ---",
            "--- Screen ---",
            "--- Test Objects ---",
            "--- Camera ---",
            "--- Managers ---"
        };

        foreach (string name in rootNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }

        // 개별 이름으로도 정리
        string[] individualNames = {
            "StudioFloor", "StudioBackdrop", "AmbientLight",
            "TestSphere", "TestCube", "TestCylinder", "TestFlatCube",
            "ScreenMesh", "ScreenAreaLight",
            "PipelineManager", "Bootstrap"
        };

        foreach (string name in individualNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private static void MoveToParent(string childName, GameObject parent)
    {
        GameObject child = GameObject.Find(childName);
        if (child != null)
        {
            child.transform.parent = parent.transform;
        }
    }
}
#endif
