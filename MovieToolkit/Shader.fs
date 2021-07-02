namespace MovieToolkit

open System
open System.IO
open OpenTK.Graphics.OpenGL4

type Shader(handle:int) =
    let mutable disposed = false
    let cleanup(disposing:bool) = 
        if not disposed then
            disposed <- true
            if disposing then
                () // unmanaged cleanup code 
            GL.DeleteProgram(handle)


    static member private FromShaderHandle(vertexShader, fragmentShader) =
        let handle = GL.CreateProgram()
        GL.AttachShader(handle, vertexShader)
        GL.AttachShader(handle, fragmentShader)
        GL.LinkProgram(handle)

        GL.DetachShader(handle, vertexShader)
        GL.DetachShader(handle, fragmentShader)
        GL.DeleteShader(vertexShader)
        GL.DeleteShader(fragmentShader)
        new Shader(handle)

    static member FromStringSource(vertexSource, fragmentSource) =
        let compileShaderFromString shaderType source =
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
            compileShaderFromString ShaderType.VertexShader vertexSource
            |> getResultOrFail
        
        let fragmentShader = 
            compileShaderFromString ShaderType.FragmentShader fragmentSource
            |> getResultOrFail

        Shader.FromShaderHandle(vertexShader, fragmentShader)

    static member FromFile(vertexPath, fragmentPath) = 
        let vertexSource = File.ReadAllText(vertexPath)
        let fragmentSource = File.ReadAllText(fragmentPath)

        Shader.FromStringSource(vertexSource, fragmentSource)

    member self.Use () = GL.UseProgram(handle)

    interface IDisposable with
        member self.Dispose() = 
            cleanup(true)
            GC.SuppressFinalize(self)
    override self.Finalize() = cleanup(false)
    member self.Handle = handle
    member self.GetAttribLocation(name) = GL.GetAttribLocation(handle, name)
    member self.SetInt(name, (v:int)) = 
        self.Use()
        let location = GL.GetUniformLocation(handle, name)
        GL.Uniform1(location, v)
    member self.SetMatrix4(name, data) = 
        self.Use()
        let mutable v = data
        let location = GL.GetUniformLocation(handle, name)
        GL.UniformMatrix4(location, true, &v)

