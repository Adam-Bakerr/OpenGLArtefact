using Dear_ImGui_Sample;
using Engine;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
using Buffer = OpenTK.Graphics.OpenGL4.Buffer;

namespace OpenTkVoxelEngine
{
    class CubeScene : IScene
    {
        //Camera
        Camera camera;

        //Buffers 
        VBO vbo;
        VAO vao;
        EBO ebo;

        //Shaders 
        Shader defaultShader;

        //Shader Locations
        int positionLocation;
        int texCoordLocation;
        int transformMatrixLocation;

        //Textuers
        Texture texture0;
        Texture texture1;
        private Color4 _clearColor = Color4.Purple;

        float[] _vertices = {
            -0.5f, -0.5f, -0.5f,  0.0f, 0.0f,
            0.5f, -0.5f, -0.5f,  1.0f, 0.0f,
            0.5f,  0.5f, -0.5f,  1.0f, 1.0f,
            0.5f,  0.5f, -0.5f,  1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 0.0f,

            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
            0.5f, -0.5f,  0.5f,  1.0f, 0.0f,
            0.5f,  0.5f,  0.5f,  1.0f, 1.0f,
            0.5f,  0.5f,  0.5f,  1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f,

            -0.5f,  0.5f,  0.5f,  1.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
            -0.5f,  0.5f,  0.5f,  1.0f, 0.0f,

            0.5f,  0.5f,  0.5f,  1.0f, 0.0f,
            0.5f,  0.5f, -0.5f,  1.0f, 1.0f,
            0.5f, -0.5f, -0.5f,  0.0f, 1.0f,
            0.5f, -0.5f, -0.5f,  0.0f, 1.0f,
            0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
            0.5f,  0.5f,  0.5f,  1.0f, 0.0f,

            -0.5f, -0.5f, -0.5f,  0.0f, 1.0f,
            0.5f, -0.5f, -0.5f,  1.0f, 1.0f,
            0.5f, -0.5f,  0.5f,  1.0f, 0.0f,
            0.5f, -0.5f,  0.5f,  1.0f, 0.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 1.0f,

            -0.5f,  0.5f, -0.5f,  0.0f, 1.0f,
            0.5f,  0.5f, -0.5f,  1.0f, 1.0f,
            0.5f,  0.5f,  0.5f,  1.0f, 0.0f,
            0.5f,  0.5f,  0.5f,  1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f,  0.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f, 1.0f
        };

        uint[] _indices = {  // note that we start from 0!
            0, 1, 3,   // first triangle
            1, 2, 3    // second triangle
        };


        public CubeScene(GameWindow window, ImGuiController controller) : base(window, controller)
        {

        }


        public override void OnUpdateFrame(FrameEventArgs args)
        {
            //Update the camera
            camera.OnUpdateFrame(args);
        }

        public override void OnRenderFrame(FrameEventArgs args)
        {
            //Clear the window and the depth buffer
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            texture0.Use(TextureUnit.Texture0);
            texture1.Use(TextureUnit.Texture1);

            //Tell our program which shader to use
            defaultShader.Use();



            defaultShader.SetMatrix4("model", camera.Model());
            defaultShader.SetMatrix4("view", camera.View());
            defaultShader.SetMatrix4("projection", camera.Projection());

            //Bind our vao
            vao.Bind();




            //Draw our triangles
            //GL.DrawElements(PrimitiveType.Triangles,_indices.Length,DrawElementsType.UnsignedInt,0);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertices.Length);


            //Present the screen
            _window.SwapBuffers();
        }

        public override void OnMouseWheel(MouseWheelEventArgs e)
        {
            //Update the camera
            camera.OnMouseWheel(e);
        }

        public override void OnUnload()
        {
            defaultShader.Dispose();
            texture0.Dispose();
            texture1.Dispose();
            vbo.Dispose();
            vao.Dispose();
            ebo.Dispose();
        }

        public override void OnLoad()
        {
            GL.ClearColor(_clearColor);

            //Enable Z Depth Testing
            GL.Enable(EnableCap.DepthTest);

            //Create Camera
            camera = new Camera(_window, 0.01f, 500f);

            CreateShader("shader.vert", "shader.frag");
            FindShaderLocations();
            LoadTextures();
            CreateBuffers();
        }

        void LoadTextures()
        {
            //create our textures and bind them to their respective units
            texture0 = Texture.LoadFromFile("Penrose-dreieck.png", TextureUnit.Texture0);
            texture0.Use(TextureUnit.Texture0);

            texture1 = Texture.LoadFromFile("smile.jpg", TextureUnit.Texture1);
            texture1.Use(TextureUnit.Texture1);

            //Tell the shader what unit the uniform reads from
            defaultShader.SetInt("texture0", 0);
            defaultShader.SetInt("texture1", 1);
        }

        void FindShaderLocations()
        {
            positionLocation = defaultShader.GetAttribLocation("aPosition");
            texCoordLocation = defaultShader.GetAttribLocation("aTexCoord");
        }


        void CreateBuffers()
        {

            //Create vbo and buffer data
            vbo = new VBO();
            vbo.BufferData(_vertices, BufferUsageHint.StaticDraw);

            //Create vao and enable it
            vao = new VAO();
            List<(int, int, VertexAttribPointerType, bool, int, int)> Pointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>()
            {
                (positionLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0),
                (texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float))
            };
            vao.Enable(Pointers);

            //Create element buffer object
            ebo = new EBO();
            ebo.BufferData(_indices, BufferUsageHint.StaticDraw);

        }


        void CreateShader(string VertexPath, string FragmentPath)
        {
            defaultShader = new Shader(VertexPath, FragmentPath);
            defaultShader.Use();
        }

    }
}
