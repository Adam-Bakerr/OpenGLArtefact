using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
using System.Reflection.Metadata;
using System.Runtime.Remoting;

namespace OpenTkVoxelEngine
{
    class Shader
    {
        int _handle;

        Dictionary<string,int> _uniformLocations = new Dictionary<string,int>();

        public Shader(string vertexPath, string fragmentPath)
        {
            if (vertexPath == "" || fragmentPath == "") return;


            //Read Shaders To Strings
            string vertexSource = File.ReadAllText(vertexPath);
            string fragmentSource = File.ReadAllText(fragmentPath);

            //Create shaders from source
            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader,vertexSource);

            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);

            //Compile shaders and check for errors
            GL.CompileShader(vertexShader);

            //Get error status
            GL.GetShader(vertexShader,ShaderParameter.CompileStatus,out int vertexStatus);
            
            //If it has failed
            if (vertexStatus == 0)
            {
                string infoLog = GL.GetShaderInfoLog(vertexShader);
                Console.WriteLine("Vertex compile error" + infoLog);
            }

            //Compile shaders and check for errors
            GL.CompileShader(fragmentShader);

            //Get error status
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fragStatus);

            //If it has failed
            if (fragStatus == 0)
            {
                string infoLog = GL.GetShaderInfoLog(fragmentShader);
                Console.WriteLine("Fragment compile error"+infoLog);
            }



            //Create shader handle

            _handle = GL.CreateProgram();

            //Attached the created shaders to the handle
            GL.AttachShader(_handle,vertexShader);
            GL.AttachShader(_handle, fragmentShader);


            //Link the shader handle
            GL.LinkProgram(_handle);

            //Check for errors with the linking process
            GL.GetProgram(_handle,GetProgramParameterName.LinkStatus,out int linkStatus);

            if (linkStatus == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_handle);
                Console.WriteLine("Shader Link Error: " + infoLog);
            }



            // Get all unifroms from the shader
            GL.GetProgram(_handle, GetProgramParameterName.ActiveUniforms, out var numberOfUniforms);

            // Next, allocate the dictionary to hold the locations.
            _uniformLocations = new Dictionary<string, int>();

            // Loop over all the uniforms,
            for (var i = 0; i < numberOfUniforms; i++)
            {
                // get the name of this uniform,
                var key = GL.GetActiveUniform(_handle, i, out _, out _);

                // get the location,
                var location = GL.GetUniformLocation(_handle, key);

                // and then add it to the dictionary.
                _uniformLocations.Add(key, location);
            }


            //Do Some cleanup
            GL.DetachShader(_handle,vertexShader);
            GL.DetachShader(_handle,fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

        }


        public void Use()
        {
            GL.UseProgram(_handle);
        }

        public void SetInt(string name, int value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform1(location, value);
        }
        public void SetUint(string name, uint value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform1(location, value);
        }

        public void SetFloat(string name, float value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform1(location, value);
        }

        public void SetBool(string name, bool value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform1(location, Convert.ToInt32(value));
        }

        public void SetVec4(string name, Vector4 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform4(location, value);
        }

        public void SetVec3(string name, Vector3 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform3(location, value);
        }

        public void SetIVec3(string name, Vector3i value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform3(location, value);
        }

        public void SetDouble(string name, double value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform1(location, value);
        }

        public void SetVec2(string name, Vector2 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform2(location, value);
        }
        public void SetIVec2(string name, Vector2i value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform2(location, value);
        }


        public void SetMatrix4(string name, Matrix4 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.UniformMatrix4(location, true, ref value);
        }

        public void SetMatrix3(string name, Matrix3 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.UniformMatrix3(location, true, ref value);
        }

        public void SetMatrix2(string name, Matrix2 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.UniformMatrix2(location, true, ref value);
        }

        //Get Attribute local from string
        public int GetAttribLocation(string attribName)
        {
            return GL.GetAttribLocation(_handle, attribName);
        }


        bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(_handle);

                disposedValue = true;
            }
        }

        ~Shader()
        {
            if(!disposedValue) Console.WriteLine("Shader Resource Leak, Handle: " + _handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
