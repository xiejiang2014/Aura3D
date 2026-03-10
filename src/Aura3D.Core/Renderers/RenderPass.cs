using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public partial class RenderPass
{
    public RenderPass(RenderPipeline renderPipeline)
    {
        this.renderPipeline = renderPipeline;

        ShaderName = GetType().Name;
    }

    protected RenderPipeline renderPipeline;

    protected Scene Scene => renderPipeline.Scene;

    protected List<Mesh> Meshes => renderPipeline.Meshes;

    protected List<PointLight> PointLights => renderPipeline.PointLights;

    protected List<SpotLight> SpotLights => renderPipeline.SpotLights;
    
    protected List<Mesh> VisibleMeshesInCamera => renderPipeline.VisibleMeshesInCamera;
    protected GL gl => renderPipeline.gl!;
    public virtual void Setup()
    {

    }

    public virtual void Destory()
    {

    }
    public bool EnableFrustumCulling => renderPipeline.EnableFrustumCulling;

    public virtual void BeforeRender(Camera camera)
    {

    }
    public virtual void Render(Camera camera)
    {

    }

    public virtual void AfterRender(Camera camera)
    {

    }


    public virtual void BeforeRender()
    {

    }
    public virtual void Render()
    {

    }

    public virtual void AfterRender()
    {

    }

    protected string? outputRenderTargetName;
    public RenderPass SetOutPutRenderTarget(string? renderTargetName)
    {
        this.outputRenderTargetName = renderTargetName;

        return this;
    }

    public void BindOutPutRenderTarget(Camera camera)
    {
        uint fbo = 0;

        if (outputRenderTargetName != null)
        {
            var rt = GetRenderTarget(outputRenderTargetName,
                new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));
            fbo = rt.FrameBufferId;
        }
        else
        {
            fbo = camera.RenderTarget.FrameBufferId;
        }
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        gl.Viewport(0, 0, camera.RenderTarget.Width, camera.RenderTarget.Height);
    }

    public RenderTarget GetRenderTarget(string name, Size size) => renderPipeline.GetRenderTarget(name, size);

    public unsafe virtual void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("modelMatrix", mesh.WorldTransform);
        gl.BindVertexArray(mesh.Geometry!.Vao);

        if (mesh != null && mesh.Material != null && mesh.Material.HasShader == true)
        {
            var callback = mesh.Material.GetShaderPassParametersCallback(ShaderName);
            if (callback != null)
            {
                callback(this);
            }
        }
        gl.DrawElements(GLEnum.Triangles, (uint)mesh.Geometry.IndicesCount, GLEnum.UnsignedInt, (void*)0);
    }

    public void RenderMeshes(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        foreach (var mesh in renderPipeline.Meshes)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }
    
    public void RenderVisibleMeshesInCamera(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        RenderMeshesFromList(VisibleMeshesInCamera, filter, view, projection);
    }

    public void RenderMeshesFromList(List<Mesh> meshes, Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        foreach (var mesh in meshes)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }
    
    List<Mesh> meshes = new List<Mesh>();
    Plane[] planes = new Plane[6];
    public void RenderStaticMeshes(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        var list = renderPipeline.Meshes;

        if (EnableFrustumCulling == true)
        {
            meshes.Clear();

            renderPipeline.UpdateVisibleMeshesInCamera(view, projection, meshes);

            list = meshes;

        }
        foreach (var mesh in list)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (mesh.IsSkinnedMesh == true)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }

    public void RenderSkinnedMeshes(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        var list = renderPipeline.Meshes;

        if (EnableFrustumCulling == true)
        {
            meshes.Clear();

            renderPipeline.UpdateVisibleMeshesInCamera(view, projection, meshes);

            list = meshes;

        }
        foreach (var mesh in list)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (mesh.IsSkinnedMesh == false)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }

    protected bool IsMaterialBlendMode(Mesh mesh, BlendMode mode)
    {
        if (mesh.Material == null)
            if (mode == BlendMode.Opaque)
                return true;
            else
                return false;
        else
        {
            if (mesh.Material.BlendMode == mode)
                return true;
            return false;

        }
    }

    public virtual void SortMeshes(List<Mesh> Meshes, Camera camera)
    {
        renderPipeline.SortMeshes(Meshes, camera);
    }    

    public void RenderCube()
    {
        renderPipeline.RenderCube();
    }

    public void RenderQuad()
    {
        renderPipeline.RenderQuad();
    }

    public void Destroy()
    { 
        foreach(var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }

}

public class RenderPass<T> : RenderPass where T : RenderPipeline
{
    public RenderPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
    }

    public T RenderPipeline => (T)renderPipeline;
}