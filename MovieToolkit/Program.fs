namespace MovieToolkit

open System
open System.IO
open OpenTK.Mathematics
open OpenTK.Graphics.OpenGL4
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Windowing.Desktop
open SkiaSharp

module Main = 

    let saveSKBitmapToPng path quality (bmp:SKBitmap) = 
        use data = bmp.Encode(SKEncodedImageFormat.Png, quality)
        use fs = new FileStream(path, FileMode.Create, FileAccess.Write)
        data.SaveTo(fs)

    type LoopRequest =
    | Initialize
    | Input of FrameEventArgs
    | Update
    | Render
    | Quit

    type LoopResponse =
    | Ok
    | RemoveMe
    and Entity = Game->LoopRequest -> LoopResponse
    and Game(gameWindowSettings:GameWindowSettings, nativeWindowSettings:NativeWindowSettings, root:Entity) as self =
        inherit GameWindow(gameWindowSettings, nativeWindowSettings)
        let mutable saveFile = true
        let mutable pointer:nativeint = 0n
        let mutable time = 0.0
        let mutable view = Matrix4.Identity
        let mutable projection = Matrix4.Identity

        [<DefaultValue>]val mutable camera:Camera

        let mutable firstMove = true
        let mutable lastPos = Vector2.Zero

        let callback e =  root self e

        member self.Time
            with get() = time

        override self.OnLoad() =
            callback Initialize |> ignore
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f)
            pointer <- System.Runtime.InteropServices.Marshal.AllocHGlobal(4 * self.Size.X * self.Size.Y)

            let aspectRatio = (float32 self.Size.X) / (float32 self.Size.Y)

            self.camera <- Camera(Vector3.UnitZ * 3.0f, aspectRatio)
            self.CursorGrabbed <- true

            base.OnLoad()

        override self.OnRenderFrame(e:FrameEventArgs) =
            time <- time + 4.0 * e.Time
            GL.Clear(ClearBufferMask.ColorBufferBit)
            callback Render |> ignore

            if saveFile then
                GL.Finish()
                GL.ReadBuffer(ReadBufferMode.Back)
                GL.ReadPixels(0, 0, self.Size.X, self.Size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, pointer)

                let info = SKImageInfo(self.Size.X, self.Size.Y, SKColorType.Rgba8888)
                let image = SKImage.FromPixels(info, pointer, self.Size.X * 4)

                use bmp = new SKBitmap(self.Size.X, self.Size.Y, SKColorType.Rgba8888, SKAlphaType.Unpremul)
                use canvas = new SKCanvas(bmp)
                canvas.Scale(1.0f, -1.0f, 0.0f, (float32 bmp.Height) / 2.0f)
                canvas.DrawImage(image, 0.0f, 0.0f)

                saveSKBitmapToPng "out.png" 100 bmp

                saveFile <- false

            self.SwapBuffers()
            base.OnRenderFrame(e)
        override self.OnUpdateFrame(e:FrameEventArgs) =
            callback (Input e) |> ignore  // TODO: skip suceeding logic?
            callback Update |> ignore
            if self.IsFocused then
                let input = self.KeyboardState
                if input.IsKeyDown(Keys.Escape) then
                    self.Close()
                else
                    let cameraSpeed = 1.5f
                    let sensitivity = 0.2f
                    if input.IsKeyDown(Keys.W) then
                        self.camera.Position <- self.camera.Position + (self.camera.Front * cameraSpeed * (float32 e.Time))
                    if input.IsKeyDown(Keys.S) then
                        self.camera.Position <- self.camera.Position - (self.camera.Front * cameraSpeed * (float32 e.Time))
                    if input.IsKeyDown(Keys.A) then
                        self.camera.Position <- self.camera.Position + (self.camera.Right * cameraSpeed * (float32 e.Time))
                    if input.IsKeyDown(Keys.D) then
                        self.camera.Position <- self.camera.Position - (self.camera.Right * cameraSpeed * (float32 e.Time))
                    if input.IsKeyDown(Keys.Space) then
                        self.camera.Position <- self.camera.Position + (self.camera.Up * cameraSpeed * (float32 e.Time))
                    if input.IsKeyDown(Keys.LeftShift) then
                        self.camera.Position <- self.camera.Position - (self.camera.Up * cameraSpeed * (float32 e.Time))
                    
                    let mouse = self.MouseState
                    if firstMove then
                        lastPos <- Vector2(mouse.X, mouse.Y)
                        firstMove <- false
                    else
                        let deltaX = mouse.X - lastPos.X
                        let deltaY = mouse.Y - lastPos.Y
                        lastPos <- Vector2(mouse.X, mouse.Y)
                        self.camera.Yaw <- self.camera.Yaw + deltaX * sensitivity
                        self.camera.Pitch <- self.camera.Pitch - deltaY * sensitivity

                    base.OnUpdateFrame(e)

        override self.OnResize(e:ResizeEventArgs) =
            GL.Viewport(0, 0, e.Width, e.Height)
            base.OnResize(e)
        override self.OnUnload() = 
            callback Quit |> ignore
            base.OnUnload()

    let root () = 
        let mutable next = Unchecked.defaultof<Entity>

        fun (g:Game) req ->
            match req with
            | Initialize ->
                let vertices = [|
                    //Position          Texture coordinates
                     0.5f;  0.5f; 0.0f; 1.0f; 1.0f; // top right
                     0.5f; -0.5f; 0.0f; 1.0f; 0.0f; // bottom right
                    -0.5f; -0.5f; 0.0f; 0.0f; 0.0f; // bottom left
                    -0.5f;  0.5f; 0.0f; 0.0f; 1.0f;  // top left
                |]
                let indices = [|
                    0u; 1u; 3u;    // first triangle
                    1u; 2u; 3u;    // second triangle
                |]
                let vertexArrayObject = GL.GenVertexArray()
                GL.BindVertexArray(vertexArrayObject)

                let vertexBufferObject = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof<float32>, vertices, BufferUsageHint.StaticDraw)

                let elementBufferObject = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject)
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof<uint>, indices, BufferUsageHint.StaticDraw)

                let shader = new Shader("shader.vert", "shader.frag")
                shader.Use()

                let vertexLocation = shader.GetAttribLocation("aPosition")
                GL.EnableVertexAttribArray(vertexLocation)
                GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 0)

                let texCoordLocation = shader.GetAttribLocation("aTexCoord")
                GL.EnableVertexAttribArray(texCoordLocation)
                GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof<float32>, 3 * sizeof<float32>)

                let texture = Texture.FromFile("container.png")
                texture.Use(TextureUnit.Texture0)

                let texture2 = Texture.FromFile("awesomeface.png")
                (*use bmp = new SKBitmap(512, 512, SKColorType.Rgba8888, SKAlphaType.Unpremul)
                use canvas = new SKCanvas(bmp)
                use paint = new SKPaint()
                paint.TextSize <- 64.0f
                paint.IsAntialias <- true
                paint.Color <- SKColors.Red
                paint.IsStroke <- false
                paint.Style <- SKPaintStyle.Fill

                use typeface = SKTypeface.FromFile("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 0)
                paint.Typeface <- typeface
                canvas.Clear(SKColors.Blue)
                canvas.DrawText("あいうえお", 100.0f, 100.0f, paint)
                // saveSKBitmapToPng "first.png" 100 bmp

                let texture2 = Texture.FromSKBitmap(bmp)
                *)

                texture2.Use(TextureUnit.Texture1)
                shader.SetInt("texture0", 0)
                shader.SetInt("texture1", 1)
                next <- 
                    fun (g:Game) req ->
                        match req with
                        | Render ->
                            GL.BindVertexArray(vertexArrayObject)

                            (*let transform = 
                                ((Matrix4.Identity * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(20f)) *
                                  Matrix4.CreateScale(1.1f)) * Matrix4.CreateTranslation(0.1f, 0.1f, 0.0f))
                            *)
                            let mult a b = a * b
                            let model = 
                                Matrix4.Identity * Matrix4.CreateRotationX(float32 (MathHelper.DegreesToRadians(g.Time)))

                            texture.Use(TextureUnit.Texture0)
                            texture2.Use(TextureUnit.Texture1)
                            shader.Use()
                            shader.SetMatrix4("model", model)
                            shader.SetMatrix4("view", g.camera.GetViewMatrix())
                            shader.SetMatrix4("projection", g.camera.GetProjectionMatrix())

                    (*        let greenValue:float32 = (Math.Sin(timeValue) |> float32) / (2.0f + 0.5f)
                            let vertexColorLocation = GL.GetUniformLocation(self.shader.Handle, "ourColor")
                            GL.Uniform4(vertexColorLocation, 0.0f, greenValue, 0.0f, 1.0f);
                    *)
                            GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0)
                            Ok

                        | Quit ->
                            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                            GL.BindVertexArray(0)
                            GL.UseProgram(0)

                            GL.DeleteBuffer(vertexBufferObject)
                            GL.DeleteBuffer(elementBufferObject)
                            GL.DeleteVertexArray(vertexArrayObject)
                            (shader :> IDisposable).Dispose()
                            (texture :> IDisposable).Dispose()
                            (texture2 :> IDisposable).Dispose()
                            Ok
                        | _ -> Ok
                Ok
            | req -> next g req

    [<EntryPoint>]
    let main argv =
        let nativeWindowSettings = 
            NativeWindowSettings()
            |> (fun it -> 
                    it.Size <- Vector2i(800, 600)
                    it.Title <- "Learn OpenTK"
                    it)
       
        use game = new Game(GameWindowSettings.Default, nativeWindowSettings, root () )
        game.Run()
        0

