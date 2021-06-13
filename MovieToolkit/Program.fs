open System
open System.IO
open OpenTK.Graphics
open OpenTK.Windowing.Desktop
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Graphics.OpenGL

open System.Runtime.InteropServices


let sizeof(t) = Marshal.SizeOf t

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
        

type Game(width: int, height: int, title: string) =
    inherit GameWindow(GameWindowSettings.Default, NativeWindowSettings.Default)
    let mutable VertexBufferObject = 0
    let mutable VertexArrayObject = 0
    let mutable ElementBufferObject = 0
    [<DefaultValue>]val mutable shader:Shader
    member self.vertices = [|
         0.5f;  0.5f; 0.0f;  // top right
         0.5f; -0.5f; 0.0f;  // bottom right
        -0.5f; -0.5f; 0.0f;  // bottom left
        -0.5f;  0.5f; 0.0f;  // top left
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
        VertexBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject)
        GL.BufferData(BufferTarget.ArrayBuffer, self.vertices.Length * sizeof(typeof<float>), self.vertices, BufferUsageHint.StaticDraw)
        VertexArrayObject <- GL.GenVertexArray()
        GL.BindVertexArray(VertexArrayObject)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(typeof<float>), 0)
        GL.EnableVertexAttribArray(0)

        ElementBufferObject <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject)
        GL.BufferData(BufferTarget.ElementArrayBuffer, self.indices.Length * sizeof(typeof<uint>), self.indices, BufferUsageHint.StaticDraw)

        self.shader <- new Shader("shader.vert", "shader.frag")
        self.shader.Use()

        base.OnLoad()
    override self.OnRenderFrame(e:FrameEventArgs) =
        GL.Clear(ClearBufferMask.ColorBufferBit)
        self.shader.Use()
        GL.BindVertexArray(VertexArrayObject)
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

        GL.DeleteBuffer(VertexBufferObject)
        GL.DeleteBuffer(ElementBufferObject)
        GL.DeleteVertexArray(VertexArrayObject);
        (self.shader :> IDisposable).Dispose()
        base.OnUnload()


[<EntryPoint>]
let main argv =
    use game = new Game(800, 600, "Learn OpenTK")
    game.Run()
    0

