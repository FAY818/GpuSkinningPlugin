// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties0' to new syntax.
// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties1' to new syntax.
// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties2' to new syntax.

#ifndef GPUSKINNING_INCLUDE
#define GPUSKINNING_INCLUDE

uniform sampler2D _GPUSkinning_TextureMatrix;
uniform sampler2D _GPUSkinning_TextureBindMatrix;

uniform float4 _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor; // x:textureWidth, y:textureHeight, z:每帧需要的纹素数量
uniform float2 _GPUSkinning_BindTextureSize;

UNITY_INSTANCING_BUFFER_START(GPUSkinningProperties0)
UNITY_DEFINE_INSTANCED_PROP(float2, _GPUSkinning_FrameIndex_PixelSegmentation) // 采样帧率和纹素尺寸
#define _GPUSkinning_FrameIndex_PixelSegmentation_arr GPUSkinningProperties0
#if !defined(ROOTON_BLENDOFF) && !defined(ROOTOFF_BLENDOFF) // 开启动画融合
	UNITY_DEFINE_INSTANCED_PROP(float3, _GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade) // 融合动画的采样帧率和像素间隔以及融合比率
#define _GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade_arr GPUSkinningProperties0
#endif
UNITY_INSTANCING_BUFFER_END(GPUSkinningProperties0)

// 开启root motion
#if defined(ROOTON_BLENDOFF) || defined(ROOTON_BLENDON_CROSSFADEROOTON) || defined(ROOTON_BLENDON_CROSSFADEROOTOFF)
UNITY_INSTANCING_BUFFER_START(GPUSkinningProperties1)
UNITY_DEFINE_INSTANCED_PROP(float4x4, _GPUSkinning_RootMotion) // 当前动画RootMotion的BindPose的逆矩阵 
#define _GPUSkinning_RootMotion_arr GPUSkinningProperties1
UNITY_INSTANCING_BUFFER_END(GPUSkinningProperties1)
#endif

// 开启root motion融合
#if defined(ROOTON_BLENDON_CROSSFADEROOTON) || defined(ROOTOFF_BLENDON_CROSSFADEROOTON)
UNITY_INSTANCING_BUFFER_START(GPUSkinningProperties2)
	UNITY_DEFINE_INSTANCED_PROP(float4x4, _GPUSkinning_RootMotion_CrossFade)  // 融合动画RootMotion的BindPose的逆矩阵
#define _GPUSkinning_RootMotion_CrossFade_arr GPUSkinningProperties2
UNITY_INSTANCING_BUFFER_END(GPUSkinningProperties2)
#endif

float4x4 QuaternionToMatrix(float4 vec)
{
    float4x4 ret;
    ret._11 = 2.0 * (vec.x * vec.x + vec.w * vec.w) - 1;
    ret._21 = 2.0 * (vec.x * vec.y + vec.z * vec.w);
    ret._31 = 2.0 * (vec.x * vec.z - vec.y * vec.w);
    ret._41 = 0.0;

    ret._12 = 2.0 * (vec.x * vec.y - vec.z * vec.w);
    ret._22 = 2.0 * (vec.y * vec.y + vec.w * vec.w) - 1;
    ret._32 = 2.0 * (vec.y * vec.z + vec.x * vec.w);
    ret._42 = 0.0;

    ret._13 = 2.0 * (vec.x * vec.z + vec.y * vec.w);
    ret._23 = 2.0 * (vec.y * vec.z - vec.x * vec.w);
    ret._33 = 2.0 * (vec.z * vec.z + vec.w * vec.w) - 1;
    ret._43 = 0.0;
    // 没有平移
    ret._14 = 0.0;
    ret._24 = 0.0;
    ret._34 = 0.0;
    ret._44 = 1.0;
    return ret;
}

// m_dual：对偶（虚）四元数，表示旋转
// m_real：常规四元数，表示位移和缩放
float4x4 DualQuaternionToMatrix(float4 m_dual, float4 m_real)
{
    // 旋转矩阵
    float4x4 rotationMatrix = QuaternionToMatrix(float4(m_dual.x, m_dual.y, m_dual.z, m_dual.w));
    // 平移矩阵
    float4x4 translationMatrix;
    translationMatrix._11_12_13_14 = float4(1, 0, 0, 0);
    translationMatrix._21_22_23_24 = float4(0, 1, 0, 0);
    translationMatrix._31_32_33_34 = float4(0, 0, 1, 0);
    translationMatrix._41_42_43_44 = float4(0, 0, 0, 1);
    translationMatrix._14 = m_real.x;
    translationMatrix._24 = m_real.y;
    translationMatrix._34 = m_real.z;
    // 缩放矩阵
    float4x4 scaleMatrix;
    scaleMatrix._11_12_13_14 = float4(1, 0, 0, 0);
    scaleMatrix._21_22_23_24 = float4(0, 1, 0, 0);
    scaleMatrix._31_32_33_34 = float4(0, 0, 1, 0);
    scaleMatrix._41_42_43_44 = float4(0, 0, 0, 1);
    scaleMatrix._11 = m_real.w;
    scaleMatrix._22 = m_real.w;
    scaleMatrix._33 = m_real.w;
    scaleMatrix._44 = 1;
    float4x4 M = mul(translationMatrix, mul(rotationMatrix, scaleMatrix));
    return M;
}

inline float4 indexToUV(float index)
{
    int row = (int)(index / _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.x); // 除以textureWidth
    float col = index - row * _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.x;
    return float4(col / _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.x,
                  row / _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.y, 0, 0);
}

inline float4 bindIndexToUV(float index)
{
	int row = (int)(index / _GPUSkinning_BindTextureSize.x); // 除以textureWidth
	float col = index - row * _GPUSkinning_BindTextureSize.x;
	return float4(col / _GPUSkinning_BindTextureSize.x,
				  row / _GPUSkinning_BindTextureSize.y, 0, 0);
}

inline float4 Slerp(float4 q1, float4 q2, float t)
{
    float cos_a = dot(q1, q2);
    if (cos_a < 0)
    {
        q2 = - q2;
        cos_a = -cos_a;
    }
    // 当两个四元数的夹角为0时，sin = 0
    // 所以当夹角小于一定程度时，直接退化为线性插值
    if (cos_a > 0.99f)
    {
        return normalize(q1 * (1 - t) + q2 * t);
    }
    float a = acos(cos_a);
    return (q1 * sin((1 - t) * a) + q2 * sin(t * a)) / sin(a);
}

inline float4 NormalLerp(float4 q1, float4 q2, float t)
{
	normalize(q1 * (1 - t) + q2 * t);
}


//获取补帧的变换矩阵信息
inline float4x4 getMatrix(float frameStartIndex, float nextframeStartIndex, float boneIndex)
{
	int bindMatStartIndex = boneIndex * 3;
	float4 UV1 = bindIndexToUV(bindMatStartIndex);
	float4 UV2 = bindIndexToUV(bindMatStartIndex + 1);
	float4 UV3 = bindIndexToUV(bindMatStartIndex + 2);
	float4 dualBind = tex2Dlod(_GPUSkinning_TextureBindMatrix, UV1);
	float4 realBind = tex2Dlod(_GPUSkinning_TextureBindMatrix, UV2);
	float4 parentBoneIndex = tex2Dlod(_GPUSkinning_TextureBindMatrix, UV3);

	float frameInterpFactor = _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.w;
	// 当前帧的变化矩阵
    int curFrameIndex = frameStartIndex;
    int curMatStartIndex = curFrameIndex + boneIndex * 2;
	float4 curUV1 = indexToUV(curMatStartIndex);
	float4 curUV2 = indexToUV(curMatStartIndex + 1);
    float4 curDual = tex2Dlod(_GPUSkinning_TextureMatrix, curUV1);
    float4 curReal = tex2Dlod(_GPUSkinning_TextureMatrix, curUV2);
	
    //下一帧的变换矩阵
    int nextFrameIndex = nextframeStartIndex;
    int nextMatStartIndex = nextFrameIndex + boneIndex * 2;
	float4 nextUV1 = indexToUV(nextMatStartIndex);
	float4 nextUV2 = indexToUV(nextMatStartIndex + 1);
    float4 nextDual = tex2Dlod(_GPUSkinning_TextureMatrix, nextUV1);
    float4 nextReal = tex2Dlod(_GPUSkinning_TextureMatrix, nextUV2);

	// 插帧
	float4 dual = Slerp(curDual, nextDual, frameInterpFactor);
	float4 real = lerp(curReal, nextReal, frameInterpFactor);
	
    float4x4 curMat = DualQuaternionToMatrix(dual, real);
	float4x4 bindMat = DualQuaternionToMatrix(dualBind, realBind);
	float4x4 mat = mul(curMat, bindMat);
    return mat;
}

//获取融合矩阵
inline float4x4 getCrossFadeMatrix(float startIndexA, float startIndexB, float boneIndex, float crossFadeBlend)
{
	int bindMatStartIndex = boneIndex * 3;
	float4 UV1 = bindIndexToUV(bindMatStartIndex);
	float4 UV2 = bindIndexToUV(bindMatStartIndex + 1);
	float4 UV3 = bindIndexToUV(bindMatStartIndex + 2);
	float4 dualBind = tex2Dlod(_GPUSkinning_TextureBindMatrix, UV1);
	float4 realBind = tex2Dlod(_GPUSkinning_TextureBindMatrix, UV2);
	float4 parentBoneIndex = tex2Dlod(_GPUSkinning_TextureBindMatrix, UV3);
	
	float frameInterpFactorA = frac(startIndexA);
	// 当前帧取样
	int curFrameIndexA = startIndexA;
	float curMatStartIndexA = curFrameIndexA + boneIndex * 2;
	float4 curUV1A = indexToUV(curMatStartIndexA);
	float4 curUV2A = indexToUV(curMatStartIndexA + 1);
	float4 curDualA = tex2Dlod(_GPUSkinning_TextureMatrix, curUV1A);
	float4 curRealA = tex2Dlod(_GPUSkinning_TextureMatrix, curUV2A);
	//下一帧取样
	int nextFrameIndexA = curFrameIndexA + 1;
	float nextMatStartIndexA = nextFrameIndexA + boneIndex * 2;
	float4 nextUV1A = indexToUV(nextMatStartIndexA);
	float4 nextUV2A = indexToUV(nextMatStartIndexA + 1);
	float4 nextDualA = tex2Dlod(_GPUSkinning_TextureMatrix, nextUV1A);
	float4 nextRealA = tex2Dlod(_GPUSkinning_TextureMatrix, nextUV2A);
	// 插帧
	float4 dualA = Slerp(curDualA, nextDualA, frameInterpFactorA);
	float4 realA = lerp(curRealA, nextRealA, frameInterpFactorA);
	
	float frameInterpFactorB = frac(startIndexB);
	// 当前帧取样
	int curFrameIndexB = startIndexB;
	float curMatStartIndexB = curFrameIndexB + boneIndex * 2;
	float4 curUV1B = indexToUV(curMatStartIndexB);
	float4 curUV2B = indexToUV(curMatStartIndexB + 1);
	float4 curDualB = tex2Dlod(_GPUSkinning_TextureMatrix, curUV1B);
	float4 curRealB = tex2Dlod(_GPUSkinning_TextureMatrix, curUV2B);
	
	//下一帧取样
	int nextFrameIndexB = curFrameIndexB + 1;
	float nextMatStartIndexB = nextFrameIndexB + boneIndex * 2;
	float4 nextUV1B = indexToUV(nextMatStartIndexB);
	float4 nextUV2B = indexToUV(nextMatStartIndexB + 1);
	float4 nextDualB = tex2Dlod(_GPUSkinning_TextureMatrix, nextUV1B);
	float4 nextRealB = tex2Dlod(_GPUSkinning_TextureMatrix, nextUV2B);
	
	// 插帧
	float4 dualB = Slerp(curDualB, nextDualB, frameInterpFactorB);
	float4 realB = lerp(curRealB, nextRealB, frameInterpFactorB);
	
	// 融合插帧
	float4 dual = Slerp(dualA, dualB, crossFadeBlend);
	float4 real = lerp(realA, realB, crossFadeBlend);
	
	float4x4 mat = DualQuaternionToMatrix(dual, real);
	float4x4 matBind = DualQuaternionToMatrix(dualBind, realBind);

	mat = mul(mat, matBind);
	return mat;
}

inline float getFrameStartIndex()
{
    float2 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_arr,
                                                            _GPUSkinning_FrameIndex_PixelSegmentation);
    float segment = frameIndex_segment.y; // 动画间隔
    float frameIndex = frameIndex_segment.x; // 当前帧
    float frameStartIndex = segment + frameIndex * _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.z;
    return frameStartIndex;
}

inline float getNextFrameStartIndex()
{
	float2 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_arr,
															_GPUSkinning_FrameIndex_PixelSegmentation);
	float segment = frameIndex_segment.y; // 动画间隔
	float frameIndex = frameIndex_segment.x + 1; // 当前帧
	float frameStartIndex = segment + frameIndex * _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.z;
	return frameStartIndex;
}

#if !defined(ROOTON_BLENDOFF) && !defined(ROOTOFF_BLENDOFF) // 开启动画融合
// 获取融合开始帧的纹素索引
inline float getFrameStartIndex_crossFade()
{
	float3 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade_arr, _GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade);
	float segment = frameIndex_segment.y;
	float frameIndex = frameIndex_segment.x;
	float frameStartIndex = segment + frameIndex * _GPUSkinning_TextureSize_NumPixelsPerFrame_interpolationFactor.z;
	return frameStartIndex;
}
#endif

#define crossFadeBlend UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade_arr, _GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade).z

#define rootMotion UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_RootMotion_arr, _GPUSkinning_RootMotion)

#define rootMotion_crossFade UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_RootMotion_CrossFade_arr, _GPUSkinning_RootMotion_CrossFade)

// 当前帧的位移矩阵的函数
#define textureMatrix(uv2, uv3) float frameStartIndex = getFrameStartIndex(); \
                                float nextStartIndex = getNextFrameStartIndex(); \
								float4x4 mat0 = getMatrix(frameStartIndex,nextStartIndex, uv2.x); \
								float4x4 mat1 = getMatrix(frameStartIndex,nextStartIndex, uv2.z); \
								float4x4 mat2 = getMatrix(frameStartIndex,nextStartIndex, uv3.x); \
								float4x4 mat3 = getMatrix(frameStartIndex,nextStartIndex, uv3.z);
// 动画融合的位移矩阵函数
#define textureMatrix_crossFade(uv2, uv3) float frameStartIndex_crossFade = getFrameStartIndex_crossFade(); \
                                          float frameStartIndex = getFrameStartIndex(); \
                                          float4x4 mat0_crossFade = getCrossFadeMatrix(frameStartIndex_crossFade, frameStartIndex, uv2.x, crossFadeBlend); \
                                          float4x4 mat1_crossFade = getCrossFadeMatrix(frameStartIndex_crossFade, frameStartIndex, uv2.z, crossFadeBlend); \
                                          float4x4 mat2_crossFade = getCrossFadeMatrix(frameStartIndex_crossFade, frameStartIndex, uv3.x, crossFadeBlend); \
                                          float4x4 mat3_crossFade = getCrossFadeMatrix(frameStartIndex_crossFade, frameStartIndex, uv3.z, crossFadeBlend);

///////////////////////////////////////////// 计算顶点坐标的函数 ///////////////////////////////////////////////////////
#define skin1_noroot(mat0, mat1, mat2, mat3) mul(mat0, vertex) * uv2.y;

#define skin1_root(mat0, mat1, mat2, mat3, root) mul(root, mul(mat0, vertex)) * uv2.y;

#define skin2_noroot(mat0, mat1, mat2, mat3) mul(mat0, vertex) * uv2.y + \
									mul(mat1, vertex) * uv2.w;

#define skin2_root(mat0, mat1, mat2, mat3, root) mul(root, mul(mat0, vertex)) * uv2.y + \
										mul(root, mul(mat1, vertex)) * uv2.w;

#define skin4_noroot(mat0, mat1, mat2, mat3) mul(mat0, vertex) * uv2.y + \
												mul(mat1, vertex) * uv2.w + \
												mul(mat2, vertex) * uv3.y + \
												mul(mat3, vertex) * uv3.w;

#define skin4_root(mat0, mat1, mat2, mat3, root) mul(root, mul(mat0, vertex)) * uv2.y + \
													mul(root, mul(mat1, vertex)) * uv2.w + \
													mul(root, mul(mat2, vertex)) * uv3.y + \
													mul(root, mul(mat3, vertex)) * uv3.w;
// 动画实际混合计算
#define skin_blend(pos0, pos1) pos1.xyz + (pos0.xyz - pos1.xyz) * crossFadeBlend

////////////////////////////////////////////// 六种不同的蒙皮模式实现 //////////////////////////////////////////////////////

// 关闭Root运动，关闭动画融合 
#define rootOff_BlendOff(quality) textureMatrix(uv2, uv3); \
									return skin##quality##_noroot(mat0, mat1, mat2, mat3);
// 开启Root运动，关闭动画融合
// #define rootOn_BlendOff(quality) textureMatrix(uv2, uv3); \
// 									float4x4 root = rootMotion; \
// 									return skin##quality##_root(mat0, mat1, mat2, mat3, root);
// 开启Root运动，开启动画融合，开启Root融合
// #define rootOn_BlendOn_CrossFadeRootOn(quality) textureMatrix(uv2, uv3); \
// 												textureMatrix_crossFade(uv2, uv3); \
// 												float4x4 root = rootMotion; \
// 												float4x4 root_crossFade = rootMotion_crossFade; \
// 												float4 pos0 = skin##quality##_root(mat0, mat1, mat2, mat3, root); \
// 												float4 pos1 = skin##quality##_root(mat0_crossFade, mat1_crossFade, mat2_crossFade, mat3_crossFade, root_crossFade); \
// 												return float4(skin_blend(pos0, pos1), 1);
// 开启Root运动，开启动画融合，关闭Root融合
// #define rootOn_BlendOn_CrossFadeRootOff(quality) textureMatrix(uv2, uv3); \
// 												textureMatrix_crossFade(uv2, uv3); \
// 												float4x4 root = rootMotion; \
// 												float4 pos0 = skin##quality##_root(mat0, mat1, mat2, mat3, root); \
// 												float4 pos1 = skin##quality##_noroot(mat0_crossFade, mat1_crossFade, mat2_crossFade, mat3_crossFade); \
// 												return float4(skin_blend(pos0, pos1), 1);
// 关闭Root运动，开启动画融合，开启Root融合
// #define rootOff_BlendOn_CrossFadeRootOn(quality) textureMatrix(uv2, uv3); \
// 												textureMatrix_crossFade(uv2, uv3); \
// 												float4x4 root_crossFade = rootMotion_crossFade; \
// 												float4 pos0 = skin##quality##_noroot(mat0, mat1, mat2, mat3); \
// 												float4 pos1 = skin##quality##_root(mat0_crossFade, mat1_crossFade, mat2_crossFade, mat3_crossFade, root_crossFade); \
// 												return float4(skin_blend(pos0, pos1), 1);
// 关闭Root运动，开启动画融合，关闭Root融合
#define rootOff_BlendOn_CrossFadeRootOff(quality)  textureMatrix_crossFade(uv2, uv3); \
												   return skin##quality##_noroot(mat0_crossFade, mat1_crossFade, mat2_crossFade, mat3_crossFade);

//////////////////////////////////////////////////// 蒙皮函数 ///////////////////////////////////////////////////////////

inline float4 skin1(float4 vertex, float4 uv2, float4 uv3)
{
    #if ROOTOFF_BLENDOFF
	rootOff_BlendOff(1);
    #endif
    #if ROOTON_BLENDOFF
    //rootOn_BlendOff(1);
    #endif
    #if ROOTON_BLENDON_CROSSFADEROOTON
	//rootOn_BlendOn_CrossFadeRootOn(1);
    #endif
    #if ROOTON_BLENDON_CROSSFADEROOTOFF
	//rootOn_BlendOn_CrossFadeRootOff(1);
    #endif
    #if ROOTOFF_BLENDON_CROSSFADEROOTON
	//rootOff_BlendOn_CrossFadeRootOn(1);
    #endif
    #if ROOTOFF_BLENDON_CROSSFADEROOTOFF
	rootOff_BlendOn_CrossFadeRootOff(1);
    #endif
    return 0;
}

inline float4 skin2(float4 vertex, float4 uv2, float4 uv3)
{
    #if ROOTOFF_BLENDOFF
	rootOff_BlendOff(2);
    #endif
    #if ROOTON_BLENDOFF
    //rootOn_BlendOff(2);
    #endif
    #if ROOTON_BLENDON_CROSSFADEROOTON
	//rootOn_BlendOn_CrossFadeRootOn(2);
    #endif
    #if ROOTON_BLENDON_CROSSFADEROOTOFF
	//rootOn_BlendOn_CrossFadeRootOff(2);
    #endif
    #if ROOTOFF_BLENDON_CROSSFADEROOTON
	//rootOff_BlendOn_CrossFadeRootOn(2);
    #endif
    #if ROOTOFF_BLENDON_CROSSFADEROOTOFF
	rootOff_BlendOn_CrossFadeRootOff(2);
    #endif
    return 0;
}

inline float4 skin4(float4 vertex, float4 uv2, float4 uv3)
{
    #if ROOTOFF_BLENDOFF
	rootOff_BlendOff(4);
    #endif
    #if ROOTON_BLENDOFF
    //rootOn_BlendOff(4);
    #endif
    #if ROOTON_BLENDON_CROSSFADEROOTON
	//rootOn_BlendOn_CrossFadeRootOn(4);
    #endif
    #if ROOTON_BLENDON_CROSSFADEROOTOFF
	//rootOn_BlendOn_CrossFadeRootOff(4);
    #endif
    #if ROOTOFF_BLENDON_CROSSFADEROOTON
	//rootOff_BlendOn_CrossFadeRootOn(4);
    #endif
    #if ROOTOFF_BLENDON_CROSSFADEROOTOFF
	rootOff_BlendOn_CrossFadeRootOff(4);
    #endif
    return 0;
}

#endif
