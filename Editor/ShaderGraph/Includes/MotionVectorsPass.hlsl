#ifndef SG_MOTION_VECTORS_PASS_INCLUDED
#define SG_MOTION_VECTORS_PASS_INCLUDED

struct AttributesExtended
{
    Attributes attributes;
    float3 positionOld          : TEXCOORD4;
};

struct PackedVaryingsExtended
{
    PackedVaryings packedVaryings;
    float4 positionVP           : TEXCOORD1;
    float4 previousPositionVP   : TEXCOORD2;
};

PackedVaryingsExtended vert(AttributesExtended input)
{
    Varyings output = (Varyings)0;
    output = BuildVaryings(input.attributes);
    PackedVaryingsExtended packedOutputExtended = (PackedVaryingsExtended)0;
    packedOutputExtended.packedVaryings = PackVaryings(output);
    // output.previousPositionVP -> packedOutputExtended.previousPositionVP
    // input.position -> input.attributes.positionOS
    //packedOutputExtended.previousPositionVP = mul(_PrevViewProjMatrix, mul(unity_MatrixPreviousM, unity_MotionVectorsParams.x == 1 ? float4(input.positionOld, 1) : input.attributes.positionOS));
    return packedOutputExtended;
}

half4 frag(PackedVaryingsExtended packedInput) : SV_TARGET 
{    
    Varyings unpacked = UnpackVaryings(packedInput.packedVaryings);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);

    SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(unpacked);
    SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

    #if _AlphaClip
        clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
    #endif

    return float4(0.0, 1.0, 1.0, 1.0);

    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }
    
    // Calculate positions
    packedInput.positionVP.xy = packedInput.positionVP.xy / packedInput.positionVP.w;
    packedInput.previousPositionVP.xy = packedInput.previousPositionVP.xy / packedInput.previousPositionVP.w;

    // Calculate velocity
    float2 velocity = (packedInput.positionVP.xy - packedInput.previousPositionVP.xy);
    #if UNITY_UV_STARTS_AT_TOP
        velocity.y = -velocity.y;
    #endif

    // Convert from Clip space (-1..1) to NDC 0..1 space.
    // Note it doesn't mean we don't have negative value, we store negative or positive offset in NDC space.
    // Note: ((positionCS * 0.5 + 0.5) - (previousPositionCS * 0.5 + 0.5)) = (velocity * 0.5)
    return float4(velocity.xy * 0.5, 0, 0);
}

#endif
