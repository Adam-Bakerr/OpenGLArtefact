using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static OpenTkVoxelEngine.HydraulicErosion;
using GL = OpenTK.Graphics.OpenGL4.GL;

namespace OpenTkVoxelEngine
{
    internal class NoiseVisualization : IScene
    {
        private Camera _camera;

        private VAO _vao;

        private Shader _shader;
        private string _assemblyPath = "OpenGL_Artefact_Solution.Shaders.NoiseVisualization";
        private string _vertexFileName = "shader.vert";
        private string _fragmentFileName = "shader.frag";
        private float _time;

        private FBMNoiseVariables _noiseVariables;
        private float _jitter = 1f;
        private NoiseType currentNoiseType = NoiseType.PerlinFBM;

        private enum NoiseType
        {
            Perlin,
            PerlinFBM,
            Vornori,
            VornoriFBM,
            Curl,
            CurlFBM,
            Snoise,
            SnoiseFBM,
            DomainWarping,
            normalNoise,
            count
        }

        public NoiseVisualization(GameWindow window, ImGuiController controller) : base(window, controller)
        {
            Console.WriteLine("TEst");
        }

        public void CreateScreenShader()
        {
            _shader = new Shader(_assemblyPath, _vertexFileName, _fragmentFileName);
            _shader.Use();
        }

        public void CreateBuffers()
        {
            _vao = new VAO();
        }

        public override void OnUpdateFrame(FrameEventArgs args)
        {
            _camera.OnUpdateFrame(args);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            GL.ClearColor(Color4.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);


            _time += (float)args.Time;

            UpdateFragVariables();

            _vao.Bind();


            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            DrawImgui();

            _window.SwapBuffers();
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            _camera.OnMouseWheel(e);
        }

        public override void DrawImgui()
        {
            if (!_window.IsKeyDown(Keys.LeftAlt)) return;

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 70));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(900, _window.ClientSize.Y));
            ImGui.SetNextItemWidth(900);
            ImGui.Begin("Noise Variables", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground);

            //Select Noise Type
            for (int i = 0; i < (int)NoiseType.count; i++)
            {
                if (i == (int)currentNoiseType)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button(Enum.GetName((NoiseType)i)))
                {
                    currentNoiseType = (NoiseType)i;
                }
                if (i == (int)currentNoiseType)
                {
                    ImGui.EndDisabled();
                }
            }

            if (currentNoiseType == NoiseType.CurlFBM || currentNoiseType == NoiseType.PerlinFBM || currentNoiseType == NoiseType.SnoiseFBM || currentNoiseType == NoiseType.VornoriFBM || currentNoiseType == NoiseType.DomainWarping)
            {
                ImGui.DragInt("seed", ref _noiseVariables.seed, 1);
                ImGui.DragInt("numLayers", ref _noiseVariables.NumLayers, 1, 0, 8);
                ImGui.DragFloat("baseRoughness", ref _noiseVariables.baseRoughness, .005f, 0);
                ImGui.DragFloat("Roughness", ref _noiseVariables.roughness, .005f, 0);
                ImGui.DragFloat("persistence", ref _noiseVariables.persistence, .01f, 0);
                ImGui.DragFloat("strength", ref _noiseVariables.strength, 0.005f, 0.0001f);
                ImGui.DragFloat("scale", ref _noiseVariables.scale, .005f, 0.0001f);
            }

            if (currentNoiseType == NoiseType.Vornori || currentNoiseType == NoiseType.VornoriFBM)
            {
                ImGui.DragFloat("jitter", ref _jitter, .005f, 0.0001f);
            }

            ImGui.End();
            _controller.Render();
        }

        public override void OnUnload()
        {
        }

        public void UpdateFragVariables()
        {
            _shader.Use();
            _shader.SetFloat("time", _time);
            _shader.SetVec2("dimensions", _window.ClientSize);
            _shader.SetVec3("iro", _camera.Position());
            _shader.SetVec3("cameraForward", _camera.Forward());
            _shader.SetVec3("cameraUp", _camera.Up());
            _shader.SetVec3("cameraRight", _camera.Right());

            _shader.SetInt("currentNoiseType", (int)currentNoiseType);

            _shader.SetInt("seed", _noiseVariables.seed);
            _shader.SetInt("NumLayers", _noiseVariables.NumLayers);
            _shader.SetVec3("centre", _noiseVariables.centre);
            _shader.SetFloat("baseRoughness", _noiseVariables.baseRoughness);
            _shader.SetFloat("roughness", _noiseVariables.roughness);
            _shader.SetFloat("persistence", _noiseVariables.persistence);
            _shader.SetFloat("strength", _noiseVariables.strength);
            _shader.SetFloat("scale", _noiseVariables.scale);
            _shader.SetFloat("lacunicity", _noiseVariables.lacunicity);
            _shader.SetFloat("jitter", _jitter);
        }

        public override void OnLoad()
        {
            _camera = new Camera(_window, 0.1f, 100, Vector3.UnitY);

            _noiseVariables = new FBMNoiseVariables(0, 3, Vector3.Zero, .6f, .8f, 4.31f, 0, 1f, 0.105f, 1, 1, 1);

            CreateScreenShader();
            CreateBuffers();
        }
    }
}