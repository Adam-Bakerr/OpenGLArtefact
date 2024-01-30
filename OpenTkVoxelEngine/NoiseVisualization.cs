using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static OpenTkVoxelEngine.HydraulicErosion;
using GL = OpenTK.Graphics.OpenGL4.GL;
using IntPtr = System.IntPtr;

namespace OpenTkVoxelEngine
{
    internal class NoiseVisualization : IScene
    {
        Camera _camera;

        VAO _vao;
        int _vbo;

        Shader _shader;
        string _assemblyPath = "OpenTkVoxelEngine.Shaders.NoiseVisualization";
        string _vertexFileName = "shader.vert";
        string _fragmentFileName = "shader.frag";

        float _time = 0f;

        public NoiseVisualization(GameWindow window, ImGuiController controller) : base(window, controller)
        {
        }

        public void CreateScreenShader()
        {
            _shader = new Shader(_assemblyPath, _vertexFileName, _fragmentFileName);
            _shader.Use();
        }

        public void CreateBuffers()
        {
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * 4 * 3, nint.Zero, BufferUsageHint.StaticDraw);

            _vao = new VAO();

            // Tell the shader which numbers mean what in the buffer
            List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (_shader.GetAttribLocation("aPosition"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 0)
            };
            _vao.Enable(Pointers);

        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            _camera.OnUpdateFrame(args);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            _time += (float)args.Time;

            _shader.Use();
            _shader.SetFloat("time",_time);
            _shader.SetVec2("dimensions",_window.ClientSize);
            _shader.SetVec3("iro",_camera.Position());
            _shader.SetVec3("cameraForward", _camera.Forward());
            _shader.SetVec3("cameraUp", _camera.Up());
            _shader.SetVec3("cameraRight", _camera.Right());
            GL.DrawArrays(PrimitiveType.Triangles,0,6);
            _window.SwapBuffers();
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            _camera.OnMouseWheel(e);
        }

        public override void OnUnload()
        {
        }

        public override void OnLoad()
        {
            _camera = new Camera(_window, 0.1f, 100);
            CreateScreenShader();
            CreateBuffers();
        }
    }
}
