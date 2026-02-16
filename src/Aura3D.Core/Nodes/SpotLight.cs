using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

public class SpotLight : Light
{
    public SpotLight()
    {
    }

    public ShadowConfig ShadowConfig = new ShadowConfig
    {
        NearPlane = 1,
        FarPlane = 100
    };

    public float InnerConeAngleDegree { get; set; } = 10;
    public float OuterAngleDegree { get; set; } = 15;

    public float LuminousIntensity { get; set; } = 1000;

    public float Intensity => LuminousIntensity * 0.001f;

    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径
    public float SoftRatio { get; set; } = 0.9f; // 阴影柔化半径

}
