using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using System.Threading.Channels;

namespace Aura3D.Core.Renderers;

public class LightPass : RenderPass
{
    Resources.Texture defaultBaseColor;

    Resources.Texture defaultNormal;

    private int directionalLightLimit;
    private int pointLightLimit;
    private int spotLightLimit;
    public void UpdateLightNumLimit(int directionalLightLimit, int pointLightLimit, int spotLightLimit)
    {
        FragmentShader = ShaderResource.MeshFrag
            .Replace("#define MAX_DIRECTIONAL_LIGHTS 4", "#define MAX_DIRECTIONAL_LIGHTS " + directionalLightLimit)
            .Replace("#define MAX_POINT_LIGHTS 4", "#define MAX_POINT_LIGHTS " + pointLightLimit)
            .Replace("#define MAX_SPOT_LIGHTS 4", "#define MAX_SPOT_LIGHTS " + spotLightLimit)
            .Replace("REPEAT_DL_SHADOW_ASSIGN_4//", "REPEAT_DL_SHADOW_ASSIGN_" + directionalLightLimit)
            .Replace("REPEAT_PL_SHADOW_ASSIGN_4//", "REPEAT_PL_SHADOW_ASSIGN_" + pointLightLimit)
            .Replace("REPEAT_SP_SHADOW_ASSIGN_4//", "REPEAT_SP_SHADOW_ASSIGN_" + spotLightLimit);
        foreach (var (key, shader) in Shaders)
        {
            gl.DeleteProgram(shader.ProgramId);
        }
        Shaders.Clear();

        this.directionalLightLimit = directionalLightLimit;
        this.pointLightLimit = pointLightLimit;
        this.spotLightLimit = spotLightLimit;
    }
    

    public float AmbientIntensity = 0.1f;

    public LightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.MeshVert;

        FragmentShader = ShaderResource.MeshFrag;

        ShaderName = nameof(LightPass);

        defaultBaseColor = Resources.Texture.CreateFromColor(Color.White);

        defaultNormal = Resources.Texture.CreateFromColor(Color.FromArgb(255, 128, 128, 255));
    }
    public override void Setup()
    {
        defaultBaseColor.Upload(gl);
        defaultNormal.Upload(gl);
    }

    public override void Destroy()
    {
        defaultBaseColor.Destroy(gl);
        defaultNormal.Destroy(gl);
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.CullFace(TriangleFace.Back);


    }

    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        UseShader();
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);
    }

    protected void SetupUniform(Matrix4x4 view, Matrix4x4 projection)
    {
        SetupCameraUniforms(view, projection);
        SetupDirectionalLights();
        SetupPointLights();
        SetupSpotLights();
    }

    private void SetupCameraUniforms(Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformVector3("cameraPosition", view.Inverse().Translation);
    }

    private void SetupDirectionalLights()
    {
        for (int i = 0; i < directionalLightLimit; i++)
        {
            if (i >= renderPipeline.DirectionalLights.Count)
            {
                SetupInactiveDirectionalLight(i);
            }
            else
            {
                SetupActiveDirectionalLight(i, renderPipeline.DirectionalLights[i]);
            }
        }
    }

    private void SetupInactiveDirectionalLight(int index)
    {
        UniformVector3($"DirectionalLights[{index}].direction", Vector3.Zero);
        UniformVector3($"DirectionalLights[{index}].color", Vector3.Zero);
        UniformTexture($"DirectionalLightShadowMaps[{index}]", 0);
        UniformMatrix4($"DirectionalLights[{index}].shadowMapMatrix", Matrix4x4.Identity);
    }

    private void SetupActiveDirectionalLight(int index, DirectionalLight light)
    {
        UniformVector3($"DirectionalLights[{index}].direction", light.Forward);
        UniformVector3($"DirectionalLights[{index}].color", new Vector3(light.LightColor.R / 255f, light.LightColor.G / 255f, light.LightColor.B / 255f));
        UniformFloat($"DirectionalLights[{index}].castShadow", light.CastShadow ? 1.0f : 0.0f);

        var rt = light.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

        if (light.CastShadow && rt != null)
        {
            var shadowView = Matrix4x4.CreateLookAt(light.WorldTransform.Translation, light.WorldTransform.Translation + light.WorldTransform.ForwardVector(), light.WorldTransform.UpVector());
            var shadowProjection = Matrix4x4.CreateOrthographic(light.ShadowConfig.Width, light.ShadowConfig.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

            UniformTexture($"DirectionalLightShadowMaps[{index}]", rt.DepthStencilTexture);
            UniformMatrix4($"DirectionalLights[{index}].shadowMapMatrix", shadowView * shadowProjection);
        }
        else
        {
            UniformTexture($"DirectionalLightShadowMaps[{index}]", 0);
            UniformMatrix4($"DirectionalLights[{index}].shadowMapMatrix", Matrix4x4.Identity);
        }
    }

    private void SetupPointLights()
    {
        Span<Matrix4x4> shadowViews = stackalloc Matrix4x4[6];

        for (int i = 0; i < pointLightLimit; i++)
        {
            if (i >= renderPipeline.PointLights.Count)
            {
                SetupInactivePointLight(i);
            }
            else
            {
                SetupActivePointLight(i, renderPipeline.PointLights[i], shadowViews);
            }
        }
    }

    private void SetupInactivePointLight(int index)
    {
        UniformVector3($"PointLights[{index}].color", Vector3.Zero);
        UniformVector3($"PointLights[{index}].position", Vector3.Zero);
        UniformFloat($"PointLights[{index}].radius", 0.0f);
        UniformFloat($"PointLights[{index}].softRatio", 0.1f);
        UniformTextureCubeMap($"PointLightShadowMaps[{index}]", 0);

        for (int j = 0; j < 6; j++)
        {
            UniformMatrix4($"PointLights[{index}].shadowMapMatrices[{j}]", Matrix4x4.Identity);
        }
    }

    private void SetupActivePointLight(int index, PointLight light, Span<Matrix4x4> shadowViews)
    {
        UniformVector3($"PointLights[{index}].color", new Vector3(light.LightColor.R / 255f, light.LightColor.G / 255f, light.LightColor.B / 255f));
        UniformVector3($"PointLights[{index}].position", light.WorldTransform.Translation);
        UniformFloat($"PointLights[{index}].radius", light.AttenuationRadius);
        UniformFloat($"PointLights[{index}].softRatio", light.SoftRatio);
        UniformFloat($"PointLights[{index}].castShadow", light.CastShadow ? 1.0f : 0.0f);

        var rt = light.GetPipelineGpuResource<CubeRenderTarget>("ShadowMapRenderTarget");

        if (light.CastShadow && rt != null)
        {
            var position = light.WorldTransform.Translation;

            shadowViews[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
            shadowViews[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
            shadowViews[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            shadowViews[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
            shadowViews[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
            shadowViews[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));

            var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), rt.Width / (float)rt.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

            for (int j = 0; j < 6; j++)
            {
                UniformMatrix4($"PointLights[{index}].shadowMapMatrices[{j}]", shadowViews[j] * shadowProjection);
            }
            UniformTextureCubeMap($"PointLightShadowMaps[{index}]", rt.DepthStencilTexture);
        }
        else
        {
            UniformTextureCubeMap($"PointLightShadowMaps[{index}]", 0);
            for (int j = 0; j < 6; j++)
            {
                UniformMatrix4($"PointLights[{index}].shadowMapMatrices[{j}]", Matrix4x4.Identity);
            }
        }
    }

    private void SetupSpotLights()
    {
        for (int i = 0; i < spotLightLimit; i++)
        {
            if (i >= renderPipeline.SpotLights.Count)
            {
                SetupInactiveSpotLight(i);
            }
            else
            {
                SetupActiveSpotLight(i, renderPipeline.SpotLights[i]);
            }
        }
    }

    private void SetupInactiveSpotLight(int index)
    {
        UniformVector3($"SpotLights[{index}].color", Vector3.Zero);
        UniformVector3($"SpotLights[{index}].position", Vector3.Zero);
        UniformVector3($"SpotLights[{index}].direction", Vector3.Zero);
        UniformFloat($"SpotLights[{index}].inner_cone_cos", 0.0f);
        UniformFloat($"SpotLights[{index}].outer_cone_cos", 0.0f);
        UniformFloat($"SpotLights[{index}].radius", 0.0f);
        UniformFloat($"SpotLights[{index}].softRatio", 0.1f);
        UniformMatrix4($"SpotLights[{index}].shadowMapMatrix", Matrix4x4.Identity);
        UniformTexture($"SpotLightShadowMaps[{index}]", 0);
    }

    private void SetupActiveSpotLight(int index, SpotLight light)
    {
        UniformVector3($"SpotLights[{index}].color", new Vector3(light.LightColor.R / 255f, light.LightColor.G / 255f, light.LightColor.B / 255f));
        UniformVector3($"SpotLights[{index}].position", light.WorldTransform.Translation);
        UniformVector3($"SpotLights[{index}].direction", light.Forward);
        UniformFloat($"SpotLights[{index}].inner_cone_cos", MathF.Cos(light.InnerConeAngleDegree.DegreeToRadians()));
        UniformFloat($"SpotLights[{index}].outer_cone_cos", MathF.Cos(light.OuterAngleDegree.DegreeToRadians()));
        UniformFloat($"SpotLights[{index}].radius", light.AttenuationRadius);
        UniformFloat($"SpotLights[{index}].softRatio", light.SoftRatio);
        UniformFloat($"SpotLights[{index}].castShadow", light.CastShadow ? 1.0f : 0.0f);

        var rt = light.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

        if (light.CastShadow && rt != null)
        {
            var position = light.WorldTransform.Translation;
            var shadowView = Matrix4x4.CreateLookAt(position, position + light.WorldTransform.ForwardVector(), light.WorldTransform.UpVector());
            var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(light.OuterAngleDegree.DegreeToRadians(), rt.Width / (float)rt.Height, light.ShadowConfig.NearPlane, light.ShadowConfig.FarPlane);

            UniformTexture($"SpotLightShadowMaps[{index}]", rt.DepthStencilTexture);
            UniformMatrix4($"SpotLights[{index}].shadowMapMatrix", shadowView * shadowProjection);
        }
        else
        {
            UniformTexture($"SpotLightShadowMaps[{index}]", 0);
            UniformMatrix4($"SpotLights[{index}].shadowMapMatrix", Matrix4x4.Identity);
        }
    }
    public override void AfterRender(Camera camera)
    {
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupUniform(view, projection);


        UniformTexture("BaseColorTexture", mesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor);
        UniformTexture("NormalTexture", mesh.Material?.GetTexture("Normal") ?? defaultNormal);

        if (mesh.Material != null)
        {

            if (mesh.Material.DoubleSided == false)
            {
                gl.Enable(EnableCap.CullFace);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }

            UniformFloat("alphaCutoff", mesh.Material.AlphaCutoff);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            UniformFloat("alphaCutoff", 0.0f);

        }

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        if (mesh.IsSkinnedMesh)
        {
            var skeleton = mesh.Skeleton;
            if (mesh.Model.AnimationSampler != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * mesh.Model.AnimationSampler.BonesTransform[i]);
                }
            }
            else
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skeleton.Bones[i].WorldMatrix);
                }
            }
        }
        
        base.RenderMesh(mesh, view, projection);
    }

}
