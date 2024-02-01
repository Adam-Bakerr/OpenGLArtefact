﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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
    internal class SurfaceNets : IScene
    {
        //Camera
        Camera _camera;

        //Shaders
        Shader _shader;
        ComputeShader _dfShader;
        ComputeShader _intersectionLocatorShader;
        ComputeShader _createVertexShader;

        //Shader Paths
        string _assemblyPath = "OpenTkVoxelEngine.Shaders.SurfaceNets";
        string _vertexPath = "shader.vert";
        string _fragmentPath = "shader.frag";
        string _distanceFieldGenerationPath = "createDF.compute";
        string _marchCubesShaderPath = "dualContour.compute";
        string _createVertexPath = "CreateVerticies.compute";

        //Buffers
        VAO _vao;
        int _vbo;
        int _dfbo; //Distance Field Buffer Object
        int _fpbo; //triangle connection buffer object
        int _vcbo; //Vertex Counter Buffer Object

        //Variables
        Vector3i _dimensions = new Vector3i(128, 128, 128);
        Vector3 _resolution = new Vector3(.1f);
        int _workGroupSize = 8;
        float _surfaceLevel = .5f;
        float _grassBlendAmount = .875f;
        float _grassSlopeThreshold = .15f;
        float _dualContourErrorValue = .1f;
        bool _drawTestSpheres;

        uint vertexCounterValue;

        //noise variables
        HydraulicErosion.FBMNoiseVariables _heightMapNoiseVariables;
        HydraulicErosion.FBMNoiseVariables _caveMapNoiseVariables;

        public int VertexSize() => (sizeof(float) * 12);
        public int VertexCount() => _dimensions.X * _dimensions.Y * _dimensions.Z;


        public SurfaceNets(GameWindow window, ImGuiController controller) : base(window, controller)
        {

        }

        public void CreateBuffers()
        {
            int numPoints = _dimensions.X * _dimensions.Y * _dimensions.Z;
            Vector3i numVoxelsPerAxis = new Vector3i(_dimensions.X - 1, _dimensions.Y - 1, _dimensions.Z - 1);
            int numVoxels = numVoxelsPerAxis.X * numVoxelsPerAxis.Y * numVoxelsPerAxis.Z;
            int maxTriangleCount = numVoxels * 5;
            int maxVertexCount = maxTriangleCount * 4;


            //Vertex Buffer
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, VertexSize() * maxVertexCount, nint.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _vbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //vertex counter
            _vcbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer,_vcbo);
            GL.BufferData(BufferTarget.AtomicCounterBuffer,sizeof(uint),0,BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);

            //DF buffer
            _dfbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer,_dfbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,sizeof(float) * 4 * VertexCount(),nint.Zero,BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _dfbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Feature Point buffer
            _fpbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _fpbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * 4 * VertexCount(), nint.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _fpbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            _vao = new VAO();

            // Tell the shader which numbers mean what in the buffer
            List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (_shader.GetAttribLocation("aPosition"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 0),
                (_shader.GetAttribLocation("aColor"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 4 * sizeof(float)),
                (_shader.GetAttribLocation("aNormal"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 8 * sizeof(float))

            };
            _vao.Enable(Pointers);


        }

        public void ResetAtomicCounter()
        {
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, _vcbo);
            GL.ClearNamedBufferData(_vcbo, PixelInternalFormat.R32ui, PixelFormat.Red, PixelType.UnsignedInt, 0); 
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);
        }

        float centerOffset = 0;

        public void RunShaders()
        {
            ResetAtomicCounter();


            ////////////////////Create DF Values////////////////////////////
            int dfx = (int)(MathF.Ceiling(MathF.Max((_dimensions.X ), 1) / _workGroupSize));
            int dfy = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y), 1) / _workGroupSize));
            int dfz = (int)(MathF.Ceiling(MathF.Max((_dimensions.Z), 1) / _workGroupSize));

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _dfbo);

            _dfShader.use();
            _dfShader.SetFloat("centerOffset", centerOffset);

            GL.DispatchCompute(dfx, dfy, dfz);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);


            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ///////////////////////////////////////////////////////////////


            ////////////////////Find Feature Points////////////////////////////
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _dfbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _fpbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _fpbo);


            _intersectionLocatorShader.use();

            int fpx = (int)(MathF.Ceiling(MathF.Max((_dimensions.X), 1) / _workGroupSize));
            int fpy = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y),1) / _workGroupSize));
            int fpz = (int)(MathF.Ceiling(MathF.Max((_dimensions.Z), 1) / _workGroupSize));
            
            GL.DispatchCompute(fpx, fpy, fpz);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ///////////////////////////////////////////////////////////////

            /// ////////////////////Create Verticies////////////////////////////
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _dfbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _fpbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _fpbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _vbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _vbo);

            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, _vcbo);
            GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 3, _vcbo);

            _createVertexShader.use();

            int cvx = (int)(MathF.Ceiling(MathF.Max((_dimensions.X ), 1) / _workGroupSize));
            int cvy = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y ), 1) / _workGroupSize));
            int cvz = (int)(MathF.Ceiling(MathF.Max((_dimensions.Z), 1) / _workGroupSize));

            GL.DispatchCompute(cvx, cvy, cvz);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ///////////////////////////////////////////////////////////////


            //Get Counter Data To Reduce The Amount Of Verticies Drawn My a order of magnitude 
            GL.GetBufferSubData(BufferTarget.AtomicCounterBuffer,0,sizeof(uint),ref vertexCounterValue);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);


        }

        public void CreateShaders()
        {
            //Create the shader used to draw verts to the screen
            _shader = new Shader(_assemblyPath, _vertexPath, _fragmentPath);
            UpdateDrawingShader();

            //Create our cube df data
            _dfShader = new ComputeShader(_assemblyPath, _distanceFieldGenerationPath);
            _heightMapNoiseVariables = new FBMNoiseVariables(0, 3, Vector3.Zero, .6f, .8f, 4.31f, 0, 19.935f, 0.105f, 1, 150, 1);
            _caveMapNoiseVariables = new FBMNoiseVariables(0, 3, Vector3.Zero, .6f, .2f, 4.31f, 0, 19.935f, 0.05f, 0, 150, 1);

            UpdateDFShader();

            //Create the marching cubes shaders
            _intersectionLocatorShader = new ComputeShader(_assemblyPath, _marchCubesShaderPath);
            UpdateIntersectionLocatorShader();

            //create the vertexCreationShader
            _createVertexShader = new ComputeShader(_assemblyPath, _createVertexPath);
            UpdateVertexCreationShader();


        }

        public void UpdateDrawingShader()
        {
            _shader.Use();
            _shader.SetIVec3("_dimensions",_dimensions);
        }

        public void UpdateVertexCreationShader()
        {
            _createVertexShader.use();
            _createVertexShader.SetVec4("resolution", new Vector4(_resolution,1));
            _createVertexShader.SetIVec3("vertexCount", _dimensions);
            _createVertexShader.SetFloat("surfaceLevel", _surfaceLevel);
        }

        public void UpdateDFShader()
        {
            _dfShader.use();
            _dfShader.SetVec3("resolution", _resolution);
            _dfShader.SetIVec3("vertexCount", _dimensions);
            _dfShader.SetFloat("totalTime", _totalTime);
            _dfShader.SetBool("testSpheres", _drawTestSpheres);
            _dfShader.SetInt("baseHeightmap.seed", _heightMapNoiseVariables.seed);
            _dfShader.SetInt("baseHeightmap.NumLayers", _heightMapNoiseVariables.NumLayers);
            _dfShader.SetVec3("baseHeightmap.centre", _heightMapNoiseVariables.centre);
            _dfShader.SetFloat("baseHeightmap.baseRoughness", _heightMapNoiseVariables.baseRoughness);
            _dfShader.SetFloat("baseHeightmap.roughness", _heightMapNoiseVariables.roughness);
            _dfShader.SetFloat("baseHeightmap.persistence", _heightMapNoiseVariables.persistence);
            _dfShader.SetFloat("baseHeightmap.minValue", _heightMapNoiseVariables.minValue);
            _dfShader.SetFloat("baseHeightmap.strength", _heightMapNoiseVariables.strength);
            _dfShader.SetFloat("baseHeightmap.scale", _heightMapNoiseVariables.scale);
            _dfShader.SetFloat("baseHeightmap.minHeight", _heightMapNoiseVariables.minHeight);
            _dfShader.SetFloat("baseHeightmap.maxHeight", _heightMapNoiseVariables.maxHeight);

            _dfShader.SetInt("baseCaveMap.seed", _caveMapNoiseVariables.seed);
            _dfShader.SetInt("baseCaveMap.NumLayers", _caveMapNoiseVariables.NumLayers);
            _dfShader.SetVec3("baseCaveMap.centre", _caveMapNoiseVariables.centre);
            _dfShader.SetFloat("baseCaveMap.baseRoughness", _caveMapNoiseVariables.baseRoughness);
            _dfShader.SetFloat("baseCaveMap.roughness", _caveMapNoiseVariables.roughness);
            _dfShader.SetFloat("baseCaveMap.persistence", _caveMapNoiseVariables.persistence);
            _dfShader.SetFloat("baseCaveMap.minValue", _caveMapNoiseVariables.minValue);
            _dfShader.SetFloat("baseCaveMap.strength", _caveMapNoiseVariables.strength);
            _dfShader.SetFloat("baseCaveMap.scale", _caveMapNoiseVariables.scale);
            _dfShader.SetFloat("baseCaveMap.minHeight", _caveMapNoiseVariables.minHeight);
            _dfShader.SetFloat("baseCaveMap.maxHeight", _caveMapNoiseVariables.maxHeight);

            _dfShader.SetFloat("errorValue", _dualContourErrorValue);
        }

        public void UpdateIntersectionLocatorShader()
        {
            _intersectionLocatorShader.use();
            _intersectionLocatorShader.SetVec3("resolution", _resolution);
            _intersectionLocatorShader.SetIVec3("vertexCount", _dimensions);
            _intersectionLocatorShader.SetFloat("surfaceLevel",_surfaceLevel);

            _intersectionLocatorShader.SetFloat("_GrassSlopeThreshold", _grassSlopeThreshold);
            _intersectionLocatorShader.SetFloat("_GrassBlendAmount", _grassBlendAmount);
        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            _camera.OnUpdateFrame(args);
        }




        float _totalTime = 0;
        public override void OnRenderFrame(FrameEventArgs args)
        {
            _totalTime += (float)args.Time;
            OnDFUpdate();


            //Clear the window and the depth buffer
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _shader.Use();

            _shader.SetMatrix4("model", Matrix4.Identity);
            _shader.SetMatrix4("view", _camera.View());
            _shader.SetMatrix4("projection", _camera.Projection());

            _shader.SetVec3("light.position", Vector3.UnitY + Vector3.UnitY * MathF.Sin(_totalTime) * 8);
            _shader.SetFloat("light.constant", 1.0f);
            _shader.SetFloat("light.linear", 0.09f);
            _shader.SetFloat("light.quadratic", 0.032f);
            _shader.SetVec3("light.ambient", Vector3.UnitX * .15f);
            _shader.SetVec3("light.diffuse", new Vector3(0.8f, 0.8f, 0.8f));
            _shader.SetVec3("light.specular", new Vector3(.10f, .10f, .10f));

            _vao.Bind();
            GL.DrawArrays(PrimitiveType.Triangles,0, (int)vertexCounterValue);

            DrawImgui();

            _window.SwapBuffers();
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
        }

        public override void OnUnload()
        {
        }



        public override void OnLoad()
        {

            var watch = System.Diagnostics.Stopwatch.StartNew();

            //Change the clear color
            GL.ClearColor(Color.Black);

            //Create the camera
            _camera = new Camera(_window, 0.01f, 2000f);

            //Create Shaders
            CreateShaders();

            //Create Buffers
            CreateBuffers();

            //Run ALl The Shaders
            RunShaders();

            //Enable Z Depth Testing
            GL.Enable(EnableCap.DepthTest);

            watch.Stop();

            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");

        }

        public void OnDFUpdate()
        {
            UpdateDFShader();
            UpdateIntersectionLocatorShader();
            RunShaders();
        }

        public override void DrawImgui()
        {
            if (!_window.IsKeyDown(Keys.LeftAlt)) return;
            ImGui.Begin("Marching Cubes Noise Variables");
            ImGui.Text("Height Map");
            if (ImGui.DragInt("HeightMap seed", ref _heightMapNoiseVariables.seed, 1)) OnDFUpdate();
            if (ImGui.DragInt("HeightMap numLayers", ref _heightMapNoiseVariables.NumLayers, 1, 0, 8)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap baseRoughness", ref _heightMapNoiseVariables.baseRoughness, .005f, 0)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap Roughness", ref _heightMapNoiseVariables.roughness, .005f, 0)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap persistence", ref _heightMapNoiseVariables.persistence, .01f, 0)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap minValue", ref _heightMapNoiseVariables.minValue)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap strength", ref _heightMapNoiseVariables.strength, 0.005f, 0.0001f)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap scale", ref _heightMapNoiseVariables.scale, .005f, 0.0001f)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap minHeight", ref _heightMapNoiseVariables.minHeight)) OnDFUpdate();
            if (ImGui.DragFloat("HeightMap maxHeight", ref _heightMapNoiseVariables.maxHeight)) OnDFUpdate();
            ImGui.Spacing();
            ImGui.Text("Cave Map");
            if (ImGui.DragInt("Cavemap seed", ref _caveMapNoiseVariables.seed, 1)) OnDFUpdate();
            if (ImGui.DragInt("Cavemap numLayers", ref _caveMapNoiseVariables.NumLayers, 1, 0, 8)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap baseRoughness", ref _caveMapNoiseVariables.baseRoughness, .005f, 0)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap Roughness", ref _caveMapNoiseVariables.roughness, .005f, 0)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap persistence", ref _caveMapNoiseVariables.persistence, .01f, 0)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap minValue", ref _caveMapNoiseVariables.minValue)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap strength", ref _caveMapNoiseVariables.strength, 0.005f, 0.0001f)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap scale", ref _caveMapNoiseVariables.scale, .005f, 0.0001f)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap minHeight", ref _caveMapNoiseVariables.minHeight)) OnDFUpdate();
            if (ImGui.DragFloat("Cavemap maxHeight", ref _caveMapNoiseVariables.maxHeight)) OnDFUpdate();
            ImGui.Spacing();
            if (ImGui.DragFloat("IsoLevel", ref _surfaceLevel, .025f, 0, 1)) OnDFUpdate();
            if (ImGui.DragFloat("Grass Slope Threshold", ref _grassSlopeThreshold, .025f, 0, 1))OnDFUpdate();
            if(ImGui.DragFloat("Grass Blend Amount", ref _grassBlendAmount, .025f, 0, 1))OnDFUpdate();
            ImGui.Checkbox("Debug Test Spheres", ref _drawTestSpheres);
            ImGui.End();

            _controller.Render();
        }


        #region temp

            int packIntUnitsAndFloat(int fourBitInt, float Float)
        {

            int units = (int)MathF.Floor(Float);
            float largerFloat = Float - units;

            // Make sure the 4-bit integer is 4 bits
            fourBitInt &= 0xF;

            // Make sure the units fit within 8 bits
            units &= 0xFF;

            // Convert the larger float to a 16-bit integer (adjust conversion factor as needed)
            int intLargerFloat = (int)(largerFloat * 32767.0f);
            intLargerFloat &= 0xFFFF;  // Make sure it's 16 bits

            // Pack the numbers into a single int
            int result = 0;
            result |= (fourBitInt << 24);        // Shift fourBitInt to the left by 24 bits
            result |= (units << 16);             // Shift units to the left by 16 bits
            result |= intLargerFloat;            // No need to shift intLargerFloat

            return result;
        }

        //Type and distance
        (int, float) unpackIntUnitsAndFloat(int packedInt)
        {
            // Extract the 4-bit integer, 8-bit units, and 16-bit float from the packed int
            int fourBitInt = (packedInt >> 24) & 0xF;
            int units = (packedInt >> 16) & 0xFF;
            int intLargerFloat = packedInt & 0xFFFF;

            // Convert back to float (adjust conversion factor as needed)
            float largerFloat = (float)(intLargerFloat) / 32767.0f;

            // Print the unpacked values
            return (fourBitInt, units + largerFloat);
        }

        public float CircleSDF(Vector3 point1, Vector3 point2, float radius)
        {
            return Vector3.Distance(point2, point1) - radius;
        }
        
        int[] triangulation = new int[]{
         -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1 ,
         3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1 ,
         3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1 ,
         3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1 ,
         9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1 ,
         9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 ,
         2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1 ,
         8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1 ,
         9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 ,
         4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1 ,
         3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1 ,
         1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1 ,
         4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1 ,
         4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1 ,
         9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 ,
         5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1 ,
         2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1 ,
         9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 ,
         0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 ,
         2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1 ,
         10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1 ,
         4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1 ,
         5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1 ,
         5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1 ,
         9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1 ,
         0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1 ,
         1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1 ,
         10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1 ,
         8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1 ,
         2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1 ,
         7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1 ,
         9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1 ,
         2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1 ,
         11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1 ,
         9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1 ,
         5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1 ,
         11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1 ,
         11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 ,
         1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1 ,
         9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1 ,
         5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1 ,
         2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 ,
         0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 ,
         5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1 ,
         6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1 ,
         3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1 ,
         6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1 ,
         5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1 ,
         1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 ,
         10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1 ,
         6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1 ,
         8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1 ,
         7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1 ,
         3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 ,
         5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1 ,
         0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1 ,
         9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1 ,
         8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1 ,
         5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1 ,
         0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1 ,
         6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1 ,
         10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1 ,
         10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1 ,
         8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1 ,
         1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1 ,
         3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1 ,
         0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1 ,
         10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1 ,
         3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1 ,
         6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1 ,
         9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1 ,
         8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1 ,
         3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1 ,
         6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1 ,
         0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1 ,
         10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1 ,
         10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1 ,
         2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1 ,
         7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1 ,
         7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1 ,
         2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1 ,
         1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1 ,
         11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1 ,
         8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1 ,
         0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1 ,
         7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 ,
         10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 ,
         2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 ,
         6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1 ,
         7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1 ,
         2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1 ,
         1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1 ,
         10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1 ,
         10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1 ,
         0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1 ,
         7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1 ,
         6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1 ,
         8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1 ,
         6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1 ,
         4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1 ,
         10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1 ,
         8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1 ,
         0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1 ,
         1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1 ,
         8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1 ,
         10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1 ,
         4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1 ,
         10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 ,
         5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 ,
         11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1 ,
         9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 ,
         6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1 ,
         7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1 ,
         3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1 ,
         7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1 ,
         9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1 ,
         3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1 ,
         6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1 ,
         9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1 ,
         1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1 ,
         4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1 ,
         7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1 ,
         6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1 ,
         3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1 ,
         0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1 ,
         6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1 ,
         0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1 ,
         11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1 ,
         6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1 ,
         5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1 ,
         9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1 ,
         1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1 ,
         1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1 ,
         10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1 ,
         0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1 ,
         5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1 ,
         10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1 ,
         11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1 ,
         9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1 ,
         7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1 ,
         2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1 ,
         8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1 ,
         9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1 ,
         9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1 ,
         1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1 ,
         9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1 ,
         9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1 ,
         5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1 ,
         0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1 ,
         10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1 ,
         2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1 ,
         0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1 ,
         0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1 ,
         9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1 ,
         5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1 ,
         3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1 ,
         5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1 ,
         8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1 ,
         9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1 ,
         0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1 ,
         1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1 ,
         3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1 ,
         4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1 ,
         9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1 ,
         11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1 ,
         11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1 ,
         2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1 ,
         9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1 ,
         3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1 ,
         1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1 ,
         4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1 ,
         4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1 ,
         0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1 ,
         3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1 ,
         3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1 ,
         0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1 ,
         9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1 ,
         1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 ,
         -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
    };

        #endregion
    }
}