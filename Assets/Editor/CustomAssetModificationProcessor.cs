using UnityEngine;
using System.Collections;
using UnityEditor;

public class CustomAssetModificationProcessor : UnityEditor.AssetModificationProcessor
{
    public static bool IsOpenForEdit(string assetPath, out string message)
    {
        message = string.Empty;
        return true;
    }

    public static void OnWillCreateAsset(string assetPath)
    {
        Debug.Log("Unity 创建了一个新文件：" + assetPath + "。");
    }

    public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
    {
        switch(options)
        {
            case RemoveAssetOptions.DeleteAssets:
                Debug.Log("Unity 删除了一个文件：" + assetPath + "。");
                break;
            case RemoveAssetOptions.MoveAssetToTrash:
                Debug.Log("Unity 把一个文件：" + assetPath + " 放到了回收站。");
                break;
        }
        //if(assetPath.StartsWith("Assets/Editor/Dungeon"))
        //{
        //    GameObject dungeonEditorObject = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;

        //}
        return AssetDeleteResult.DidNotDelete;
    }

    public static AssetMoveResult OnWillMoveAsset(string oldPath, string newPath)
    {
        if(EditorUtility.DisplayDialog("确定移动文件？", "你确定要把 " + oldPath + " 移动到 " + newPath + " 去吗？", "确定", "取消"))
        {
            return AssetMoveResult.DidNotMove;
        }
        else
        {
            return AssetMoveResult.FailedMove;
        }
    }

    public static string[] OnWillSaveAssets(string[] names)
    {
        string temp = string.Empty;
        foreach(string name in names)
        {
            temp += name + ", ";
        }
        if(temp != string.Empty)
        {
            Debug.Log("Unity 保存了下列文件： " + temp + "。");
        }
        return names;
    }
}
