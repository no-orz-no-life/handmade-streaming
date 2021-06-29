namespace MovieToolkit
open OpenTK.Graphics.OpenGL4
open SkiaSharp

type Texture(handle:int) = 
    static member FromSKBitmap(bmp:SKBitmap) = 
        use bmp1 = new SKBitmap(bmp.Width, bmp.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        use canvas = new SKCanvas(bmp1)
        canvas.Scale(1.0f, -1.0f, 0.0f, (float32 bmp1.Height) / 2.0f)
        canvas.DrawBitmap(bmp, 0.0f, 0.0f)

        let handle = GL.GenTexture()
        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindTexture(TextureTarget.Texture2D, handle)
        GL.TexImage2D(TextureTarget.Texture2D, 
            0,
            PixelInternalFormat.Rgba,
            bmp1.Width,
            bmp1.Height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            bmp1.Bytes)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, int TextureMinFilter.Linear)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, int TextureWrapMode.Repeat)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, int TextureWrapMode.Repeat)
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)

        Texture(handle)

    static member FromFile(path:string) = 
        use bmp = SKBitmap.Decode(path)
        Texture.FromSKBitmap(bmp)

    member self.Use(unit) =
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.Texture2D, handle)

