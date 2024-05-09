using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 动画播放器
/// </summary>
public class GPUSkinningPlayer
{
    public delegate void OnAnimEvent(GPUSkinningPlayer player, int eventId);

    private GameObject go = null;

    private Transform transform = null;

    private MeshRenderer mr = null;

    private MeshFilter mf = null;

    private float time = 0; // 当前动画播放到的时间（单次动画）

    private float timeDiff = 0; // 随机动画时间，用以错帧播放

    private float crossFadeTime = -1;

    private float crossFadeProgress = 0; // 动画融合已进行的时间

    private float lastPlayedTime = 0; // 上一个动画的播放时间（会在Update中更新）

    private GPUSkinningClip lastPlayedClip = null; // 上一个动画

    private int lastPlayingFrameIndex = -1; // 上一帧的动画的索引

    private GPUSkinningClip lastPlayingClip = null; // 上一帧播的动画

    private GPUSkinningClip playingClip = null; 

    private GPUSkinningPlayerResources res = null;

    private MaterialPropertyBlock mpb = null;

    private int rootMotionFrameIndex = -1;

    public event OnAnimEvent onAnimEvent;

    /// <summary>
    /// 当前动画播放器的Mono脚本是否开启RootMotion
    /// </summary>
    private bool rootMotionEnabled = false;
    public bool RootMotionEnabled
    {
        get
        {
            return rootMotionEnabled;
        }
        set
        {
            rootMotionFrameIndex = -1;
            rootMotionEnabled = value;
        }
    }

    private GPUSKinningCullingMode cullingMode = GPUSKinningCullingMode.CullUpdateTransforms;
    public GPUSKinningCullingMode CullingMode
    {
        get
        {
            return Application.isPlaying ? cullingMode : GPUSKinningCullingMode.AlwaysAnimate;
        }
        set
        {
            cullingMode = value;
        }
    }

    // 动画的可见性
    private bool visible = false;
    public bool Visible
    {
        get
        {
            return Application.isPlaying ? visible : true;
        }
        set
        {
            visible = value;
        }
    }

    private bool lodEnabled = true;
    public bool LODEnabled
    {
        get
        {
            return lodEnabled;
        }
        set
        {
            lodEnabled = value;
            res.LODSettingChanged(this);
        }
    }

    // 标记动画状态，由外部接口影响
    private bool isPlaying = false;
    public bool IsPlaying
    {
        get
        {
            return isPlaying;
        }
    }

    public string PlayingClipName
    {
        get
        {
            return playingClip == null ? null : playingClip.name;
        }
    }
    
    public Vector3 Position
    {
        get
        {
            return transform == null ? Vector3.zero : transform.position;
        }
    }

    public Vector3 LocalPosition
    {
        get
        {
            return transform == null ? Vector3.zero : transform.localPosition;
        }
    }

    private List<GPUSkinningPlayerJoint> joints = null;
    public List<GPUSkinningPlayerJoint> Joints
    {
        get
        {
            return joints;
        }
    }

    public GPUSkinningWrapMode WrapMode
    {
        get
        {
            return playingClip == null ? GPUSkinningWrapMode.Once : playingClip.wrapMode;
        }
    }

    // 是否是当前动画的帧尾
    public bool IsTimeAtTheEndOfLoop
    {
        get
        {
            if(playingClip == null)
            {
                return false;
            }
            else
            {
                return GetFrameIndex() == ((int)(playingClip.length * playingClip.fps) - 1);
            }
        }
    }

    /// <summary>
    /// 标准播放时间（0 - 1）
    /// </summary>
    public float NormalizedTime
    {
        get
        {
            if(playingClip == null)
            {
                return 0;
            }
            else
            {
                return GetFrameIndex() / (float)((int)(playingClip.length * playingClip.fps) - 1);
            }
        }
        set
        {
            if(playingClip != null)
            {
                float v = Mathf.Clamp01(value);
                if(WrapMode == GPUSkinningWrapMode.Once)
                {
                    this.time = v * playingClip.length;
                }
                else if(WrapMode == GPUSkinningWrapMode.Loop)
                {
                    if(playingClip.individualDifferenceEnabled)
                    {
                        res.Time = playingClip.length +  v * playingClip.length - this.timeDiff;
                    }
                    else
                    {
                        res.Time = v * playingClip.length;
                    }
                }
                else
                {
                    throw new System.NotImplementedException();
                }
            }
        }
    }

    public GPUSkinningPlayer(GameObject attachToThisGo, GPUSkinningPlayerResources res)
    {
        go = attachToThisGo;
        transform = go.transform;
        this.res = res;

        mr = go.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = go.AddComponent<MeshRenderer>();
        }
        mf = go.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = go.AddComponent<MeshFilter>();
        }

        GPUSkinningMaterial mtrl = GetCurrentMaterial();
        mr.sharedMaterial = mtrl == null ? null : mtrl.material;
        mf.sharedMesh = res.mesh;

        mpb = new MaterialPropertyBlock();

        ConstructJoints();
    }

    #region 外部接口

    /// <summary>
    /// 播放动画
    /// </summary>
    public void Play(string clipName)
    {
        GPUSkinningClip[] clips = res.anim.clips;
        int numClips = clips == null ? 0 : clips.Length;
        for(int i = 0; i < numClips; ++i)
        {
            if(clips[i].name == clipName)
            {
                if (playingClip != clips[i] || 
                    (playingClip != null && playingClip.wrapMode == GPUSkinningWrapMode.Once && IsTimeAtTheEndOfLoop) || 
                    (playingClip != null && !isPlaying))
                {
                    // 当前没有正在播的动画、动画播到帧尾
                    SetNewPlayingClip(clips[i]);
                }
                return;
            }
        }
    }

    /// <summary>
    /// 融合动画
    /// </summary>
    /// <param name="clipName">新的动画</param>
    /// <param name="fadeLength">淡入淡出的时间</param>
    public void CrossFade(string clipName, float fadeLength)
    {
        if (playingClip == null)
        {
            Play(clipName);
        }
        else
        {
            GPUSkinningClip[] clips = res.anim.clips;
            int numClips = clips == null ? 0 : clips.Length;
            for (int i = 0; i < numClips; ++i)
            {
                if (clips[i].name == clipName)
                {
                    if (playingClip != clips[i])
                    {
                        // 新旧动画不一致，需要融合
                        crossFadeProgress = 0;
                        crossFadeTime = fadeLength;
                        SetNewPlayingClip(clips[i]);
                        return;
                    }
                    
                    if ((playingClip != null && playingClip.wrapMode == GPUSkinningWrapMode.Once && IsTimeAtTheEndOfLoop) ||
                        (playingClip != null && !isPlaying))
                    {
                        // 当前没有正在播的动画、动画播到帧尾
                        SetNewPlayingClip(clips[i]);
                        return;
                    }
                }
            }
        }
    }
    
    public void Stop()
    {
        isPlaying = false;
    }

    public void Resume()
    {
        if(playingClip != null)
        {
            isPlaying = true;
        }
    }

    // 设置MeshFilter的Mesh
    public void SetLODMesh(Mesh mesh)
    {
        if(!LODEnabled)
        {
            mesh = res.mesh;
        }

        if(mf != null && mf.sharedMesh != mesh)
        {
            mf.sharedMesh = mesh;
        }
    }

#if UNITY_EDITOR
    public void Update_Editor(float timeDelta)
    {
        Update_Internal(timeDelta);
    }
#endif

    public void Update(float timeDelta)
    {
        Update_Internal(timeDelta);
    }

    #endregion
    
    private void FillEvents(GPUSkinningClip clip, GPUSkinningBetterList<GPUSkinningAnimEvent> events)
    {
        events.Clear();
        if(clip != null && clip.events != null && clip.events.Length > 0)
        {
            events.AddRange(clip.events);
        }
    }

    /// <summary>
    /// 设置新的动画片段
    /// </summary>
    private void SetNewPlayingClip(GPUSkinningClip clip)
    {
        lastPlayedClip = playingClip;
        lastPlayedTime = GetCurrentTime();

        isPlaying = true;
        playingClip = clip;
        rootMotionFrameIndex = -1;
        time = 0;
        res.Time = 0;
        timeDiff = Random.Range(0, playingClip.length);
    }

    /// <summary>
    /// 动画的更新逻辑，主要更新运动矩阵材质
    /// </summary>
    private void Update_Internal(float timeDelta)
    {
        if (!isPlaying || playingClip == null)
        {
            return;
        }
        GPUSkinningMaterial currMtrl = GetCurrentMaterial();
        if(currMtrl == null)
        {
            return;    
        }

        if(mr.sharedMaterial != currMtrl.material)
        {
            mr.sharedMaterial = currMtrl.material;
        }
        
        if (playingClip.wrapMode == GPUSkinningWrapMode.Loop)
        {
            UpdateMaterial(timeDelta, currMtrl);
        }
        else if(playingClip.wrapMode == GPUSkinningWrapMode.Once)
        {
            if (time >= playingClip.length)
            {
                // 单次播放，播放结束保持在尾帧
                time = playingClip.length;
                UpdateMaterial(timeDelta, currMtrl);
            }
            else
            {
                UpdateMaterial(timeDelta, currMtrl);
                time += timeDelta; // 时间的积累是在更新材质之后，可以保证在资源材质前，动画时间和上一帧一致，从而可以正确驱动 res.Update(deltaTime, currMtrl)
                if(time > playingClip.length)
                {
                    time = playingClip.length;
                }
            }
        }
        else
        {
            throw new System.NotImplementedException();
        }

        crossFadeProgress += timeDelta;
        lastPlayedTime += timeDelta;
    }
    
    // 更新贴图
    private void UpdateMaterial(float deltaTime, GPUSkinningMaterial currMtrl)
    {
        // 检测是否有变化需要更新
        int frameIndex = GetFrameIndex();
        float interpolationFactor = GetInterpolationFactor();
        if(lastPlayingClip == playingClip && frameIndex == lastPlayingFrameIndex)
        {
            res.Update(deltaTime, currMtrl, interpolationFactor);
            return;
        }
        lastPlayingClip = playingClip;
        lastPlayingFrameIndex = frameIndex;
        
        int nextFrameIndex = GetNextFrameIndex();
        GPUSkinningFrame nextFrame = playingClip.frames[nextFrameIndex]; // 下一帧数据
        GPUSkinningFrame frame = playingClip.frames[frameIndex]; // 当前帧数据
        if (Visible || CullingMode == GPUSKinningCullingMode.AlwaysAnimate)
        {
            res.Update(deltaTime, currMtrl, interpolationFactor);
            // 设置融合属性
            res.UpdatePlayingData(
                mpb, playingClip, frameIndex, frame, playingClip.rootMotionEnabled && rootMotionEnabled,
                lastPlayedClip, GetCrossFadeFrameIndex(), crossFadeTime, crossFadeProgress
            );
            mr.SetPropertyBlock(mpb); // 应用属性到shader中

            if (res.IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
            {
                int frameIndexCrossFade = GetCrossFadeFrameIndex();
                int nextIndexCrossFade = GetNextCrossFadeFrameIndex();
                GPUSkinningFrame frameCrossFade = lastPlayedClip.frames[frameIndexCrossFade];
                GPUSkinningFrame nextFrameCrossFade = lastPlayedClip.frames[nextIndexCrossFade];
                float crossFadeBlendFactor = res.CrossFadeBlendFactor(crossFadeProgress, crossFadeTime);
                UpdateJointsCrossFade(frameCrossFade, nextFrameCrossFade, frame, nextFrame, interpolationFactor, crossFadeBlendFactor);
                
            }
            else
            {
                UpdateJoints(frame, nextFrame, interpolationFactor);
            }
        }
        
        // 动画融合，主要用以计算RootMotion，骨骼动画的融合在res.UpdatePlayingData内部判定
        float blend_crossFade = 1;
        int frameIndex_crossFade = -1;
        GPUSkinningFrame frame_crossFade = null;
        if (res.IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
        {
            frameIndex_crossFade = GetCrossFadeFrameIndex(); // 融合帧
            frame_crossFade = lastPlayedClip.frames[frameIndex_crossFade]; // 融合帧的数据
            blend_crossFade = res.CrossFadeBlendFactor(crossFadeProgress, crossFadeTime); // 融合比率
        }
        
        // RootMotion
        if (playingClip.rootMotionEnabled && rootMotionEnabled && frameIndex != rootMotionFrameIndex)
        {
            if (CullingMode != GPUSKinningCullingMode.CullCompletely)
            {
                rootMotionFrameIndex = frameIndex;
                DoRootMotion(frame_crossFade, 1 - blend_crossFade, false);
                DoRootMotion(frame, blend_crossFade, true);
            }
        }

        // 动画事件
        UpdateEvents(playingClip, frameIndex, frame_crossFade == null ? null : lastPlayedClip, frameIndex_crossFade);
    }

    private void UpdateEvents(GPUSkinningClip playingClip, int playingFrameIndex, GPUSkinningClip corssFadeClip, int crossFadeFrameIndex)
    {
        UpdateClipEvent(playingClip, playingFrameIndex);
        UpdateClipEvent(corssFadeClip, crossFadeFrameIndex);
    }

    private void UpdateClipEvent(GPUSkinningClip clip, int frameIndex)
    {
        if(clip == null || clip.events == null || clip.events.Length == 0)
        {
            return;
        }

        GPUSkinningAnimEvent[] events = clip.events;
        int numEvents = events.Length;
        for(int i = 0; i < numEvents; ++i)
        {
            if(events[i].frameIndex == frameIndex && onAnimEvent != null)
            {
                onAnimEvent(this, events[i].eventId);
                break;
            }
        }
    }
    
    /// <summary>
    /// 获取运动贴图
    /// </summary>
    private GPUSkinningMaterial GetCurrentMaterial()
    {
        if(res == null)
        {
            return null;
        }

        if(playingClip == null)
        {
            // 默认RootOff_BlendOff状态
            return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOff_BlendOff);
        }
        // rootMotion 需要脚本和动画资源都支持
        if(playingClip.rootMotionEnabled && rootMotionEnabled)
        {
            if(res.IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
            {
                if(lastPlayedClip.rootMotionEnabled)
                {
                    // 当前动画片段和Mono的RootMotion都开启，可以动画融合，旧动画片段的RootMotion开启
                    return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOn_BlendOn_CrossFadeRootOn);
                }
                // 当前动画片段和Mono的RootMotion都开启，可以动画融合，旧动画片段的RootMotion关闭
                return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOn_BlendOn_CrossFadeRootOff);
            }
            // 当前动画片段和Mono的RootMotion都开启，不可以动画融合
            return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOn_BlendOff);
        }
        if(res.IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
        {
            if (lastPlayedClip.rootMotionEnabled)
            {
                // 当前动画片段和Mono的RootMotion都关闭，可以动画融合，旧动画片段的RootMotion开启
                return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOff_BlendOn_CrossFadeRootOn);
            }
            // 当前动画片段和Mono的RootMotion都关闭，可以动画融合，旧动画片段的RootMotion关闭
            return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOff_BlendOn_CrossFadeRootOff);
        }
        else
        {
            // 当前动画片段和Mono的RootMotion都关闭，不可以动画融合
            return res.GetMaterial(GPUSkinningPlayerResources.MaterialState.RootOff_BlendOff);
        }
    }

    // 根节点运动
    private void DoRootMotion(GPUSkinningFrame frame, float blend, bool doRotate)
    {
        if(frame == null)
        {
            return;
        }
        
        Quaternion deltaRotation = frame.rootMotionDeltaPositionQ;
        Vector3 newForward = deltaRotation * transform.forward;
        Vector3 deltaPosition = newForward * frame.rootMotionDeltaPositionL * blend;
        transform.Translate(deltaPosition, Space.World);

        if (doRotate)
        {
            transform.rotation *= frame.rootMotionDeltaRotation;
        }
    }

    #region 动画帧相关

    // 动画当前播放的时间，如果是单次动画播放结束会返回动画的总时长，如果是循环动画，返回播放的总时间（不受循环次数的影响）
    private float GetCurrentTime()
    {
        float time = 0;
        if (WrapMode == GPUSkinningWrapMode.Once)
        {
            time = this.time; 
        }
        else if (WrapMode == GPUSkinningWrapMode.Loop)
        {
            time = res.Time + (playingClip.individualDifferenceEnabled ? this.timeDiff : 0); // 资源实际播放到的时间
        }
        else
        {
            throw new System.NotImplementedException();
        }
        return time;
    }
    
    // 获取当前动画的帧索引
    private int GetFrameIndex()
    {
        float time = GetCurrentTime();
        if (Mathf.Approximately(playingClip.length, time))
        {
            return GetTheLastFrameIndex_WrapMode_Once(playingClip);
        }
        else
        {
            return GetFrameIndex_WrapMode_Loop(playingClip, time);
        }
    }

    private int GetNextFrameIndex()
    {
        if (playingClip == null)
        {
            return -1;
        }

        int frameIndex = GetFrameIndex();
        if (frameIndex != (int)(playingClip.length * playingClip.fps) - 1)
        {
            frameIndex = frameIndex + 1;
        }
        return frameIndex;
    }

    // 获取融合的帧索引（来自上一个动画，会随时间变化）
    private int GetCrossFadeFrameIndex()
    {
        if (lastPlayedClip == null)
        {
            return 0;
        }

        if (lastPlayedClip.wrapMode == GPUSkinningWrapMode.Once)
        {
            if (lastPlayedTime >= lastPlayedClip.length)
            {
                // 单次播放，动画播完，返回最后一帧
                return GetTheLastFrameIndex_WrapMode_Once(lastPlayedClip);
            }
            else
            {
                // 单次播放，动画未播完，返回当前帧
                return GetFrameIndex_WrapMode_Loop(lastPlayedClip, lastPlayedTime);
            }
        }
        else if (lastPlayedClip.wrapMode == GPUSkinningWrapMode.Loop)
        {
            // 循环播放，返回当前帧
            return GetFrameIndex_WrapMode_Loop(lastPlayedClip, lastPlayedTime);
        }
        else
        {
            throw new System.NotImplementedException();
        }
    }

    private int GetNextCrossFadeFrameIndex()
    {
        if (lastPlayedClip == null)
        {
            return -1;
        }
        
        int frameIndex = GetCrossFadeFrameIndex();
        if (frameIndex != (int)(lastPlayedClip.length * lastPlayedClip.fps) - 1)
        {
            frameIndex = frameIndex + 1;
        }
        return frameIndex;
    }

    /// <summary>
    /// 获取clip的最后一帧的索引
    /// </summary>
    /// <returns></returns>
    private int GetTheLastFrameIndex_WrapMode_Once(GPUSkinningClip clip)
    {
        return (int)(clip.length * clip.fps) - 1; // 数组的索引从0开始
    }

    /// <summary>
    /// 获取clip在time时间点对应的帧索引，如果是循环动画是会取余
    /// </summary>
    private int GetFrameIndex_WrapMode_Loop(GPUSkinningClip clip, float time)
    {
        return (int)(time * clip.fps) % (int)(clip.length * clip.fps);
    }

    /// <summary>
    /// 获取插帧融合因子
    /// </summary>
    /// <returns></returns>
    private float GetInterpolationFactor()
    {
        float time = GetCurrentTime();
        float interpolationFactor = 0.0f;
        if (Mathf.Approximately(playingClip.length,time))
        {
            // 尾帧不需要插帧
            return interpolationFactor;
        }
        else
        {
            if (IsFrameTail(time))
            {
                return interpolationFactor;
            }

            interpolationFactor = (time * playingClip.fps) % (playingClip.length * playingClip.fps);
            return interpolationFactor - (int)interpolationFactor;
        }
    }

    // 是否是尾帧
    private bool IsFrameTail(float time)
    {
        if (playingClip == null)
        {
            return false;
        }

        int frameIndex = GetFrameIndex_WrapMode_Loop(playingClip, time);
        int frameTailIndex = (int)(playingClip.length * playingClip.fps) - 1;
        if (frameIndex == frameTailIndex)
        {
            return true;
        }
        return false;
    }

    #endregion

    #region 挂点相关

    private Vector4 posCache = new Vector4();
    private Vector4 posCacheNext = new Vector4();
    private Vector3 scaleCache = new Vector3();
    // 更新挂点位置，间隔帧融合
    private void UpdateJoints(GPUSkinningFrame frame, GPUSkinningFrame nextFrame, float interpolationFactor)
    {
        if(joints == null)
        {
            return;
        }

        Matrix4x4[] matrices = frame.matrices;
        Matrix4x4[] nextMatrices = nextFrame.matrices;
        int numJoints = joints.Count;
        for(int i = 0; i < numJoints; ++i)
        {
            GPUSkinningPlayerJoint joint = joints[i];
            if (joint == null)
            {
                return;
            }

            Transform jointTransform = Application.isPlaying ? joint.Transform : joint.transform;
            if (jointTransform != null)
            {
                Matrix4x4 jointMatrix = matrices[joint.BoneIndex];
                Matrix4x4 nextJointMatrix = nextMatrices[joint.BoneIndex];
                
                // TODO 处理RootMotion的插帧
                if(playingClip.rootMotionEnabled && rootMotionEnabled)
                {
                    jointMatrix = frame.RootMotionInv(res.anim.rootBoneIndex, res.anim.bones[res.anim.rootBoneIndex].bindpose) * jointMatrix;
                }
                // MultiplyVector：只关心方向和缩放，MultiplyPoint：适用于完整的变换
                // 对于骨骼本身而言，并不需要考虑绑定矩阵
                Quaternion rotation = GPUSkinningUtil.ToQuaternion(jointMatrix); // 提取旋转相关的4元数
                Vector3 scale = jointMatrix.lossyScale;
                var pos = jointMatrix.GetColumn(3);
                posCache.x = pos.x;
                posCache.y = pos.y;
                posCache.z = pos.z;
                posCache.w = Mathf.Clamp01(scale.x); // 只考虑同轴缩放
                
                Quaternion rotationNext = GPUSkinningUtil.ToQuaternion(nextJointMatrix); // 提取旋转相关的4元数
                Vector3 scaleNext = nextJointMatrix.lossyScale;
                var posNext = nextJointMatrix.GetColumn(3);
                posCacheNext.x = posNext.x;
                posCacheNext.y = posNext.y;
                posCacheNext.z = posNext.z;
                posCacheNext.w = Mathf.Clamp01(scaleNext.x);
                
                pos = Vector4.Lerp(posCache, posCacheNext, interpolationFactor);
                rotation = Quaternion.Slerp(rotation, rotationNext, interpolationFactor);

                jointTransform.localPosition = pos;
                jointTransform.localRotation = rotation;
                scaleCache.x = pos.w;
                scaleCache.y = pos.w;
                scaleCache.z = pos.w;
                jointTransform.localScale = scaleCache;
                // jointTransform.localPosition = jointMatrix.MultiplyPoint(Vector3.zero); // 对坐标原点进行变换，来设定关节点在父关节点中的位置坐标
                // Vector3 jointDir = jointMatrix.MultiplyVector(Vector3.right); // 确定关节X轴方向
                // Quaternion jointRotation = Quaternion.FromToRotation(Vector3.right, jointDir); // 计算旋转四元数
                // jointTransform.localRotation = jointRotation; // 设置关节点的旋转
            }
            else
            {
                joints.RemoveAt(i);
                --i;
                --numJoints;
            }
        }
    }

    private Vector4 crossPosCache = new Vector4();
    private Vector4 nextCrossPosCache = new Vector4();
    private void UpdateJointsCrossFade(GPUSkinningFrame frameCrossFade, GPUSkinningFrame nextFrameCrossFade,  GPUSkinningFrame frame, GPUSkinningFrame nextFrame, float interpolationFactor, float crossFadeBlendFactorGPUSkinningFrame)
    {
        if(joints == null)
        {
            return;
        }

        Matrix4x4[] matrices = frame.matrices;
        Matrix4x4[] nextMatrices = nextFrame.matrices;
        Matrix4x4[] crossMatrices = frameCrossFade.matrices;
        Matrix4x4[] nextCrossMatrices= nextFrameCrossFade.matrices;
        int numJoints = joints.Count;
        for(int i = 0; i < numJoints; ++i)
        {
            GPUSkinningPlayerJoint joint = joints[i];
            Transform jointTransform = Application.isPlaying ? joint.Transform : joint.transform;
            if (jointTransform != null)
            {
                Matrix4x4 jointMatrix = matrices[joint.BoneIndex];
                Matrix4x4 nextJointMatrix = nextMatrices[joint.BoneIndex];
                Matrix4x4 crossJointMatrix = crossMatrices[joint.BoneIndex];
                Matrix4x4 nextCrossJointMatrix = nextCrossMatrices[joint.BoneIndex];
                
                // TODO 处理RootMotion的插帧
                if(playingClip.rootMotionEnabled && rootMotionEnabled)
                {
                    jointMatrix = frame.RootMotionInv(res.anim.rootBoneIndex, res.anim.bones[res.anim.rootBoneIndex].bindpose) * jointMatrix;
                }
                // MultiplyVector：只关心方向和缩放，MultiplyPoint：适用于完整的变换
                // 对于骨骼本身而言，并不需要考虑绑定矩阵
                Quaternion rotation = GPUSkinningUtil.ToQuaternion(jointMatrix); // 提取旋转相关的4元数
                Vector3 scale = jointMatrix.lossyScale;
                var pos = jointMatrix.GetColumn(3);
                posCache.x = pos.x;
                posCache.y = pos.y;
                posCache.z = pos.z;
                posCache.w = Mathf.Clamp01(scale.x); // 只考虑同轴缩放
                
                Quaternion rotationNext = GPUSkinningUtil.ToQuaternion(nextJointMatrix); // 提取旋转相关的4元数
                Vector3 scaleNext = nextJointMatrix.lossyScale;
                var posNext = nextJointMatrix.GetColumn(3);
                posCacheNext.x = posNext.x;
                posCacheNext.y = posNext.y;
                posCacheNext.z = posNext.z;
                posCacheNext.w = Mathf.Clamp01(scaleNext.x);
                pos = Vector4.Lerp(posCache, posCacheNext, interpolationFactor);
                rotation = Quaternion.Slerp(rotation, rotationNext, interpolationFactor);
                
                Quaternion crossRotation = GPUSkinningUtil.ToQuaternion(crossJointMatrix); // 提取旋转相关的4元数
                Vector3 crossScale = crossJointMatrix.lossyScale;
                var crossPos = crossJointMatrix.GetColumn(3);
                crossPosCache.x = crossPos.x;
                crossPosCache.y = crossPos.y;
                crossPosCache.z = crossPos.z;
                crossPosCache.w = Mathf.Clamp01(crossScale.x); // 只考虑同轴缩放
                
                Quaternion nextCrossRotation = GPUSkinningUtil.ToQuaternion(nextCrossJointMatrix); // 提取旋转相关的4元数
                Vector3 nextCrossScale = nextCrossJointMatrix.lossyScale;
                var nextCrossPos = nextCrossJointMatrix.GetColumn(3);
                nextCrossPosCache.x = nextCrossPos.x;
                nextCrossPosCache.y = nextCrossPos.y;
                nextCrossPosCache.z = nextCrossPos.z;
                nextCrossPosCache.w = Mathf.Clamp01(nextCrossScale.x);
                crossPos = Vector4.Lerp(crossPosCache, nextCrossPosCache, interpolationFactor);
                crossRotation = Quaternion.Slerp(crossRotation, nextCrossRotation, interpolationFactor);

                pos = Vector4.Lerp(crossPos, pos, crossFadeBlendFactorGPUSkinningFrame);
                rotation = Quaternion.Slerp(crossRotation, rotation, crossFadeBlendFactorGPUSkinningFrame);
                
                jointTransform.localPosition = pos;
                jointTransform.localRotation = rotation;
                scaleCache.x = pos.w;
                scaleCache.y = pos.w;
                scaleCache.z = pos.w;
                jointTransform.localScale = scaleCache;
            }
            else
            {
                joints.RemoveAt(i);
                --i;
                --numJoints;
            }
        }
    }

    // 创建节点
    private void ConstructJoints()
    {
        if (joints == null)
        {
            // 原插件通过GetComponentsInChildren来找挂点的方式不适用于挂点的循环嵌套的情况
            //GPUSkinningPlayerJoint[] existingJoints = go.GetComponentsInChildren<GPUSkinningPlayerJoint>();
            GPUSkinningPlayerJoint[] existingJoints;
            List<GPUSkinningPlayerJoint> existingJointsList = new List<GPUSkinningPlayerJoint>();
            int childCount = go.transform.childCount;
            if (childCount > 0)
            {
                for (int i = 0; i < childCount; i++)
                {
                    GPUSkinningPlayerJoint gpuSkinningPlayerJoint;
                    if (go.transform.GetChild(i).TryGetComponent(out gpuSkinningPlayerJoint))
                    {
                        existingJointsList.Add(gpuSkinningPlayerJoint);
                    }
                }
            }
            existingJoints = existingJointsList.ToArray();
            
            // 遍历骨骼数据
            GPUSkinningBone[] bones = res.anim.bones;
            int numBones = bones == null ? 0 : bones.Length;
            for (int i = 0; i < numBones; ++i)
            {
                GPUSkinningBone bone = bones[i];
                // 找到需要导出的骨骼节点
                if (bone.isExposed)
                {
                    if (joints == null)
                    {
                        joints = new List<GPUSkinningPlayerJoint>();
                    }

                    // 如果当前节点已将存在，则更新信息
                    bool inTheExistingJoints = false;
                    if (existingJoints != null)
                    {
                        for (int j = 0; j < existingJoints.Length; ++j)
                        {
                            if(existingJoints[j] != null && existingJoints[j].BoneGUID == bone.guid)
                            {
                                if (existingJoints[j].BoneIndex != i)
                                {
                                    existingJoints[j].Init(i, bone.guid);
                                    GPUSkinningUtil.MarkAllScenesDirty();
                                }
                                joints.Add(existingJoints[j]);
                                existingJoints[j] = null;
                                inTheExistingJoints = true;
                                break;
                            }
                        }
                    }

                    // 挂点不存在就创建挂点
                    if(!inTheExistingJoints)
                    {
                        GameObject jointGo = new GameObject(bone.name);
                        jointGo.transform.parent = go.transform;
                        jointGo.transform.localPosition = Vector3.zero;
                        jointGo.transform.localScale = Vector3.one;

                        GPUSkinningPlayerJoint joint = jointGo.AddComponent<GPUSkinningPlayerJoint>();
                        joints.Add(joint);
                        joint.Init(i, bone.guid);
                        GPUSkinningUtil.MarkAllScenesDirty();
                    }
                }
            }

            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.CallbackFunction DelayCall = null;
                DelayCall = () => 
                {
                    UnityEditor.EditorApplication.delayCall -= DelayCall;
                    DeleteInvalidJoints(existingJoints);
                };
                UnityEditor.EditorApplication.delayCall += DelayCall;
#endif
            }
            else
            {
                // 运行时，删除无效的节点
                DeleteInvalidJoints(existingJoints);
            }
        }
    }

    //删除无效挂点
    private void DeleteInvalidJoints(GPUSkinningPlayerJoint[] joints)
    {
        if (joints != null)
        {
            for (int i = 0; i < joints.Length; ++i)
            {
                if (joints[i] != null)
                {
                    for (int j = 0; j < joints[i].transform.childCount; ++j)
                    {
                        Transform child = joints[i].transform.GetChild(j);
                        child.parent = go.transform;
                        child.localPosition = Vector3.zero;
                    }
                    Object.DestroyImmediate(joints[i].transform.gameObject);
                    GPUSkinningUtil.MarkAllScenesDirty();
                }
            }
        }
    }
    
    #endregion
}
