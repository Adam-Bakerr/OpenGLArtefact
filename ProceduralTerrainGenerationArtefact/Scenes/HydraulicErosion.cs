﻿using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using BufferTarget = OpenTK.Graphics.OpenGL4.BufferTarget;
using BufferUsageHint = OpenTK.Graphics.OpenGL4.BufferUsageHint;
using ClearBufferMask = OpenTK.Graphics.OpenGL4.ClearBufferMask;
using DrawElementsType = OpenTK.Graphics.OpenGL4.DrawElementsType;
using EnableCap = OpenTK.Graphics.OpenGL4.EnableCap;
using GL = OpenTK.Graphics.OpenGL4.GL;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using PixelInternalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat;
using PixelType = OpenTK.Graphics.OpenGL4.PixelType;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using SizedInternalFormat = OpenTK.Graphics.OpenGL4.SizedInternalFormat;
using TextureMagFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter;
using TextureMinFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL4.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL4.TextureTarget;
using TextureUnit = OpenTK.Graphics.OpenGL4.TextureUnit;
using TextureWrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode;
using VertexAttribPointerType = OpenTK.Graphics.OpenGL4.VertexAttribPointerType;

namespace OpenTkVoxelEngine
{
    internal class HydraulicErosion : IScene
    {
        //Grid Definitions
        Vector2i _gridVertexCount = new Vector2i(512, 512);
        Vector2 _gridDimensions = new Vector2(25, 25);
        Vector2 Resolution() => _gridDimensions / _gridVertexCount;
        int VertexCount() => _gridVertexCount.X * _gridVertexCount.Y;
        int IndexCount() => (_gridVertexCount.X - 1) * (_gridVertexCount.Y - 1);

        //Erosion Variables
        int particleCount = 5000;
        int sqrtParticle;
        int currentParticleCount = 1;
        public int erosionBrushRadius = 9;
        public int maxLifetime = 200;
        public float sedimentCapacityFactor = 9;
        public float minSedimentCapacity = .50f;
        public float depositSpeed = 2.3f;
        public float erodeSpeed = 0.3f;
        public float evaporateSpeed = .01f;
        public float gravity = 4;
        public float startSpeed = 1;
        public float startWater = 1;
        public float inertia = 0.16f;

        //Misc Variables
        float totalTime = 0;
        float RenderTime = 0f;
        bool _initialized = false;
        bool shaderInit = false;
        bool _shouldErode = false;
        Vector3 LightPos = Vector3.One;
        float _workGroupSize = 8.0f;

        //Buffers
        int _ssbo; // this acts as a vbo
        int _hmbo; //Height map buffer object
        int _rdbo; //Random Droplet Buffer Object
        int _bibo; // Brush Indices Buffer Object
        int _bwbo; // Brush Weight Buffer Object
        int _pesb; // Post Erosion Smoothing Buffer
        int _ebo; // Element Buffer Object
        VAO _vao; // Vertex array object
        int _meshInfoBuffer; // Mesh info Buffer (minMax Buffer)

        //Shaders
        Shader _terrainShader;
        ComputeShader _vertexCreationShader;
        ComputeShader _indexCreationShader;
        ComputeShader _noiseApplicationShader;
        ComputeShader _fallOffApplicationShader;
        ComputeShader _normalCalculationShader;
        ComputeShader _biomeGenerationShader;
        ComputeShader _biomeApplicationShader;
        ComputeShader _erosionShader;
        ComputeShader _particleCreationShader;
        ComputeShader _postErosionPassOne;
        ComputeShader _postErosionPassTwo;

        //Shader paths located in the assembly
        string _assemblyPath = "S225170_-_OPENGL_-_Procedural_Content_Generation_Artefact.Shaders.ErosionShaders";
        string _vertexPath = "Shaders/ErosionShaders/erosionVert.vert";
        string _fragmentPath = "Shaders/ErosionShaders/erosionFrag.frag";
        string _createVertexComputePath = "createVertcies.glsl";
        string _createIndicesComputePath = "createIndices.glsl";
        string _noiseApplicationComputePath = "noiseApplication.glsl";
        string _fallOffComputePath = "fallOffApplication.glsl";
        string _normalCalculationComputePath = "normalCalculation.glsl";
        string _biomeGenerationComputePath = "biomeGeneration.glsl";
        string _biomeApplicationShaderPath = "applyBiomeMap.glsl";
        string _erosionComputePath = "erode.glsl";
        string _particleCreationPath = "createParticles.glsl";
        string _postErosionPassOnePath = "postErosionSmoothingPassOne.glsl";
        string _postErosionPassTwoPath = "postErosionSmoothingPassTwo.glsl";

        //Camera
        Camera camera;

        //Textures Handles
        int _biomeTexture;

        //Random Number Generator (used to hash the random droplets in the compute shader)
        Random random;

        //Shader Noise Variables
        int _minMaxPrecisionFactor = 10000000; //Used to convert a float to a interger so we can peform atomic operations on the gpu

        //Noise Varibles for the height ,falloff and humitidy map
        FBMNoiseVariables _noiseVariables;
        FBMNoiseVariables _falloffNoiseVariables;
        FBMNoiseVariables _humitidyNoiseVariables;
        float _fallOffJitter = 1;

        //Erosion Brush handles spreading out of droplets effects over and area to prevent spiking in the terrain
        List<int> brushIndexOffsets;
        List<float> brushWeights;

        public override void OnLoad()
        {
            //Set clear color and enable depth testing in opengl
            GL.ClearColor(Color4.Black);
            GL.Enable(EnableCap.DepthTest);

            //Create the camera
            camera = new Camera(_window, 0.01f, 500f);

            //Create shader and update its uniforms
            CreateShaders();

            //create all buffers
            CreateBuffers();

            _initialized = true;

            //Create inital mesh
            UpdateMesh();
        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            //Update the camera
            camera.OnUpdateFrame(args);

            //Erode
            if (_shouldErode) Erode();
        }

        void SaveToTexture()
        {
            float[] heights = new float[VertexCount()];
            int[] minmax = new int[4];

            //Gets All Heights From heightmap buffer
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _hmbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _hmbo);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, VertexCount() * sizeof(float), heights);

            //Gets minmax From info buffer
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _meshInfoBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _meshInfoBuffer);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, 4 * sizeof(int), minmax);

            float min = minmax[0] / (float)_minMaxPrecisionFactor;
            float max = minmax[1] / (float)_minMaxPrecisionFactor;

            int width = _gridVertexCount.X; // assuming a width of 2
            int height = heights.Length / width; // calculate the height based on the width

            using (Image<Rgba32> image = new Image<Rgba32>(width, height))
            {
                for (int i = 0; i < heights.Length; i++)
                {
                    float value = ((heights[i] - min) / (max - min)) * 255.0f;
                    Rgba32 color = new Rgba32((byte)value, (byte)value, (byte)value);
                    image[i % width, i / width] = color;
                }

                image.Save("newoutput.png");
            }
        }

        bool runBenchmark = false;
        List<double> executionTimes = new();

        public void Benchmark()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            //Run ALl The Shaders
            UpdateMesh();

            watch.Stop();

            executionTimes.Add(watch.Elapsed.TotalNanoseconds);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Time The Amount of time all computes take to run
            float startTime = DateTime.Now.Microsecond;

            //Tell openGL to clear the color buffer and depth buffer
            GL.ClearColor(Color4.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            totalTime += (float)args.Time;
            LightPos = new Vector3(0, 5f + (float)Math.Sin(totalTime) * 5f, 0f);

            if (executionTimes.Count < 100 && runBenchmark) Benchmark();
            else if (executionTimes.Count >= 100) Console.WriteLine($"Average Execution Time: {executionTimes.Average()} ns");


            //Use our terrain material
            _terrainShader.Use();
            _terrainShader.SetMatrix4("model", Matrix4.Identity);
            _terrainShader.SetMatrix4("view", camera.View());
            _terrainShader.SetMatrix4("projection", camera.Projection());
            _terrainShader.SetVec3("viewPos", camera.Position());
            _terrainShader.SetVec2("gridDimensions", _gridDimensions);
            _terrainShader.SetFloat("minHeight", _noiseVariables.minHeight);

            //Point Light Settings
            _terrainShader.SetVec3("light.position", LightPos);
            _terrainShader.SetFloat("light.constant", 1.0f);
            _terrainShader.SetFloat("light.linear", 0.09f);
            _terrainShader.SetFloat("light.quadratic", 0.032f);
            _terrainShader.SetVec3("light.ambient", Vector3.UnitZ * .05f);
            _terrainShader.SetVec3("light.diffuse", new Vector3(0.8f, 0.8f, 0.8f));
            _terrainShader.SetVec3("light.specular", new Vector3(.10f, .10f, .10f));

            //Activate and bind the biome texture ready for drawing
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _biomeTexture);

            //Draw the mesh
            _vao.Bind();
            GL.DrawElements(PrimitiveType.Triangles, IndexCount() * 6, DrawElementsType.UnsignedInt, 0);

            //Draw the imgui Gui
            DrawImgui();

            _window.SwapBuffers();

            //Track the time taken this frame
            RenderTime = DateTime.Now.Microsecond - startTime;
        }

        /// <summary>
        /// Creates the Erosion Brush Indices And Weights, Used to spread a particles effect over an area within the erosion compute shader
        /// </summary>
        void CreateErosionBrushes()
        {
            // Create brush
            brushIndexOffsets = new List<int>();
            brushWeights = new List<float>();

            float weightSum = 0;
            for (int brushY = -erosionBrushRadius; brushY <= erosionBrushRadius; brushY++)
            {
                for (int brushX = -erosionBrushRadius; brushX <= erosionBrushRadius; brushX++)
                {
                    float sqrDst = brushX * brushX + brushY * brushY;
                    if (sqrDst < erosionBrushRadius * erosionBrushRadius)
                    {
                        brushIndexOffsets.Add(brushY * _gridVertexCount.X + brushX);
                        float brushWeight = 1 - MathF.Sqrt(sqrDst) / erosionBrushRadius;
                        weightSum += brushWeight;
                        brushWeights.Add(brushWeight);
                    }
                }
            }

            //Average the brush weights over the area depending on the square of the size of the brush(the amount of nodes effected)
            for (int i = 0; i < brushWeights.Count; i++)
            {
                brushWeights[i] /= weightSum;
            }

            //Delete the buffer if changing the brush size at run time
            if (_bibo != 0)
            {
                GL.DeleteBuffer(_bibo);
                _bibo = 0;
            }

            //Create the brush index buffer
            _bibo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _bibo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int) * brushIndexOffsets.Count, brushIndexOffsets.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, _bibo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Create the brush index buffer
            _bwbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _bwbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * brushWeights.Count, brushWeights.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, _bwbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>
        /// Creates all required buffers for the shader storage
        /// </summary>
        void CreateBuffers()
        {
            if (_ssbo != 0) GL.DeleteBuffer(_ssbo);
            if (_hmbo != 0) GL.DeleteBuffer(_hmbo);
            if (_vao != null) if (_vao._objectHandle != 0) _vao.Dispose();
            if (_ebo != 0) GL.DeleteBuffer(_ebo);

            //Vertex Buffer
            _ssbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _ssbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * 12 * VertexCount(), nint.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Create Our Heightmap buffer object
            _hmbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _hmbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * VertexCount(), nint.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _hmbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Create our mesh info buffer which just holds two floats for min and max vertex height
            _meshInfoBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _meshInfoBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int) * 4, new int[] { Int32.MaxValue, 0 }, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _meshInfoBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Create the random droplet buffer
            _rdbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _rdbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * 8 * particleCount, nint.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _rdbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            currentParticleCount = particleCount;

            //Vertex array object buffer
            _vao = new VAO();

            // Tell the shader which numbers mean what in the buffer
            List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (_terrainShader.GetAttribLocation("aPosition"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 0),
                (_terrainShader.GetAttribLocation("aColor"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 4 * sizeof(float)),
                (_terrainShader.GetAttribLocation("aNormal"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 8 * sizeof(float))
            };
            _vao.Enable(Pointers);

            //Bind our vao before creating a ebo
            _vao.Bind();
            GL.BindVertexArray(_vao._objectHandle);

            //Create our index buffer
            _ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, IndexCount() * 6 * sizeof(uint), IntPtr.Zero, BufferUsageHint.StaticDraw);
        }

        /// <summary>
        /// Creates all of the required shaders and updates their variables
        /// </summary>
        void CreateShaders()
        {
            //Our material shader
            _terrainShader = new Shader(_assemblyPath, "erosionVert.vert", "erosionFrag.frag");
            _terrainShader.Use();
            _terrainShader.SetInt("biomeMap", 0);

            //Compute shader that handles the creation of verticies
            _vertexCreationShader = new ComputeShader(_assemblyPath, _createVertexComputePath);
            _vertexCreationShader.use();
            UpdateVertexCreationShader();

            //Compute shader that handles the creation of indices
            _indexCreationShader = new ComputeShader(_assemblyPath, _createIndicesComputePath);
            _indexCreationShader.use();
            UpdateIndexCreationShader();

            //Compute shader that handles applying a base noise map onto the mesh
            _noiseApplicationShader = new ComputeShader(_assemblyPath, _noiseApplicationComputePath);
            _noiseApplicationShader.use();
            _noiseVariables = new FBMNoiseVariables(0, 4, Vector3.Zero, .6f, .8f, 1, 0, 2, 0.05f, 0, 150, 1);
            UpdateHeightmapNoiseVariables();

            //Compute shader that handles applying a falloff noise map onto the mesh
            _fallOffApplicationShader = new ComputeShader(_assemblyPath, _fallOffComputePath);
            _fallOffApplicationShader.use();
            _falloffNoiseVariables = new FBMNoiseVariables(0, 3, new Vector3(0, -0.34f, 0), 1, .34f, 1, 0, 1, 0.06f, 0, 150, 1.77f);
            UpdateFallOffmapNoiseVariables();

            //Compute shader that handles recalculation normals of the mesh
            _normalCalculationShader = new ComputeShader(_assemblyPath, _normalCalculationComputePath);
            _normalCalculationShader.use();
            UpdateNormalShader();

            //Compute shader that handles the creation of a biome texture for the mesh
            _biomeGenerationShader = new ComputeShader(_assemblyPath, _biomeGenerationComputePath);
            _biomeGenerationShader.use();
            _humitidyNoiseVariables = new FBMNoiseVariables(0, 7, Vector3.Zero, .4f, .8f, .5f, 0, 1, 0.15f, 0, 1, 1);
            CreateBiomeTexture();
            UpdateBiomeShader();
            UpdateHumidityNoiseVariables();

            //Compute shader that handles maping the biome texture to the mesh
            _biomeApplicationShader = new ComputeShader(_assemblyPath, _biomeApplicationShaderPath);
            _biomeApplicationShader.use();
            UpdateBiomeApplcationShader();

            //compute shader that handles the erosion simulations
            _erosionShader = new ComputeShader(_assemblyPath, _erosionComputePath);
            _erosionShader.use();
            CreateErosionBrushes();
            UpdateErosionShader();

            //compute shader that handles the creation of droplet particles for the compute shader
            _particleCreationShader = new ComputeShader(_assemblyPath, _particleCreationPath);
            _particleCreationShader.use();
            UpdateParticleCreationShader();

            //compute shader that handles the first stage of smoothing of the heightmap
            _postErosionPassOne = new ComputeShader(_assemblyPath, _postErosionPassOnePath);
            _postErosionPassOne.use();

            //compute shader that handles the second stage of the smoothing of the heightmap
            _postErosionPassTwo = new ComputeShader(_assemblyPath, _postErosionPassTwoPath);
            _postErosionPassTwo.use();
            UpdatePostErosionPasses();
        }

        /// <summary>
        /// Runs the entire creation process of the height map and mesh
        /// </summary>
        public void UpdateMesh()
        {
            if (!_initialized || _shouldErode) return;

            ////////////////////Create Mesh Indices////////////////////////////////////
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ebo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ebo);

            _indexCreationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling((_gridVertexCount.X) / _workGroupSize), (int)MathF.Ceiling((_gridVertexCount.Y) / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ////////////////////////////////////////////////////////////////////////

            ////////////////////Add Noise To HeightMap////////////////////////////////////
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _hmbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _hmbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _meshInfoBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _meshInfoBuffer);

            _noiseApplicationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ////////////////////////////////////////////////////////////////////////

            ////////////////////Add Falloff map////////////////////////////////////
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _hmbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _hmbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _meshInfoBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _meshInfoBuffer);

            _fallOffApplicationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ////////////////////////////////////////////////////////////////////////

            CreateVerticiesFromHeightMap();

            UpdateNormals();

            ////////////////////Create Biome Map////////////////////////////////////
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _biomeTexture);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _ssbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _meshInfoBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _meshInfoBuffer);

            _biomeGenerationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ////////////////////////////////////////////////////////////////////////

            //////////////////Apply biome map to generate colors//////////////////
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _biomeTexture);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _ssbo);

            _biomeApplicationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ////////////////////////////////////////////////////////////////////////
        }

        public void InitErodeShader()
        {
            if (_erosionShader != null)
            {
                _erosionShader.Dispose();

                _erosionShader = new ComputeShader(_assemblyPath, _erosionComputePath);
                _erosionShader.use();
                CreateErosionBrushes();
                UpdateErosionShader();
                shaderInit = true;
            }
        }

        /// <summary>
        /// Runs one tick of erosion
        /// </summary>
        public void Erode()
        {
            //Init Shader if not created already
            if (!shaderInit)
            {
                InitErodeShader();
            }

            //Create random particles on the gpu, spread over the entire heightmap
            CreateParticles();

            //Bind buffers and dispatch the compute shader
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _hmbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _hmbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _rdbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _rdbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _bibo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _bibo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _bwbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, _bwbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, _ssbo);

            UpdateErosionShader();

            GL.DispatchCompute((int)MathF.Ceiling(particleCount / 1024f), 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            CreateVerticiesFromHeightMap();
            UpdateNormals();
        }

        /// <summary>
        /// Recalculate the normals of the mesh as we erode
        /// </summary>
        public void UpdateNormals()
        {
            //Update Normals
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);

            _normalCalculationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>
        /// Creates the mesh from the given heightmap
        /// </summary>
        public void CreateVerticiesFromHeightMap()
        {
            //Create Vertices From HeightMap
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _hmbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _hmbo);

            _vertexCreationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>
        /// Runs a very basic average across all verticies smoothing them with their neighbour's
        /// </summary>
        public void PostErosionPass()
        {
            UpdatePostErosionPasses();

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);

            //Create the smoothing buffer then delete after use
            _pesb = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _pesb);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * 12 * VertexCount(), nint.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _pesb);

            _postErosionPassOne.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _pesb);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _pesb);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _ssbo);

            _postErosionPassTwo.use();
            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            GL.DeleteBuffer(_pesb);
        }

        /// <summary>
        /// Creates random droplets on the gpu, with a given count spread over the entire heightmap
        /// </summary>
        public void CreateParticles()
        {
            if (random == null) random = new Random();
            if (currentParticleCount != particleCount)
            {
                currentParticleCount = particleCount;

                GL.DeleteBuffer(_rdbo);

                //Create the random droplet buffer
                _rdbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _rdbo);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * 2 * particleCount, nint.Zero, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _rdbo);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            }

            sqrtParticle = (int)MathF.Floor(MathF.Sqrt(particleCount));

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _rdbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _rdbo);

            _particleCreationShader.use();
            _particleCreationShader.SetFloat("randomHashOffset", (float)random.NextDouble());
            _particleCreationShader.SetFloat("totalTime", totalTime);
            _particleCreationShader.SetIVec2("vertexCount", _gridVertexCount);
            _particleCreationShader.SetInt("sqrtParticleCount", sqrtParticle);
            _particleCreationShader.SetInt("borderSize", erosionBrushRadius);

            GL.DispatchCompute((int)MathF.Ceiling(sqrtParticle / _workGroupSize), (int)MathF.Ceiling(sqrtParticle / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>
        /// Updates Variables
        /// </summary>
        public void UpdateErosionShader()
        {
            _erosionShader.use();
            _erosionShader.SetIVec2("vertexCount", _gridVertexCount);
            _erosionShader.SetInt("borderSize", erosionBrushRadius);
            _erosionShader.SetInt("brushLength", brushIndexOffsets.Count);
            _erosionShader.SetInt("maxLifetime", maxLifetime);
            _erosionShader.SetInt("particleCount", particleCount);
            _erosionShader.SetFloat("inertia", inertia);
            _erosionShader.SetFloat("sedimentCapacityFactor", sedimentCapacityFactor);
            _erosionShader.SetFloat("minSedimentCapacity", minSedimentCapacity);
            _erosionShader.SetFloat("depositSpeed", depositSpeed);
            _erosionShader.SetFloat("erodeSpeed", erodeSpeed);
            _erosionShader.SetFloat("evaporateSpeed", evaporateSpeed);
            _erosionShader.SetFloat("gravity", gravity);
            _erosionShader.SetFloat("startSpeed", startSpeed);
            _erosionShader.SetFloat("startWater", startWater);
        }

        /// <summary>
        /// Updates Variables
        /// </summary>
        public void UpdatePostErosionPasses()
        {
            _postErosionPassOne.use();
            _postErosionPassOne.SetIVec2("vertexCount", _gridVertexCount);

            _postErosionPassTwo.use();
            _postErosionPassTwo.SetIVec2("vertexCount", _gridVertexCount);
        }

        /// <summary>
        /// Draw the IMGui gui for the specific scene
        /// </summary>
        public override void DrawImgui()
        {
            if (!_window.IsKeyDown(Keys.LeftAlt)) return;

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 25));
            ImGui.Begin("Debug Menu", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration);
            ImGui.Text("Vertex Count: " + VertexCount().ToString());
            ImGui.Text("GPU Frame Time In Micro-Seconds:" + RenderTime);

            ImGui.End();

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 70));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(900, _window.ClientSize.Y));
            ImGui.SetNextItemWidth(900);
            ImGui.Begin("Settings", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground);
            ImGui.Checkbox("Run Benchmark", ref runBenchmark);
            ImGui.Text("Height Map Settings");
            ImGui.SetWindowFontScale(.95f);
            if (ImGui.DragInt("HeightMap seed", ref _noiseVariables.seed, 1)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragInt("HeightMap numLayers", ref _noiseVariables.NumLayers, 1, 0, 8)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap baseRoughness", ref _noiseVariables.baseRoughness, .005f, 0)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap Roughness", ref _noiseVariables.roughness, .005f, 0)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap persistence", ref _noiseVariables.persistence, .01f, 0)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap minValue", ref _noiseVariables.minValue)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap strength", ref _noiseVariables.strength, 0.005f, 0.0001f)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap scale", ref _noiseVariables.scale, .005f, 0.0001f)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap minHeight", ref _noiseVariables.minHeight)) UpdateHeightmapNoiseVariables();
            if (ImGui.DragFloat("HeightMap maxHeight", ref _noiseVariables.maxHeight)) UpdateHeightmapNoiseVariables();
            ImGui.Spacing();
            ImGui.Text("FallOff Map Settings");
            if (ImGui.DragInt("FallOff seed", ref _falloffNoiseVariables.seed, 1)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragInt("FallOff numLayers", ref _falloffNoiseVariables.NumLayers, 1, 0, 8)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff baseRoughness", ref _falloffNoiseVariables.baseRoughness, .005f, 0)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff Roughness", ref _falloffNoiseVariables.roughness, .005f, 0)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff persistence", ref _falloffNoiseVariables.persistence, .01f, 0)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff minValue", ref _falloffNoiseVariables.minValue)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff strength", ref _falloffNoiseVariables.strength, 0.005f, 0.0001f)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff scale", ref _falloffNoiseVariables.scale, .005f, 0.0001f)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff minHeight", ref _falloffNoiseVariables.minHeight)) UpdateFallOffmapNoiseVariables();
            if (ImGui.DragFloat("FallOff maxHeight", ref _falloffNoiseVariables.maxHeight)) UpdateFallOffmapNoiseVariables();
            ImGui.Spacing();
            ImGui.Text("Humidity Map Settings");
            if (ImGui.DragInt("seed", ref _humitidyNoiseVariables.seed, 1)) UpdateHumidityNoiseVariables();
            if (ImGui.DragInt("numLayers", ref _humitidyNoiseVariables.NumLayers, 1, 0, 8)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("baseRoughness", ref _humitidyNoiseVariables.baseRoughness, .005f, 0)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("Roughness", ref _humitidyNoiseVariables.roughness, .005f, 0)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("persistence", ref _humitidyNoiseVariables.persistence, .01f, 0)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("minValue", ref _humitidyNoiseVariables.minValue)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("strength", ref _humitidyNoiseVariables.strength, 0.005f, 0.0001f)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("scale", ref _humitidyNoiseVariables.scale, .005f, 0.0001f)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("minHeight", ref _humitidyNoiseVariables.minHeight)) UpdateHumidityNoiseVariables();
            if (ImGui.DragFloat("maxHeight", ref _humitidyNoiseVariables.maxHeight)) UpdateHumidityNoiseVariables();
            ImGui.Spacing();
            ImGui.Text("Erosion Settings");
            ImGui.Checkbox("Toggle Erosion Simulation", ref _shouldErode);
            if (ImGui.Button("Save Heightmap To File")) SaveToTexture();
            ImGui.SameLine();
            if (ImGui.Button("Post Erosion Pass")) PostErosionPass();
            if (ImGui.DragInt("particle count", ref particleCount, 100)) UpdateErosionShader();
            if (ImGui.DragInt("Max Lifetime", ref maxLifetime, 1)) UpdateErosionShader();
            if (ImGui.DragFloat("inertia", ref inertia, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("sedimentCapacityFactor", ref sedimentCapacityFactor, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("minSedimentCapacity", ref minSedimentCapacity, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("depositSpeed", ref depositSpeed, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("erodeSpeed", ref erodeSpeed, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("evaporateSpeed", ref evaporateSpeed, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("gravity", ref gravity, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("startSpeed", ref startSpeed, 0.01f)) UpdateErosionShader();
            if (ImGui.DragFloat("startWater", ref startWater, 0.01f)) UpdateErosionShader();

            ImGui.End();
            _controller.Render();
        }

        /// <summary>
        /// creates the biome texture
        /// </summary>
        public void CreateBiomeTexture()
        {
            _biomeTexture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _biomeTexture);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, _gridVertexCount.X, _gridVertexCount.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

            GL.BindImageTexture(0, _biomeTexture, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        }

        /// <summary>
        /// Updates Variables
        /// </summary>
        public void UpdateParticleCreationShader()
        {
            _particleCreationShader.use();
            _particleCreationShader.SetIVec2("vertexCount", _gridVertexCount);
            _particleCreationShader.SetInt("borderSize", erosionBrushRadius);
        }

        public void UpdateBiomeShader()
        {
            _biomeGenerationShader.use();
            _biomeGenerationShader.SetIVec2("gridDimensions", _gridVertexCount);
            _biomeGenerationShader.SetInt("biomeTexture", 0);
            _biomeGenerationShader.SetInt("minMaxPrecisionFactor", _minMaxPrecisionFactor);
        }

        public void UpdateBiomeApplcationShader()
        {
            _biomeApplicationShader.use();
            _biomeApplicationShader.SetIVec2("vertexCount", _gridVertexCount);
            _biomeApplicationShader.SetInt("biomeTexture", 0);
        }

        public void UpdateVertexCreationShader()
        {
            _vertexCreationShader.use();
            _vertexCreationShader.SetIVec2("vertexCount", _gridVertexCount);
            _vertexCreationShader.SetVec2("resolution", new Vector2(_gridDimensions.X / _gridVertexCount.X, _gridDimensions.Y / _gridVertexCount.Y));
        }

        public void UpdateIndexCreationShader()
        {
            _indexCreationShader.use();
            _indexCreationShader.SetInt("trianglesPerRow", _gridVertexCount.X - 1);
        }

        public void UpdateHeightmapNoiseVariables()
        {
            _noiseApplicationShader.use();
            _noiseApplicationShader.SetIVec2("vertexCount", _gridVertexCount);
            _noiseApplicationShader.SetInt("seed", _noiseVariables.seed);
            _noiseApplicationShader.SetInt("NumLayers", _noiseVariables.NumLayers);
            _noiseApplicationShader.SetVec3("centre", _noiseVariables.centre);
            _noiseApplicationShader.SetFloat("baseRoughness", _noiseVariables.baseRoughness);
            _noiseApplicationShader.SetFloat("roughness", _noiseVariables.roughness);
            _noiseApplicationShader.SetFloat("persistence", _noiseVariables.persistence);
            _noiseApplicationShader.SetFloat("minValue", _noiseVariables.minValue);
            _noiseApplicationShader.SetFloat("strength", _noiseVariables.strength);
            _noiseApplicationShader.SetFloat("scale", _noiseVariables.scale);
            _noiseApplicationShader.SetFloat("minHeight", _noiseVariables.minHeight);
            _noiseApplicationShader.SetFloat("maxHeight", _noiseVariables.maxHeight);
            _noiseApplicationShader.SetInt("minMaxPrecisionFactor", _minMaxPrecisionFactor);
            _noiseApplicationShader.SetVec2("resolution", Resolution());
            //Update the mesh when a noise variable is changed
            UpdateMesh();
        }

        public void UpdateFallOffmapNoiseVariables()
        {
            _fallOffApplicationShader.use();
            _fallOffApplicationShader.SetIVec2("vertexCount", _gridVertexCount);
            _fallOffApplicationShader.SetInt("seed", _falloffNoiseVariables.seed);
            _fallOffApplicationShader.SetInt("NumLayers", _falloffNoiseVariables.NumLayers);
            _fallOffApplicationShader.SetVec3("centre", _falloffNoiseVariables.centre);
            _fallOffApplicationShader.SetFloat("baseRoughness", _falloffNoiseVariables.baseRoughness);
            _fallOffApplicationShader.SetFloat("roughness", _falloffNoiseVariables.roughness);
            _fallOffApplicationShader.SetFloat("persistence", _falloffNoiseVariables.persistence);
            _fallOffApplicationShader.SetFloat("minValue", _falloffNoiseVariables.minValue);
            _fallOffApplicationShader.SetFloat("strength", _falloffNoiseVariables.strength);
            _fallOffApplicationShader.SetFloat("scale", _falloffNoiseVariables.scale);
            _fallOffApplicationShader.SetFloat("minHeight", _falloffNoiseVariables.minHeight);
            _fallOffApplicationShader.SetFloat("maxHeight", _falloffNoiseVariables.maxHeight);
            _fallOffApplicationShader.SetFloat("lacunicity", _falloffNoiseVariables.lacunicity);
            _fallOffApplicationShader.SetFloat("jitter", _fallOffJitter);
            _fallOffApplicationShader.SetVec2("resolution", Resolution());
            _fallOffApplicationShader.SetInt("minMaxPrecisionFactor", _minMaxPrecisionFactor);
            //Update the mesh when a noise variable is changed
            UpdateMesh();
        }

        public void UpdateHumidityNoiseVariables()
        {
            _biomeGenerationShader.use();
            _biomeGenerationShader.SetInt("seed", _humitidyNoiseVariables.seed);
            _biomeGenerationShader.SetInt("NumLayers", _humitidyNoiseVariables.NumLayers);
            _biomeGenerationShader.SetVec3("centre", _humitidyNoiseVariables.centre);
            _biomeGenerationShader.SetFloat("baseRoughness", _humitidyNoiseVariables.baseRoughness);
            _biomeGenerationShader.SetFloat("roughness", _humitidyNoiseVariables.roughness);
            _biomeGenerationShader.SetFloat("persistence", _humitidyNoiseVariables.persistence);
            _biomeGenerationShader.SetFloat("minValue", _humitidyNoiseVariables.minValue);
            _biomeGenerationShader.SetFloat("strength", _humitidyNoiseVariables.strength);
            _biomeGenerationShader.SetFloat("scale", _humitidyNoiseVariables.scale);
            _biomeGenerationShader.SetFloat("minHeight", _humitidyNoiseVariables.minHeight);
            _biomeGenerationShader.SetFloat("maxHeight", _humitidyNoiseVariables.maxHeight);

            //Update the mesh when a noise variable is changed
            UpdateMesh();
        }

        public void UpdateNormalShader()
        {
            _normalCalculationShader.use();
            _normalCalculationShader.SetIVec2("vertexCount", _gridVertexCount);
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
        }

        public override void OnUnload()
        {
        }

        public class FBMNoiseVariables
        {
            public int seed = 0;
            public int NumLayers = 7;
            public Vector3 centre = Vector3.Zero;
            public float baseRoughness = .4f;
            public float roughness = .8f;
            public float persistence = 1;
            public float minValue = 1;
            public float strength = 1;
            public float scale = 0.05f;
            public float minHeight = 0;
            public float maxHeight = 5;
            public float lacunicity = 1;

            public FBMNoiseVariables(int seed, int numLayers, Vector3 center, float baseRoughness, float roughness, float persistence, float minValue, float strength, float scale, float minHeight, float maxHeight, float lacunicity)
            {
                this.seed = seed;
                this.NumLayers = numLayers;
                this.centre = center;
                this.baseRoughness = baseRoughness;
                this.roughness = roughness;
                this.persistence = persistence;
                this.minValue = minValue;
                this.strength = strength;
                this.scale = scale;
                this.minHeight = minHeight;
                this.maxHeight = maxHeight;
                this.lacunicity = lacunicity;
            }
        }

        //https://slideplayer.com/slide/3447433/12/images/14/Robert+Whittaker,+Cornell+Uni..jpg
        enum BiomeType
        {
            Desert,
            Savanna,
            TropicalRainforest,
            Grassland,
            Woodland,
            SeasonalForest,
            TemperateRainforest,
            BorealForest,
            Tundra,
            Ice
        }

        BiomeType[,] Biometable = new BiomeType[,]{
            //COLDEST        //COLDER          //COLD                  //HOT                          //HOTTER                       //HOTTEST
            { BiomeType.Ice, BiomeType.Tundra, BiomeType.Grassland,    BiomeType.Desert,              BiomeType.Desert,              BiomeType.Desert },              //DRYEST
            { BiomeType.Ice, BiomeType.Tundra, BiomeType.Grassland,    BiomeType.Desert,              BiomeType.Desert,              BiomeType.Desert },              //DRYER
            { BiomeType.Ice, BiomeType.Tundra, BiomeType.Woodland,     BiomeType.Woodland,            BiomeType.Savanna,             BiomeType.Savanna },             //DRY
            { BiomeType.Ice, BiomeType.Tundra, BiomeType.BorealForest, BiomeType.Woodland,            BiomeType.Savanna,             BiomeType.Savanna },             //WET
            { BiomeType.Ice, BiomeType.Tundra, BiomeType.BorealForest, BiomeType.SeasonalForest,      BiomeType.TropicalRainforest,  BiomeType.TropicalRainforest },  //WETTER
            { BiomeType.Ice, BiomeType.Tundra, BiomeType.BorealForest, BiomeType.TemperateRainforest, BiomeType.TropicalRainforest,  BiomeType.TropicalRainforest }   //WETTEST
        };

        public HydraulicErosion(GameWindow window, ImGuiController controller) : base(window, controller)
        {
        }
    }
}