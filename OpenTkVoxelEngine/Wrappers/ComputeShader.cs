using Engine;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
using System.Reflection.Metadata;

namespace OpenTkVoxelEngine
{
    public class ComputeShader
    {
        int _handle;

        public void use()
        {
            GL.UseProgram(_handle);
        }

        //Load From File
        public ComputeShader(string Path)
        {
            //Read Shader Soruce Code
            string ShaderSoruce = File.ReadAllText(Path);

            var compute = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(compute, ShaderSoruce);
            GL.CompileShader(compute);

            //Get the compile status of the shader to see if it compiled 
            GL.GetShader(compute, ShaderParameter.CompileStatus, out int successCompute);
            //Check to see if failed
            if (successCompute == 0)
            {
                string infoLog = GL.GetShaderInfoLog(compute);
                Console.WriteLine(infoLog + "In Shader" + Path);
            }

            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, compute);
            GL.LinkProgram(_handle);

            //Get the Link status of the program
            GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out int successProgram);
            //Check to see if failed
            if (successProgram == 0)
            {
                string infoLog = GL.GetShaderInfoLog(_handle);
                Console.WriteLine(infoLog + "In Shader" + Path);
            }

            GL.DetachShader(_handle, compute);

            GL.DeleteShader(compute);

        }
        //Load From Assembly
        public ComputeShader(string AssemblyPath, string FileName)
        {
            if (AssemblyPath == "" || FileName == "") return;

            string computeSource;

            //Read From Assembly Shaders To Strings
            using Stream strv = typeof(MainProgram).Assembly.GetManifestResourceStream(AssemblyPath + "." + FileName);
            using (StreamReader reader = new StreamReader(strv))
            {
                computeSource = reader.ReadToEnd();
            }


            var compute = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(compute, computeSource);
            GL.CompileShader(compute);

            //Get the compile status of the shader to see if it compiled 
            GL.GetShader(compute, ShaderParameter.CompileStatus, out int successCompute);
            //Check to see if failed
            if (successCompute == 0)
            {
                string infoLog = GL.GetShaderInfoLog(compute);
                Console.WriteLine(infoLog + "In Shader" + FileName);
            }

            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, compute);
            GL.LinkProgram(_handle);

            //Get the Link status of the program
            GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out int successProgram);
            //Check to see if failed
            if (successProgram == 0)
            {
                string infoLog = GL.GetShaderInfoLog(_handle);
                Console.WriteLine(infoLog + "In Shader" + FileName);
            }

            GL.DetachShader(_handle, compute);

            GL.DeleteShader(compute);

        }

        public int GetAttribLocation(String attribName)
        {
            return GL.GetAttribLocation(_handle, attribName);
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

        public void SetFloatA(string name, float[] values)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform1(location, values.Length,values);
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

        public void SetIVec3(string name, Vector3i value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.Uniform3(location, value);
        }

        public void SetIVec3A(string name, Vector3i[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                int location = GL.GetUniformLocation(_handle, name + "[" + i + "]");
                GL.Uniform3(location, value[i]);
            }
        }

        public void SetVec3(string name, Vector3 value)
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


        //Dispose of shader, cannot be done in finalazer due to oo natures
        bool disposedValue = false;


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(_handle);
                disposedValue = true;
            }
        }

        //Warn if dispose isnt called on shutdown
        ~ComputeShader()
        {
            if (disposedValue == false)
            {
                Console.WriteLine("GPU Resource leak, did you forget to call Dispose()?");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}
