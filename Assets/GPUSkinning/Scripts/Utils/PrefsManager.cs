using UnityEditor;
using UnityEngine;

public static class PrefsManager
{
    #region PlayerPrefs
    
    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
    
    public static int GetInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }
    
    public static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }
    
    public static float GetFloat(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(key, defaultValue);
    }
    
    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }
    
    public static string GetString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(key, defaultValue);
    }
    
    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
    }
    
    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
    }

    #endregion
    
    #region EditorPrefs
    
    public static void SetEditorInt(string key, int value)
    {
        EditorPrefs.SetInt(Constants.EDITOR_PREFS_PREFIX + key, value);
    }
    
    public static int GetEditorInt(string key, int defaultValue = 0)
    {
        return EditorPrefs.GetInt(Constants.EDITOR_PREFS_PREFIX + key, defaultValue);
    }
    
    public static void SetEditorFloat(string key, float value)
    {
        EditorPrefs.SetFloat(Constants.EDITOR_PREFS_PREFIX + key, value);
    }
    
    public static float GetEditorFloat(string key, float defaultValue = 0f)
    {
        return EditorPrefs.GetFloat(Constants.EDITOR_PREFS_PREFIX + key, defaultValue);
    }
    
    public static void SetEditorString(string key, string value)
    {
        EditorPrefs.SetString(Constants.EDITOR_PREFS_PREFIX + key, value);
    }
    
    public static string GetEditorString(string key, string defaultValue = "")
    {
        return EditorPrefs.GetString(Constants.EDITOR_PREFS_PREFIX + key, defaultValue);
    }
    
    public static void SetEditorBool(string key, bool value)
    {
        EditorPrefs.SetBool(Constants.EDITOR_PREFS_PREFIX + key, value);
    }
    
    public static bool GetEditorBool(string key, bool defaultValue = false)
    {
        return EditorPrefs.GetBool(Constants.EDITOR_PREFS_PREFIX + key, defaultValue);
    }
    
    public static void DeleteEditorKey(string key)
    {
        EditorPrefs.DeleteKey(Constants.EDITOR_PREFS_PREFIX + key);
    }
    
    public static void DeleteEditorAll()
    {
        EditorPrefs.DeleteAll();
    }

    #endregion
}

