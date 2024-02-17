using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;

namespace OpenTkVoxelEngine
{
    //Vertex Array Object
    public class VAO
    {
        public int _objectHandle { get; private set; }

        List<(int, int, VertexAttribPointerType, bool, int, int)> VertexAttribPointers = new List<(int, int, VertexAttribPointerType, bool, int, int)>();

        public VAO()
        {
            _objectHandle = GL.GenVertexArray();
            GL.BindVertexArray(_objectHandle);
        }

        public void Enable(List<(int, int, VertexAttribPointerType, bool, int, int)> pointers)
        {
            VertexAttribPointers = pointers;
            for (int i = 0; i < pointers.Count; i++)
            {
                GL.EnableVertexAttribArray(pointers[i].Item1);
                GL.VertexAttribPointer(pointers[i].Item1, pointers[i].Item2, pointers[i].Item3, pointers[i].Item4, pointers[i].Item5, pointers[i].Item6);
            }
        }


        public void Bind()
        {
            GL.BindVertexArray(_objectHandle);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(_objectHandle);
        }
    }

    //Vertex Buffer Object
    public class VBO
    {
        public int _objectHandle { get; private set; }


        public VBO()
        {
            _objectHandle = GL.GenBuffer();
        }

        public void BufferData<T>(T[] Data, BufferUsageHint hint) where T : struct, IComparable
        {

            GL.BindBuffer(BufferTarget.ArrayBuffer, _objectHandle);
            GL.BufferData(BufferTarget.ArrayBuffer, Data.Length * sizeof(float), Data, hint);

        }
        public void Dispose()
        {
            GL.DeleteBuffer(_objectHandle);
        }
    }

    //Always need to have a vao bound before we can use this otherwise we will get a error thrown
    public class EBO
    {
        public int _objectHandle { get; private set; }

        public EBO()
        {
            _objectHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _objectHandle);
        }

        public void BufferData<T>(T[] Data, BufferUsageHint hint) where T : struct, IComparable
        {

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _objectHandle);
            GL.BufferData(BufferTarget.ElementArrayBuffer, Data.Length * sizeof(uint), Data, hint);

        }

        public void Dispose()
        {
            GL.DeleteBuffer(_objectHandle);
        }

    }

    public class SSBO
    {
        public int _objectHandle { get; private set; }

        public SSBO()
        {
            _objectHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _objectHandle);
        }

        public void BufferData<T>(T[] Data, BufferUsageHint hint, int BindingIndex) where T : struct, IComparable
        {

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _objectHandle);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Data.Length * sizeof(uint), Data, hint);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, BindingIndex,_objectHandle);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer,0);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(_objectHandle);
        }

    }
}
