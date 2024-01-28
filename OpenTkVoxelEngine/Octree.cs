using Dear_ImGui_Sample;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
using Buffer = OpenTK.Graphics.OpenGL4.Buffer;



namespace OpenTkVoxelEngine
{
    public class Bounds()
    {
        public Vector3 min;
        public Vector3 max;
        public Vector3 Center() => min + max / 2f;


        public bool ContainsPoint(Vector3 point)
        {
            return point.X >= min.X && point.X <= max.X &&
                point.Y >= min.Y && point.Y <= max.Y &&
                point.Z >= min.Z && point.Z <= max.Z;
        }

        public Bounds(Vector3 minBound, Vector3 maxBound) : this()
        {
            this.min = minBound; this.max = maxBound;
        }
    }

    public class Octree
    {
        int _maxDepth;
        Node _rootNode;
        public Node RootNode() => _rootNode;
        public int MaxDepth() => _maxDepth;
        public void MaxDepth(int value) => _maxDepth = value;
        public Octree(Bounds Size, int maxDepth)
        {
            _maxDepth = maxDepth;

            _rootNode = new Node(Size,0,_maxDepth);
            _rootNode.PopulateChildren();

        }
    }

    public class MarchingCubesChunkPlaceHolder
    {
        public MarchingCubesChunkPlaceHolder(Bounds bounds)
        {

        }
    }

    public class Node
    {
        MarchingCubesChunkPlaceHolder chunk;
        Bounds bounds;
        Node[] Children;
        int _depth;
        int _maxDepth;
        bool isLeaf() => Children != null;
        public Node(Bounds bounds,int currentDepth,int maxDepth)
        {
            chunk = new MarchingCubesChunkPlaceHolder(bounds);
            _depth = currentDepth;
            _maxDepth = maxDepth;
            this.bounds = bounds;
        }

        public void DeleteChildren()
        {
            chunk = new MarchingCubesChunkPlaceHolder(bounds);
            Children = null;
        }

        public void PopulateChildren()
        {
            if (_depth >= _maxDepth) return;
            chunk = null;
            Children = new Node[8];
            Vector3 midPoint = bounds.Center();
            for (int i = 0; i < 8; i++)
            {
                Vector3 childMin = new Vector3(
                    i % 2 == 0 ? midPoint.X : bounds.min.X,
                    (i >> 1) % 2 == 0 ? midPoint.Y : bounds.min.Y,
                    (i >> 2) % 2 == 0 ? midPoint.Z : bounds.min.Z
                );

                Vector3 childMax = new Vector3(
                    i % 2 == 0 ? bounds.max.X : midPoint.X,
                    (i >> 1) % 2 == 0 ? bounds.max.Y : midPoint.Y,
                    (i >> 2) % 2 == 0 ? bounds.max.Z : midPoint.Z
                );

                Bounds childBounds = new Bounds(childMin, childMax);

                Children[i] = new Node(childBounds,_depth+1,_maxDepth);
            }
        }
    }
}
