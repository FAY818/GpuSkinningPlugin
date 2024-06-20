
public enum GPUSkinningAnimType
{
    Vertices,
    Skeleton
}

public enum GPUSkinningQuality
{
    Bone1,
    Bone2, 
    Bone4
}

public enum GPUSkinningShaderType
{
    Unlit, 
    StandardSpecular, 
    StandardMetallic
}

public enum GPUSkinningWrapMode
{
    Once, 
    Loop 
}

public enum GPUSKinningCullingMode
{
    AlwaysAnimate, 
    CullUpdateTransforms, 
    CullCompletely
}

public static class Constants
{
    // skeleton
    public const string TEMP_SAVED_ANIM_PATH = "GPUSkinning_Temp_Save_Anim_Path";
    public const string TEMP_SAVED_MTRL_PATH = "GPUSkinning_Temp_Save_Mtrl_Path";
    public const string TEMP_SAVED_MESH_PATH = "GPUSkinning_Temp_Save_Mesh_Path";
    public const string TEMP_SAVED_SHADER_PATH = "GPUSkinning_Temp_Save_Shader_Path";
    public const string TEMP_SAVED_TEXTURE_PATH = "GPUSkinning_Temp_Save_Texture_Path";
    
    public const string TEMP_SAVED_TEXTUREBIND_PATH = "GPUSkinning_Temp_Save_TextureBind_Path"; //
    
    // vertex
    public const string TEMP_SAVED_ANIM_VERTEX_PATH = "GPUSkinning_Temp_Save_Anim_vertex_Path";
    public const string TEMP_SAVED_MTRL_VERTEX_PATH = "GPUSkinning_Temp_Save_Mtrl_vertex_Path";
    public const string TEMP_SAVED_MESH_VERTEX_PATH = "GPUSkinning_Temp_Save_Mesh_vertex_Path";
    public const string TEMP_SAVED_SHADER_VERTEX_PATH = "GPUSkinning_Temp_Save_Shader_vertex_Path";
    public const string TEMP_SAVED_TEXTURE_VERTEX_PATH = "GPUSkinning_Temp_Save_Texture_vertex_Path";

    // EditorPrefs
    public const string EDITOR_PREFS_PREFIX = "GPUSkinningSamplerEditorPrefs_";
    public const string EDITOR_PREFS_KEY_BOUNDS = "isBoundsFoldout";
    public const string EDITOR_PREFS_KEY_Joints = "isJointsFoldout";
    public const string EDITOR_PREFS_KEY_ROOTMOTION = "isRootMotionFoldout";
    public const string EDITOR_PREFS_KEY_LOD = "isLODFoldout";
    public const string EDITOR_PREFS_KEY_ANIMEVENTS = "isAnimEventsFoldout";
    
    // PlayerPrefs
    public const string USER_PREFS_DIR = "GPUSkinning_UserPreferDir";
}