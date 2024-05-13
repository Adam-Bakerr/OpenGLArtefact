using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Drawing;
using static OpenTkVoxelEngine.HydraulicErosion;
using GL = OpenTK.Graphics.OpenGL4.GL;

namespace OpenTkVoxelEngine
{
    internal class MCChunk
    {
        public int _vbo;
        public Matrix4 modelMatrix;
        public Vector3 position;
        public uint vertexCount;

        public MCChunk(Vector3i vertexDims, Vector3 res, Vector3 position)
        {
            modelMatrix = Matrix4.CreateTranslation(position);
            this.position = position;
        }

        public void CreateNewVertexBuffer(Vector3i _dimensions, Vector3 res, int size)
        {
            int numPoints = _dimensions.X * _dimensions.Y * _dimensions.Z;
            Vector3i numVoxelsPerAxis = new Vector3i(_dimensions.X - 1, _dimensions.Y - 1, _dimensions.Z - 1);
            int numVoxels = numVoxelsPerAxis.X * numVoxelsPerAxis.Y * numVoxelsPerAxis.Z;
            int maxTriangleCount = numVoxels * 5;
            int maxVertexCount = maxTriangleCount * 4;

            //Vertex Buffer
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * 12 * maxVertexCount, nint.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _vbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }
    }

    internal class ChunkedMarchingCubes : IScene
    {
        private List<MCChunk> chunks = new List<MCChunk>();
        private List<MCChunk> chunksToCreate = new List<MCChunk>();

        //Camera
        private Camera _camera;

        //Shaders
        private Shader _shader;

        private ComputeShader _dfShader;
        private ComputeShader _marchCubesShader;

        //Shader Paths
        private string _assemblyPath = "S225170_-_OPENGL_-_Procedural_Content_Generation_Artefact.Shaders.MarchingCubes";

        private string _vertexPath = "shader.vert";
        private string _fragmentPath = "shader.frag";
        private string _distanceFieldGenerationPath = "createDF.compute";
        private string _marchCubesShaderPath = "MarchCubesShader.glsl";

        //Buffers
        private VAO _vao;

        private int _vbo;
        private int _dfbo; //Distance Field Buffer Object
        private int _tcbo; //triangle connection buffer object
        private int _vcbo; //Vertex Counter Buffer Object

        //Variables
        private Matrix4 _model = Matrix4.Identity;

        private Vector3i _dimensions = new Vector3i(32, 8, 32);
        private Vector3 _resolution = new Vector3(.125f);
        private int _workGroupSize = 8;
        private float _surfaceLevel = .425f;
        private float _grassBlendAmount = .875f;
        private float _grassSlopeThreshold = .15f;
        private float _totalTime = 0f;
        private bool _drawTestSpheres;

        private uint vertexCounterValue;

        //noise variables
        private HydraulicErosion.FBMNoiseVariables _heightMapNoiseVariables;

        private HydraulicErosion.FBMNoiseVariables _caveMapNoiseVariables;

        public int VertexSize() => (sizeof(float) * 12);

        public int VertexCount() => _dimensions.X * _dimensions.Y * _dimensions.Z;

        public int MaxVertexCount() => (((_dimensions.X - 1) * (_dimensions.Y - 1) * (_dimensions.Z - 1)) * 20);

        public ChunkedMarchingCubes(GameWindow window, ImGuiController controller) : base(window, controller)
        {
        }

        /// <summary>
        /// Creates all buffers
        /// </summary>
        public void CreateBuffers()
        {
            //Vertex Buffer
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
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, _vcbo);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), 0, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);

            //DF buffer
            _dfbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * VertexCount(), nint.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _dfbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //trianglition buffer
            _tcbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _tcbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int) * 4096, triangulation, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _tcbo);
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

        /// <summary>
        /// Used to reset the vertex counter
        /// </summary>
        public void ResetAtomicCounter()
        {
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, _vcbo);
            GL.ClearNamedBufferData(_vcbo, PixelInternalFormat.R32ui, PixelFormat.Red, PixelType.UnsignedInt, 0);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);
        }

        /// <summary>
        /// Runs The Shaders
        /// </summary>
        public void RunShaders(MCChunk chunk)
        {
            ResetAtomicCounter();

            int dfx = (int)(MathF.Ceiling(MathF.Max((_dimensions.X), 1) / _workGroupSize));

            int dfy = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y), 1) / _workGroupSize));

            int dfz = (int)(MathF.Ceiling(MathF.Max((_dimensions.Z), 1) / _workGroupSize));

            //Create DF Values
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _dfbo);

            _dfShader.use();
            _dfShader.SetVec3("chunkPosition", chunk.position);
            _dfShader.SetFloat("totalTime", _totalTime);
            _dfShader.SetBool("testSpheres", _drawTestSpheres);

            GL.DispatchCompute(dfx, dfy, dfz);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //March Cubes
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _dfbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _vbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _vbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _tcbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _tcbo);

            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, _vcbo);
            GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 3, _vcbo);

            _marchCubesShader.use();

            int x = (int)(MathF.Ceiling(MathF.Max((_dimensions.X - 1), 1) / _workGroupSize));
            int y = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y - 1), 1) / _workGroupSize));
            int z = (int)(MathF.Ceiling(MathF.Max((_dimensions.Z - 1), 1) / _workGroupSize));

            GL.DispatchCompute(x, y, z);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            GL.GetBufferSubData(BufferTarget.AtomicCounterBuffer, 0, sizeof(uint), ref chunk.vertexCount);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);

            if (chunk._vbo == 0) chunk._vbo = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ArrayBuffer, chunk._vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, VertexSize() * (int)chunk.vertexCount, nint.Zero, BufferUsageHint.DynamicDraw);

            GL.BindBuffer(BufferTarget.CopyReadBuffer, _vbo);
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, chunk._vbo);
            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, nint.Zero, nint.Zero, VertexSize() * (int)chunk.vertexCount);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

            chunks.Add(chunk);
        }

        public void DeleteBuffers()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_dfbo);
            GL.DeleteBuffer(_tcbo);
            GL.DeleteBuffer(_vcbo);
        }

        /// <summary>
        /// create all shaders
        /// </summary>
        public void CreateShaders()
        {
            //Create the shader used to draw verts to the screen
            _shader = new Shader(_assemblyPath, _vertexPath, _fragmentPath);
            UpdateDrawingShader();

            //Create our cube df data
            _dfShader = new ComputeShader(_assemblyPath, _distanceFieldGenerationPath);
            _heightMapNoiseVariables = new FBMNoiseVariables(0, 5, Vector3.Zero, -0.16f, .725f, 3.060f, -10, 0.025f, -0.005f, 0, 150, 1);
            _caveMapNoiseVariables = new FBMNoiseVariables(0, 3, Vector3.Zero, .6f, .2f, 4.31f, 0, 19.935f, 0.105f, 0, 150, 1);

            UpdateDFShader();

            //Create the marching cubes shaders
            _marchCubesShader = new ComputeShader(_assemblyPath, _marchCubesShaderPath);
            UpdateMarchCubesShader();
        }

        /// <summary>
        /// update the screen shader
        /// </summary>
        public void UpdateDrawingShader()
        {
            _shader.Use();
            _shader.SetIVec3("_dimensions", _dimensions);
        }

        /// <summary>
        /// update the distance field variables
        /// </summary>
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
        }

        /// <summary>
        /// Update the marching cubes variables
        /// </summary>
        public void UpdateMarchCubesShader()
        {
            _marchCubesShader.use();
            _marchCubesShader.SetVec3("resolution", _resolution);
            _marchCubesShader.SetIVec3("vertexCount", _dimensions);
            _marchCubesShader.SetFloat("surfaceLevel", _surfaceLevel);

            _marchCubesShader.SetFloat("_GrassSlopeThreshold", _grassSlopeThreshold);
            _marchCubesShader.SetFloat("_GrassBlendAmount", _grassBlendAmount);
        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            chunksToCreate = chunksToCreate.OrderBy(o => ((o.position) - _camera.Position()).Length).ToList();
            if (chunksToCreate.Count > 0)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                float i = 0;
                while (i < frameTime)
                {
                    //used to track the loadtime
                    Vector3 cameraPosition = _camera.Position();
                    Vector3 chunkPos = chunksToCreate[0].position;
                    if (Vector3.Distance(new Vector3(chunkPos.X, cameraPosition.Y, chunkPos.Z), cameraPosition) < 256)
                    {
                        RunShaders(chunksToCreate[0]);
                        chunksToCreate.RemoveAt(0);
                    }

                    i += (float)watch.Elapsed.TotalSeconds;
                }
                watch.Stop();
            }

            for (int i = 0; i < chunks.Count; i++)
            {
                Vector3 cameraPosition = _camera.Position();
                Vector3 chunkPos = chunks[i].position;
                if (Vector3.Distance(new Vector3(chunkPos.X, cameraPosition.Y, chunkPos.Z), cameraPosition) > 256)
                {
                    chunksToCreate.Add(new MCChunk(_dimensions, _resolution, chunkPos));
                    int bufferToDelete = chunks[i]._vbo;
                    chunks.RemoveAt(i);
                    GL.DeleteBuffer(bufferToDelete);
                }
            }
            _camera.OnUpdateFrame(args);
        }

        public float frameTime = 0;

        public override void OnRenderFrame(FrameEventArgs args)
        {
            //icreminent total time
            _totalTime += (float)args.Time;
            frameTime = (float)args.Time;

            //Clear the window and the depth buffer
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //Basic shader stuff
            _shader.Use();
            _shader.SetMatrix4("model", Matrix4.Identity);
            _shader.SetMatrix4("view", _camera.View());
            _shader.SetMatrix4("projection", _camera.Projection());

            //Update the light visually
            _shader.SetVec3("light.position", Vector3.UnitY);
            _shader.SetFloat("light.constant", 1.0f);
            _shader.SetFloat("light.linear", 0.09f);
            _shader.SetFloat("light.quadratic", 0.032f);
            _shader.SetVec3("light.ambient", Vector3.UnitZ * .05f);
            _shader.SetVec3("light.diffuse", new Vector3(0.8f, 0.8f, 0.8f));
            _shader.SetVec3("light.specular", new Vector3(.10f, .10f, .10f));

            //Draw all of our triangles, missing empty verticies
            _vao.Bind();
            for (int i = 0; i < chunks.Count; i++)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, chunks[i]._vbo);
                _shader.SetMatrix4("model", chunks[i].modelMatrix);

                List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
                {
                    (_shader.GetAttribLocation("aPosition"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 0),
                    (_shader.GetAttribLocation("aColor"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 4 * sizeof(float)),
                    (_shader.GetAttribLocation("aNormal"), 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 8 * sizeof(float))
                };
                _vao.Enable(Pointers);

                GL.DrawArrays(PrimitiveType.Triangles, 0, (int)chunks[i].vertexCount);
            }

            //Draws the imgui window
            DrawImgui();

            //Swap buffers to draw the screen
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
            //used to track the loadtime
            var watch = System.Diagnostics.Stopwatch.StartNew();

            //Change the clear color
            GL.ClearColor(Color.Black);

            //Create the camera
            _camera = new Camera(_window, 0.01f, 2000f);

            //Create Shaders
            CreateShaders();

            //Create Buffers
            CreateBuffers();
            for (int x = 0; x < 256; x++)
            {
                for (int z = 0; z < 256; z++)
                {
                    chunksToCreate.Add(new MCChunk(_dimensions, _resolution, new Vector3(x, 0, z) * (_dimensions - Vector3i.One) * _resolution));
                }
            }

            //DeleteBuffers();

            //Enable Z Depth Testing
            GL.Enable(EnableCap.DepthTest);

            //Log the execution Time
            watch.Stop();
            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// The necassary functions in order to be called to update the volume
        /// </summary>
        public void OnDFUpdate()
        {
            UpdateDFShader();
            UpdateMarchCubesShader();

            for (int i = 0; i < chunks.Count; i++)
            {
                RunShaders(chunks[i]);
            }
        }

        /// <summary>
        /// Draws the imgui window allowing the user to edit noise variables
        /// </summary>
        public override void DrawImgui()
        {
            if (!_window.IsKeyDown(Keys.LeftAlt)) return;

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 25));
            ImGui.Begin("Debug Menu", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration);
            ImGui.Text("Vertex Count: " + vertexCounterValue.ToString());
            ImGui.Checkbox("Debug Test Spheres", ref _drawTestSpheres);
            ImGui.End();

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 70));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(900, _window.ClientSize.Y));
            ImGui.SetNextItemWidth(900);
            ImGui.Begin("Marching Cubes Noise Variables", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground);
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
            if (ImGui.DragFloat("Grass Slope Threshold", ref _grassSlopeThreshold, .025f, 0, 1)) OnDFUpdate();
            if (ImGui.DragFloat("Grass Blend Amount", ref _grassBlendAmount, .025f, 0, 1)) OnDFUpdate();
            ImGui.End();

            _controller.Render();
        }

        /// <summary>
        /// Table can be found at https://paulbourke.net/geometry/polygonise/
        /// I Flattened it myself to make passing into a buffer just that bit easier
        /// </summary>
        private int[] triangulation = new int[]{
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
    }
}