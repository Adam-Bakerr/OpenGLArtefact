using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace OpenTkVoxelEngine
{
    internal class HydraulicErosion : IScene
    {

        Timer _timer;

        //Grid Definitions
        Vector2i _gridVertexCount = new Vector2i(512, 512);
        Vector2 _gridDimensions = new Vector2(10, 10);
        Vector2 Resolution() => _gridDimensions / _gridVertexCount;
        int VertexCount() => _gridVertexCount.X * _gridVertexCount.Y;
        int IndexCount() => (_gridVertexCount.X - 1) * (_gridVertexCount.Y - 1);

        //Buffers
        int _ssbo; // this acts as a vbo
        int _ebo;
        VAO _vao;

        //Shaders
        Shader _terrainShader;
        ComputeShader _vertexCreationShader;
        ComputeShader _indexCreationShader;
        ComputeShader _noiseApplicationShader;
        ComputeShader _normalCalculationShader;

        //Camera
        Camera camera;

        string _vertexPath = "erosionVert.vert";
        string _fragmentPath = "erosionFrag.frag";
        string _createVertexComputePath = "createVertcies.compute";
        string _createIndicesComputePath = "createIndices.compute";
        string _noiseApplicationComputePath = "noiseApplication.compute";
        string _normalCalculationComputePath = "normalCalculation.compute";

        //Shader Noise Variables
        FBMNoiseVariables _noiseVariables;

        void CreateBuffers()
        {            
            //Vertex Buffer
            _ssbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer,_ssbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * 12 * VertexCount(), nint.Zero,BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

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
            GL.BindBuffer(BufferTarget.ElementArrayBuffer,_ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer,IndexCount() * 6 * sizeof(uint),IntPtr.Zero, BufferUsageHint.StaticDraw);
            
        }

        void CreateShaders()
        {
            //Our material shader
            _terrainShader = new Shader(_vertexPath, _fragmentPath);
            _terrainShader.Use();

            //Compute shader that handles the creation of verticies
            _vertexCreationShader = new ComputeShader(_createVertexComputePath);
            _vertexCreationShader.use();
            UpdateVertexCreationShader();

            //Compute shader that handles the creation of indices
            _indexCreationShader = new ComputeShader(_createIndicesComputePath);
            _indexCreationShader.use();
            UpdateIndexCreationShader();

            //Compute shader that handles applying a base noise map onto the mesh
            _noiseApplicationShader = new ComputeShader(_noiseApplicationComputePath);
            _noiseApplicationShader.use();
            _noiseVariables = new FBMNoiseVariables();
            UpdateNoiseVariables();


            //Compute shader that handles recalculation normals of the mesh
            _normalCalculationShader = new ComputeShader(_normalCalculationComputePath);
            _normalCalculationShader.use();
            UpdateNormalShader();
        }

        public HydraulicErosion(GameWindow window) : base(window)
        {            
            //Set clear color
            GL.ClearColor(Color.Black);

            //Create the camera
            camera = new Camera(_window,0.01f,500f);

            //Create shader and update its uniforms
            CreateShaders();

            //create all buffers
            CreateBuffers();

        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            camera.OnUpdateFrame(args);
        }

        Vector3 LightPos = Vector3.One;
        float _workGroupSize = 8.0f;

        float totalTime = 0;
        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Tell openGL to clear the color buffer and depth buffer
            GL.Clear(ClearBufferMask.ColorBufferBit);
            totalTime += (float)args.Time;
            LightPos = new Vector3(0, 5f + (float)Math.Sin(totalTime) * 5f,0f);


            //Create Vertices
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);

            _vertexCreationShader.use();
            _vertexCreationShader.SetFloat("time",totalTime);

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);


            //Create Indices
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ebo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ebo);

            _indexCreationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling((_gridVertexCount.X) / _workGroupSize), (int)MathF.Ceiling((_gridVertexCount.Y) / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);


            //Add Noise
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);

            _noiseApplicationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Update Normals
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);

            _normalCalculationShader.use();

            GL.DispatchCompute((int)MathF.Ceiling(_gridVertexCount.X / _workGroupSize), (int)MathF.Ceiling(_gridVertexCount.Y / _workGroupSize), 1);
            GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);


            //Use our terrain material
            _terrainShader.Use();
            _terrainShader.SetMatrix4("model",Matrix4.Identity);
            _terrainShader.SetMatrix4("view", camera.View());
            _terrainShader.SetMatrix4("projection", camera.Projection());
            _terrainShader.SetVec3("viewPos",camera.Position());

            //Point Light Settings
            _terrainShader.SetVec3("light.position", LightPos);
            _terrainShader.SetFloat("light.constant", 1.0f);
            _terrainShader.SetFloat("light.linear", 0.09f);
            _terrainShader.SetFloat("light.quadratic", 0.032f);
            _terrainShader.SetVec3("light.ambient", Vector3.UnitZ * .05f);
            _terrainShader.SetVec3("light.diffuse", new Vector3(0.8f, 0.8f, 0.8f));
            _terrainShader.SetVec3("light.specular", new Vector3(0, 1.0f, 0));


            //Change the drawmode of the mesh
            //GL.PolygonMode(MaterialFace.FrontAndBack,PolygonMode.Line);

            //Draw the mesh
            _vao.Bind();
            GL.DrawElements(PrimitiveType.Triangles,IndexCount() * 6,DrawElementsType.UnsignedInt,0);

            //Swap buffers to render the mesh
            _window.SwapBuffers();
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

        public void UpdateNoiseVariables()
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

        public override void OnLoad()
        {
            
        }


        public class FBMNoiseVariables{

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
            public float maxHeight = 15;
        }
    }
}
