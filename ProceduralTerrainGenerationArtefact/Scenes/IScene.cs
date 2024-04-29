using Dear_ImGui_Sample;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using GL = OpenTK.Graphics.OpenGL4.GL;

namespace OpenTkVoxelEngine
{
    /// <summary>
    /// IScene is a interface that all scenes containing content have in common, implementing all basic functions needed to draw and update a scene aswell as handling the update and render loop code for each scene
    /// </summary>
    internal abstract class IScene
    {
        public IScene(GameWindow window, ImGuiController controller)
        {
            _window = window;
            _controller = controller;
        }

        /// <summary>
        /// Subscribes to all window callbacks
        /// </summary>
        private void AddListeners()
        {
            _window.Unload += OnUnload;
            _window.UpdateFrame += OnUpdateFrame;
            _window.RenderFrame += OnRenderFrame;
            _window.Resize += OnResize;
            _window.MouseWheel += OnMouseWheel;
        }

        /// <summary>
        /// unsubs from all window callbacks
        /// </summary>
        private void RemoveListeners()
        {
            _window.Unload -= OnUnload;
            _window.UpdateFrame -= OnUpdateFrame;
            _window.RenderFrame -= OnRenderFrame;
            _window.Resize -= OnResize;
            _window.MouseWheel -= OnMouseWheel;
        }

        public void SetActive(bool value)
        {
            if (value)
            {
                AddListeners();
                if (!initalized) OnLoad();
                initalized = true;
            }
            else
            {
                RemoveListeners();
                OnUnload();
            }
        }

        private bool initalized = false;
        protected GameWindow _window;
        protected ImGuiController _controller;

        public abstract void OnUpdateFrame(FrameEventArgs args);

        public abstract void OnRenderFrame(FrameEventArgs args);

        public abstract void OnMouseWheel(MouseWheelEventArgs e);

        public void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);
        }

        public abstract void DrawImgui();

        public abstract void OnUnload();

        public abstract void OnLoad();
    }
}