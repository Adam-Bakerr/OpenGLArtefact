using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using Dear_ImGui_Sample;
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

        //Shaders
        Shader rayShader;
        ComputeShader computeShader;
        ComputeShader marchShader;
        //Camera
        Camera camera;


        //Sparse Voxel Octree
        SVO svo;

        public VoxelScene(GameWindow window, ImGuiController controller) : base(window,controller)
        {

        }

        void CreateShader()
        {
            rayShader = new Shader("raytrace.vert", "raytrace.frag");
            computeShader = new ComputeShader("CreateUSDF.compute");
            marchShader = new ComputeShader("USDFMarch.compute");

            marchShader.SetFloat("aspect", (float)screenTextureWidth / (float)screenTextureHeight);
        }

        int TextureWidth = 512, TextureHeight = 512, TextureDepth = 512;
        int screenTextureWidth = 1920, screenTextureHeight = 1080;
        float Resolution = .2f;
        int MaxRayDistance = 8;
        int _texture, _screenTexture;
  
        void CreateTextures()
        {
    

            byte[] imageData = new byte[TextureWidth * TextureHeight * TextureDepth];
            for (int z = 0; z < TextureHeight; z++)
            {
                for (int y = 0; y < TextureHeight; y++)
                {
                    for (int x = 0; x < TextureWidth; x++)
                    {
                        int index = (z * TextureWidth * TextureHeight + y * TextureWidth + x);
                        imageData[index] = (byte)MaxRayDistance;

                    }
                }
            }

            _texture = GL.GenTexture();


            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture3D, _texture);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);


            GL.TexImage3D(TextureTarget.Texture3D,0,PixelInternalFormat.R32ui,TextureWidth,TextureHeight,TextureDepth,0,PixelFormat.RedInteger,PixelType.UnsignedByte, imageData);



            GL.BindImageTexture(0,_texture,0,false,0,TextureAccess.ReadWrite,SizedInternalFormat.R32ui);




            _screenTexture = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _screenTexture);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, screenTextureWidth, screenTextureHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            
            GL.BindImageTexture(1,_screenTexture,0,false,0,TextureAccess.ReadWrite,SizedInternalFormat.Rgba32f);

            rayShader.SetInt("texture0", 0);
            rayShader.SetInt("texturem1", 1);

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

        }

        //Fix full screen triangle

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            camera.OnUpdateFrame(args);
        }

        bool hasRan = false;
        Vector3 Offset = Vector3.UnitY * 25f;
        float layer = 0;

        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Clear only the color buffer
            GL.Clear(ClearBufferMask.ColorBufferBit);


            //Use The Texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture3D, _texture);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _screenTexture);


            computeShader.use();

            computeShader.SetInt("currentDistance",MaxRayDistance);



            //Handles moving the noise
            if (_window.IsKeyDown(Keys.Up))
            {
                Offset.Y -= (float)args.Time * 10f;
                UpdateSDF();
            }
            if (_window.IsKeyDown(Keys.Down))
            {
                Offset.Y += (float)args.Time * 10f;
                UpdateSDF();
            }
            if (_window.IsKeyDown(Keys.Left))
            {
                Offset.X += (float)args.Time * 10f;
                UpdateSDF();
            }
            if (_window.IsKeyDown(Keys.Right))
            {
                Offset.X -= (float)args.Time * 10f;
                UpdateSDF();
            }


            //Controls ray distance
            if (_window.IsKeyDown(Keys.PageUp))
            {
                MaxRayDistance = Math.Clamp(MaxRayDistance + 1, 8, 32); ;
                GL.DeleteTexture(_texture);
                CreateTextures();
                UpdateSDF();
            }
            if (_window.IsKeyDown(Keys.PageDown))
            {
                MaxRayDistance = Math.Clamp(MaxRayDistance - 1 , 8 , 32);
                GL.DeleteTexture(_texture);
                CreateTextures();
                UpdateSDF();
            }


            marchShader.use();


            marchShader.SetVec3("CameraPos", camera.Position());
            marchShader.SetVec3("CameraForward", camera.Forward());
            marchShader.SetVec3("CameraRight", camera.Right());
            marchShader.SetVec3("CameraUp", camera.Up());
            marchShader.SetVec3("sdfTextureSize", new Vector3(TextureWidth,TextureHeight,TextureDepth));
            marchShader.SetFloat("fov",camera.FOV());
            marchShader.SetMatrix4("viewMatrix", camera.View());

            GL.DispatchCompute((int)Math.Ceiling(screenTextureWidth / 4.0f), (int)Math.Ceiling(screenTextureHeight / 4.0f), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            //Handles which layer of the texture to sample
            if (_window.IsKeyDown(Keys.Period))
            {
                layer = Math.Clamp(layer + (float)args.Time * 15f, 0, 127);
            }
            if (_window.IsKeyDown(Keys.Comma))
            {
                layer = Math.Clamp(layer - (float)args.Time * 15f, 0, 127);
            }




            rayShader.Use();
            rayShader.SetInt("texture0", 0);
            rayShader.SetInt("texturem1", 1);
            rayShader.SetFloat("layer",layer);
            rayShader.SetInt("maxdistance",MaxRayDistance);

            vao.Bind();

            //Draw The Buffer
            GL.DrawArrays(PrimitiveType.Triangles,0,vertices.Length);
          
            _window.SwapBuffers();

        }

        void UpdateSDF()
        {

            rayShader.Use();
            computeShader.use();

            computeShader.SetVec3("Offset", Offset);
            computeShader.SetIVec3("textureSize", new Vector3i(TextureWidth, TextureHeight, TextureDepth));
            computeShader.SetInt("distance", MaxRayDistance);

            GL.DispatchCompute((int)Math.Ceiling(TextureWidth / 4.0f), (int)Math.Ceiling(TextureHeight / 4.0f), (int)Math.Ceiling(TextureDepth / 4.0f));
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            camera.OnMouseWheel(e);
        }


        public override void DrawImgui()
        {
        }

        public override void OnUnload()
        {
            rayShader?.Dispose();

            vbo?.Dispose();
            vao?.Dispose();

        }

        public override void OnLoad()
        {
            camera = new Camera(_window, 0.01f, 500f);

            //Disable Z Depth Testing
            GL.Disable(EnableCap.DepthTest);



            CreateShader();
            CreateBuffers();
            CreateTextures();

            UpdateSDF();

        }

        public void SetActive(bool condition)
        {

        }

        ~VoxelScene()
        {

        }
    }
}
