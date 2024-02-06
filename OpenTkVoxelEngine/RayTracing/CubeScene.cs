using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static OpenTkVoxelEngine.HydraulicErosion;
using GL = OpenTK.Graphics.OpenGL4.GL;
using IntPtr = System.IntPtr;

namespace OpenTkVoxelEngine.Scenes
{
    internal class CubeScene : IScene
    {



        private readonly float[] _vertices =
        {
            // Position
            0, 0, 0, 0.0f,0.0f,-1.0f,// Front face
            1f, 0, 0, 0.0f,0.0f,-1.0f,
            1f,  1f, 0, 0.0f,0.0f,-1.0f,
            1f,  1f, 0, 0.0f,0.0f,-1.0f,
            0,  1f, 0, 0.0f,0.0f,-1.0f,
            0, 0, 0, 0.0f,0.0f,-1.0f,

            0, 0,  1f, 0.0f,0.0f,1.0f, // Back face
            1f, 0,  1f, 0.0f,0.0f,1.0f,
            1f,  1f,  1f, 0.0f,0.0f,1.0f,
            1f,  1f,  1f, 0.0f,0.0f,1.0f,
            0,  1f,  1f, 0.0f,0.0f,1.0f,
            0, 0,  1f, 0.0f,0.0f,1.0f,

            0,  1f,  1f, -1.0f,0.0f,0.0f,// Left face
            0,  1f, 0, -1.0f,0.0f,0.0f,
            0, 0, 0, -1.0f,0.0f,0.0f,
            0, 0, 0, -1.0f,0.0f,0.0f,
            0, 0,  1f, -1.0f,0.0f,0.0f,
            0,  1f,  1f, -1.0f,0.0f,0.0f,

            1f,  1f,  1f, 1.0f,0.0f,0.0f, // Right face
            1f,  1f, 0, 1.0f,0.0f,0.0f,
            1f, 0, 0, 1.0f,0.0f,0.0f,
            1f, 0, 0, 1.0f,0.0f,0.0f,
            1f, 0,  1f, 1.0f,0.0f,0.0f,
            1f,  1f,  1f, 1.0f,0.0f,0.0f,

            0, 0, 0, 0.0f,-1.0f,0.0f, // Bottom face
            1f, 0, 0, 0.0f,-1.0f,0.0f,
            1f, 0,  1f, 0.0f,-1.0f,0.0f,
            1f, 0,  1f, 0.0f,-1.0f,0.0f,
            0, 0,  1f, 0.0f,-1.0f,0.0f,
            0, 0, 0, 0.0f,-1.0f,0.0f,

            0,  1f, 0, 0.0f,1.0f,0.0f, // Top face
            1f,  1f, 0, 0.0f,1.0f,0.0f,
            1f,  1f,  1f, 0.0f,1.0f,0.0f,
            1f,  1f,  1f, 0.0f,1.0f,0.0f,
            0,  1f,  1f, 0.0f,1.0f,0.0f,
            0,  1f, 0, 0.0f,1.0f,0.0f
        };


        public int _voxelDimensions = 256;
        public float chunkSize = 80.0f;
        float time = 0f;

        int _vbo;
        VAO _vao;



        //Shaders
        Shader _shader;
        ComputeShader _tracer;
        string _assemblyName = "OpenTkVoxelEngine.Shaders.ray";
        string _fragName = "shader.frag";
        string _vertName = "shader.vert";
        string _tracerName = "VoxelRaytrace.compute";

        //Camera
        Camera _camera;

        //3D Texture
        int _screenTexture;
        int _demoTexture;

        public CubeScene(GameWindow window, ImGuiController controller) : base(window, controller)
        {
        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            _camera.OnUpdateFrame(args);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Clear the screen
            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            time += (float)args.Time;

            _tracer.use();
            UpdateTracerShader();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _screenTexture);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture3D,_demoTexture);

            GL.DispatchCompute((int)Math.Ceiling(_window.ClientSize.X / 32.0f), (int)Math.Ceiling(_window.ClientSize.Y / 32.0f), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

            //Set MVP matrix of shader
            UpdateDrawShader();

            GL.BindTexture(TextureTarget.Texture2D, _screenTexture);

            GL.DrawArrays(PrimitiveType.Triangles,0,6);

            _window.SwapBuffers();
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            _camera.OnMouseWheel(e);
        }

        public override void DrawImgui()
        {
        }

        public override void OnUnload()
        {
        }

        public override void OnLoad()
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
                
            _camera = new Camera(_window, 0.1f, 1000f,new Vector3(.5f, .5f,-.5f));
            CreateShaders();
            CreateBuffers();
            CreateTexture();
        }

        Random rand;
        public void CreateTexture()
        {
            rand = new Random();
            

            _demoTexture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture3D, _demoTexture);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            byte[] textureData = new byte[_voxelDimensions* _voxelDimensions * _voxelDimensions * 4];
            int counter = 0;
            Vector3 Center = new Vector3(_voxelDimensions / 2f);
            for (int x = 0; x < _voxelDimensions; x++)
            {
                for (int z = 0; z < _voxelDimensions; z++)
                {
                    for (int y = 0; y < _voxelDimensions; y++)
                    {
                        int index = (y * (_voxelDimensions * _voxelDimensions) + z * _voxelDimensions + x) * 4;

                        //bool isSolid = y % 8 == 0 && x % 8 == 0 && z % 8 == 0;
                        bool isSolid = Vector3.DistanceSquared(new Vector3(x, y, z), Center) < (_voxelDimensions * _voxelDimensions) / 6f;

                        textureData[index] = isSolid ? (byte)(rand.NextDouble() * 255f) : (byte)0; //r
                        textureData[index + 1] = isSolid ? (byte)(rand.NextDouble() * 255f) : (byte)0; //g
                        textureData[index + 2] = isSolid ? (byte)(rand.NextDouble() * 255f) : (byte)0; //b
                        textureData[index + 3] = (byte)255; //a

                        counter += Convert.ToInt32(isSolid);
                    }
                }
            }
            Console.WriteLine(counter);
            GL.TexImage3D(TextureTarget.Texture3D, 0, PixelInternalFormat.Rgba32f, _voxelDimensions, _voxelDimensions, _voxelDimensions, 0, PixelFormat.Rgba, PixelType.UnsignedByte, textureData);
            GL.BindImageTexture(1, _demoTexture, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

            _screenTexture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D,_screenTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, _window.ClientSize.X, _window.ClientSize.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.BindImageTexture(0, _screenTexture, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

        }

        public void CreateShaders()
        {
            _shader = new Shader(_assemblyName, _vertName, _fragName);
            UpdateDrawShader();

            _tracer = new ComputeShader(_assemblyName, _tracerName);
            UpdateTracerShader();
        }

        public void UpdateDrawShader()
        {
            _shader.Use();
            /*_shader.SetMatrix4("model", Matrix4.Identity);
            _shader.SetMatrix4("view", _camera.View());
            _shader.SetMatrix4("projection", _camera.Projection());

            _shader.SetVec2("dimensions",_window.ClientSize);

            _shader.SetVec3("cameraForward", _camera.Forward());
            _shader.SetVec3("cameraRight", _camera.Right());
            _shader.SetVec3("cameraUp", _camera.Up());
            _shader.SetVec3("cameraPos",_camera.Position());


            _shader.SetIVec3("textureDims",Vector3i.One * _voxelDimensions); 
            _shader.SetVec3("position", Vector3.Zero);
            _shader.SetFloat("voxelSize", 1 / (float)_voxelDimensions);
            _shader.SetFloat("chunkSize", chunkSize);*/
        }

        public void UpdateTracerShader()
        {
            _tracer.use();
            
            _tracer.SetVec2("dimensions", _window.ClientSize);

            _tracer.SetVec3("cameraForward", _camera.Forward());
            _tracer.SetVec3("cameraRight", _camera.Right());
            _tracer.SetVec3("cameraUp", _camera.Up());
            _tracer.SetVec3("ro", _camera.Position());


            _tracer.SetIVec3("textureDims", Vector3i.One * _voxelDimensions);
            _tracer.SetVec3("position", Vector3.Zero);
            _tracer.SetFloat("voxelSize", 1 / (float)_voxelDimensions);
            _tracer.SetFloat("chunkSize", chunkSize);
        }

        public void CreateBuffers()
        {
            //temp scale verts
            for (int i = 0; i < _vertices.Length; i += 6)
            {
                _vertices[i] *= chunkSize;
                _vertices[i + 1] *= chunkSize;
                _vertices[i + 2] *= chunkSize;
            }


            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer,_vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,sizeof(float) * _vertices.Length, _vertices,BufferUsageHint.StaticCopy);


            _vao = new VAO();

            // Tell the shader which numbers mean what in the buffer
            List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (_shader.GetAttribLocation("aPosition"), 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0),
                (_shader.GetAttribLocation("aNormal"), 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float)),
            };
            _vao.Enable(Pointers);

            //Bind our vao before creating a ebo
            _vao.Bind();
        }
    }
}
