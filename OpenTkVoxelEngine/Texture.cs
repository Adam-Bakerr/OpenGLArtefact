using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;


namespace OpenTkVoxelEngine
{
    public class Texture
    {

        int _objectHandle;

        public Texture(int handle)
        {
            _objectHandle = handle;
        }

        public void Use(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D,_objectHandle);
        }

        public void Dispose()
        {
            GL.DeleteTexture(_objectHandle);
        }

        public static Texture LoadFromFile(string FilePath,TextureUnit unit)
        {
            int handle = GL.GenTexture();

            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D,handle);


            //Reverse image due to how it is loaded
            StbImage.stbi_set_flip_vertically_on_load(1);

            using (Stream stream = File.OpenRead(FilePath))
            {
                ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba,image.Width,image.Height,0,PixelFormat.Rgba,PixelType.UnsignedByte,image.Data);
            }

            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);


            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);


            return new Texture(handle);
        }
    }
}
