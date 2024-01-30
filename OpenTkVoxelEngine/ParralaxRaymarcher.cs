using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace OpenTkVoxelEngine
{
    internal class ParralaxRaymarcher : IScene
    {
        int _vbo;
        int _ebo;
        VAO _vao;

        Camera _camera;

        Shader vertFragShader;

        float[] vertices = {
            -0.5f, -0.5f, -0.5f, 1.0f,
            0.5f, -0.5f, -0.5f,  1.0f,
            0.5f,  0.5f, -0.5f,  1.0f,
            0.5f,  0.5f, -0.5f,  1.0f,
            -0.5f,  0.5f, -0.5f, 1.0f,
            -0.5f, -0.5f, -0.5f, 1.0f,

            -0.5f, -0.5f,  0.5f, 1.0f,
            0.5f, -0.5f,  0.5f,  1.0f,
            0.5f,  0.5f,  0.5f,  1.0f,
            0.5f,  0.5f,  0.5f,  1.0f,
            -0.5f,  0.5f,  0.5f, 1.0f, 
            -0.5f, -0.5f,  0.5f, 1.0f, 

            -0.5f,  0.5f,  0.5f, 1.0f, 
            -0.5f,  0.5f, -0.5f, 1.0f, 
            -0.5f, -0.5f, -0.5f, 1.0f, 
            -0.5f, -0.5f, -0.5f, 1.0f, 
            -0.5f, -0.5f,  0.5f, 1.0f, 
            -0.5f,  0.5f,  0.5f, 1.0f, 

            0.5f,  0.5f,  0.5f,  1.0f,
            0.5f,  0.5f, -0.5f,  1.0f,
            0.5f, -0.5f, -0.5f,  1.0f,
            0.5f, -0.5f, -0.5f,  1.0f,
            0.5f, -0.5f,  0.5f,  1.0f,
            0.5f,  0.5f,  0.5f,  1.0f,

            -0.5f, -0.5f, -0.5f, 1.0f, 
            0.5f, -0.5f, -0.5f,  1.0f,
            0.5f, -0.5f,  0.5f,  1.0f,
            0.5f, -0.5f,  0.5f,  1.0f,
            -0.5f, -0.5f,  0.5f, 1.0f, 
            -0.5f, -0.5f, -0.5f, 1.0f, 

            -0.5f,  0.5f, -0.5f, 1.0f, 
            0.5f,  0.5f, -0.5f,  1.0f,
            0.5f,  0.5f,  0.5f,  1.0f,
            0.5f,  0.5f,  0.5f,  1.0f,
            -0.5f,  0.5f,  0.5f, 1.0f, 
            -0.5f,  0.5f, -0.5f, 1.0f 
        };

        public ParralaxRaymarcher(GameWindow window, ImGuiController controller) : base(window, controller)
        {

        }

        void CreateBuffers()
        {
            //Create vertex buffer object
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * vertices.Length, vertices, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _vbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //create vao
            _vao = new VAO();

            // Tell the shader which numbers mean what in the buffer
            List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (vertFragShader.GetAttribLocation("aPosition"), 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0),
            };
            _vao.Enable(Pointers);

            //Bind our vao before creating a ebo
            _vao.Bind();
            GL.BindVertexArray(_vao._objectHandle);

        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            _camera.OnUpdateFrame(args);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _vao.Bind();

            vertFragShader.Use();
            vertFragShader.SetMatrix4("model", Matrix4.Identity);
            vertFragShader.SetMatrix4("view", _camera.View());
            vertFragShader.SetMatrix4("projection", _camera.Projection());
            vertFragShader.SetVec3("cameraForward", _camera.Forward());
            vertFragShader.SetVec3("cameraRight", _camera.Right());
            vertFragShader.SetVec3("cameraUp", _camera.Up());
            vertFragShader.SetVec3("cameraPosition", _camera.Position());
            vertFragShader.SetIVec2("res", _window.Size);

            GL.DrawArrays(PrimitiveType.Triangles,0,vertices.Length);
            _window.SwapBuffers();
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
        }

        public override void DrawImgui()
        {
        }

        public override void OnUnload()
        {
        }

        public override void OnLoad()
        {
            _camera = new Camera(_window, 0.01f, 100f);
            vertFragShader = new Shader("rayMarcher.vert", "rayMarcher.frag");

            CreateBuffers();
        }
    }
}
