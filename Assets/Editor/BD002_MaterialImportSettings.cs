// 放置路径: Assets/Editor/BD002_MaterialImportSettings.cs
// Unity 2022 兼容
// 功能: 自动将图片中的 Materials 导入设置应用到所有新导入的模型资产

using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动为导入的模型应用以下 Materials 设置:
///   - Material Creation Mode : Import via MaterialDescription
///   - Location               : Use External Materials (Legacy)
///   - Naming                 : From Model's Material
///   - Search                 : Project-Wide
/// </summary>
public class BD002_MaterialImportSettings : AssetPostprocessor
{
    // ------------------------------------------------------------------ //
    //  在模型导入【之前】触发，用于修改 ModelImporter 设置
    // ------------------------------------------------------------------ //
    void OnPreprocessModel()
    {
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        // 只在第一次导入时自动应用，避免覆盖手动修改
        // 如需每次都强制覆盖，请删除下面这行 if 判断
        if (!importer.importSettingsMissing) return;

        ApplyMaterialSettings(importer);

        Debug.Log($"[BD002] 已自动应用 Materials 导入设置: {assetPath}");
    }

    // ------------------------------------------------------------------ //
    //  核心：应用 Materials Tab 的四项设置
    // ------------------------------------------------------------------ //
    static void ApplyMaterialSettings(ModelImporter importer)
    {
        // 1. Material Creation Mode → Import via MaterialDescription
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        // 2. Location → Use External Materials (Legacy)
        importer.materialLocation = ModelImporterMaterialLocation.External;

        // 3. Naming → From Model's Material
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;

        // 4. Search → Project-Wide
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
    }

    // ------------------------------------------------------------------ //
    //  菜单项：批量重新应用到 Project 中已有的所有模型
    // ------------------------------------------------------------------ //
    [MenuItem("Tools/BD002/批量应用 Material 导入设置")]
    static void ApplyToAllModelsInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ApplyMaterialSettings(importer);
            importer.SaveAndReimport();
            count++;
        }

        Debug.Log($"[BD002] 批量处理完成，共更新 {count} 个模型。");
        EditorUtility.DisplayDialog(
            "BD002 批量设置完成",
            $"已对 {count} 个模型应用 Materials 导入设置。",
            "OK"
        );
    }

    // ------------------------------------------------------------------ //
    //  菜单项：仅对 Project 窗口中【选中的模型】应用设置
    // ------------------------------------------------------------------ //
    [MenuItem("Tools/BD002/对选中模型应用 Material 导入设置")]
    static void ApplyToSelectedModels()
    {
        Object[] selected = Selection.objects;
        int count = 0;

        foreach (Object obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ApplyMaterialSettings(importer);
            importer.SaveAndReimport();
            count++;
        }

        Debug.Log($"[BD002] 已对选中的 {count} 个模型应用 Materials 导入设置。");
    }

    // 验证菜单项：没有选中模型时灰显
    [MenuItem("Tools/BD002/对选中模型应用 Material 导入设置", true)]
    static bool ValidateApplyToSelectedModels()
    {
        return Selection.objects.Length > 0;
    }
}
