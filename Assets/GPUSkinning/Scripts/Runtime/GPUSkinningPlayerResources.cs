using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理在运行时依赖的本地资源
/// </summary>
public class GPUSkinningPlayerResources
{
    /// <summary>
    /// 动画贴图的状态，对应贴图的关键词，只有开启动画融合才需要考虑Root融合
    /// </summary>
    public enum MaterialState
    {
        RootOn_BlendOff = 0,             // 开启Root运动，关闭动画融合
        RootOn_BlendOn_CrossFadeRootOn,  // 开启Root运动，开启动画融合，开启Root融合
        RootOn_BlendOn_CrossFadeRootOff, // 开启Root运动，开启动画融合，关闭Root融合
        RootOff_BlendOff,                // 关闭Root运动，关闭动画融合
        RootOff_BlendOn_CrossFadeRootOn, // 关闭Root运动，开启动画融合，开启Root融合
        RootOff_BlendOn_CrossFadeRootOff,// 关闭Root运动，开启动画融合，关闭Root融合
        Count = 6
    }

    // shader中定义的关键词，可以影响渲染状态
    private static string[] keywords = new string[] 
    {
        "ROOTON_BLENDOFF", 
        "ROOTON_BLENDON_CROSSFADEROOTON",
        "ROOTON_BLENDON_CROSSFADEROOTOFF",
        "ROOTOFF_BLENDOFF", 
        "ROOTOFF_BLENDON_CROSSFADEROOTON", 
        "ROOTOFF_BLENDON_CROSSFADEROOTOFF" 
    };
    
    public GPUSkinningAnimation anim = null;

    public Mesh mesh = null;

    public Texture2D texture = null;

    public Texture textureBind = null;

    public List<GPUSkinningPlayerMono> players = new List<GPUSkinningPlayerMono>(); // 播放器列表

    private CullingGroup cullingGroup = null;

    private GPUSkinningBetterList<BoundingSphere> cullingBounds = new GPUSkinningBetterList<BoundingSphere>(100);

    private GPUSkinningMaterial[] mtrls = null; // 动画贴图数组
    
    // 帧标记
    private GPUSkinningExecuteOncePerFrame executeOncePerFrame = new GPUSkinningExecuteOncePerFrame();

    // 动画正播放到的时间
    private float time = 0;
    public float Time
    {
        get
        {
            return time;
        }
        set
        {
            time = value;
        }
    }

    // 设置矩阵贴图
    private static int shaderPropID_GPUSkinning_TextureMatrix = -1;
    // 设置矩阵绑定贴图
    private static int shaderPropID_GPUSkinning_TextureBindMatrix = -1;
    // 贴图的尺寸信息
    private static int shaderPropID_GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor = 0;
    // 矩阵绑定贴图的尺寸信息
    private static int shaderPropID_GPUSkinning_BindTextureSize = 0;
    // 设置采样帧率和动画间的纹素间隔
    private static int shaderPorpID_GPUSkinning_FrameIndex_PixelSegmentation = 0;
    // 当前RootMotion的BindPose的逆矩阵
    private static int shaderPropID_GPUSkinning_RootMotion = 0;
    // 融合的采样帧率和像素间隔以及融合比率
    private static int shaderPorpID_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade = 0;
    // 融合RootMotion的BindPose的逆矩阵
    private static int shaderPropID_GPUSkinning_RootMotion_CrossFade = 0;

    public GPUSkinningPlayerResources()
    {
        if (shaderPropID_GPUSkinning_TextureMatrix == -1)
        {
            shaderPropID_GPUSkinning_TextureBindMatrix = Shader.PropertyToID("_GPUSkinning_TextureBindMatrix");
            shaderPropID_GPUSkinning_TextureMatrix = Shader.PropertyToID("_GPUSkinning_TextureMatrix");
            shaderPropID_GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor = Shader.PropertyToID("_GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor");
            shaderPropID_GPUSkinning_BindTextureSize = Shader.PropertyToID("_GPUSkinning_BindTextureSize");
            shaderPorpID_GPUSkinning_FrameIndex_PixelSegmentation = Shader.PropertyToID("_GPUSkinning_FrameIndex_PixelSegmentation");
            shaderPropID_GPUSkinning_RootMotion = Shader.PropertyToID("_GPUSkinning_RootMotion");
            shaderPorpID_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade = Shader.PropertyToID("_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade");
            shaderPropID_GPUSkinning_RootMotion_CrossFade = Shader.PropertyToID("_GPUSkinning_RootMotion_CrossFade");
        }
    }

    ~GPUSkinningPlayerResources()
    {
        DestroyCullingGroup();
    }

    public void Destroy()
    {
        anim = null;
        mesh = null;

        if(cullingBounds != null)
        {
            cullingBounds.Release();
            cullingBounds = null;
        }

        DestroyCullingGroup();

        if(mtrls != null)
        {
            for(int i = 0; i < mtrls.Length; ++i)
            {
                mtrls[i].Destroy();
                mtrls[i] = null;
            }
            mtrls = null;
        }

        if (texture != null)
        {
            Object.DestroyImmediate(texture);
            texture = null;
        }
        
        if (textureBind != null)
        {
            Object.DestroyImmediate(textureBind);
            textureBind = null;
        }

        if (players != null)
        {
            players.Clear();
            players = null;
        }
    }

    public void AddCullingBounds()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.targetCamera = Camera.main;
            cullingGroup.SetBoundingDistances(anim.lodDistances);
            cullingGroup.SetDistanceReferencePoint(Camera.main.transform);
            cullingGroup.onStateChanged = OnLodCullingGroupOnStateChangedHandler;
        }

        cullingBounds.Add(new BoundingSphere());
        cullingGroup.SetBoundingSpheres(cullingBounds.buffer);
        cullingGroup.SetBoundingSphereCount(players.Count);
    }

    public void RemoveCullingBounds(int index)
    {
        cullingBounds.RemoveAt(index);
        cullingGroup.SetBoundingSpheres(cullingBounds.buffer);
        cullingGroup.SetBoundingSphereCount(players.Count);
    }

    public void LODSettingChanged(GPUSkinningPlayer player)
    {
        if(player.LODEnabled)
        {
            int numPlayers = players.Count;
            for(int i = 0; i < numPlayers; ++i)
            {
                if(players[i].Player == player)
                {
                    int distanceIndex = cullingGroup.GetDistance(i);
                    SetLODMeshByDistanceIndex(distanceIndex, players[i].Player);
                    break;
                }
            }
        }
        else
        {
            player.SetLODMesh(null);
        }
    }

    private void OnLodCullingGroupOnStateChangedHandler(CullingGroupEvent evt)
    {
        GPUSkinningPlayerMono player = players[evt.index];
        if(evt.isVisible)
        {
            SetLODMeshByDistanceIndex(evt.currentDistance, player.Player);
            player.Player.Visible = true;
        }
        else
        {
            player.Player.Visible = false;
        }
    }

    private void DestroyCullingGroup()
    {
        if (cullingGroup != null)
        {
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }

    private void SetLODMeshByDistanceIndex(int index, GPUSkinningPlayer player)
    {
        Mesh lodMesh = null;
        if (index == 0)
        {
            lodMesh = this.mesh;
        }
        else
        {
            Mesh[] lodMeshes = anim.lodMeshes;
            lodMesh = lodMeshes == null || lodMeshes.Length == 0 ? this.mesh : lodMeshes[Mathf.Min(index - 1, lodMeshes.Length - 1)];
            if (lodMesh == null) lodMesh = this.mesh;
        }
        player.SetLODMesh(lodMesh);
    }

    private void UpdateCullingBounds()
    {
        int numPlayers = players.Count;
        for (int i = 0; i < numPlayers; ++i)
        {
            GPUSkinningPlayerMono player = players[i];
            BoundingSphere bounds = cullingBounds[i];
            bounds.position = player.Player.Position;
            bounds.radius = anim.sphereRadius;
            cullingBounds[i] = bounds;
        }
    }

    // 更新贴图的信息，传递给Shader
    public void Update(float deltaTime, GPUSkinningMaterial mtrl, float interpolationFactor)
    {
        // 这里需要传递帧融合因子
        if (executeOncePerFrame.CanBeExecute())
        {
            executeOncePerFrame.MarkAsExecuted();
            time += deltaTime;
            UpdateCullingBounds();
        }

        if (mtrl.executeOncePerFrame.CanBeExecute())
        {
            mtrl.executeOncePerFrame.MarkAsExecuted();
            mtrl.material.SetTexture(shaderPropID_GPUSkinning_TextureMatrix, texture);
            mtrl.material.SetTexture(shaderPropID_GPUSkinning_TextureBindMatrix, textureBind);
            mtrl.material.SetVector(shaderPropID_GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor, 
                new Vector4(anim.textureWidth, anim.textureHeight, anim.skinningBoneNum * 2, interpolationFactor));
            mtrl.material.SetVector(shaderPropID_GPUSkinning_BindTextureSize, new Vector4(anim.bindTextureWidth, anim.bindTextureHeight));
            
        }
    }

    /// <summary>
    /// 更新动画材质，属性
    /// </summary>
    /// <param name="mpb">材质属性</param>
    /// <param name="playingClip">正在播放的动画片段</param>
    /// <param name="frameIndex">当前动画的帧索引</param>
    /// <param name="frame">当前帧数据</param>
    /// <param name="rootMotionEnabled">是否开启RootMotion</param>
    /// <param name="lastPlayedClip">上一个动画片段</param>
    /// <param name="frameIndex_crossFade">上一个动画开始融合的帧</param>
    /// <param name="crossFadeTime">融合时间</param>
    /// <param name="crossFadeProgress">融合进度</param>
    public void UpdatePlayingData(
        MaterialPropertyBlock mpb, 
        GPUSkinningClip playingClip, 
        int frameIndex, 
        GPUSkinningFrame frame, 
        bool rootMotionEnabled,
        GPUSkinningClip lastPlayedClip, 
        int frameIndex_crossFade, 
        float crossFadeTime, 
        float crossFadeProgress)
    {
        mpb.SetVector(shaderPorpID_GPUSkinning_FrameIndex_PixelSegmentation, new Vector4(frameIndex, playingClip.pixelSegmentation, 0, 0));
        // 根运动
        if (rootMotionEnabled)
        {
            Matrix4x4 rootMotionInv = frame.RootMotionInv(anim.rootBoneIndex, anim.bones[anim.rootBoneIndex].bindpose);
            mpb.SetMatrix(shaderPropID_GPUSkinning_RootMotion, rootMotionInv);
        }

        // 存在融合
        if (IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
        {
            if (lastPlayedClip.rootMotionEnabled)
            {
                // 融合动画帧的root的逆矩阵
                mpb.SetMatrix(shaderPropID_GPUSkinning_RootMotion_CrossFade, lastPlayedClip.frames[frameIndex_crossFade].RootMotionInv(anim.rootBoneIndex, anim.bones[anim.rootBoneIndex].bindpose));
            }
            mpb.SetVector(shaderPorpID_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade,
                new Vector4(frameIndex_crossFade, lastPlayedClip.pixelSegmentation, GetBlendFactor(crossFadeProgress, crossFadeTime))); //TODO 融合帧的插帧因子
            
            //Debug.LogFormat("上一个动画帧:{0}，下一个动画帧:{1}, 融合为因子：{2}", frameIndex_crossFade, frameIndex, CrossFadeBlendFactor(crossFadeProgress, crossFadeTime));
        }
    }

    public float GetBlendFactor(float crossFadeProgress, float crossFadeTime)
    {
        return BezierBlendFactor(crossFadeProgress, crossFadeTime);
    }
    
    private float LinearBlendFactor(float crossFadeProgress, float crossFadeTime)
    {
        return Mathf.Clamp01(crossFadeProgress / crossFadeTime);
    }

    private float BezierBlendFactor(float crossFadeProgress, float crossFadeTime)
    {
        float bStart = 0f; // 开始时的融合因子
        float bEnd = 1f; // 结束时的融合因子
        
        float t = Mathf.Clamp01(crossFadeProgress / crossFadeTime);
        float oneMinusT = 1f - t;
        float a = oneMinusT * oneMinusT * oneMinusT;
        float b = 3 * oneMinusT * oneMinusT * t;
        float c = 3 * oneMinusT * t * t;
        float d = t * t * t;
        // 这里将Bezier的切线都视为混合因子bStart和bEnd
        return (a + b) * bStart + (c + d) * bEnd;
    }


    /// <summary>
    /// 判定是否可以动画融合
    /// </summary>
    public bool IsCrossFadeBlending(GPUSkinningClip lastPlayedClip, float crossFadeTime, float crossFadeProgress)
    {
        return lastPlayedClip != null && crossFadeTime > 0 && crossFadeProgress <= crossFadeTime;
    }

    /// <summary>
    /// 根据贴图的状态（关键词）获取动画贴图
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public GPUSkinningMaterial GetMaterial(MaterialState state)
    {
        return mtrls[(int)state];
    }

    /// <summary>
    /// 初始化动画贴图
    /// </summary>
    /// <param name="originalMaterial">导出的原始贴图</param>
    /// <param name="hideFlags"></param>
    public void InitMaterial(Material originalMaterial, HideFlags hideFlags)
    {
        if(mtrls != null)
        {
            return;
        }

        mtrls = new GPUSkinningMaterial[(int)MaterialState.Count];

        for (int i = 0; i < mtrls.Length; ++i)
        {
            mtrls[i] = new GPUSkinningMaterial() { material = new Material(originalMaterial) };
            mtrls[i].material.name = keywords[i];
            mtrls[i].material.hideFlags = hideFlags;
            mtrls[i].material.enableInstancing = true; // enable instancing in Unity 5.6
            EnableKeywords(i, mtrls[i]);
        }
    }

    /// <summary>
    /// 激活对应动画贴图的关键词
    /// </summary>
    /// <param name="ki"></param>
    /// <param name="mtrl"></param>
    private void EnableKeywords(int ki, GPUSkinningMaterial mtrl)
    {
        for(int i = 0; i < mtrls.Length; ++i)
        {
            if(i == ki)
            {
                mtrl.material.EnableKeyword(keywords[i]);
            }
            else
            {
                mtrl.material.DisableKeyword(keywords[i]);
            }
        }
    }
}
