using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Avalonia;

public class Aura3DView<T> : Aura3DView where T : IRenderPipelineCreateInstance
{ 
    public Aura3DView()
    {
        CreateRenderPipeline = T.CreateInstance;
    }

}
public class Aura3DView : Aura3DViewBase
{
    public UpdateRoutedEventArgs? updateRoutedEventArgs;

    public Aura3DView()
    {
    }


    public static readonly RoutedEvent<RoutedEventArgs> SetupPipelineEvent =
      RoutedEvent.Register<Aura3DView, RoutedEventArgs>(nameof(SceneInitialized), RoutingStrategies.Direct);

    public event EventHandler<RoutedEventArgs> SetupPipeline
    {
        add => AddHandler(SetupPipelineEvent, value);
        remove => RemoveHandler(SetupPipelineEvent, value);
    }


    public static readonly RoutedEvent<InitializedRoutedEventArgs> SceneInitializedEvent =
      RoutedEvent.Register<Aura3DView, InitializedRoutedEventArgs>(nameof(SceneInitialized), RoutingStrategies.Direct);

    public event EventHandler<InitializedRoutedEventArgs> SceneInitialized
    {
        add => AddHandler(SceneInitializedEvent, value);
        remove => RemoveHandler(SceneInitializedEvent, value);
    }

    public static readonly RoutedEvent<DestroyedRoutedEventArgs> SceneDestroyedEvent =
     RoutedEvent.Register<Aura3DView, DestroyedRoutedEventArgs>(nameof(SceneDestroyed), RoutingStrategies.Direct);


    public event EventHandler<DestroyedRoutedEventArgs> SceneDestroyed
    {
        add => AddHandler(SceneInitializedEvent, value);
        remove => RemoveHandler(SceneInitializedEvent, value);
    }

    public static readonly RoutedEvent<UpdateRoutedEventArgs> OnSceneUpdatedEvent =
     RoutedEvent.Register<Aura3DView, UpdateRoutedEventArgs>(nameof(SceneUpdated), RoutingStrategies.Direct);

    public event EventHandler<UpdateRoutedEventArgs> SceneUpdated
    {
        add => AddHandler(OnSceneUpdatedEvent, value);
        remove => RemoveHandler(OnSceneUpdatedEvent, value);
    }
    protected override void OnOpenGlInit(GlInterface gl)
    {
        RoutedEventArgs args = new RoutedEventArgs(SetupPipelineEvent);
        RaiseEvent(args);
        base.OnOpenGlInit(gl);
    }

    protected override void OnSceneInitialized()
    {
        updateRoutedEventArgs = new UpdateRoutedEventArgs(OnSceneUpdatedEvent, Scene!);
        RoutedEventArgs args = new InitializedRoutedEventArgs(SceneInitializedEvent, Scene!);
        RaiseEvent(args);
    }

    protected override void OnSceneDestroyed()
    {
        RoutedEventArgs args = new DestroyedRoutedEventArgs(SceneDestroyedEvent, Scene!);
        RaiseEvent(args);
    }

    protected override void OnSceneUpdated(double deltaTime)
    {
        updateRoutedEventArgs!.DeltaTime = deltaTime;
        RaiseEvent(updateRoutedEventArgs);
    }
}


public class UpdateRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public double DeltaTime { get; set; }
    public UpdateRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

public class InitializedRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public InitializedRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}
public class DestroyedRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public DestroyedRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}