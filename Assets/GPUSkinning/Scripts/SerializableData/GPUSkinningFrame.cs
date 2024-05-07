using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// 采样的骨骼矩阵数据
/// </summary>
[System.Serializable]
public class GPUSkinningFrame
{
    public Matrix4x4[] matrices = null;
    // 前方与位移方向的旋转变化
    public Quaternion rootMotionDeltaPositionQ;
    // 相对于上一帧的位移距离
    public float rootMotionDeltaPositionL;
    // 相对于上一帧的旋转变化
    public Quaternion rootMotionDeltaRotation;
    
    [System.NonSerialized]
    private bool rootMotionInvInit = false;
    [System.NonSerialized]
    private Matrix4x4 rootMotionInv;
    
    /// <summary>
    /// 根骨骼变换的逆矩阵
    /// </summary>
    /// <returns></returns>
    public Matrix4x4 RootMotionInv(int rootBoneIndex, Matrix4x4 bindPose)
    {
        if (!rootMotionInvInit)
        {
            rootMotionInv = matrices[rootBoneIndex] * bindPose;
            rootMotionInv = rootMotionInv.inverse;
            rootMotionInvInit = true;
        }
        return rootMotionInv;
    }
}
