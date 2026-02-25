// Assets/Scripts/Environment/IEnvironmentModule.cs
// 환경 모듈 인터페이스.
// 모든 자연환경 구성 요소(지형, 하늘, 초목, 조명, 대기)는 이 인터페이스를 구현한다.
// NaturalEnvironmentBuilder가 모듈을 발견하고 순차적으로 Build()를 호출한다.
//
// 확장 시: 새 모듈 클래스를 생성하고 IEnvironmentModule을 구현한 뒤,
// NaturalEnvironmentBuilder.CreateModules()에 등록하면 된다.

using UnityEngine;

/// <summary>
/// 환경 모듈 인터페이스.
/// 각 모듈은 독립적으로 환경의 한 측면(지형, 하늘, 초목 등)을 생성하고 관리한다.
/// </summary>
public interface IEnvironmentModule
{
    /// <summary>모듈 이름 (로그 및 디버그용)</summary>
    string ModuleName { get; }

    /// <summary>
    /// 환경 요소를 생성하고 생성된 루트 GameObject 배열을 반환한다.
    /// 반환된 오브젝트는 NaturalEnvironmentBuilder가 씬 하이어라키에 배치한다.
    /// </summary>
    GameObject[] Build(UIShaderConfig config);

    /// <summary>모듈이 생성한 모든 오브젝트를 정리한다</summary>
    void Cleanup();
}
