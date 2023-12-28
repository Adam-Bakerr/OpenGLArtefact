using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using static System.Runtime.InteropServices.JavaScript.JSType;
using GL = OpenTK.Graphics.OpenGL4.GL;

namespace OpenTkVoxelEngine
{
    class VoxelScene : IScene
    {

        // Create a single full screen triangle
        float[] vertices = {
            0.0f,  3.0f,
            -3.0f, -3.0f,
            3.0f, -3.0f
        };

        //Buffers
        VBO vbo;
        VAO vao;
        SSBO ssbo;

        //Shaders
        Shader rayShader;
        ComputeShader computeShader;

        //Camera
        Camera camera;

        //Textures
        Texture texture;


        //Sparse Voxel Octree
        SVO svo;

        public VoxelScene(GameWindow window) : base(window)
        {

        }

        void CreateShader()
        {
            rayShader = new Shader("raytrace.vert", "raytrace.frag");
            computeShader = new ComputeShader("ray.compute");
        }

        int TextureWidth = 1920, TextureHeight = 1080;
        int _texture;

        void CreateTextures()
        {
            //texture = Texture.LoadFromFile("smile.jpg", TextureUnit.Texture0);
            //texture.Use(TextureUnit.Texture0);



            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba32f,TextureWidth,TextureHeight,0,PixelFormat.Rgba,PixelType.Float, (nint)null);

            GL.BindImageTexture(0,_texture,0,false,0,TextureAccess.ReadWrite,SizedInternalFormat.Rgba32f);

            rayShader.SetInt("texture0",0);
        }

        void CreateBuffers()
        {
            //Create the buffers for the full screen triangle
            vbo = new VBO();
            vbo.BufferData(vertices,BufferUsageHint.StaticDraw);

            vao = new VAO();
            vao.Enable(new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (rayShader.GetAttribLocation("aPosition"), 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0),
            });

            ssbo = new SSBO();
            var test = OctreeNode.SizeOfNode();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo._objectHandle);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, svo.nodes.Count * OctreeNode.SizeOfNode(), svo.nodes.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, ssbo._objectHandle);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        //Fix full screen triangle

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            camera.OnUpdateFrame(args);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Clear only the color buffer
            GL.Clear(ClearBufferMask.ColorBufferBit);


            //Use The Texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);

            computeShader.use();

            computeShader.SetVec3("position",camera.Position());

            computeShader.SetVec3("CameraForward", camera.Forward());
            computeShader.SetVec3("CameraRight", camera.Right());
            computeShader.SetVec3("CameraUp", camera.Up());
            computeShader.SetInt("bufferSize",svo.nodes.Count);

            GL.DispatchCompute(TextureWidth,TextureHeight,1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            rayShader.Use();
     
            vao.Bind();

            //Draw the full screen triangle
            GL.DrawArrays(PrimitiveType.Triangles,0,vertices.Length);

            _window.SwapBuffers();

        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            camera.OnMouseWheel(e);
        }

        public override void OnResize(ResizeEventArgs e)
        {
        }

        public override void OnUnload()
        {
            rayShader?.Dispose();
            texture?.Dispose();
            vbo?.Dispose();
            vao?.Dispose();

        }

        public override void OnLoad()
        {
            camera = new Camera(_window);


            svo = new SVO(Vector3.UnitZ * 10f, Vector3.One * 20f, 3);


            //Disable Z Depth Testing
            GL.Disable(EnableCap.DepthTest);

            CreateShader();
            CreateBuffers();
            CreateTextures();
        }

        public void SetActive(bool condition)
        {

        }

        ~VoxelScene()
        {

        }
    }
}
