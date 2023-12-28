using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkVoxelEngine;
using System;
using Terrain;
using Buffer = OpenTK.Graphics.OpenGL4.Buffer;

namespace OpenTkVoxelEngine
{
    public class SVO
    {
        OctreeNode rootNode;
        public List<OctreeNode> nodes;

        public Vector3 CenterOfoctree;
        float maxHeight;

        public static Random random;
        public static Noise noise;

        public SVO(Vector3 minBound, Vector3 maxBound, int maxDepth)
        {
            random = new Random();

            maxHeight = maxBound.Y - minBound.Y;
            CenterOfoctree = minBound + ((maxBound - minBound) / 2f);

            nodes = new List<OctreeNode>();
            rootNode = BuildOctree(minBound, maxBound, maxDepth);
            nodes.Add(rootNode);
        }

        OctreeNode BuildOctree(Vector3 minBound, Vector3 maxBound, int Depth)
        {
            OctreeNode node = new OctreeNode(minBound, maxBound);

            //Check if all child nodes are going to be occupied
            Vector3 midPoint = (minBound + maxBound) / 2;
            Vector3 Center = (node.minBounds + ((node.maxBounds - node.minBounds) / 2f)).Xyz;

            if (Depth == 0 || !CanSubdivide(minBound, maxBound, Depth))
            {
                node.isLeaf = 1;

                
                node.isOccupied = Vector3.Distance(Center, CenterOfoctree) < 10 ? 1 : 0;
            }
            else
            {


                //Check if we need to create 8 children
                for (int i = 0; i < 8; i++)
                {
                    //currentIndex = node.ChildrenOffset;

                    //Determine pos of child node
                    Vector3 childMin = new Vector3(
                        i % 2 == 0 ? midPoint.X : minBound.X,
                        (i >> 1) % 2 == 0 ? midPoint.Y : minBound.Y,
                        (i >> 2) % 2 == 0 ? midPoint.Z : minBound.Z
                    );

                    Vector3 childMax = new Vector3(
                        i % 2 == 0 ? maxBound.X : midPoint.X,
                        (i >> 1) % 2 == 0 ? maxBound.Y : midPoint.Y,
                        (i >> 2) % 2 == 0 ? maxBound.Z : midPoint.Z
                    );

                    //node.isOccupied = math.distance(Center, CenterOfoctree) < 30 ? 1 : 0;

                    //Check if we should create node
                    if (CanSubdivide(minBound, maxBound, Depth) && node.isOccupied == 0)
                    {
                        nodes.Add(BuildOctree(childMin, childMax, Depth - 1));
                        node.childIndicies[i + 1] = nodes.Count - 1;
                    }
                }

            }
            return node;
        }


        //Estimate the max total size of the tree
        int CalculateArraySize(int maxDepth)
        {
            return (int)((MathF.Pow(8, maxDepth + 1) - 1) / 7);
        }

        //Check if we have exceeded the maximum depth of the tree
        bool CanSubdivide(Vector3 minBound, Vector3 maxBound, int currentDepth)
        {
            return currentDepth > 0;
        }


        bool ShouldCreateChild(Vector3 minBound, Vector3 maxBound)
        {
            return true;
        }


        float sfbm(Vector3 x)
        {
            float v = 0.0f;
            float a = .97f;
            float freq = 1f;
            float weight = 1;
            for (int i = 0; i < 7; ++i)
            {

                var value = (x * freq);
                float eval = a * noise.Generate(value.X, (x * freq).Y, (x * freq).Z);
                v += (eval + 1) * weight;
                x = x * 1;
                a *= .78f;
                freq *= 1.46f;
            }
            v = MathF.Max(0, v - -5.52f);
            return v * 0.07f;
        }

        float GetNodeValue(Vector3 minBound, Vector3 maxBound)
        {
            float x = ((minBound.X + maxBound.X) / 2);
            float y = ((minBound.Y + maxBound.Y) / 2);
            float z = ((minBound.Z + maxBound.Z) / 2);

            //Add Return Here Maybe 3d Noise?
            //This is the part that determines if a node should be a active node or not

            return (z / maxHeight);
        }

    }

    public struct OctreeNode
    {
        public Vector4 minBounds;
        public Vector4 maxBounds;
        public Vector4 color;
        public int isLeaf;
        public int isOccupied;
        public childIndices childIndicies;
        int padding = 0;
        int padding2 = 0;
        public static int SizeOfNode()
        {
            return (sizeof(float) * 4 +
                sizeof(float) * 4 +
                sizeof(float) * 4 +
                sizeof(int) +
                sizeof(int)) + sizeof(int) * 10;
        }

        public OctreeNode(Vector3 MinBounds, Vector3 MaxBounds)
        {
            minBounds = new Vector4(MinBounds,1);
            maxBounds = new Vector4(MaxBounds, 1);
            float val = (float)OpenTkVoxelEngine.SVO.random.NextDouble();
            color = new Vector4(val, 0, 1, 1);
            isLeaf = 0;
            isOccupied = 0;
            childIndicies = new childIndices(-1);

        }

        public struct childIndices
        {
            int one;
            int two;
            int three;
            int four;
            int five;
            int six;
            int seven;
            int eight;

            public childIndices(int DefaultValue)
            {
                one = DefaultValue;
                two = DefaultValue;
                three = DefaultValue;
                four = DefaultValue;
                five = DefaultValue;
                six = DefaultValue;
                seven = DefaultValue;
                eight = DefaultValue;
            }

            public int this[int i]
            {
                set
                {
                    switch (i)
                    {
                        case (1):
                            one = value;
                            break;
                        case (2):
                            two = value;
                            break;
                        case (3):
                            three = value;
                            break;
                        case (4):
                            four = value;
                            break;
                        case (5):
                            five = value;
                            break;
                        case (6):
                            six = value;
                            break;
                        case (7):
                            seven = value;
                            break;
                        case (8):
                            eight = value;
                            break;
                        default:
                            break;
                    }
                }
                get
                {
                    switch (i)
                    {
                        case 1:
                            return one;

                        case 2:
                            return two;
                        case 3:
                            return three;

                        case 4:
                            return four;

                        case 5:
                            return five;

                        case 6:
                            return six;

                        case 7:
                            return seven;

                        case 8:
                            return eight;

                        default:
                            return -1;
                    }
                }
            }
        }


        public bool ContainsPoint(Vector3 point)
        {
            return point.X >= minBounds.X && point.X <= maxBounds.X &&
                point.Y >= minBounds.Y && point.Y <= maxBounds.Y &&
                point.Z >= minBounds.Z && point.Z <= maxBounds.Z;
        }
    }


}
