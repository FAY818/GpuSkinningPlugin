Shader "GPUSkinning/Vertices_Unlit_Skin"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Assets/GPUSkinning/Resources/GPUSkinningVertexInclude.cginc"

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        float2 uv2 : TEXCOORD1; // 顶点索引
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    sampler2D _MainTex;
    float4 _MainTex_ST;

    v2f vert (appdata v)
    {
        UNITY_SETUP_INSTANCE_ID(v);
        v2f o;
        float4 pos = vertex_Skin(v.uv2.x); // 顶点在模型空间中的坐标
        o.vertex = UnityObjectToClipPos(pos);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        return o;
    }

    fixed4 frag (v2f i):SV_TARGET
    {
        fixed4 col = tex2D(_MainTex, i.uv);
        return col;
    }
    
    ENDCG
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile ROOTON_BLENDOFF ROOTON_BLENDON_CROSSFADEROOTON ROOTON_BLENDON_CROSSFADEROOTOFF ROOTOFF_BLENDOFF ROOTOFF_BLENDON_CROSSFADEROOTON ROOTOFF_BLENDON_CROSSFADEROOTOFF
            ENDCG
        }
    }
}
