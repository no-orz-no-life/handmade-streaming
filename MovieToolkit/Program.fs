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
    member self.Handle = handle
        

type Game(gameWindowSettings:GameWindowSettings, nativeWindowSettings:NativeWindowSettings) =
    inherit GameWindow(gameWindowSettings, nativeWindowSettings)
    let mutable vertexBufferObject = 0
    let mutable vertexArrayObject = 0
    let mutable elementBufferObject = 0
    let timer = System.Diagnostics.Stopwatch()
    [<DefaultValue>]val mutable shader:Shader
    member self.vertices = [|
         // positions        // colors
        0.5f; -0.5f; 0.0f;  1.0f; 0.0f; 0.0f;   // bottom right
        -0.5f; -0.5f; 0.0f;  0.0f; 1.0f; 0.0f;   // bottom left
        0.0f;  0.5f; 0.0f;  0.0f; 0.0f; 1.0f;    // top 
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
        GL.BufferData(BufferTarget.ArrayBuffer, self.vertices.Length * sizeof<float32>, self.vertices, BufferUsageHint.StaticDraw)

        vertexArrayObject <- GL.GenVertexArray()
        GL.BindVertexArray(vertexArrayObject)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof<float32>, 0)
        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof<float32>, 3 * sizeof<float32>)
        GL.EnableVertexAttribArray(1)

        elementBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject)
        GL.BufferData(BufferTarget.ElementArrayBuffer, self.indices.Length * sizeof<uint>, self.indices, BufferUsageHint.StaticDraw)

        self.shader <- new Shader("shader.vert", "shader.frag")
        self.shader.Use()

        timer.Start()
        base.OnLoad()
    override self.OnRenderFrame(e:FrameEventArgs) =
        GL.Clear(ClearBufferMask.ColorBufferBit)
        self.shader.Use()
        let timeValue = timer.Elapsed.TotalSeconds
(*        let greenValue:float32 = (Math.Sin(timeValue) |> float32) / (2.0f + 0.5f)
        let vertexColorLocation = GL.GetUniformLocation(self.shader.Handle, "ourColor")
        GL.Uniform4(vertexColorLocation, 0.0f, greenValue, 0.0f, 1.0f);
*)
        GL.BindVertexArray(vertexArrayObject)
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3)
        //GL.DrawElements(PrimitiveType.Triangles, self.indices.Length, DrawElementsType.UnsignedInt, 0)

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

