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
using LearnOpenTK.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static OpenTkVoxelEngine.HydraulicErosion;
using GL = OpenTK.Graphics.OpenGL4.GL;
using IntPtr = System.IntPtr;

namespace OpenTkVoxelEngine.Scenes
{
    internal class CubeScene : IScene
    {

        public int _voxelDimensions = 256;
        public int _mipLevel = 4;
        public float _blockSize = 2;
        float time = 0f;

        //Buffer Objects
        VAO _vao;

        //Shaders
        Shader _shader;
        ComputeShader _tracer;
        ComputeShader _mipMapGenerator;
        ComputeShader _mipGammaCorrector;
        ComputeShader _textureGenerator;

        //Shader Locations In Assembly
        string _assemblyName = "OpenTkVoxelEngine.Shaders.ray";
        string _fragName = "shader.frag";
        string _vertName = "shader.vert";
        string _tracerName = "VoxelRaytrace.compute";
        string _mipGeneratorName = "MipMap.compute";
        string _mipGammaCorrectorName = "MipMapGammaPass.compute";
        string _textureCreationName = "TextureGeneration.compute";

        //Camera
        Camera _camera;

        //3D Texture
        int _screenTexture;
        int _demoTexture;
        Texture test;

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

            
            _tracer.SetInt("voxelTextureSampler",1);
            _tracer.SetFloat("time",time);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture3D, _demoTexture);
            _tracer.SetInt("voxelTexture",1);
            _tracer.SetInt("screenTexture", 0);
            GL.DispatchCompute((int)Math.Ceiling(_window.ClientSize.X / 32.0f), (int)Math.Ceiling(_window.ClientSize.Y / 32.0f), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

            //Set MVP matrix of shader
            UpdateDrawShader();
            
            _vao.Bind();

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
            if(_vao != null) _vao.Dispose();
            if(_demoTexture != 0) GL.DeleteTexture(_demoTexture);
            if(_screenTexture != 0) GL.DeleteTexture(_screenTexture);
            if(_mipMapGenerator != null) _mipMapGenerator.Dispose();
            if(_mipGammaCorrector != null) _mipGammaCorrector.Dispose();
            if(_shader != null) _shader.Dispose();
            if(_tracer != null) _tracer.Dispose();
            if(_textureGenerator != null) _textureGenerator.Dispose();
        }

        public override void OnLoad()
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            _camera = new Camera(_window, 0.1f, 1000f,new Vector3(.5f, .5f,-.5f));
            CreateShaders();
            CreateBuffers();
            CreateTexture();

            UpdateTextureDimensions();
        }

        public void CreateTexture()
        {
            

            _demoTexture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture3D, _demoTexture);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.TexParameter(TextureTarget.Texture3D,TextureParameterName.TextureMaxLevel,4);



            GL.TexImage3D(TextureTarget.Texture3D, 0, PixelInternalFormat.Rgba32f, _voxelDimensions, _voxelDimensions, _voxelDimensions, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 1, PixelInternalFormat.Rgba32f, _voxelDimensions / 2, _voxelDimensions / 2, _voxelDimensions / 2, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 2, PixelInternalFormat.Rgba32f, _voxelDimensions / 4, _voxelDimensions / 4, _voxelDimensions / 4, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 3, PixelInternalFormat.Rgba32f, _voxelDimensions / 8, _voxelDimensions / 8, _voxelDimensions / 8, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            GL.TexImage3D(TextureTarget.Texture3D, 4, PixelInternalFormat.Rgba32f, _voxelDimensions / 16, _voxelDimensions / 16, _voxelDimensions / 16, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

   
            RunTextureCreation();
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

        public void RunTextureCreation()
        {
            UpdateTextureGenerationShader();
            GL.BindTexture(TextureTarget.Texture3D, _demoTexture);
            GL.BindImageTexture(0, _demoTexture, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            
            _textureGenerator.use();
            GL.DispatchCompute((int)MathF.Ceiling(_voxelDimensions/8.0f), (int)MathF.Ceiling(_voxelDimensions / 8.0f), (int)MathF.Ceiling(_voxelDimensions / 8.0f));
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
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

            _mipMapGenerator.SetInt("voxelTexture",0);
            _mipMapGenerator.SetInt("voxelMip1", 1);
            _mipMapGenerator.SetInt("voxelMip2", 2);
            _mipMapGenerator.SetInt("voxelMip3", 3);
            _mipMapGenerator.SetInt("voxelMip4",4);


            GL.DispatchCompute((int)MathF.Ceiling(_voxelDimensions / 8.0f), (int)MathF.Ceiling(_voxelDimensions / 8.0f), (int)MathF.Ceiling(_voxelDimensions / 8.0f));
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

            _mipGammaCorrector.use();
            GL.BindTexture(TextureTarget.Texture3D, _demoTexture);

            GL.BindImageTexture(1, _demoTexture, 1, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(2, _demoTexture, 2, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(3, _demoTexture, 3, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(4, _demoTexture, 4, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);


            _mipGammaCorrector.SetInt("voxelMip1", 1);
            _mipGammaCorrector.SetInt("voxelMip2", 2);
            _mipGammaCorrector.SetInt("voxelMip3", 3);
            _mipGammaCorrector.SetInt("voxelMip4", 4);


            GL.DispatchCompute(_voxelDimensions/16, _voxelDimensions/16, _voxelDimensions/16);
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

            _mipGammaCorrector = new ComputeShader(_assemblyName, _mipGammaCorrectorName);
            UpdateGammaCorrectorShader();

            _textureGenerator = new ComputeShader(_assemblyName, _textureCreationName);
            UpdateTextureGenerationShader();
        }

        public void UpdateDrawShader()
        {
            _shader.Use();
        }

        public void UpdateMipMapGeneratorShader()
        {
            _mipMapGenerator.use();
            _mipMapGenerator.SetInt("textureDims", _voxelDimensions);
        }

        public void UpdateGammaCorrectorShader()
        {
            _mipGammaCorrector.use();
        }

        public void UpdateTextureGenerationShader()
        {
            _textureGenerator.use();
            _textureGenerator.SetInt("voxelTexture", 0);
            _textureGenerator.SetInt("textureDims",_voxelDimensions);

        }

        public void UpdateTracerShader()
        {
            _tracer.use();
            
            _tracer.SetVec2("dimensions", _window.ClientSize);

            _tracer.SetVec3("cameraForward", _camera.Forward());
            _tracer.SetVec3("cameraRight", _camera.Right());
            _tracer.SetVec3("cameraUp", _camera.Up());
            _tracer.SetVec3("ro", _camera.Position());

            _tracer.SetIVec3("originalDims",new Vector3i(_voxelDimensions));
            _tracer.SetVec3("position", Vector3.One);
            _tracer.SetFloat("chunkSize", _blockSize);
            _tracer.SetFloat("time",time);
            _tracer.SetInt("mipLevel", _mipLevel);
        }

        public void UpdateTextureDimensions()
        {
            _tracer.use();
            int _scaledVoxelDimensions0 = _voxelDimensions;
            int _scaledVoxelDimensions1 = _voxelDimensions / (int)MathF.Pow(2, 1);
            int _scaledVoxelDimensions2 = _voxelDimensions / (int)MathF.Pow(2, 2);
            int _scaledVoxelDimensions3 = _voxelDimensions / (int)MathF.Pow(2, 3);
            int _scaledVoxelDimensions4 = _voxelDimensions / (int)MathF.Pow(2, 4);

            _tracer.SetInt("textureDims[0]", _scaledVoxelDimensions0);
            _tracer.SetInt("textureDims[1]", _scaledVoxelDimensions1);
            _tracer.SetInt("textureDims[2]", _scaledVoxelDimensions2);
            _tracer.SetInt("textureDims[3]", _scaledVoxelDimensions3);
            _tracer.SetInt("textureDims[4]", _scaledVoxelDimensions4);

            _tracer.SetFloat("voxelSize[0]", _blockSize / _scaledVoxelDimensions0);
            _tracer.SetFloat("voxelSize[1]", _blockSize / _scaledVoxelDimensions1);
            _tracer.SetFloat("voxelSize[2]", _blockSize / _scaledVoxelDimensions2);
            _tracer.SetFloat("voxelSize[3]", _blockSize / _scaledVoxelDimensions3);
            _tracer.SetFloat("voxelSize[4]", _blockSize / _scaledVoxelDimensions4);

        }

        public void CreateBuffers()
        {
            _vao = new VAO();


            //Bind our vao before creating a ebo
            _vao.Bind();
        }
    }
}
