// Running-mean accumulation for RenderQueue4K's shutter blur: out = lerp(_MainTex, _NewTex, _Lerp). Put anywhere under Assets.
Shader "Hidden/CourseBuild/Accumulate"
{
    Properties { _MainTex ("Acc", 2D) = "black" {} _NewTex ("New", 2D) = "black" {} _Lerp ("Lerp", Float) = 0.5 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; sampler2D _NewTex; float _Lerp;
            fixed4 frag (v2f_img i) : SV_Target { return lerp(tex2D(_MainTex, i.uv), tex2D(_NewTex, i.uv), _Lerp); }
            ENDCG
        }
    }
}
