using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 用以在运行时，标记某帧是否被处理过
/// </summary>
public class GPUSkinningExecuteOncePerFrame
{
    private int frameCount = -1;

    public bool CanBeExecute()
    {
        if (Application.isPlaying)
        {
            return frameCount != Time.frameCount;
        }
        else
        {
            return true;
        }
    }

    public void MarkAsExecuted()
    {
        if (Application.isPlaying)
        {
            frameCount = Time.frameCount;
        }
    }
}
