using UnityEngine;
using System.Collections;

/// <summary>
/// 单个动画片段对应的配置数据
/// </summary>
[System.Serializable]
public class GPUSkinningClip
{
    public string name = null;

    public float length = 0.0f;

    public int fps = 0; // 帧率 

    public GPUSkinningWrapMode wrapMode = GPUSkinningWrapMode.Once;

    public GPUSkinningFrame[] frames = null; // 骨骼矩阵数据

    public int pixelSegmentation = 0; // 每个动画在纹素索引中的间隔

    public bool rootMotionEnabled = false; 

    public bool individualDifferenceEnabled = false; // 个体差异，错帧播放

    public GPUSkinningAnimEvent[] events = null; 
}
