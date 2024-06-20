// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties0' to new syntax.
// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties1' to new syntax.
// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties2' to new syntax.

#ifndef GPUSKINNING_VERTEX_INCLUDE
#define GPUSKINNING_VERTEX_INCLUDE
#include <HLSLSupport.cginc>
#include <UnityInstancing.cginc>

uniform sampler2D _GPUSkinning_TextureMatrix;
uniform sampler2D _GPUSkinning_TextureBindMatrix;

uniform float4 _GPUSkinning_TextureSize_NumPixelsPerFrame; // x:textureWidth, y:textureHeight, z:每帧需要的纹素数量
uniform float4 _GPUSkinning_BindTextureSize_interpolationFactor;

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

// 获取指定帧的顶点位置
inline float4 getPos(float frameIndex, int vertexIndex)
{
	float4 uv = float4((vertexIndex + 0.5) / _GPUSkinning_TextureSize_NumPixelsPerFrame.x, (frameIndex + 0.5) / _GPUSkinning_TextureSize_NumPixelsPerFrame.y, 0, 0);
	float4 pos = tex2Dlod(_GPUSkinning_TextureMatrix, uv);
	return pos;
}

// 获取帧插值后的顶点位置
// isLast 是否是前一个动画
// 前后两个动画的插值因子是不同的
// 动画融合是将前一个动画平滑过度到当前，如果没有融合就默认false
inline float4 getInterpolationVertexPos(float curFrameIndex, float nextFrameIndex, int vertexIndex, bool isLast)
{
	float frameInterpFactor;
	if(isLast)
	{
		frameInterpFactor = _GPUSkinning_BindTextureSize_interpolationFactor.z;
	}
	else
	{
		frameInterpFactor = _GPUSkinning_BindTextureSize_interpolationFactor.w;
	}
	
	float4 curPos = getPos(curFrameIndex, vertexIndex);
	float4 nextPos = getPos(nextFrameIndex, vertexIndex);
	float4 pos = lerp(curPos, nextPos, frameInterpFactor);
	return pos;
}

// 动画实际混合计算
#define skin_blend(pos0, pos1) pos1.xyz + (pos0.xyz - pos1.xyz) * crossFadeBlend

//获取融合后的顶点位置
inline float4 getCrossFadeVertexPos(float curFrameIndexA, float nextFrameIndexA, float curFrameIndexB, float nextFrameIndexB, int vertexIndex, float crossFadeBlend)
{
	float4 interpolationVertexPosA = getInterpolationVertexPos(curFrameIndexA, nextFrameIndexA, vertexIndex, true);
	float4 interpolationVertexPosB = getInterpolationVertexPos(curFrameIndexB, nextFrameIndexB, vertexIndex, false);
	float4 pos = lerp(interpolationVertexPosA, interpolationVertexPosB, crossFadeBlend);
	return pos;
}

inline float getFrameIndex()
{
    float2 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_arr,
                                                            _GPUSkinning_FrameIndex_PixelSegmentation);
	float segment = frameIndex_segment.y;
    float frameIndex = frameIndex_segment.x;
	frameIndex = segment + frameIndex;
    return frameIndex;
}

#if !defined(ROOTON_BLENDOFF) && !defined(ROOTOFF_BLENDOFF) // 开启动画融合
// 获取融合开始帧的纹素索引
inline float getFrameIndex_crossFade()
{
	float3 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade_arr, _GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade);
	float segment = frameIndex_segment.y;
	float frameIndex = frameIndex_segment.x;
	frameIndex = segment + frameIndex;
	return frameIndex;
}
#endif

#define crossFadeBlend UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade_arr, _GPUSkinning_FrameIndex_PixelSegmentation_Blend_CrossFade).z

#define rootMotion UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_RootMotion_arr, _GPUSkinning_RootMotion)

#define rootMotion_crossFade UNITY_ACCESS_INSTANCED_PROP(_GPUSkinning_RootMotion_CrossFade_arr, _GPUSkinning_RootMotion_CrossFade)

// 当前帧的位移矩阵的函数
#define textureVertex(vertexIndex) float frameIndex = getFrameIndex(); \
                                float nextFrameIndex = frameIndex + 1; \
								float4 vertexPos = getInterpolationVertexPos(frameIndex, nextFrameIndex, vertexIndex, false); 
// 动画融合的位移矩阵函数
#define textureVertex_crossFade(vertexIndex) float frameIndex_crossFade = getFrameIndex_crossFade(); \
                                          float frameNextIndex_crossFade = frameIndex_crossFade + 1; \
                                          float frameIndex = getFrameIndex(); \
                                          float frameNextIndex = frameIndex + 1; \
										  float4 vertexPos = getCrossFadeVertexPos(frameIndex_crossFade, frameNextIndex_crossFade, frameIndex, frameNextIndex, vertexIndex, crossFadeBlend); 

////////////////////////////////////////////// 不同的蒙皮模式实现 //////////////////////////////////////////////////////

#define rootOff_BlendOff_Vertex() textureVertex(vertexIndex); \
									return vertexPos;

#define rootOff_BlendOn_CrossFadeRootOff()  textureVertex_crossFade(vertexIndex); \
												   return vertexPos;
//////////////////////////////////////////////////// 蒙皮函数 ///////////////////////////////////////////////////////////

inline float4 vertex_Skin(int vertexIndex)
{
    #if ROOTOFF_BLENDOFF
	rootOff_BlendOff_Vertex();
    #endif
   
    #if ROOTOFF_BLENDON_CROSSFADEROOTOFF
	rootOff_BlendOn_CrossFadeRootOff();
    #endif

	return 0;
}

#endif
