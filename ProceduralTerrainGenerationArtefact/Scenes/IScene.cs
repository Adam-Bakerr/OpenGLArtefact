﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using static System.Runtime.InteropServices.JavaScript.JSType;
using GL = OpenTK.Graphics.OpenGL4.GL;
namespace OpenTkVoxelEngine
{
    abstract class IScene
    {
        public IScene(GameWindow window, ImGuiController controller)
        {
            _window = window;
            _controller = controller;
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

        public void SetActive(bool value) 
        { 
            if(value) 
            {
                AddListeners(); 
                if(!initalized) OnLoad();
                initalized = true;
            }else
            {
                RemoveListeners();
                OnUnload();
            }

        }

        bool initalized = false;
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