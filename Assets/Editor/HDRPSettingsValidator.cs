// Assets/Editor/HDRPSettingsValidator.cs
// 도메인 리로드 시 자동으로 HDRP 설정을 검증한다.
// 영역광, 쿠키, SSR, SSAO 등 UIShader 필수 설정이 올바른지 확인한다.
// 메뉴: UIShader > Validate HDRP Settings

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[InitializeOnLoad]
public static class HDRPSettingsValidator
{
    static HDRPSettingsValidator()
    {
        // 도메인 리로드 시 자동 검증 (에디터 시작, 스크립트 변경 등)
        EditorApplication.delayCall += ValidateOnLoad;
    }

    private static void ValidateOnLoad()
    {
        // 조용히 검증 (에러만 출력)
        Validate(verbose: false);
    }

    [MenuItem("UIShader/Validate HDRP Settings", false, 10)]
    public static void ValidateFromMenu()
    {
        Validate(verbose: true);
    }

    private static void Validate(bool verbose)
    {
        bool allPassed = true;

        // ─── HDRP 파이프라인 에셋 확인 ───
        RenderPipelineAsset rpAsset = GraphicsSettings.currentRenderPipeline;
        if (rpAsset == null)
        {
            Debug.LogError("[UIShader] HDRP 검증 실패: 렌더 파이프라인이 설정되지 않았습니다. " +
                         "Edit > Project Settings > Graphics에서 HDRP Asset을 할당하세요.");
            return;
        }

        HDRenderPipelineAsset hdrpAsset = rpAsset as HDRenderPipelineAsset;
        if (hdrpAsset == null)
        {
            Debug.LogError("[UIShader] HDRP 검증 실패: 현재 파이프라인이 HDRP가 아닙니다. " +
                         $"현재: {rpAsset.GetType().Name}");
            return;
        }

        if (verbose)
        {
            Debug.Log($"[UIShader] HDRP Asset 감지: {hdrpAsset.name}");
        }

        // ─── HDRP 설정 SerializedObject를 통한 검증 ───
        SerializedObject serialized = new SerializedObject(hdrpAsset);

        // 영역광 쿠키 관련 설정 검증
        ValidateSetting(serialized, "m_RenderPipelineSettings.lightSettings.cookieAtlasSize",
            "Cookie Atlas Size", 512, ref allPassed, verbose);

        // 그림자 설정
        ValidateSetting(serialized, "m_RenderPipelineSettings.hdShadowInitParams.maxShadowRequests",
            "Max Shadow Requests", 6, ref allPassed, verbose, checkMinimum: true);

        // ─── 품질 설정 확인 ───
        if (QualitySettings.vSyncCount != 0)
        {
            if (verbose)
                Debug.LogWarning("[UIShader] 권장: VSync를 비활성화하세요 (프로파일링 정확성)");
        }

        // ─── Player Settings 확인 ───
        if (!Application.runInBackground)
        {
            Debug.LogWarning("[UIShader] 권장: Player Settings > Run In Background를 활성화하세요 " +
                           "(CEF 통합 시 필수).");
        }

        // ─── 결과 요약 ───
        if (verbose)
        {
            if (allPassed)
            {
                Debug.Log("[UIShader] ═══════════════════════════════════════════");
                Debug.Log("[UIShader] HDRP 설정 검증 통과");
                Debug.Log("[UIShader] ═══════════════════════════════════════════");
            }
            else
            {
                Debug.LogWarning("[UIShader] HDRP 설정 검증 완료 — 일부 항목에 주의가 필요합니다.");
            }
        }

        // ─── 필수 수동 확인 항목 안내 ───
        if (verbose)
        {
            Debug.Log("[UIShader] 수동 확인 필요 항목:");
            Debug.Log("  1. HDRP Asset > Lighting > Area Lights: Enabled");
            Debug.Log("  2. HDRP Asset > Lighting > Shadows > Area Light Shadows: Enabled");
            Debug.Log("  3. HDRP Asset > Lighting > SSR: Enabled (Medium)");
            Debug.Log("  4. HDRP Asset > Lighting > SSAO: Enabled (Medium)");
            Debug.Log("  (Edit > Project Settings > Quality > HDRP에서 확인)");
        }
    }

    private static void ValidateSetting(
        SerializedObject serialized, string propertyPath,
        string displayName, int expectedMinValue,
        ref bool allPassed, bool verbose, bool checkMinimum = false)
    {
        SerializedProperty prop = serialized.FindProperty(propertyPath);
        if (prop == null)
        {
            if (verbose)
                Debug.LogWarning($"[UIShader] HDRP 속성을 찾을 수 없음: {propertyPath}");
            return;
        }

        if (checkMinimum)
        {
            if (prop.intValue < expectedMinValue)
            {
                Debug.LogWarning($"[UIShader] HDRP {displayName}: " +
                               $"현재={prop.intValue}, 권장 최소={expectedMinValue}");
                allPassed = false;
            }
            else if (verbose)
            {
                Debug.Log($"[UIShader] HDRP {displayName}: {prop.intValue} (통과)");
            }
        }
    }
}
#endif
