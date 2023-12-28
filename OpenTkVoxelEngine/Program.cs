
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
                ClientSize = new Vector2i(800,600),
                Title = "StoxelEngine",
                Flags = ContextFlags.ForwardCompatible
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

        
        //Scenes
        static List<IScene> _scenes;
        static IScene ActiveScene;

        //Called On Startup
        protected override void OnLoad()
        {
            base.OnLoad();

            UpdateFrequency = 0;
            //VSync = VSyncMode.On;

            _scenes = new List<IScene>();
            _scenes.Add(new CubeScene(this));
            _scenes.Add(new VoxelScene(this));


            ActiveScene = _scenes[1];
            ActiveScene.SetActive(true);
        }


        bool _cursorLocked = true;

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            //Listen For Window Closing
            if (IsKeyPressed(Keys.Escape))
            {
                Close();
            }


            Title = (1 / args.Time).ToString();

            //Allow for scene switching
            if (IsKeyPressed(Keys.Right) && IsKeyDown(Keys.LeftAlt))
            {
                ActiveScene.SetActive(false);
                ActiveScene = _scenes[1];
                ActiveScene.SetActive(true);
            }
            if (IsKeyPressed(Keys.Left) && IsKeyDown(Keys.LeftAlt))
            {
                ActiveScene.SetActive(false);
                ActiveScene = _scenes[0];
                ActiveScene.SetActive(true);
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
