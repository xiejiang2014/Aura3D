using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class BrdfLutPass : RenderPass
{
    private RenderTarget renderTarget;

    private bool _isSetup = false;

    public BrdfLutPass(RenderPipeline renderPipeline, RenderTarget output) : base(renderPipeline)
    {
        renderTarget = output;

        VertexShader = @"#version 300 es
precision highp float;

layout(location = 0) in vec3 a_position;
layout(location = 1) in vec2 a_texCoord;

out vec2 v_texCoord;

void main() {
    gl_Position = vec4(a_position, 1.0);
    v_texCoord = a_texCoord;
}";

        FragmentShader = @"#version 300 es
precision highp float;

in vec2 v_texCoord;
const float PI = 3.14159265359;

layout(location = 0) out vec2 o_brdf;

float GeometrySchlickGGX(float NdotV, float roughness) {
    float a = roughness;
    float k = (a * a) / 2.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(float NdotV, float NdotL, float roughness) {
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness) {
    float a = roughness * roughness;

    float phi = 2.0 * PI * Xi.x;
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a*a - 1.0) * Xi.y));
    float sinTheta = sqrt(1.0 - cosTheta*cosTheta);

    vec3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    vec3 up        = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent   = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);

    vec3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}



float RadicalInverse_VdC(uint bits) {
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10; // 1/2^32
}
vec2 Hammersley(uint i, uint N) {
    return vec2(float(i)/float(N), RadicalInverse_VdC(i));
}

void main() {
    float NdotV = v_texCoord.x;
    float roughness = v_texCoord.y;

    vec3 V;
    V.x = sqrt(1.0 - NdotV*NdotV); // sin(theta)
    V.y = 0.0;
    V.z = NdotV;                   // cos(theta)

    const uint SAMPLE_COUNT = 1024u;
    vec2 integrateBRDF = vec2(0.0, 0.0);

    vec3 N = vec3(0.0, 0.0, 1.0);

    for (uint i = 0u; i < SAMPLE_COUNT; i++) {
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);
        vec3 H = ImportanceSampleGGX(Xi, N, roughness);
        vec3 L = normalize(2.0 * dot(V, H) * H - V);

        float NdotL = max(dot(N, L), 0.0);
        float NdotH = max(dot(N, H), 0.0);
        float VdotH = max(dot(V, H), 0.0);

        if (NdotL > 0.0) {
            float G = GeometrySmith(NdotV, NdotL, roughness);
            float G_Vis = G * VdotH / (NdotH * NdotV);
            float Fc = pow(1.0 - VdotH, 5.0);

            integrateBRDF.x += (1.0 - Fc) * G_Vis;
            integrateBRDF.y += Fc * G_Vis;
        }
    }

    integrateBRDF /= float(SAMPLE_COUNT);

    o_brdf = integrateBRDF;
}
";
    }

    public override void Setup()
    {
        renderTarget.Upload(gl);
    }


    public override void Render()
    {
        if (_isSetup)
            return;
        _isSetup = true;

        gl.BindFramebuffer(Silk.NET.OpenGLES.FramebufferTarget.Framebuffer, renderTarget.FrameBufferId);

        gl.ClearColor(1, 1, 1, 1);
        gl.ClearDepth(1);
        gl.Clear((uint)(Silk.NET.OpenGLES.ClearBufferMask.ColorBufferBit | Silk.NET.OpenGLES.ClearBufferMask.DepthBufferBit));

        gl.Viewport(0, 0, (uint)renderTarget.Width, (uint)renderTarget.Height);
        UseShader();
        UseShader_Internal(null);
        RenderQuad();
    }
}
