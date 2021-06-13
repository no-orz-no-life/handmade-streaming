open System
open System.IO
open OpenTK.Mathematics
open OpenTK.Graphics.OpenGL4
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Windowing.Desktop

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
        

type Game(gameWindowSettings:GameWindowSettings, nativeWindowSettings:NativeWindowSettings) =
    inherit GameWindow(gameWindowSettings, nativeWindowSettings)
    let mutable vertexBufferObject = 0
    let mutable vertexArrayObject = 0
    let mutable elementBufferObject = 0
    let triangle = false
    [<DefaultValue>]val mutable shader:Shader
    member self.verticesRect = [|
         0.5f;  0.5f; 0.0f;  // top right
         0.5f; -0.5f; 0.0f;  // bottom right
        -0.5f; -0.5f; 0.0f;  // bottom left
        -0.5f;  0.5f; 0.0f;  // top left
    |]
    member self.verticesTri = [|
        -0.5f; -0.5f; 0.0f // Bottom-left vertex
        0.5f; -0.5f; 0.0f // Bottom-right vertex
        0.0f;  0.5f; 0.0f  // Top vertex
    |]
    member self.indices = [|
        0u; 1u; 3u;    // first triangle
        1u; 2u; 3u;    // second triangle
    |]
    override self.OnUpdateFrame(e:FrameEventArgs) =
        let input = self.KeyboardState
        if input.IsKeyDown(Keys.Escape) = true then
            self.Close()
        else
            base.OnUpdateFrame(e)
    override self.OnLoad() =
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f)

        vertexBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        if triangle then
            GL.BufferData(BufferTarget.ArrayBuffer, self.verticesTri.Length * sizeof<float32>, self.verticesTri, BufferUsageHint.StaticDraw)
        else
            GL.BufferData(BufferTarget.ArrayBuffer, self.verticesRect.Length * sizeof<float32>, self.verticesRect, BufferUsageHint.StaticDraw)

        vertexArrayObject <- GL.GenVertexArray()
        GL.BindVertexArray(vertexArrayObject)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof<float32>, 0)
        GL.EnableVertexAttribArray(0)

        if not triangle then
            elementBufferObject <- GL.GenBuffer()
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject)
            GL.BufferData(BufferTarget.ElementArrayBuffer, self.indices.Length * sizeof<uint>, self.indices, BufferUsageHint.StaticDraw)

        self.shader <- new Shader("shader.vert", "shader.frag")
        self.shader.Use()

        base.OnLoad()
    override self.OnRenderFrame(e:FrameEventArgs) =
        GL.Clear(ClearBufferMask.ColorBufferBit)
        self.shader.Use()
        GL.BindVertexArray(vertexArrayObject)
        if triangle then
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3)
        else
            GL.DrawElements(PrimitiveType.Triangles, self.indices.Length, DrawElementsType.UnsignedInt, 0)

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

