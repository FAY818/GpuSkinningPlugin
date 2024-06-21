using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GPUSkinningPlayerMono : MonoBehaviour
{

    #region property

    [HideInInspector]
    [SerializeField]
    private GPUSkinningAnimation anim = null;

    [HideInInspector]
    [SerializeField]
    private Mesh mesh = null;

    [HideInInspector]
    [SerializeField]
    private Material mtrl = null;

    [HideInInspector]
    [SerializeField]
    private TextAsset textureRawData = null;
    
    [HideInInspector]
    [SerializeField]
    private TextAsset textureBindRawData = null;

    [HideInInspector]
    [SerializeField]
    private int defaultPlayingClipIndex = 0;

    [HideInInspector]
    [SerializeField]
    private bool rootMotionEnabled = false;

    [HideInInspector]
    [SerializeField]
    private bool lodEnabled = false;

    [HideInInspector]
    [SerializeField]
    private GPUSKinningCullingMode cullingMode = GPUSKinningCullingMode.CullUpdateTransforms;

    private static GPUSkinningPlayerMonoManager playerManager = new GPUSkinningPlayerMonoManager(); // 播放管理器属于类，而不是对象

    private GPUSkinningPlayer player = null;
    public GPUSkinningPlayer Player
    {
        get
        {
            return player;
        }
    }


    #endregion

    #region Lifecycle

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
#if UNITY_EDITOR
        Update_Editor(0); 
#endif
    }

    private void Update()
    {
        // 更新播放器时间
        if (player != null)
        {
#if UNITY_EDITOR
            if(Application.isPlaying)
            {
                player.Update(Time.deltaTime);
            }
            else
            {
                player.Update_Editor(0);
            }
#else
            player.Update(Time.deltaTime);
#endif
        }
    }

    private void OnDestroy()
    {
        player = null;
        anim = null;
        mesh = null;
        mtrl = null;
        textureRawData = null;
        textureBindRawData = null;

        if (Application.isPlaying)
        {
            playerManager.Unregister(this);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Resources.UnloadUnusedAssets();
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
        }
#endif
    }

    #endregion
    
    /// <summary>
    /// 指定资源的初始化
    /// </summary>
    public void Init(GPUSkinningAnimation anim, Mesh mesh, Material mtrl, TextAsset textureRawData, TextAsset textureBindRawData)
    {
        if(player != null)
        {
            return;
        }

        this.anim = anim;
        this.mesh = mesh;
        this.mtrl = mtrl;
        this.textureRawData = textureRawData;
        this.textureBindRawData = textureBindRawData;
        Init();
    }

    // 利用面板引用资源的初始化
    public void Init()
    {
        if(player != null)
        {
            return;
        }

        if (anim != null && mesh != null && mtrl != null && textureRawData != null && textureBindRawData != null)
        {
            GPUSkinningPlayerResources res = null; // 运行依赖资源
            
            if (Application.isPlaying) // 运行模式
            {
                playerManager.Register(anim, mesh, mtrl, textureRawData, textureBindRawData, this, out res);
            }
            else // 编辑器模式
            {
                res = new GPUSkinningPlayerResources();
                res.anim = anim;
                res.mesh = mesh;
                res.InitMaterial(mtrl, HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor);
                res.texture = GPUSkinningUtil.CreateTexture2D(textureRawData, anim);
                res.textureBind = GPUSkinningUtil.CreateBindTexture2D(textureBindRawData, anim);
                res.texture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }

            // 创建播放器
            player = new GPUSkinningPlayer(gameObject, res);
            player.RootMotionEnabled = Application.isPlaying ? rootMotionEnabled : false;
            player.LODEnabled = Application.isPlaying ? lodEnabled : false;
            player.CullingMode = cullingMode;

            // 播放默认动画
            if (anim != null && anim.clips != null && anim.clips.Length > 0)
            {
                player.Play(anim.clips[Mathf.Clamp(defaultPlayingClipIndex, 0, anim.clips.Length)].name);
            }
        }
    }

#if UNITY_EDITOR
    public void DeletePlayer()
    {
        player = null;
    }

    // 编辑器模式中更新
    public void Update_Editor(float deltaTime)
    {
        if(player != null && !Application.isPlaying)
        {
            player.Update_Editor(deltaTime);
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Init();
            Update_Editor(0);
        }
    }
#endif
}
