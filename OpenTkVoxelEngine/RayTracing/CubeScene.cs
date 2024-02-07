﻿using System;
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
        public int _mipLevel = 4;
        public float chunkSize = 80.0f;
        float time = 0f;

        int _vbo;
        VAO _vao;



        //Shaders
        Shader _shader;
        ComputeShader _tracer;
        ComputeShader _mipMapGenerator;
        string _assemblyName = "OpenTkVoxelEngine.Shaders.ray";
        string _fragName = "shader.frag";
        string _vertName = "shader.vert";
        string _tracerName = "VoxelRaytrace.compute";
        string _mipGeneratorName = "MipMap.compute";

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

            if (_window.IsKeyPressed(Keys.Up)) _mipLevel = Math.Clamp(_mipLevel+1, 0, 4);
            if (_window.IsKeyPressed(Keys.Down)) _mipLevel = Math.Clamp(_mipLevel-1, 0, 4);

            _tracer.use();
            UpdateTracerShader();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _screenTexture);

            
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture3D,_demoTexture);
            _tracer.SetInt("voxelTextureSampler",1);

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

            GL.TexParameter(TextureTarget.Texture3D,TextureParameterName.TextureMaxLevel,4);

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
            GL.TexImage3D(TextureTarget.Texture3D, 1, PixelInternalFormat.Rgba32f, _voxelDimensions / 2, _voxelDimensions / 2, _voxelDimensions / 2, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 2, PixelInternalFormat.Rgba32f, _voxelDimensions / 4, _voxelDimensions / 4, _voxelDimensions / 4, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 3, PixelInternalFormat.Rgba32f, _voxelDimensions / 8, _voxelDimensions / 8, _voxelDimensions / 8, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 4, PixelInternalFormat.Rgba32f, _voxelDimensions / 16, _voxelDimensions / 16, _voxelDimensions / 16, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
  
            //GL.GenerateMipmap(GenerateMipmapTarget.Texture3D);

            CreateMipMap();


            _screenTexture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D,_screenTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, _window.ClientSize.X, _window.ClientSize.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.BindImageTexture(0, _screenTexture, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

        }

        public void CreateMipMap()
        {
            _mipMapGenerator.use();
            GL.BindTexture(TextureTarget.Texture3D,_demoTexture);

            GL.BindImageTexture(0, _demoTexture, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(1, _demoTexture, 1, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(2, _demoTexture, 2, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(3, _demoTexture, 3, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(4, _demoTexture, 4, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(5, _demoTexture, 5, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

            _mipMapGenerator.SetInt("voxelTexture",0);
            _mipMapGenerator.SetInt("voxelMip1", 1);
            _mipMapGenerator.SetInt("voxelMip2", 2);
            _mipMapGenerator.SetInt("voxelMip3", 3);
            _mipMapGenerator.SetInt("voxelMip4",4);
            _mipMapGenerator.SetInt("voxelMip5", 5);

            GL.DispatchCompute(_voxelDimensions/2, _voxelDimensions / 2, _voxelDimensions / 2);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

        }

        public void CreateShaders()
        {
            _shader = new Shader(_assemblyName, _vertName, _fragName);
            UpdateDrawShader();

            _tracer = new ComputeShader(_assemblyName, _tracerName);
            UpdateTracerShader();

            _mipMapGenerator = new ComputeShader(_assemblyName, _mipGeneratorName);
            UpdateMipMapGeneratorShader();
        }

        public void UpdateDrawShader()
        {
            _shader.Use();
        }

        public void UpdateMipMapGeneratorShader()
        {
            _mipMapGenerator.use();
            _mipMapGenerator.SetInt("mipStride", (int)MathF.Pow(2, _mipLevel));
        }

        public void UpdateTracerShader()
        {
            _tracer.use();
            
            _tracer.SetVec2("dimensions", _window.ClientSize);

            _tracer.SetVec3("cameraForward", _camera.Forward());
            _tracer.SetVec3("cameraRight", _camera.Right());
            _tracer.SetVec3("cameraUp", _camera.Up());
            _tracer.SetVec3("ro", _camera.Position());


            int _scaledVoxelDimensions = _voxelDimensions / (int)MathF.Pow(2, _mipLevel);

            _tracer.SetIVec3("textureDims", new Vector3i(_scaledVoxelDimensions));
            _tracer.SetVec3("position", Vector3.Zero);
            _tracer.SetFloat("voxelSize", 1 / (float)_scaledVoxelDimensions);
            _tracer.SetFloat("chunkSize", chunkSize);
            _tracer.SetFloat("time",time);
            _tracer.SetInt("mipLevel", _mipLevel);
        }

        public void CreateBuffers()
        {
            _vao = new VAO();


            //Bind our vao before creating a ebo
            _vao.Bind();
        }
    }
}
