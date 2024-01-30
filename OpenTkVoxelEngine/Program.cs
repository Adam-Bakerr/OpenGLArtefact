
using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
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
                Title = "StoxelEngine",
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

        int activeSceneIndex = 0;

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
            //VSync = VSyncMode.On;

            _scenes = new List<IScene>
            {
                new HydraulicErosion(this, _controller),
                new MarchingCubes(this, _controller),
                new SurfaceNets(this,_controller),
                new NoiseVisualization(this,_controller)
            };


            ActiveScene = _scenes[3];
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

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            //Update Imgui Controller
            _controller.Update(this, (float)args.Time);


            //Draws Global Imgui
            ImGui.BeginMainMenuBar();
            string sceneName = (ActiveScene.GetType()).ToString();
            ImGui.SetNextItemWidth(100);
            ImGui.BeginMenu("Select Scene");
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
            ImGui.EndMenu();
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


            Title = (1 / args.Time).ToString();



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
