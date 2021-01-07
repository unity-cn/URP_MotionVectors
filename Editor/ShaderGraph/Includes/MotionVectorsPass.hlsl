#ifndef SG_MOTION_VECTORS_PASS_INCLUDED
#define SG_MOTION_VECTORS_PASS_INCLUDED

struct AttributesPass
{
    float3 positionOld : TEXCOORD4;
};

struct VaryingsPassToPS
{
    // Note: Z component is not use currently
    // This is the clip space position. Warning, do not confuse with the value of positionCS in PackedVarying which is SV_POSITION and store in positionSS
    float4 positionVP;
    float4 previousPositionVP;
};

// Available interpolator start from TEXCOORD8
struct PackedVaryingsPassToPS
{
    // Note: Z component is not use
    float3 interpolators0 : TEXCOORD2;
    float3 interpolators1 : TEXCOORD3;
};

PackedVaryingsPassToPS PackVaryingsPassToPS(VaryingsPassToPS input)
{
    PackedVaryingsPassToPS output;
    output.interpolators0 = float3(input.positionVP.xyw);
    output.interpolators1 = float3(input.previousPositionVP.xyw);

    return output;
}

VaryingsPassToPS UnpackVaryingsPassToPS(PackedVaryingsPassToPS input)
{
    VaryingsPassToPS output;
    output.positionVP = float4(input.interpolators0.xy, 0.0, input.interpolators0.z);
    output.previousPositionVP = float4(input.interpolators1.xy, 0.0, input.interpolators1.z);

    return output;
}

void vert(Attributes input, AttributesPass inputPass, out PackedVaryings packedOutput, out PackedVaryingsPassToPS packedOutputExtra)
{
    Varyings output = (Varyings)0;
    output = BuildVaryings(input);

    VaryingsPassToPS outputExtra = (VaryingsPassToPS)0;

    // this works around an issue with dynamic batching
    // potentially remove in 5.4 when we use instancing
    #if defined(UNITY_REVERSED_Z)
        output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
    #else
        output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
    #endif

    outputExtra.positionVP = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, float4(input.positionOS, 1.0)));
    outputExtra.previousPositionVP = mul(_PrevViewProjMatrix, mul(unity_MatrixPreviousM, unity_MotionVectorsParams.x == 1 ? float4(inputPass.positionOld, 1) : float4(input.positionOS, 1.0)));

    packedOutput = (PackedVaryings)0;
    packedOutput = PackVaryings(output);

    packedOutputExtra = (PackedVaryingsPassToPS)0;
    packedOutputExtra = PackVaryingsPassToPS(outputExtra);
}

half4 frag(PackedVaryings packedInput, PackedVaryingsPassToPS packedInputExtra) : SV_TARGET 
{    
    Varyings unpacked = UnpackVaryings(packedInput);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);

    SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(unpacked);
    SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

    VaryingsPassToPS unpackedExtra = UnpackVaryingsPassToPS(packedInputExtra);

    #if _AlphaClip
        clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
    #endif

    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }
    
    // Calculate positions
    unpackedExtra.positionVP.xy = unpackedExtra.positionVP.xy / unpackedExtra.positionVP.w;
    unpackedExtra.previousPositionVP.xy = unpackedExtra.previousPositionVP.xy / unpackedExtra.previousPositionVP.w;

    // Calculate velocity
    float2 velocity = (unpackedExtra.positionVP.xy - unpackedExtra.previousPositionVP.xy);
    #if UNITY_UV_STARTS_AT_TOP
        velocity.y = -velocity.y;
    #endif

    // Convert from Clip space (-1..1) to NDC 0..1 space.
    // Note it doesn't mean we don't have negative value, we store negative or positive offset in NDC space.
    // Note: ((positionCS * 0.5 + 0.5) - (previousPositionCS * 0.5 + 0.5)) = (velocity * 0.5)
    return float4(velocity.xy * 0.5, 0, 0);
}

#endif
