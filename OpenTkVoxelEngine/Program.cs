
using System.Numerics;
using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
using OpenTkVoxelEngine.Scenes;
using Buffer = OpenTK.Graphics.OpenGL4.Buffer;

namespace Engine
{
    public static class MainProgram 
    {

        //Programs Entry Point
        static void Main()
        {


            //Create Window Settings
            var nativeWindowSettings = new NativeWindowSettings()
            {
                ClientSize = new Vector2i(1920,1080),
                Title = "Procedural Generation Artifact",
                Flags = ContextFlags.ForwardCompatible,
                Location = new Vector2i(1024,1024)
            };


            //Run The Window Until Its Closed
            using (var window = new Window(GameWindowSettings.Default, nativeWindowSettings))
            {
                window.Run();
            }

        }


    }


    //The Main Window 
    public class Window : GameWindow{
        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
            
        }

        //Imgui Controller
        ImGuiController _controller;

        //Scenes
        static List<IScene> _scenes;
        static IScene ActiveScene;
        int activeSceneIndex = 3;
        
        //Current polygon mode
        PolygonMode _polygonMode = PolygonMode.Fill;
        PolygonMode[] _polygonModes = new []
        {
            PolygonMode.Fill, PolygonMode.Point, PolygonMode.Line
        };

        //Enum of all scenes used to dynamically display imgui buttons
        enum scenes
        {
            Erosion,
            MarchingCubes,
            SurfaceNets,
            NosieVisualization,
            count
        }

        //Called On Startup
        protected override void OnLoad()
        {
            base.OnLoad();

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            
            //Setup imgui controller
            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);

            UpdateFrequency = 0;

            _scenes = new List<IScene>
            {
                new HydraulicErosion(this, _controller),
                new MarchingCubes(this, _controller),
                new SurfaceNets(this,_controller),
                new NoiseVisualization(this,_controller),
                new CubeScene(this,_controller)
            };

            ActiveScene = _scenes[activeSceneIndex];
            ActiveScene.SetActive(true);
        }


        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            _controller.WindowResized(ClientSize.X,ClientSize.Y);
        }

        bool _cursorLocked = true;

        float frames = 0;
        float fps = 0;
        float time = 0;

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            frames++;
            time += (float)args.Time;
            if (time >= 1)
            {
                Title = "FPS: " + (frames).ToString("F1");
                frames = 0;
                time = 0;
            }

            //Update Imgui Controller
            _controller.Update(this, (float)args.Time);


            //Hide debug window off screen
            ImGui.SetWindowPos(new System.Numerics.Vector2(ClientSize.X * 2, ClientSize.Y * 2));
            //Draws Global Imgui
            ImGui.BeginMainMenuBar();
            string sceneName = (ActiveScene.GetType()).ToString();
            ImGui.SetNextItemWidth(100);
            ImGui.Text("Select Scene");
            for (int i = 0; i < (int)scenes.count; i++)
            {
                if (i == activeSceneIndex)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button(Enum.GetName((scenes)i)))
                {
                    ActiveScene.SetActive(false);
                    activeSceneIndex = i;
                    ActiveScene = _scenes[activeSceneIndex];
                    ActiveScene.SetActive(true);
                }
                if (i == activeSceneIndex)
                {
                    ImGui.EndDisabled();
                }
            }
   

            ImGui.Text("Draw Mode");
            for (int i = 0; i < 3; i++)
            {
                if (i == (int)_polygonMode)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button(Enum.GetName(_polygonModes[i])))
                {
                    _polygonMode = _polygonModes[i];
                    GL.PolygonMode(MaterialFace.FrontAndBack,_polygonMode);
                }
                if (i == (int)_polygonMode)
                {
                    ImGui.EndDisabled();
                }
            }

            ImGui.EndMainMenuBar();

        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);


            //Listen For Window Closing
            if (IsKeyPressed(Keys.Escape))
            {
                Close();
            }

            //Handle closing the window
            if (IsKeyDown(Keys.LeftAlt) && _cursorLocked == true)
            {
                _cursorLocked = false;
                CursorState = CursorState.Normal;
            }
            else if (_cursorLocked == false && !IsKeyDown(Keys.LeftAlt))
            {
                _cursorLocked = true;
                CursorState = CursorState.Grabbed;
            }

        }
    }
}
