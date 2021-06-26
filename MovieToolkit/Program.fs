open System
open System.IO
open OpenTK.Mathematics
open OpenTK.Graphics.OpenGL4
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Windowing.Desktop

type Texture(handle:int) = 
    static member LoadFromFile(path:string) = 
        let handle = GL.GenTexture()
        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindTexture(TextureTarget.Texture2D, handle)

        use bmp0 = SkiaSharp.SKBitmap.Decode(path)
        use bmp1 = new SkiaSharp.SKBitmap(bmp0.Width, bmp0.Height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul)
        use canvas = new SkiaSharp.SKCanvas(bmp1)
        canvas.Scale(1.0f, -1.0f, 0.0f, (float32 bmp1.Height) / 2.0f)
        canvas.DrawBitmap(bmp0, 0.0f, 0.0f)

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
    member self.Use(unit) =
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.Texture2D, handle)


type Shader(vertexPath:string, fragmentPath:string)  =
    let mutable handle = 0
    let mutable disposed = false
    let cleanup(disposing:bool) = 
        if not disposed then
            disposed <- true
            if disposing then
                () // unmanaged cleanup code 
            GL.DeleteProgram(handle)
    do
        let compileShaderFromSourceFile shaderType path = 
            let source = File.ReadAllText(path)
            let shader = GL.CreateShader(shaderType)
            GL.ShaderSource(shader, source)
            GL.CompileShader(shader)
            match GL.GetShaderInfoLog(shader) with
            | "" -> Ok(shader)
            | infoLog -> Error(sprintf "%A" infoLog)
        
        let getResultOrFail r =
            match r with
            | Ok(v) -> v
            | Error(message) -> failwith message

        let vertexShader =
            compileShaderFromSourceFile ShaderType.VertexShader vertexPath
            |> getResultOrFail
        
        let fragmentShader = 
            compileShaderFromSourceFile ShaderType.FragmentShader fragmentPath
            |> getResultOrFail

        handle <- GL.CreateProgram()
        GL.AttachShader(handle, vertexShader)
        GL.AttachShader(handle, fragmentShader)
        GL.LinkProgram(handle)

        GL.DetachShader(handle, vertexShader)
        GL.DetachShader(handle, fragmentShader)
        GL.DeleteShader(vertexShader)
        GL.DeleteShader(fragmentShader)
    member self.Use () = GL.UseProgram(handle)

    interface IDisposable with
        member self.Dispose() = 
            cleanup(true)
            GC.SuppressFinalize(self)
    override self.Finalize() = cleanup(false)
    member self.Handle = handle
    member self.GetAttribLocation name = GL.GetAttribLocation(handle, name)
    member self.SetInt name (v:int) = 
        self.Use()
        let location = GL.GetUniformLocation(handle, name)
        GL.Uniform1(location, v)
        

type Game(gameWindowSettings:GameWindowSettings, nativeWindowSettings:NativeWindowSettings) =
    inherit GameWindow(gameWindowSettings, nativeWindowSettings)
    let mutable vertexBufferObject = 0
    let mutable vertexArrayObject = 0
    let mutable elementBufferObject = 0
    let mutable saveFile = true
    let mutable pointer:nativeint = 0n
    let timer = System.Diagnostics.Stopwatch()
    [<DefaultValue>]val mutable shader:Shader
    [<DefaultValue>]val mutable texture:Texture
    [<DefaultValue>]val mutable texture2:Texture

    member self.vertices = [|
        //Position          Texture coordinates
         0.5f;  0.5f; 0.0f; 1.0f; 1.0f; // top right
         0.5f; -0.5f; 0.0f; 1.0f; 0.0f; // bottom right
        -0.5f; -0.5f; 0.0f; 0.0f; 0.0f; // bottom left
        -0.5f;  0.5f; 0.0f; 0.0f; 1.0f;  // top left
    |]
    member self.indices = [|
        0u; 1u; 3u;    // first triangle
        1u; 2u; 3u;    // second triangle
    |]
    override self.OnUpdateFrame(e:FrameEventArgs) =
        let input = self.KeyboardState
        if input.IsKeyDown(Keys.Escape) then
            self.Close()
        else
            base.OnUpdateFrame(e)
    override self.OnLoad() =
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f)

        vertexArrayObject <- GL.GenVertexArray()
        GL.BindVertexArray(vertexArrayObject)

        vertexBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        GL.BufferData(BufferTarget.ArrayBuffer, self.vertices.Length * sizeof<float32>, self.vertices, BufferUsageHint.StaticDraw)

        elementBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject)
        GL.BufferData(BufferTarget.ElementArrayBuffer, self.indices.Length * sizeof<uint>, self.indices, BufferUsageHint.StaticDraw)

        self.shader <- new Shader("shader.vert", "shader.frag")
        self.shader.Use()

        let vertexLocation = self.shader.GetAttribLocation("aPosition")
        GL.EnableVertexAttribArray(vertexLocation)
        GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 0)

        let texCoordLocation = self.shader.GetAttribLocation("aTexCoord")
        GL.EnableVertexAttribArray(texCoordLocation)
        GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 3 * sizeof<float32>)

        self.texture <- Texture.LoadFromFile("container.png")
        self.texture.Use(TextureUnit.Texture0)
        self.texture2 <- Texture.LoadFromFile("awesomeface.png")
        self.texture.Use(TextureUnit.Texture1)
        self.shader.SetInt "texture0" 0
        self.shader.SetInt "texture1" 1

        pointer <- System.Runtime.InteropServices.Marshal.AllocHGlobal(4 * self.Size.X * self.Size.Y)
        timer.Start()
        base.OnLoad()
    override self.OnRenderFrame(e:FrameEventArgs) =
        GL.Clear(ClearBufferMask.ColorBufferBit)
        GL.BindVertexArray(vertexArrayObject)

        self.texture.Use(TextureUnit.Texture0)
        self.texture2.Use(TextureUnit.Texture1)
        self.shader.Use()

        let timeValue = timer.Elapsed.TotalSeconds
(*        let greenValue:float32 = (Math.Sin(timeValue) |> float32) / (2.0f + 0.5f)
        let vertexColorLocation = GL.GetUniformLocation(self.shader.Handle, "ourColor")
        GL.Uniform4(vertexColorLocation, 0.0f, greenValue, 0.0f, 1.0f);
*)
        GL.DrawElements(PrimitiveType.Triangles, self.indices.Length, DrawElementsType.UnsignedInt, 0)

        if saveFile then
            GL.Finish()
            GL.ReadBuffer(ReadBufferMode.Back)
            GL.ReadPixels(0, 0, self.Size.X, self.Size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, pointer)

            let info = SkiaSharp.SKImageInfo(self.Size.X, self.Size.Y, SkiaSharp.SKColorType.Rgba8888)
            let image = SkiaSharp.SKImage.FromPixels(info, pointer, self.Size.X * 4)

            use bmp = new SkiaSharp.SKBitmap(self.Size.X, self.Size.Y, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul)
            use canvas = new SkiaSharp.SKCanvas(bmp)
            canvas.Scale(1.0f, -1.0f, 0.0f, (float32 bmp.Height) / 2.0f)
            canvas.DrawImage(image, 0.0f, 0.0f)

            let data = bmp.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)
            use fs = new FileStream("out.png", FileMode.Create, FileAccess.Write)
            data.SaveTo(fs)
            saveFile <- false

        self.SwapBuffers()
        base.OnRenderFrame(e)
    override self.OnResize(e:ResizeEventArgs) =
        GL.Viewport(0, 0, e.Width, e.Height)
        base.OnResize(e)
    override self.OnUnload() = 
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
        GL.UseProgram(0)

        GL.DeleteBuffer(vertexBufferObject)
        GL.DeleteBuffer(elementBufferObject)
        GL.DeleteVertexArray(vertexArrayObject);
        (self.shader :> IDisposable).Dispose()
        base.OnUnload()


[<EntryPoint>]
let main argv =
    let nativeWindowSettings = 
        NativeWindowSettings()
        |> (fun it -> 
                it.Size <- Vector2i(800, 600)
                it.Title <- "Learn OpenTK"
                it)
   
    use game = new Game(GameWindowSettings.Default, nativeWindowSettings)
    game.Run()
    0

