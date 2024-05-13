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
    internal class SurfaceNets : IScene
    {
        //Camera
        private Camera _camera;

        //Shaders
        private Shader _shader;

        private ComputeShader _dfShader;
        private ComputeShader _intersectionLocatorShader;
        private ComputeShader _createVertexShader;

        //Shader Paths
        private string _assemblyPath = "OpenGL_Artefact_Solution.Shaders.SurfaceNets";

        private string _vertexPath = "shader.vert";
        private string _fragmentPath = "shader.frag";
        private string _distanceFieldGenerationPath = "createDF.compute";
        private string _marchCubesShaderPath = "dualContour.glsl";
        private string _createVertexPath = "CreateVerticies.glsl";

        //Buffers
        private VAO _vao;

        private int _vbo;
        private int _dfbo; //Distance Field Buffer Object
        private int _fpbo; //triangle connection buffer object
        private int _vcbo; //Vertex Counter Buffer Object

        //Variables
        private static Vector3i _dimensions = new Vector3i(128, 128, 128);
        private Vector3i _voxelDimensions = _dimensions - Vector3i.One;

        private Vector3 _resolution = new Vector3(.1f);
        private int _workGroupSize = 8;
        private float _surfaceLevel = .5f;
        private float _grassBlendAmount = .875f;
        private float _grassSlopeThreshold = .15f;
        private float _dualContourErrorValue = .1f;
        private bool _drawTestSpheres;

        private uint vertexCounterValue;

        //noise variables
        private HydraulicErosion.FBMNoiseVariables _heightMapNoiseVariables;

        public int VertexSize() => (sizeof(float) * 12);

        public int VertexCount() => _dimensions.X * _dimensions.Y * _dimensions.Z;

        public SurfaceNets(GameWindow window, ImGuiController controller) : base(window, controller)
        {
        }

        public struct vertex
        {
            public Vector4 Pos;
            public Vector4 Color;
            public Vector4 Normal;
        };

        public struct tri
        {
            public vertex a;
            public vertex b;
            public vertex c;
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
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, _vcbo);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint), 0, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);

            //DF buffer
            _dfbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _dfbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * 4 * VertexCount(), nint.Zero, BufferUsageHint.DynamicDraw);
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

        private float centerOffset = 0;

        public void RunShaders()
        {
            ResetAtomicCounter();

            ////////////////////Create DF Values////////////////////////////
            int dfx = (int)(MathF.Ceiling(MathF.Max((_dimensions.X), 1) / _workGroupSize));
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
            int fpy = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y), 1) / _workGroupSize));
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

            int cvx = (int)(MathF.Ceiling(MathF.Max((_dimensions.X), 1) / _workGroupSize));
            int cvy = (int)(MathF.Ceiling(MathF.Max((_dimensions.Y), 1) / _workGroupSize));
            int cvz = (int)(MathF.Ceiling(MathF.Max((_dimensions.Z), 1) / _workGroupSize));

            GL.DispatchCompute(cvx, cvy, cvz);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            ///////////////////////////////////////////////////////////////

            //Get Counter Data To Reduce The Amount Of Verticies Drawn My a order of magnitude
            GL.GetBufferSubData(BufferTarget.AtomicCounterBuffer, 0, sizeof(uint), ref vertexCounterValue);

            Console.WriteLine($"Triangle Count : {vertexCounterValue / 3.0f}");
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);
        }

        public void CreateShaders()
        {
            //Create the shader used to draw verts to the screen
            _shader = new Shader(_assemblyPath, _vertexPath, _fragmentPath);
            UpdateDrawingShader();

            //Create our cube df data
            _dfShader = new ComputeShader(_assemblyPath, _distanceFieldGenerationPath);
            _heightMapNoiseVariables = new FBMNoiseVariables(0, 5, Vector3.Zero, -0.16f, .725f, 3.060f, -10, 0.025f, -0.005f, 0, 150, 1);

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
            _shader.SetIVec3("_dimensions", _dimensions);
        }

        public void UpdateVertexCreationShader()
        {
            _createVertexShader.use();
            _createVertexShader.SetVec4("resolution", new Vector4(_resolution, 1));
            _createVertexShader.SetIVec3("vertexCount", _dimensions);
            _createVertexShader.SetFloat("surfaceLevel", _surfaceLevel);
        }

        public void UpdateDFShader()
        {
            _dfShader.use();
            _dfShader.SetVec3("resolution", _resolution);
            _dfShader.SetIVec3("vertexCount", _dimensions);
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

            _dfShader.SetFloat("errorValue", _dualContourErrorValue);
        }

        public void UpdateIntersectionLocatorShader()
        {
            _intersectionLocatorShader.use();
            _intersectionLocatorShader.SetVec3("resolution", _resolution);
            _intersectionLocatorShader.SetIVec3("vertexCount", _dimensions);
            _intersectionLocatorShader.SetFloat("surfaceLevel", _surfaceLevel);

            _intersectionLocatorShader.SetFloat("_GrassSlopeThreshold", _grassSlopeThreshold);
            _intersectionLocatorShader.SetFloat("_GrassBlendAmount", _grassBlendAmount);
        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            _camera.OnUpdateFrame(args);
        }

        private float _totalTime = 0;

        public override void OnRenderFrame(FrameEventArgs args)
        {
            if (_window.IsKeyDown(Keys.End)) _totalTime += (float)args.Time;
            //OnDFUpdate();

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
            GL.DrawArrays(PrimitiveType.Triangles, 0, (int)vertexCounterValue);

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

            if (ImGui.DragFloat("IsoLevel", ref _surfaceLevel, .025f, 0, 1)) OnDFUpdate();
            if (ImGui.DragFloat("Grass Slope Threshold", ref _grassSlopeThreshold, .025f, 0, 1)) OnDFUpdate();
            if (ImGui.DragFloat("Grass Blend Amount", ref _grassBlendAmount, .025f, 0, 1)) OnDFUpdate();
            ImGui.Checkbox("Debug Test Spheres", ref _drawTestSpheres);
            ImGui.End();

            _controller.Render();
        }
    }
}