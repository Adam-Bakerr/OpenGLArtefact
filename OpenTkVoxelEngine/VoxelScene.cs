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
using OpenTK.Windowing.GraphicsLibraryFramework;
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
            computeShader = new ComputeShader("CreateUSDF.compute");
        }

        int TextureWidth = 128, TextureHeight = 128;
        int MaxRayDistance = 32;
        int _texture;

        void CreateTextures()
        {
            //texture = Texture.LoadFromFile("smile.jpg", TextureUnit.Texture0);
            //texture.Use(TextureUnit.Texture0);

            byte[] imageData = new byte[TextureWidth * TextureHeight];

            for (int y = 0; y < TextureHeight; y++)
            {
                for (int x = 0; x < TextureWidth; x++)
                {
                    int index = (y * TextureWidth + x);
                    imageData[index] = (byte)MaxRayDistance;

                }
            }

            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.R32ui,TextureWidth,TextureHeight,0,PixelFormat.RedInteger,PixelType.UnsignedByte, imageData);

            GL.BindImageTexture(0,_texture,0,false,0,TextureAccess.ReadWrite,SizedInternalFormat.R32ui);

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

            /*ssbo = new SSBO();
            var test = OctreeNode.SizeOfNode();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo._objectHandle);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, svo.nodes.Count * OctreeNode.SizeOfNode(), svo.nodes.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, ssbo._objectHandle);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);*/
        }

        //Fix full screen triangle

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            camera.OnUpdateFrame(args);
        }

        bool hasRan = false;
        Vector3 Offset = Vector3.Zero;
        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Clear only the color buffer
            GL.Clear(ClearBufferMask.ColorBufferBit);


            //Use The Texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);


            if (_window.IsKeyDown(Keys.Up)) Offset.Y -= (float)args.Time * 10f;
            if (_window.IsKeyDown(Keys.Down)) Offset.Y += (float)args.Time * 10f;
            if (_window.IsKeyDown(Keys.Left)) Offset.X += (float)args.Time * 10f;
            if (_window.IsKeyDown(Keys.Right)) Offset.X -= (float)args.Time * 10f;

            computeShader.use();
            computeShader.SetVec3("Offset", Offset);

            /*
            computeShader.SetVec3("position",camera.Position());
            computeShader.SetVec3("CameraForward", camera.Forward());
            computeShader.SetVec3("CameraRight", camera.Right());
            computeShader.SetVec3("CameraUp", camera.Up());*/

            if (!hasRan)
            {
                UpdateSDF();
                //hasRan = true;
            }

            rayShader.Use();
     
            vao.Bind();

            //Draw the full screen triangle
            GL.DrawArrays(PrimitiveType.Triangles,0,vertices.Length);

            _window.SwapBuffers();

        }

        void UpdateSDF()
        {
                GL.DispatchCompute((int)Math.Ceiling(TextureWidth / 8.0f), (int)Math.Ceiling(TextureHeight / 8.0f), 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            camera.OnMouseWheel(e);
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


            //svo = new SVO(Vector3.UnitZ * 10f, Vector3.One * 20f, 3);


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
