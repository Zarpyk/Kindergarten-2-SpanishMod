#if UNITY_EDITOR
using UnityEditor;

public class CreateAssetBundles {
    [MenuItem("Assets/Build AssetBundles")]
    private static void BuildAllAssetBundles() {
        BuildPipeline.BuildAssetBundles("Assets/AssetBundles/assets", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
#endif