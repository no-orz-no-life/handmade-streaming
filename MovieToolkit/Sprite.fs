module Sprite
    open System
    open SkiaSharp
    open MovieToolkit
    open OpenTK.Mathematics
    open OpenTK.Graphics.OpenGL4

    type ColorKeyOption = 
    | None
    | FromPixel of int*int
    | ByColor of SKColor
    let setColorKey (bmp:SKBitmap) (colorKey:SKColor) =
        let mutable x = 0
        let mutable y = 0
        try
            // apply colorkey
            for x in 0..(bmp.Width - 1) do
                for y in 0..(bmp.Height - 1) do
                    if bmp.GetPixel(x, y) = colorKey then
                        bmp.SetPixel(x, y, SKColor(255uy, 0uy, 0uy, 0uy))
        with
        | :? System.ArgumentOutOfRangeException as e ->
            printfn "%A %A => %A" x y e
    let makeTextureFromSKBitmap (src:SKBitmap) colorKeyOption = 
        let powerOfTwo n =
            let mutable v = 1
            while v < n do
                v <- v <<< 1
            v

        let textureW = powerOfTwo src.Width
        let textureH = powerOfTwo src.Height
        use bmp = new SKBitmap(textureW, textureH, SKColorType.Rgba8888, SKAlphaType.Premul)
        use canvas = new SKCanvas(bmp)
        canvas.DrawBitmap(src, SKPoint(0f, 0f))

        match colorKeyOption with
        | None -> ()
        | FromPixel(x,y) -> setColorKey bmp (src.GetPixel(x, y))
        | ByColor(c) -> setColorKey bmp c

        let w = src.Width
        let h = src.Height

        let texture = Texture.FromSKBitmap(bmp)
        (texture, w, h, textureW, textureH)
        
    let makeTextureFromImageFile (path:string) colorKeyOption = 
        use bmp = SKBitmap.Decode(path)
        makeTextureFromSKBitmap bmp colorKeyOption

    type Sprite (texture:Texture, w:int, h:int, textureW:int, textureH:int) =
        let mutable disposed = false
        let mutable vertexArrayObject = 0
        let mutable vertexBufferObject = 0
        let mutable elementBufferObject = 0
        let mutable indicesLength = 0
        let mutable shader:Shader = Unchecked.defaultof<Shader>
        let cleanup(disposing:bool) = 
            if not disposed then
                disposed <- true
                if disposing then
                    () // unmanaged cleanup code
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                GL.BindVertexArray(0)
                GL.UseProgram(0)

                GL.DeleteBuffer(vertexBufferObject)
                GL.DeleteBuffer(elementBufferObject)
                GL.DeleteVertexArray(vertexArrayObject)
                (shader :> IDisposable).Dispose()
                (texture :> IDisposable).Dispose()

        do
            let w2 = (float32 w) / 2f
            let h2 = (float32 h) / 2f

            let vertices = 
                let texMaxX = (float32 w) / (float32 textureW)
                let texMaxY = (float32 h) / (float32 textureH)
                [|
                    //Position          Texture coordinates
                       w2;    h2; 0.0f; texMaxX;    0.0f; // top right
                       w2;   -h2; 0.0f; texMaxX; texMaxY; // bottom right
                      -w2;   -h2; 0.0f;    0.0f; texMaxY; // bottom left
                      -w2;    h2; 0.0f;    0.0f;    0.0f;  // top left
                |]
            let indices = [|
                0u; 1u; 3u;    // first triangle
                1u; 2u; 3u;    // second triangle
            |]
            indicesLength <- indices.Length
            vertexArrayObject <- GL.GenVertexArray()
            GL.BindVertexArray(vertexArrayObject)

            vertexBufferObject <- GL.GenBuffer()
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof<float32>, vertices, BufferUsageHint.StaticDraw)

            elementBufferObject <- GL.GenBuffer()
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject)
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof<uint>, indices, BufferUsageHint.StaticDraw)

            let vertexShader = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
out vec2 texCoord;
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main(void)
{
    texCoord = aTexCoord;
    gl_Position = vec4(aPosition, 1.0) * model * view * projection;
}
                "
            let fragmentShader = @"
#version 330
out vec4 outputColor;
in vec2 texCoord;
uniform sampler2D texture0;
void main()
{
    outputColor = texture(texture0, texCoord);
}
            "
            shader <- Shader.FromStringSource(vertexShader, fragmentShader)
            shader.Use()

            let vertexLocation = shader.GetAttribLocation("aPosition")
            GL.EnableVertexAttribArray(vertexLocation)
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 0)

            let texCoordLocation = shader.GetAttribLocation("aTexCoord")
            GL.EnableVertexAttribArray(texCoordLocation)
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 3 * sizeof<float32>)

            texture.Use(TextureUnit.Texture0)
            shader.SetInt("texture0", 0)
        member self.Width 
            with get() = w
        member self.Height
            with get() = h
        member self.Use(view, projection) = 
            GL.BindVertexArray(vertexArrayObject)

            texture.Use(TextureUnit.Texture0)
            shader.Use()
            shader.SetMatrix4("view", view)
            shader.SetMatrix4("projection", projection)
        member self.Render(x, y) = 
            let model = Matrix4.CreateTranslation(float32 x, float32 y, 1.0f)
            shader.SetMatrix4("model", model)
            GL.DrawElements(PrimitiveType.Triangles, indicesLength, DrawElementsType.UnsignedInt, 0)

        static member FromFile(path, colorKeyOption) =
            new Sprite(makeTextureFromImageFile path colorKeyOption)
        static member FromSKBitmap(bmp, colorKeyOption) = 
            new Sprite(makeTextureFromSKBitmap bmp colorKeyOption)
        interface IDisposable with
            member self.Dispose() = 
                cleanup(true)
                GC.SuppressFinalize(self)
