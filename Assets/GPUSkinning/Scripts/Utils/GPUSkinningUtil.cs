using UnityEngine;
using System.Collections;
using System.Security.Cryptography;

public class GPUSkinningUtil
{
    public static void MarkAllScenesDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.CallbackFunction DelayCall = null;
            DelayCall = () =>
            {
                UnityEditor.EditorApplication.delayCall -= DelayCall;
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            };
            UnityEditor.EditorApplication.delayCall += DelayCall;
        }
#endif
    }

    /// <summary>
    /// 创建动画纹理
    /// </summary>
    /// <param name="textureRawData">采样的纹理数据</param>
    /// <param name="anim">采样的动画数据</param>
    /// <returns></returns>
    public static Texture2D CreateTexture2D(TextAsset textureRawData, GPUSkinningAnimation anim)
    {
        if (textureRawData == null || anim == null)
        {
            return null;
        }

        Texture2D texture = new Texture2D(anim.textureWidth, anim.textureHeight, TextureFormat.RGBAHalf, false, true);
        texture.name = "GPUSkinningTextureMatrix";
        texture.filterMode = FilterMode.Point;
        texture.LoadRawTextureData(textureRawData.bytes);
        texture.Apply(false, true);

        return texture;
    }
    
    public static Texture2D CreateBindTexture2D(TextAsset textureRawData, GPUSkinningAnimation anim)
    {
        if (textureRawData == null || anim == null)
        {
            return null;
        }

        Texture2D texture = new Texture2D(anim.bindTextureWidth, anim.bindTextureHeight, TextureFormat.RGBAHalf, false, true);
        texture.name = "GPUSkinningBindTextureMatrix";
        texture.filterMode = FilterMode.Point;
        texture.LoadRawTextureData(textureRawData.bytes);
        texture.Apply(false, true);

        return texture;
    }

    public static string BonesHierarchyTree(GPUSkinningAnimation gpuSkinningAnimation)
    {
        if (gpuSkinningAnimation == null || gpuSkinningAnimation.bones == null)
        {
            return null;
        }

        string str = string.Empty;
        BonesHierarchy_Internal(gpuSkinningAnimation, gpuSkinningAnimation.bones[gpuSkinningAnimation.rootBoneIndex],
            string.Empty, ref str);
        return str;
    }

    public static void BonesHierarchy_Internal(GPUSkinningAnimation gpuSkinningAnimation, GPUSkinningBone bone,
        string tabs, ref string str)
    {
        str += tabs + bone.name + "\n";

        int numChildren = bone.childrenBonesIndices == null ? 0 : bone.childrenBonesIndices.Length;
        for (int i = 0; i < numChildren; ++i)
        {
            BonesHierarchy_Internal(gpuSkinningAnimation, gpuSkinningAnimation.bones[bone.childrenBonesIndices[i]],
                tabs + "    ", ref str);
        }
    }

    /// <summary>
    /// 更具索引获取骨骼在Hierarchy中的路径
    /// </summary>
    /// <param name="bones">骨骼数组</param>
    /// <param name="boneIndex">所需节点的索引</param>
    /// <returns></returns>
    public static string BoneHierarchyPath(GPUSkinningBone[] bones, int boneIndex)
    {
        if (bones == null || boneIndex < 0 || boneIndex >= bones.Length)
        {
            return null;
        }

        GPUSkinningBone bone = bones[boneIndex];
        string path = bone.name;
        while (bone.parentBoneIndex != -1)
        {
            bone = bones[bone.parentBoneIndex];
            path = bone.name + "/" + path;
        }

        return path;
    }

    public static string BoneHierarchyPath(GPUSkinningAnimation gpuSkinningAnimation, int boneIndex)
    {
        if (gpuSkinningAnimation == null)
        {
            return null;
        }

        return BoneHierarchyPath(gpuSkinningAnimation.bones, boneIndex);
    }

    /// <summary>
    /// 生成MD5码作为GUID
    /// </summary>
    public static string MD5(string input)
    {
        MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
        byte[] bytValue, bytHash;
        bytValue = System.Text.Encoding.UTF8.GetBytes(input);
        bytHash = md5.ComputeHash(bytValue);
        md5.Clear();
        string sTemp = string.Empty;
        for (int i = 0; i < bytHash.Length; i++)
        {
            sTemp += bytHash[i].ToString("X").PadLeft(2, '0');
        }

        return sTemp.ToLower();
    }

    public static int NormalizeTimeToFrameIndex(GPUSkinningClip clip, float normalizedTime)
    {
        if (clip == null)
        {
            return 0;
        }

        normalizedTime = Mathf.Clamp01(normalizedTime);
        return (int)(normalizedTime * (clip.length * clip.fps - 1));
    }

    public static float FrameIndexToNormalizedTime(GPUSkinningClip clip, int frameIndex)
    {
        if (clip == null)
        {
            return 0;
        }

        int totalFrams = (int)(clip.fps * clip.length);
        frameIndex = Mathf.Clamp(frameIndex, 0, totalFrams - 1);
        return (float)frameIndex / (float)(totalFrams - 1);
    }

    // public static Quaternion ToQuaternion(Matrix4x4 mat)
    // {
    //     float det = mat.determinant;
    //     if (!CompareApproximately(det, 1.0F, .005f))
    //         return Quaternion.identity;
    //
    //     Quaternion quat = Quaternion.identity;
    //     float tr = mat.m00 + mat.m11 + mat.m22;
    //
    //     // check the diagonal
    //     if (tr > 0.0f)
    //     {
    //         float fRoot = Mathf.Sqrt(tr + 1.0f); // 2w
    //         quat.w = 0.5f * fRoot;
    //         fRoot = 0.5f / fRoot; // 1/(4w)
    //         quat.x = (mat[2, 1] - mat[1, 2]) * fRoot;
    //         quat.y = (mat[0, 2] - mat[2, 0]) * fRoot;
    //         quat.z = (mat[1, 0] - mat[0, 1]) * fRoot;
    //     }
    //     else
    //     {
    //         // |w| <= 1/2
    //         int[] s_iNext = { 1, 2, 0 };
    //         int i = 0;
    //         if (mat.m11 > mat.m00)
    //             i = 1;
    //         if (mat.m22 > mat[i, i])
    //             i = 2;
    //         int j = s_iNext[i];
    //         int k = s_iNext[j];
    //
    //         float fRoot = Mathf.Sqrt(mat[i, i] - mat[j, j] - mat[k, k] + 1.0f);
    //         if (fRoot < float.Epsilon)
    //             return Quaternion.identity;
    //
    //         quat[i] = 0.5f * fRoot;
    //         fRoot = 0.5f / fRoot;
    //         quat.w = (mat[k, j] - mat[j, k]) * fRoot;
    //         quat[j] = (mat[j, i] + mat[i, j]) * fRoot;
    //         quat[k] = (mat[k, i] + mat[i, k]) * fRoot;
    //     }
    //
    //     return QuaternionNormalize(quat);
    // }

    public static Quaternion ToQuaternion(Matrix4x4 mat)
    {
        return QuaternionNormalize(Quaternion.LookRotation(mat.GetColumn(2), mat.GetColumn(1)));
    }

    // 归一化
    static Vector4 scaleCache = Vector4.zero;
    static Quaternion quatCache = Quaternion.identity;
    public static Quaternion QuaternionNormalize(Quaternion quat)
    {
        scaleCache.x = quat.x;
        scaleCache.y = quat.y;
        scaleCache.z = quat.z;
        scaleCache.w = quat.w;
        
        float scale = scaleCache.magnitude;
        scale = 1.0f / scale;

        quatCache.x = scale * quat.x;
        quatCache.y = scale * quat.y;
        quatCache.z = scale * quat.z;
        quatCache.w = scale * quat.w;

        return quatCache;
    }
    
    public static bool CompareApproximately(float f0, float f1, float epsilon = 0.000001F)
    {
        float dist = (f0 - f1);
        dist = Mathf.Abs(dist);
        return dist < epsilon;
    }

    private static Vector3 uniformScale = new Vector3();
    public static Matrix4x4 DualQuaternionToMatrix(Quaternion quaternion, Vector4 pos)
    {
        uniformScale.x = pos.w;
        uniformScale.y = pos.w;
        uniformScale.z = pos.w;
        Matrix4x4 matrix = Matrix4x4.TRS(pos, quaternion, uniformScale);
        return matrix;
    }

    public static Vector3 GetPositionFromMatrix(Matrix4x4 matrix4X4)
    {
        Vector3 pos = matrix4X4.GetColumn(3);
        return pos;
    }
    
    public static Quaternion GetQuaternionFromMatrix(Matrix4x4 matrix4X4)
    {
        Quaternion rotation = Quaternion.LookRotation(matrix4X4.GetColumn(2), matrix4X4.GetColumn(1));
        return rotation;
    }
    
    public static float GetScaleFromMatrix(Matrix4x4 matrix4X4)
    {
        float scale = Vector3.Magnitude(matrix4X4.GetColumn(0));
        return scale;
    }
    
    
}