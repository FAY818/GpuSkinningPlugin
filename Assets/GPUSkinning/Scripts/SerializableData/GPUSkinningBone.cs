using UnityEngine;
using System.Collections;
using UnityEngine.XR;

/// <summary>
/// 骨骼数据信息
/// </summary>
[System.Serializable]
public class GPUSkinningBone
{
	[System.NonSerialized]
	public Transform transform = null;

	public Matrix4x4 bindpose; // T-Pose绑定矩阵
	
	public int parentBoneIndex = -1; // 父节点索引

	public int[] childrenBonesIndices = null; // 子节点数组，保存子节点的索引

	[System.NonSerialized]
	public Matrix4x4 animationMatrix;

	public string name = null;

    public string guid = null; 

    public bool isExposed = false; // 决定骨骼节点是否导出

    public bool isSkinningBone = false; // 是否是参与蒙皮的骨骼

    // 逆矩阵 从骨骼空间到模型空间
    [System.NonSerialized]
    private bool bindposeInvInit = false;
    [System.NonSerialized]
    private Matrix4x4 bindposeInv;
    public Matrix4x4 BindposeInv
    {
        get
        {
            if(!bindposeInvInit)
            {
                bindposeInv = bindpose.inverse;
                bindposeInvInit = true;
            }
            return bindposeInv;
        }
    }
    
}
