open System
open System.IO
open OpenTK.Graphics
open OpenTK.Windowing.Desktop
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Graphics.OpenGL

open System.Runtime.InteropServices


let sizeof(t) = Marshal.SizeOf t

type Shader(vertexPath:string, fragmentPath:string) as self =
    let mutable handle = 0
    let mutable disposed = false
    let cleanup(disposing:bool) = 
        if not disposed then
            disposed <- true
            if disposing then
                () // unmanaged cleanup code 
            GL.DeleteProgram(handle)
    do
        let vertexShaderSource = File.ReadAllText(vertexPath)
        let fragmentShaderSource = File.ReadAllText(fragmentPath)

        let vertexShader = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vertexShader, vertexShaderSource)
        let fragmentShader = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fragmentShader, fragmentShaderSource)
        GL.CompileShader(vertexShader)
        match GL.GetShaderInfoLog(vertexShader) with
        | "" -> ()
        | infoLogVert -> printfn "%A" infoLogVert
        GL.CompileShader(fragmentShader)
        match GL.GetShaderInfoLog(fragmentShader) with
        | "" -> ()
        | infoLogFrag -> printfn "%A" infoLogFrag
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
        

type Game(width: int, height: int, title: string) =
    inherit GameWindow(GameWindowSettings.Default, NativeWindowSettings.Default)
    let mutable VertexBufferObject = 0
    [<DefaultValue>]val mutable shader:Shader
    let mutable VertexArrayObject = 0
    member self.vertices = [|
        -0.5f; -0.5f; 0.0f //Bottom-left vertex
        0.5f; -0.5f; 0.0f  //Bottom-right vertex
        0.0f;  0.5f; 0.0f  //Top vertex
    |]
    override self.OnUpdateFrame(e:FrameEventArgs) =
        let input = self.KeyboardState
        if input.IsKeyDown(Keys.Escape) = true then
            Environment.Exit(0)
        else
            base.OnUpdateFrame(e)
    override self.OnLoad() =
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f)
        VertexBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject)
        GL.BufferData(BufferTarget.ArrayBuffer, self.vertices.Length * sizeof(typeof<float>), self.vertices, BufferUsageHint.StaticDraw)
        VertexArrayObject <- GL.GenVertexArray()
        GL.BindVertexArray(VertexArrayObject)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(typeof<float>), 0)
        GL.EnableVertexAttribArray(0)

        self.shader <- new Shader("shader.vert", "shader.frag")
        self.shader.Use()

        base.OnLoad()
    override self.OnRenderFrame(e:FrameEventArgs) =
        GL.Clear(ClearBufferMask.ColorBufferBit)
        self.shader.Use()
        GL.BindVertexArray(VertexArrayObject)
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3)

        self.Context.SwapBuffers()
        base.OnRenderFrame(e)
    override self.OnResize(e:ResizeEventArgs) =
        GL.Viewport(0, 0, e.Width, e.Height)
        base.OnResize(e)
    override self.OnUnload() = 
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.DeleteBuffer(VertexBufferObject)
        //self.shader.Dispose()
        base.OnUnload()


[<EntryPoint>]
let main argv =
    use game = new Game(800, 600, "Learn OpenTK")
    game.Run()
    0

