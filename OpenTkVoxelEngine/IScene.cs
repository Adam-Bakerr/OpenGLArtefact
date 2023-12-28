using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Windowing.Desktop;

namespace OpenTkVoxelEngine
{
    abstract class IScene
    {
        public IScene(GameWindow window)
        {
            _window = window;
        }

        void AddListeners()
        {
            _window.Unload += OnUnload;
            _window.UpdateFrame += OnUpdateFrame;
            _window.RenderFrame += OnRenderFrame;
            _window.Resize += OnResize;
            _window.MouseWheel += OnMouseWheel;
        }

        void RemoveListeners()
        {
            _window.Unload -= OnUnload;
            _window.UpdateFrame -= OnUpdateFrame;
            _window.RenderFrame -= OnRenderFrame;
            _window.Resize -= OnResize;
            _window.MouseWheel -= OnMouseWheel;
        }

        public void SetActive(bool value) { if(value)
        {
            AddListeners(); 
            OnLoad();
        }else
        {
            RemoveListeners();
            OnUnload();
        }

        }

        protected GameWindow _window;

        public abstract void OnUpdateFrame(FrameEventArgs args);
        public abstract void OnRenderFrame(FrameEventArgs args);
        public abstract void OnMouseWheel(MouseWheelEventArgs e);
        public abstract void OnResize(ResizeEventArgs e);
        public abstract void OnUnload();
        public abstract void OnLoad();
    }


}
