using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
#if UNITY_EDITOR
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEditor;
#endif

/// <summary>
/// GPUSkinning 动画采样器
/// </summary>
[ExecuteInEditMode]
public class GPUSkinningSampler : MonoBehaviour 
{
	
#if UNITY_EDITOR
	#region Properties
	[HideInInspector]
    [SerializeField]
	public string animName = null;

    [HideInInspector]
    [System.NonSerialized]
	public AnimationClip animClip = null; // 当前正在采样的动画片段

    [HideInInspector]
    [SerializeField]
    public AnimationClip[] animClips = null; // 待采样的动画片段集合

    [HideInInspector]
    [SerializeField]
    public GPUSkinningWrapMode[] wrapModes = null; // Inspector中暴露的设置选项

    [HideInInspector]
    [SerializeField]
    public int[] fpsList = null;// Inspector中暴露的设置选项

    [HideInInspector]
    [SerializeField]
    public bool[] rootMotionEnabled = null;// Inspector中暴露的设置选项

    [HideInInspector]
    [SerializeField]
    public bool[] individualDifferenceEnabled = null;// Inspector中暴露的设置选项

    [HideInInspector]
    [SerializeField]
    public Mesh[] lodMeshes = null;

    [HideInInspector]
    [SerializeField]
    public float[] lodDistances = null;

    [HideInInspector]
    [SerializeField]
    private float sphereRadius = 1.0f;

    [HideInInspector]
    [SerializeField]
    public bool createNewShader = false;

    [HideInInspector]
    [System.NonSerialized]
    public int samplingClipIndex = -1;

    [HideInInspector]
    [SerializeField]
    public TextAsset texture = null;
    
    [HideInInspector]
    [SerializeField]
    public TextAsset textureBind = null;

    [HideInInspector]
    [SerializeField]
	public GPUSkinningQuality skinQuality = GPUSkinningQuality.Bone2;

    [HideInInspector]
    [SerializeField]
	public Transform rootBoneTransform = null; // 此关节点是骨骼和挂点的Root，只收集从此点开始的位移信息；

    [HideInInspector]
    [SerializeField]
    public GPUSkinningAnimation anim = null; // 持久化保存的动画的配置文件

    [HideInInspector]
    [SerializeField]
	public GPUSkinningShaderType shaderType = GPUSkinningShaderType.Unlit;

	[HideInInspector]
	[System.NonSerialized]
	public bool isSampling = false;

    [HideInInspector]
    [SerializeField]
    public Mesh savedMesh = null; // 导出的Mesh

    [HideInInspector]
    [SerializeField]
    public Material savedMtrl = null; // 导出的材质

    [HideInInspector]
    [SerializeField]
    public Shader savedShader = null;

    [HideInInspector]
    [SerializeField]
    public bool updateOrNew = true;

    private Animation animation = null;

	private Animator animator = null;
    private RuntimeAnimatorController runtimeAnimatorController = null; // 当前采样的RuntimeAnimatorController 

	private SkinnedMeshRenderer smr = null;

	private GPUSkinningAnimation gpuSkinningAnimation = null; //  此脚本运行时使用的配置文件

    private GPUSkinningClip gpuSkinningClip = null; // 当前动画片段的采集数据

    private Vector3 rootMotionPosition;

    private Quaternion rootMotionRotation;

    [HideInInspector]
	[System.NonSerialized]
	public int samplingTotalFrams = 0; // 当前动画片段总采样帧数

	[HideInInspector]
	[System.NonSerialized]
	public int samplingFrameIndex = 0; // 当前采样帧索引

    // 采样文件保存路径
	public const string TEMP_SAVED_ANIM_PATH = "GPUSkinning_Temp_Save_Anim_Path";
	public const string TEMP_SAVED_MTRL_PATH = "GPUSkinning_Temp_Save_Mtrl_Path";
	public const string TEMP_SAVED_MESH_PATH = "GPUSkinning_Temp_Save_Mesh_Path";
    public const string TEMP_SAVED_SHADER_PATH = "GPUSkinning_Temp_Save_Shader_Path";
    public const string TEMP_SAVED_TEXTURE_PATH = "GPUSkinning_Temp_Save_Texture_Path";
    public const string TEMP_SAVED_TEXTUREBIND_PATH = "GPUSkinning_Temp_Save_TextureBind_Path";

	#endregion
	
	#region Mono

    private void Awake()
	{
        // 确定动画机制是Animation/Animator
        animation = GetComponentInChildren<Animation>();
		animator = GetComponentInChildren<Animator>();
        if (animator == null && animation == null)
        {
            DestroyImmediate(this);
            ShowDialog("Cannot find Animator Or Animation Component");
            return;
        }
        if(animator != null && animation != null)
        {
            DestroyImmediate(this);
            ShowDialog("Animation is not coexisting with Animator");
            return;
        }
        if (animator != null)
        {
            if (animator.runtimeAnimatorController == null)
            {
                DestroyImmediate(this);
                ShowDialog("Missing RuntimeAnimatorController");
                return;
            }
            if (animator.runtimeAnimatorController is AnimatorOverrideController)
            {
                DestroyImmediate(this);
                ShowDialog("RuntimeAnimatorController could not be a AnimatorOverrideController");
                return;
            }
            runtimeAnimatorController = animator.runtimeAnimatorController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 对象处于摄像机视锥体之外，或者处于非活动状态，持续更新动画状态并计算动画
            //InitTransform();
            return;
        }
        if(animation != null)
        {
            MappingAnimationClips();
            animation.Stop();
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            //InitTransform();
            return;
        }
	}
    
	private void Update()
	{
		if(!isSampling)
		{
			return;
		}

        int totalFrams = (int)(gpuSkinningClip.length * gpuSkinningClip.fps);
		samplingTotalFrams = totalFrams;

		// 这里每个Clip采样结束都会调用
        if (samplingFrameIndex >= totalFrams)
        {
	        if(animator != null)
            {
                animator.StopPlayback();
            }

            string savePath = null;
            if (anim == null)
            {
                savePath = EditorUtility.SaveFolderPanel("GPUSkinning Sampler Save", GetUserPreferDir(), animName);
            }
            else
            {
                string animPath = AssetDatabase.GetAssetPath(anim);
                savePath = new FileInfo(animPath).Directory.FullName.Replace('\\', '/');
            }

			if(!string.IsNullOrEmpty(savePath))
			{
				if(!savePath.Contains(Application.dataPath.Replace('\\', '/')))
				{
					ShowDialog("Must select a directory in the project's Asset folder.");
				}
				else
				{
					SaveUserPreferDir(savePath);

					string dir = "Assets" + savePath.Substring(Application.dataPath.Length);

					string savedAnimPath = dir + "/GPUSKinning_Anim_" + animName + ".asset";
                    SetSthAboutTexture(gpuSkinningAnimation);
                    SetBindBoneTexture(gpuSkinningAnimation);
                    EditorUtility.SetDirty(gpuSkinningAnimation);
                    if (anim != gpuSkinningAnimation)
                    {
                        AssetDatabase.CreateAsset(gpuSkinningAnimation, savedAnimPath);
                    }
                    WriteTempData(TEMP_SAVED_ANIM_PATH, savedAnimPath);
                    
                    
                    CreateTextureMatrix(dir, gpuSkinningAnimation); // 骨骼矩阵贴图
                    
                    if (samplingClipIndex == 0)
                    {
                        Mesh newMesh = CreateNewMesh(smr.sharedMesh, "GPUSkinning_Mesh");
                        if (savedMesh != null)
                        {
                            newMesh.bounds = savedMesh.bounds;
                        }
                        string savedMeshPath = dir + "/GPUSKinning_Mesh_" + animName + ".asset";
                        AssetDatabase.CreateAsset(newMesh, savedMeshPath);
                        WriteTempData(TEMP_SAVED_MESH_PATH, savedMeshPath);
                        savedMesh = newMesh;

                        CreateShaderAndMaterial(dir);

                        CreateLODMeshes(newMesh.bounds, dir);

                        string prefabFileName = "GPUSKinning_Prefab_" + animName + ".prefab";
                        string dataFileName = "GPUSKinning_Anim_" + animName + ".asset";
                        string meshFileName = "GPUSKinning_Mesh_" + animName + ".asset";
                        string mtrlFileName = "GPUSKinning_Material_" + animName + ".mat";
                        string textureRawDataFileName = "GPUSKinning_Texture_" + animName + ".bytes";
                        string textureRawBindDataFileName = "GPUSKinning_TextureBind_" + animName + ".bytes";
                        CreatePrefab(dir, prefabFileName, dataFileName, meshFileName, mtrlFileName, textureRawDataFileName, textureRawBindDataFileName);
                    }
                    ClearSkinningBones();
                    anim = gpuSkinningAnimation;
                    AssetDatabase.Refresh();
					AssetDatabase.SaveAssets();
				}
			}
			isSampling = false;
            return;
        }
        // 准备一个GPUSkinningFrame对象用以本帧的数据采样
        float time = gpuSkinningClip.length * ((float)samplingFrameIndex / totalFrams); // 当前采样动画播放时间
        GPUSkinningFrame frame = new GPUSkinningFrame();
        gpuSkinningClip.frames[samplingFrameIndex] = frame;
        frame.matrices = new Matrix4x4[gpuSkinningAnimation.bones.Length];
        if (animation == null)
        {
	        // 采样animator
	        animator.playbackTime = time; // 设置采样时间
            animator.Update(0); // 刷新animator，0代表没有时间流逝
        }
        else
        {
	        // 采样animation
            animation.Stop();
            AnimationState animState = animation[animClip.name];
            if(animState != null)
            {
	            animation.clip = animState.clip; // 只有animation中的clip在播放时才会改变模型
                animState.time = time; //采样将动画状态设置到指定时间
                animation.Sample(); // 采样，将动画直接设置到当前时间
                animation.Play();
            }
        }
        
        StartCoroutine(SamplingCoroutine(frame, totalFrams));
    }

	/// <summary>
	/// 创建预制体
	/// </summary>
	private void CreatePrefab(string savePath, string prefabFileName, string dataFileName, string meshFileName, string mtrlFileName, string textureRawDataFileName, string textureRawBindDataFileName)
	{
		GameObject prefab = new GameObject(prefabFileName);
		MeshFilter meshFilter = prefab.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = savedMesh;
		MeshRenderer meshRenderer = prefab.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = savedMtrl;

		GPUSkinningPlayerMono gpuSkinningPlayerMono = prefab.AddComponent<GPUSkinningPlayerMono>();
		GPUSkinningAnimation anim = AssetDatabase.LoadAssetAtPath<GPUSkinningAnimation>(Path.Combine(savePath, dataFileName));
		Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Path.Combine(savePath, meshFileName));;
		Material mtrl = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(savePath, mtrlFileName));;
		TextAsset textureRawData = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.Combine(savePath, textureRawDataFileName));
		TextAsset textureBindRawData = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.Combine(savePath, textureRawBindDataFileName));

		
		gpuSkinningPlayerMono.Init(anim, mesh, mtrl, textureRawData, textureBindRawData);
		string prefabPath = Path.Combine(savePath, prefabFileName);
		PrefabUtility.CreatePrefab(prefabPath, prefab);
		GameObject.DestroyImmediate(prefab);
	}
	
	#endregion
	
	#region Sample

    public void BeginSample()
    {
	    samplingClipIndex = 0;
    }

    public void EndSample()
    {
	    samplingClipIndex = -1;
    }
    
    // 每一个动画采集完毕会自动进入下一个动画的采集
	public void StartSample()
	{
        // 采样前的检测
        if (isSampling)
        {
            return;
        }

        if (string.IsNullOrEmpty(animName.Trim()))
        {
            ShowDialog("Animation name is empty.");
            return;
        }

        if (rootBoneTransform == null)
        {
            ShowDialog("Please set Root Bone.");
            return;
        }

        if (animClips == null || animClips.Length == 0)
        {
            ShowDialog("Please set Anim Clips.");
            return;
        }

        animClip = animClips[samplingClipIndex];
        if (animClip == null)
		{
            isSampling = false;
			return;
		}

        int numFrames = (int)(GetClipFPS(animClip, samplingClipIndex) * animClip.length);
        if(numFrames == 0)
        {
            isSampling = false;
            return;
        }
        smr = GetComponentInChildren<SkinnedMeshRenderer>(); // 从这里看出此插件只支持处理单个SkinnedMeshRenderer
		if(smr == null)
		{
			ShowDialog("Cannot find SkinnedMeshRenderer.");
			return;
		}
		if(smr.sharedMesh == null)
		{
			ShowDialog("Cannot find SkinnedMeshRenderer.mesh.");
			return;
		}

		Mesh mesh = smr.sharedMesh;
		if(mesh == null)
		{
			ShowDialog("Missing Mesh");
			return;
		}

		samplingFrameIndex = 0;
		// 配置文件优先读取本地
		gpuSkinningAnimation = anim == null ? ScriptableObject.CreateInstance<GPUSkinningAnimation>() : anim;
		gpuSkinningAnimation.name = animName;

        if(anim == null)
        {
            gpuSkinningAnimation.guid = System.Guid.NewGuid().ToString(); // 创建全局唯一标准       
        }

        // 骨骼相关信息，每次开始采样前都重新赋值新的GPUSkinningBone数据对象
		List<GPUSkinningBone> bones_result = new List<GPUSkinningBone>();
		CollectBones(bones_result, smr.bones, mesh.bindposes, null, rootBoneTransform, 0);
        GPUSkinningBone[] newBones = bones_result.ToArray();
        GenerateBonesGUID(newBones);
        if (anim != null) 
	        RestoreCustomBoneData(anim.bones, newBones); // 骨骼暴露导出信息
        gpuSkinningAnimation.bones = newBones;
        gpuSkinningAnimation.skinningBones = CollectSkinningBones(newBones, smr.bones);
        gpuSkinningAnimation.skinningBoneNum = gpuSkinningAnimation.skinningBones.Length;
        gpuSkinningAnimation.rootBoneIndex = 0;

        // 动画片段相关
        // 当前采样动画在配置表中的索引（非-1代表覆盖索引，即当前动画在配置表中已经采样过一次）
        int numClips = gpuSkinningAnimation.clips == null ? 0 : gpuSkinningAnimation.clips.Length;
        int overrideClipIndex = -1; 
        for (int i = 0; i < numClips; ++i)
        {
            if (gpuSkinningAnimation.clips[i].name == animClip.name)
            {
                overrideClipIndex = i;
                break;
            }
        }
        
        gpuSkinningClip = new GPUSkinningClip();
        gpuSkinningClip.name = animClip.name;
        gpuSkinningClip.fps = GetClipFPS(animClip, samplingClipIndex);
        gpuSkinningClip.length = animClip.length;
        gpuSkinningClip.wrapMode = wrapModes[samplingClipIndex];
        gpuSkinningClip.frames = new GPUSkinningFrame[numFrames];
        gpuSkinningClip.rootMotionEnabled = rootMotionEnabled[samplingClipIndex];
        gpuSkinningClip.individualDifferenceEnabled = individualDifferenceEnabled[samplingClipIndex];
        // 尚未采样
        if(gpuSkinningAnimation.clips == null)
        {
            gpuSkinningAnimation.clips = new GPUSkinningClip[] { gpuSkinningClip };
        }
        else
        {
            // 新的采样数据
            if (overrideClipIndex == -1)
            {
                List<GPUSkinningClip> clips = new List<GPUSkinningClip>(gpuSkinningAnimation.clips);
                clips.Add(gpuSkinningClip);
                gpuSkinningAnimation.clips = clips.ToArray();
            }
            // 覆盖原有的采样数据
            else
            {
                GPUSkinningClip overridedClip = gpuSkinningAnimation.clips[overrideClipIndex]; // 原来的
                RestoreCustomClipData(overridedClip, gpuSkinningClip); // 将事件复制到新的
                gpuSkinningAnimation.clips[overrideClipIndex] = gpuSkinningClip; // 更新原来的
            }
        }

        // 与Animator相关
        SetCurrentAnimationClip(); // 更新动画片段
        PrepareRecordAnimator(); // 录制新的动画片段以支持回放

        isSampling = true;
    }
	
	/// <summary>
	/// Animator 开始采样前的准备，将动画录制一遍，设置回放模式
	/// </summary>
	private void PrepareRecordAnimator()
	{
		if (animator != null)
		{
			int numFrames = (int)(gpuSkinningClip.fps * gpuSkinningClip.length);

			animator.applyRootMotion = gpuSkinningClip.rootMotionEnabled;
			animator.Rebind(); // 动画状态修改后重绑定
			animator.recorderStartTime = 0;
			// 录制动画
			animator.StartRecording(numFrames);
			for (int i = 0; i < numFrames; ++i)
			{
				animator.Update(1.0f / gpuSkinningClip.fps);
			}
			animator.StopRecording();
			//开始回放，Playback模式的动画不走游戏逻辑，方便设置时间进度
			animator.StartPlayback(); 
		}
	}

    public bool IsSamplingProgress()
    {
	    return samplingClipIndex != -1;
    }

    public bool IsAnimatorOrAnimation()
    {
	    return animator != null || animation != null ; 
    }
    
    /// <summary>
    /// 映射inspector面板中的AnimationClip信息，自动
    /// </summary>
    public void MappingAnimationClips()
    {
	    if(animation == null)
	    {
		    return;
	    }
	    
	    List<AnimationClip> newClips = null;
	    AnimationClip[] clips = AnimationUtility.GetAnimationClips(animation.transform.gameObject);
	    if (clips != null)
	    {
		    for (int i = 0; i < clips.Length; ++i)
		    {
			    AnimationClip clip = clips[i];
			    if (clip != null)
			    {
				    // 没有采样，或者是新的动画片段
				    if (animClips == null || System.Array.IndexOf(animClips, clip) == -1)
				    {
					    if (newClips == null)
					    {
						    newClips = new List<AnimationClip>();
					    }
					    newClips.Clear();
					    if (animClips != null) newClips.AddRange(animClips);
					    newClips.Add(clip);
					    animClips = newClips.ToArray();
				    }
			    }
		    }
	    }

	    if(animClips != null && clips != null)
	    {
		    for(int i = 0; i < animClips.Length; ++i)
		    {
			    AnimationClip clip = animClips[i];
			    if (clip != null)
			    {
				    if(System.Array.IndexOf(clips, clip) == -1)
				    {
					    if(newClips == null)
					    {
						    newClips = new List<AnimationClip>();
					    }
					    newClips.Clear();
					    newClips.AddRange(animClips);
					    newClips.RemoveAt(i);
					    animClips = newClips.ToArray();
					    --i;
				    }
			    }
		    }
	    }
    }
    
	/// <summary>
	/// 采样，将骨骼变化采样到 GPUSkinningFrame 对象中
	/// </summary>
	/// <param name="frame"></param>
	/// <param name="totalFrames"></param>
	/// <returns></returns>
	private IEnumerator SamplingCoroutine(GPUSkinningFrame frame, int totalFrames)
    {
		yield return new WaitForEndOfFrame();
		
	    GPUSkinningBone[] bones = gpuSkinningAnimation.bones;
        int numBones = bones.Length;
        // // 原插件的采样方式
        // for(int i = 0; i < numBones; ++i)
        // {
	       //  GPUSkinningBone currentBone = bones[i];
	       //  Matrix4x4 mat = Matrix4x4.TRS(currentBone.transform.localPosition, currentBone.transform.localRotation, currentBone.transform.localScale);
        //     frame.matrices[i] = mat;
        //     // 向上遍历骨骼的所有父节点
        //     do
        //     {
	       //      if (currentBone.parentBoneIndex == -1)
        //         {
        //             break;
        //         }
        //         else
        //         {
        //             currentBone = bones[currentBone.parentBoneIndex];
        //         }
	       //      // 骨骼的变换矩阵
	       //      mat = Matrix4x4.TRS(currentBone.transform.localPosition, currentBone.transform.localRotation, currentBone.transform.localScale);
	       //      // 模型空间-骨骼空间-父骨骼-...-根节点
	       //      frame.matrices[i] = mat * frame.matrices[i]; 
        //     }
        //     while (true);
        // }
        
        GPUSkinningBone currentBone;
        for (int i = 0; i < numBones; ++i)
        {
	        currentBone = bones[i];
	        Matrix4x4 mat = Matrix4x4.TRS(currentBone.transform.localPosition, currentBone.transform.localRotation, currentBone.transform.localScale);
	        frame.matrices[i] = mat;
        }
        
        // 处理根节点的变换
        if(samplingFrameIndex == 0)
        {
	        rootMotionPosition = bones[gpuSkinningAnimation.rootBoneIndex].transform.localPosition;
            rootMotionRotation = bones[gpuSkinningAnimation.rootBoneIndex].transform.localRotation;
        }
        else
        {
	        Vector3 newPosition = bones[gpuSkinningAnimation.rootBoneIndex].transform.localPosition;
            Quaternion newRotation = bones[gpuSkinningAnimation.rootBoneIndex].transform.localRotation;
            Vector3 deltaPosition = newPosition - rootMotionPosition;// 位置变化
            // 前方与位移方向的旋转变化
            frame.rootMotionDeltaPositionQ = Quaternion.Inverse(Quaternion.Euler(transform.forward.normalized)) * Quaternion.Euler(deltaPosition.normalized);
            frame.rootMotionDeltaPositionL = deltaPosition.magnitude; // 位移距离
            frame.rootMotionDeltaRotation = Quaternion.Inverse(rootMotionRotation) * newRotation; // 旋转变化
            rootMotionPosition = newPosition;
            rootMotionRotation = newRotation;

            // 将前2帧的Root Motion的变化保持一致，尚不清楚原因
            if(samplingFrameIndex == 1)
            {
                gpuSkinningClip.frames[0].rootMotionDeltaPositionQ = gpuSkinningClip.frames[1].rootMotionDeltaPositionQ;
                gpuSkinningClip.frames[0].rootMotionDeltaPositionL = gpuSkinningClip.frames[1].rootMotionDeltaPositionL;
                gpuSkinningClip.frames[0].rootMotionDeltaRotation = gpuSkinningClip.frames[1].rootMotionDeltaRotation;
            }
        }

        ++samplingFrameIndex;
    }
	
	#endregion
    
    #region Mesh
    
    /// <summary>
	/// 骨骼的权重和索引分别中2个UV中保存
	/// </summary>
	private Mesh CreateNewMesh(Mesh mesh, string meshName)
    {
	    // 当前Mesh的数据
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Color[] colors = mesh.colors;
        Vector2[] uv = mesh.uv;

        // 新mesh复制
        Mesh newMesh = new Mesh();
        newMesh.name = meshName;
        newMesh.vertices = mesh.vertices;
        if (normals != null && normals.Length > 0) { newMesh.normals = normals; }
        if (tangents != null && tangents.Length > 0) { newMesh.tangents = tangents; }
        if (colors != null && colors.Length > 0) { newMesh.colors = colors; }
        if (uv != null && uv.Length > 0) { newMesh.uv = uv; }

        int numVertices = mesh.vertexCount;
        BoneWeight[] boneWeights = mesh.boneWeights;
        Vector4[] uv2 = new Vector4[numVertices];
		Vector4[] uv3 = new Vector4[numVertices];
        Transform[] smrBones = smr.bones;
        for(int i = 0; i < numVertices; ++i)
        {
            BoneWeight boneWeight = boneWeights[i];

            // 4根骨骼
			BoneWeightSortData[] weights = new BoneWeightSortData[4];
			weights[0] = new BoneWeightSortData(){ index=boneWeight.boneIndex0, weight=boneWeight.weight0 };
			weights[1] = new BoneWeightSortData(){ index=boneWeight.boneIndex1, weight=boneWeight.weight1 };
			weights[2] = new BoneWeightSortData(){ index=boneWeight.boneIndex2, weight=boneWeight.weight2 };
			weights[3] = new BoneWeightSortData(){ index=boneWeight.boneIndex3, weight=boneWeight.weight3 };
			System.Array.Sort(weights); // 按权重排序

			// 通过tran拿到配置表中对应的骨骼数据，通过骨骼数据拿到索引
			GPUSkinningBone bone0 = GetBoneByTransform(smrBones[weights[0].index]);
			GPUSkinningBone bone1 = GetBoneByTransform(smrBones[weights[1].index]);
			GPUSkinningBone bone2 = GetBoneByTransform(smrBones[weights[2].index]);
			GPUSkinningBone bone3 = GetBoneByTransform(smrBones[weights[3].index]);
			
			Vector4 skinData_01 = new Vector4();
			skinData_01.x = GetBoneIndex(bone0); // 索引（序列化数据中的索引）
			skinData_01.y = weights[0].weight; // 权重
			skinData_01.z = GetBoneIndex(bone1);
			skinData_01.w = weights[1].weight;
			uv2[i] = skinData_01;

			Vector4 skinData_23 = new Vector4();
			skinData_23.x = GetBoneIndex(bone2);
			skinData_23.y = weights[2].weight;
			skinData_23.z = GetBoneIndex(bone3);
			skinData_23.w = weights[3].weight;
			uv3[i] = skinData_23;
        }
        newMesh.SetUVs(1, new List<Vector4>(uv2));
		newMesh.SetUVs(2, new List<Vector4>(uv3));

        newMesh.triangles = mesh.triangles;
        return newMesh;
    }
	
	private void CreateLODMeshes(Bounds bounds, string dir)
	{
		gpuSkinningAnimation.lodMeshes = null;
		gpuSkinningAnimation.lodDistances = null;
		gpuSkinningAnimation.sphereRadius = sphereRadius;

		if(lodMeshes != null)
		{
			List<Mesh> newMeshes = new List<Mesh>();
			List<float> newLodDistances = new List<float>();
			for (int i = 0; i < lodMeshes.Length; ++i)
			{
				Mesh lodMesh = lodMeshes[i];
				if(lodMesh != null)
				{
					Mesh newMesh = CreateNewMesh(lodMesh, "GPUSkinning_Mesh_LOD" + (i + 1));
					newMesh.bounds = bounds;
					string savedMeshPath = dir + "/GPUSKinning_Mesh_" + animName + "_LOD" + (i + 1) + ".asset";
					AssetDatabase.CreateAsset(newMesh, savedMeshPath);
					newMeshes.Add(newMesh);
					newLodDistances.Add(lodDistances[i]);
				}
			}
			gpuSkinningAnimation.lodMeshes = newMeshes.ToArray();

			newLodDistances.Add(9999);
			gpuSkinningAnimation.lodDistances = newLodDistances.ToArray();
		}

		EditorUtility.SetDirty(gpuSkinningAnimation);
	}
	
    #endregion
    
    #region Bone

    // 用来给骨骼排序的结构
    private class BoneWeightSortData : System.IComparable<BoneWeightSortData>
    {
	    public int index = 0; // 骨骼在Mesh中的索引

	    public float weight = 0;

	    public int CompareTo(BoneWeightSortData b)
	    {
		    return weight > b.weight ? -1 : 1;
	    }
    }
    
    /// <summary>
    /// 将需要导出的骨骼节点数据从原配置中读取
    /// </summary>
    /// <param name="bonesOrig"></param>
    /// <param name="bonesNew"></param>
    private void RestoreCustomBoneData(GPUSkinningBone[] bonesOrig, GPUSkinningBone[] bonesNew)
    {
	    for(int i = 0; i < bonesNew.Length; ++i)
	    {
		    for(int j = 0; j < bonesOrig.Length; ++j)
		    {
			    if(bonesNew[i].guid == bonesOrig[j].guid)
			    {
				    bonesNew[i].isExposed = bonesOrig[j].isExposed;
				    break;
			    }
		    }
	    }
    }
    
    /// <summary>
    /// 为骨骼生成唯一标识
    /// </summary>
    /// <param name="bones"></param>
    private void GenerateBonesGUID(GPUSkinningBone[] bones)
    {
	    int numBones = bones == null ? 0 : bones.Length;
	    for(int i = 0; i < numBones; ++i)
	    {
		    string boneHierarchyPath = GPUSkinningUtil.BoneHierarchyPath(bones, i);
		    string guid = GPUSkinningUtil.MD5(boneHierarchyPath);
		    bones[i].guid = guid;
	    }
    }
    
    /// <summary>
    /// 获取骨骼的数据，主要是一些初始化的数据
    /// </summary>
    private void CollectBones(List<GPUSkinningBone> bones_result, Transform[] bones_smr, Matrix4x4[] bindposes, GPUSkinningBone parentBone, Transform currentBoneTransform, int currentBoneIndex)
    {
	    GPUSkinningBone currentBone = new GPUSkinningBone();
	    bones_result.Add(currentBone);

	    int indexOfSmrBones = System.Array.IndexOf(bones_smr, currentBoneTransform); // 获取当前骨骼的索引
	    currentBone.transform = currentBoneTransform;
	    currentBone.name = currentBone.transform.gameObject.name;

	    if (indexOfSmrBones == -1)
	    {
		    currentBone.bindpose = Matrix4x4.identity;
		    currentBone.isSkinningBone = false;
	    }
	    else
	    {
		    currentBone.bindpose = bindposes[indexOfSmrBones];
		    currentBone.isSkinningBone = true;
	    }
	    
	    currentBone.parentBoneIndex = parentBone == null ? -1 : bones_result.IndexOf(parentBone);

	    if(parentBone != null)
	    {
		    parentBone.childrenBonesIndices[currentBoneIndex] = bones_result.IndexOf(currentBone);
	    }

	    // 递归获取每个子节点
	    int numChildren = currentBone.transform.childCount;
	    if(numChildren > 0)
	    {
		    currentBone.childrenBonesIndices = new int[numChildren];
		    for(int i = 0; i < numChildren; ++i)
		    {
			    CollectBones(bones_result, bones_smr, bindposes, currentBone, currentBone.transform.GetChild(i) , i);
		    }
	    }
    }

    private GPUSkinningBone[] CollectSkinningBones(GPUSkinningBone[] bones, Transform[] bones_smr)
    {
	    List<GPUSkinningBone> skinningBones = new List<GPUSkinningBone>();
	    for (int i = 0; i < bones.Length; i++)
	    {
		    if (bones[i].isSkinningBone)
		    {
			    GPUSkinningBone currentBone = new GPUSkinningBone();
			    currentBone = bones[i];
			    skinningBones.Add(currentBone);
		    }
	    }
	    GPUSkinningBone[] skinningBonesArray = skinningBones.ToArray();

	    // 用以查看蒙皮骨骼的索引信息
	    // GPUSkinningBone bone;
	    // for (int i = 0; i < skinningBonesArray.Length; i++)
	    // {
		   //  bone = skinningBonesArray[i];
		   //  int parentIndex = GetBoneIndex(skinningBonesArray, bone.transform.parent);
		   //  bone.parentBoneIndex = parentIndex;
		   //  
		   //  int childCount = bone.transform.childCount;
		   //  List<int> childrenBonesIndexList = new List<int>();
		   //  for (int j = 0; j < childCount; j++)
		   //  {
			  //   var childIndex= GetBoneIndex(skinningBonesArray, bone.transform.GetChild(j));
			  //   if (childIndex != -1)
			  //   {
				 //    childrenBonesIndexList.Add(childIndex);
			  //   }
		   //  }
		   //  bone.childrenBonesIndices = childrenBonesIndexList.ToArray();
	    // }
	    
	    return skinningBonesArray;
    }

    private int GetBoneIndex(GPUSkinningBone[] bones, Transform bone)
    {
	    for (int i = 0; i < bones.Length; i++)
	    {
		    if (bones[i].transform == bone)
		    {
			    return i;
		    }
	    }
	    return -1;
    }


    private void ClearSkinningBones()
    {
	    gpuSkinningAnimation.skinningBones = null;
    }

    private GPUSkinningBone GetBoneByTransform(Transform transform)
    {
	    GPUSkinningBone[] bones = gpuSkinningAnimation.skinningBones;
	    int numBones = bones.Length;
	    for(int i = 0; i < numBones; ++i)
	    {
		    if(bones[i].transform == transform)
		    {
			    return bones[i];
		    }
	    }
	    return null;
    }
    
    //骨骼索引，此索引是序列化数据中的骨骼索引，非Mesh中
    private int GetBoneIndex(GPUSkinningBone bone)
    {
	    int index = System.Array.IndexOf(gpuSkinningAnimation.skinningBones, bone);
	    return index;
    }

    // Todo
    private int GetSkinningBoneParentIndex(GPUSkinningBone bone)
    {
	    if (bone.parentBoneIndex == -1)
	    {
		    return -1;
	    }
	    GPUSkinningBone parentBone = gpuSkinningAnimation.bones[bone.parentBoneIndex];
	    if (!parentBone.isSkinningBone)
	    {
		    return -1;
	    }
	    int index = System.Array.IndexOf(gpuSkinningAnimation.skinningBones, parentBone);
	    return index;
    }

    #endregion
    
    #region AnimationClip
    
    /// <summary>
    /// 用Inspector面板配置的动画片段来Override animator的控制器
    /// </summary>
    private void SetCurrentAnimationClip()
    {
	    if (animation == null)
	    {
		    AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController();
		    AnimationClip[] clips = runtimeAnimatorController.animationClips;
		    AnimationClipPair[] pairs = new AnimationClipPair[clips.Length];
		    for (int i = 0; i < clips.Length; ++i)
		    {
			    AnimationClipPair pair = new AnimationClipPair();
			    pairs[i] = pair;
			    pair.originalClip = clips[i]; // 从动画控制器上获取的动画片段
			    pair.overrideClip = animClip; // 从Inspector面板配置的动画片段
		    }
		    animatorOverrideController.runtimeAnimatorController = runtimeAnimatorController;
		    animatorOverrideController.clips = pairs;
		    animator.runtimeAnimatorController = animatorOverrideController;
	    }
    }
    
    /// <summary>
    /// 获取AnimationClip的帧率，可能在Inspector的配置中，默认在AnimationClip中
    /// </summary>
    private int GetClipFPS(AnimationClip clip, int clipIndex)
    {
	    return fpsList[clipIndex] == 0 ? (int)clip.frameRate : fpsList[clipIndex];
    }

    /// <summary>
    /// 保存动画事件数据
    /// </summary>
    private void RestoreCustomClipData(GPUSkinningClip src, GPUSkinningClip dest)
    {
	    if(src.events != null)
	    {
		    int totalFrames = (int)(dest.length * dest.fps);
		    dest.events = new GPUSkinningAnimEvent[src.events.Length];
		    for(int i = 0; i < dest.events.Length; ++i)
		    {
			    GPUSkinningAnimEvent evt = new GPUSkinningAnimEvent();
			    evt.eventId = src.events[i].eventId;
			    evt.frameIndex = Mathf.Clamp(src.events[i].frameIndex, 0, totalFrames - 1);
			    dest.events[i] = evt;
		    }
	    }
    }
    
    #endregion
    
    #region Texture
    
    // 设置与贴图相关的数据
    private void SetSthAboutTexture(GPUSkinningAnimation gpuSkinningAnim)
    {
	    int numPixels = 0;

	    GPUSkinningClip[] clips = gpuSkinningAnim.clips;
	    int numClips = clips.Length;
	    for (int clipIndex = 0; clipIndex < numClips; ++clipIndex)
	    {
		    GPUSkinningClip clip = clips[clipIndex];
		    clip.pixelSegmentation = numPixels;

		    GPUSkinningFrame[] frames = clip.frames;
		    int numFrames = frames.Length;
		    numPixels += gpuSkinningAnim.skinningBoneNum * 2 * numFrames;
		    //numPixels += gpuSkinningAnim.bones.Length * 2 * numFrames;
	    }

	    CalculateTextureSize(numPixels, out gpuSkinningAnim.textureWidth, out gpuSkinningAnim.textureHeight);
    }
    
    private void CalculateTextureSize(int numPixels, out int texWidth, out int texHeight)
    {
	    texWidth = 1;
	    texHeight = 1;
	    while (true)
	    {
		    if (texWidth * texHeight >= numPixels) break;
		    texWidth *= 2;
		    if (texWidth * texHeight >= numPixels) break;
		    texHeight *= 2;
	    }
    }

    private void SetBindBoneTexture(GPUSkinningAnimation gpuSkinningAnim)
    {
	    int numPixels = gpuSkinningAnim.skinningBoneNum * 3;
	    CalculateTextureSize(numPixels, out gpuSkinningAnim.bindTextureWidth, out gpuSkinningAnim.bindTextureHeight);
    }

    /// <summary>
    /// 创建纹理贴图文件，保存格式为.bytes
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="gpuSkinningAnim"></param>
    private void CreateTextureMatrix(string dir, GPUSkinningAnimation gpuSkinningAnim)
    {
	    Texture2D textureBind = new Texture2D(gpuSkinningAnim.bindTextureWidth, gpuSkinningAnim.bindTextureHeight, TextureFormat.RGBAHalf, false, true);
	    textureBind.filterMode = FilterMode.Point;
	    Color[] pixelsBind = textureBind.GetPixels();
	    int bindPixelIndex = 0;
	    for (int i = 0; i < gpuSkinningAnim.skinningBones.Length; i++)
	    {
		    GPUSkinningBone curBone = gpuSkinningAnim.skinningBones[i];
		    Matrix4x4 bindMatrix = curBone.bindpose;
		    Quaternion rotationBind = GPUSkinningUtil.ToQuaternion(bindMatrix);
		    Vector3 scaleBind = bindMatrix.lossyScale;
		    var bindPos = bindMatrix.GetColumn(3);
		    pixelsBind[bindPixelIndex] = new Color(rotationBind.x, rotationBind.y, rotationBind.z, rotationBind.w);
		    bindPixelIndex++;
		    pixelsBind[bindPixelIndex] = new Color(bindPos.x, bindPos.y, bindPos.z, scaleBind.x);
		    bindPixelIndex++;
		    int parentBoneIndex = GetSkinningBoneParentIndex(curBone);
		    pixelsBind[bindPixelIndex] = new Color(parentBoneIndex, 0, 0, 0);
		    bindPixelIndex++;
	    }
	    
	    textureBind.SetPixels(pixelsBind);
	    textureBind.Apply();
	    
	    string savedPathBind = dir + "/GPUSKinning_TextureBind_" + animName + ".bytes";
	    using (FileStream fileStream = new FileStream(savedPathBind, FileMode.Create))
	    {
		    byte[] bytes = textureBind.GetRawTextureData();
		    fileStream.Write(bytes, 0, bytes.Length);
		    fileStream.Flush();
		    fileStream.Close();
		    fileStream.Dispose();
	    }
	    WriteTempData(TEMP_SAVED_TEXTUREBIND_PATH, savedPathBind);
	    
	    
        Texture2D texture = new Texture2D(gpuSkinningAnim.textureWidth, gpuSkinningAnim.textureHeight, TextureFormat.RGBAHalf, false, true);
        texture.filterMode = FilterMode.Point;
        Color[] pixels = texture.GetPixels();
        int pixelIndex = 0; // 像素索引
        // 逐动画
        for (int clipIndex = 0; clipIndex < gpuSkinningAnim.clips.Length; ++clipIndex)
        {
            GPUSkinningClip clip = gpuSkinningAnim.clips[clipIndex]; // 待采样的GPUSkinningClip
            GPUSkinningFrame[] frames = clip.frames;
            int numFrames = frames.Length;
            // 逐帧
            for (int frameIndex = 0; frameIndex < numFrames; ++frameIndex)
            {
                GPUSkinningFrame frame = frames[frameIndex]; // 当前帧的骨骼采样数据
                Matrix4x4[] matrices = frame.matrices;
                int numMatrices = matrices.Length;
                if (numMatrices > gpuSkinningAnim.bones.Length)
                {
	                Debug.LogError("Set wrong RootBone");
	                return;
                }
                //逐骨骼
                for (int matrixIndex = 0; matrixIndex < numMatrices; ++matrixIndex)
                {
	                if (gpuSkinningAnim.bones[matrixIndex].isSkinningBone)
	                {
		                Matrix4x4 matrix = matrices[matrixIndex]; // 骨骼的变换矩阵
		                Quaternion rotation = GPUSkinningUtil.ToQuaternion(matrix); // 提取旋转相关的4元数
		                Vector3 scale = matrix.lossyScale;
		                var pos = matrix.GetColumn(3);
		                pixels[pixelIndex] = new Color(rotation.x, rotation.y, rotation.z, rotation.w); // 旋转
		                pixelIndex++;
		                pixels[pixelIndex] = new Color(pos.x, pos.y, pos.z, scale.x); // 位移与缩放
		                pixelIndex++;
	                }
                }
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        
        string savedPath = dir + "/GPUSKinning_Texture_" + animName + ".bytes";
        using (FileStream fileStream = new FileStream(savedPath, FileMode.Create))
        {
            byte[] bytes = texture.GetRawTextureData();
            fileStream.Write(bytes, 0, bytes.Length);
            fileStream.Flush();
            fileStream.Close();
            fileStream.Dispose();
        }
        WriteTempData(TEMP_SAVED_TEXTURE_PATH, savedPath);
    }
	
    #endregion

    #region Shader/Material

	private void CreateShaderAndMaterial(string dir)
	{
        Shader shader = null;
        if (createNewShader)
        {
            string shaderTemplate =
                shaderType == GPUSkinningShaderType.Unlit ? "GPUSkinningUnlit_Template" :
                shaderType == GPUSkinningShaderType.StandardSpecular ? "GPUSkinningSpecular_Template" :
                shaderType == GPUSkinningShaderType.StandardMetallic ? "GPUSkinningMetallic_Template" : string.Empty;

            string shaderStr = ((TextAsset)Resources.Load(shaderTemplate)).text;
            shaderStr = shaderStr.Replace("_$AnimName$_", animName);
            shaderStr = SkinQualityShaderStr(shaderStr);
            string shaderPath = dir + "/GPUSKinning_Shader_" + animName + ".shader";
            File.WriteAllText(shaderPath, shaderStr);
            WriteTempData(TEMP_SAVED_SHADER_PATH, shaderPath);
            AssetDatabase.ImportAsset(shaderPath);
            shader = AssetDatabase.LoadMainAssetAtPath(shaderPath) as Shader;
        }
        else
        {
            string shaderName =
                shaderType == GPUSkinningShaderType.Unlit ? "GPUSkinning/GPUSkinning_Unlit_Skin" :
                shaderType == GPUSkinningShaderType.StandardSpecular ? "GPUSkinning/GPUSkinning_Specular_Skin" :
                shaderType == GPUSkinningShaderType.StandardMetallic ? "GPUSkinning_Metallic_Skin" : string.Empty;
            shaderName +=
                skinQuality == GPUSkinningQuality.Bone1 ? 1 :
                skinQuality == GPUSkinningQuality.Bone2 ? 2 :
                skinQuality == GPUSkinningQuality.Bone4 ? 4 : 1;
            shader = Shader.Find(shaderName);
            WriteTempData(TEMP_SAVED_SHADER_PATH, AssetDatabase.GetAssetPath(shader));
        }

		Material mtrl = new Material(shader);
		if(smr.sharedMaterial != null)
		{
			mtrl.CopyPropertiesFromMaterial(smr.sharedMaterial);
		}
		string savedMtrlPath = dir + "/GPUSKinning_Material_" + animName + ".mat";
		AssetDatabase.CreateAsset(mtrl, savedMtrlPath);
        WriteTempData(TEMP_SAVED_MTRL_PATH, savedMtrlPath);
	}

	private string SkinQualityShaderStr(string shaderStr)
	{
		GPUSkinningQuality removalQuality1 = 
			skinQuality == GPUSkinningQuality.Bone1 ? GPUSkinningQuality.Bone2 : 
			skinQuality == GPUSkinningQuality.Bone2 ? GPUSkinningQuality.Bone1 : 
			skinQuality == GPUSkinningQuality.Bone4 ? GPUSkinningQuality.Bone1 : GPUSkinningQuality.Bone1;

		GPUSkinningQuality removalQuality2 = 
			skinQuality == GPUSkinningQuality.Bone1 ? GPUSkinningQuality.Bone4 : 
			skinQuality == GPUSkinningQuality.Bone2 ? GPUSkinningQuality.Bone4 : 
			skinQuality == GPUSkinningQuality.Bone4 ? GPUSkinningQuality.Bone2 : GPUSkinningQuality.Bone1;

		shaderStr = Regex.Replace(shaderStr, @"_\$" + removalQuality1 + @"[\s\S]*" + removalQuality1 + @"\$_", string.Empty);
		shaderStr = Regex.Replace(shaderStr, @"_\$" + removalQuality2 + @"[\s\S]*" + removalQuality2 + @"\$_", string.Empty);
		shaderStr = shaderStr.Replace("_$" + skinQuality, string.Empty);
		shaderStr = shaderStr.Replace(skinQuality + "$_", string.Empty);

		return shaderStr;
	}
    #endregion

    #region PlayerPrefs

    private void SaveUserPreferDir(string dirPath)
    {
	    PlayerPrefs.SetString("GPUSkinning_UserPreferDir", dirPath);
    }

    private string GetUserPreferDir()
    {
	    return PlayerPrefs.GetString("GPUSkinning_UserPreferDir", Application.dataPath);
    }

    // 写入本地缓存数据（这里的数据主要是各种资源的）
    public static void WriteTempData(string key, string value)
    {
	    PlayerPrefs.SetString(key, value);
    }

    // 读取本地缓存数据
    public static string ReadTempData(string key)
    {
	    return PlayerPrefs.GetString(key, string.Empty);
    }

    // 删除本地缓存数据
    public static void DeleteTempData(string key)
    {
	    PlayerPrefs.DeleteKey(key);
    }

    #endregion
    
    private void InitTransform()
    {
	    transform.parent = null;
	    transform.position = Vector3.zero;
	    transform.eulerAngles = Vector3.zero;
    }
    
    // 弹对话框
	public static void ShowDialog(string msg)
	{
		EditorUtility.DisplayDialog("GPUSkinning", msg, "OK");
	}
	
#endif
}
