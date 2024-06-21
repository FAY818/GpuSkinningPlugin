using UnityEngine;
using System.Collections;

/// <summary>
/// 动画的配置文件
/// </summary>
public class GPUSkinningAnimation : ScriptableObject
{
    public string guid = null;

    public string name = null;

    public bool createMountPoint = false;

    public GPUSkinningAnimType gpuSkinningAnimType = GPUSkinningAnimType.Skeleton;
    
    public GPUSkinningBone[] bones = null;

    public int skinningBoneNum = 0;

    public int skinningVertexNum = 0;
    
    [HideInInspector]
    public GPUSkinningBone[] skinningBones = null; 
    
    public int rootBoneIndex = 0;

    public GPUSkinningClip[] clips = null;

    public Bounds bounds;

    public int textureWidth = 0;

    public int textureHeight = 0;
    
    public int bindTextureWidth = 0;

    public int bindTextureHeight = 0;

    public float[] lodDistances = null;

    public Mesh[] lodMeshes = null;

    public float sphereRadius = 1.0f;
}
