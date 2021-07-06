namespace MovieToolkit

open System
open System.IO
open OpenTK.Mathematics
open OpenTK.Graphics.OpenGL4
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Windowing.Desktop
open SkiaSharp
open FFMediaToolkit.Graphics
open FFMediaToolkit.Encoding

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

        let saveFrameToPng path bmp = 
            saveSKBitmapToPng path 100 bmp

        let mutable mediaOutput:MediaOutput = Unchecked.defaultof<MediaOutput>
        //[<DefaultValue>]val mutable frameImageData:ImageData

        let newFrame (bmp:SKBitmap) =
            let frameImageData = ImageData.FromArray(bmp.Bytes, ImagePixelFormat.Rgba32, System.Drawing.Size(self.Size.X, self.Size.Y))
            mediaOutput.Video.AddFrame(frameImageData) |> ignore

        member self.Time
            with get() = time

        override self.OnLoad() =
            callback Initialize |> ignore
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f)
            pointer <- System.Runtime.InteropServices.Marshal.AllocHGlobal(4 * self.Size.X * self.Size.Y)

            let aspectRatio = (float32 self.Size.X) / (float32 self.Size.Y)

            self.camera <- Camera(Vector3.UnitZ * 3.0f, aspectRatio)
            self.CursorGrabbed <- true

            let settings = VideoEncoderSettings(self.Size.X, self.Size.Y, 60, VideoCodec.H264)
            settings.EncoderPreset <- EncoderPreset.Fast
            settings.CRF <- 17
            mediaOutput <- MediaBuilder.CreateContainer(Path.GetFullPath("output/out.mkv")).WithVideo(settings).Create()
            //self.frameImageData <- (ImageData.FromPointer(pointer, ImagePixelFormat.Rgba32, System.Drawing.Size(self.Size.X, self.Size.Y)))

            base.OnLoad()

        override self.OnRenderFrame(e:FrameEventArgs) =
            time <- time + 4.0 * e.Time
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            callback Render |> ignore

            if saveFile then
                GL.Finish()
                GL.ReadBuffer(ReadBufferMode.Back)
                GL.ReadPixels(0, 0, self.Size.X, self.Size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, pointer)

                let info = SKImageInfo(self.Size.X, self.Size.Y, SKColorType.Rgba8888)
                let image = SKImage.FromPixels(info, pointer, self.Size.X * 4)

                use bmp = new SKBitmap(self.Size.X, self.Size.Y, SKColorType.Rgba8888, SKAlphaType.Premul)
                use canvas = new SKCanvas(bmp)
                canvas.Scale(1.0f, -1.0f, 0.0f, (float32 bmp.Height) / 2.0f)
                canvas.DrawImage(image, 0.0f, 0.0f)

                newFrame bmp
                (*saveFrameToPng "output/out.png"
                saveFile <- false*)

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
            mediaOutput.Dispose()
            base.OnUnload()

    let makeEntity initial = 
        let mutable next = Unchecked.defaultof<Entity>

        fun (g:Game) req ->
            match req with
            | Initialize ->
                next <- initial g req
                Ok
            | req -> next g req
    type ColorKeyOption = 
    | None
    | FromPixel of int*int
    | ByColor of SKColor
    let setColorKey (bmp:SKBitmap) (colorKey:SKColor) =
        let mutable x = 0
        let mutable y = 0
        try
            // apply colorkey
            for x in 0..(bmp.Width) do
                for y in 0..(bmp.Height) do
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

    let testFont () =
        makeEntity <|
        fun (g:Game) req -> 
            use bmp = new SKBitmap(512, 512, SKColorType.Rgba8888, SKAlphaType.Unpremul)
            use canvas = new SKCanvas(bmp)

            canvas.Clear(SKColors.Transparent)

            use paint = new SKPaint()
            paint.TextSize <- 80.0f
            paint.IsAntialias <- true

            use typeface = SKTypeface.FromFile("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 0)
            paint.Typeface <- typeface

            paint.Style <- SKPaintStyle.Fill
            paint.Color <- SKColors.White
            canvas.DrawText("あいうえお", 100.0f, 100.0f, paint)

            paint.Color <- SKColors.Red
            paint.Style <- SKPaintStyle.Stroke
            paint.StrokeWidth <- 3.0f
            canvas.DrawText("あいうえお", 100.0f, 100.0f, paint)

            let sprite = Sprite.FromSKBitmap(bmp, ColorKeyOption.None)
            let view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f)
            let projection = Matrix4.CreateOrthographicOffCenter(0.0f, (float32 g.Size.X), (float32 g.Size.Y), 0.0f, 0.1f, 100.0f)
            fun (g:Game) req ->
                match req with
                | Render ->
                    GL.Enable(EnableCap.Blend)
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)

                    sprite.Use(view, projection)
                    sprite.Render(400, 300)

                    GL.Disable(EnableCap.Blend)
                    Ok
                | Quit ->
                    (sprite :> IDisposable).Dispose()
                    Ok
                | _ -> Ok

    let testsprite () = 

        makeEntity <|
        fun (g:Game) req ->
            let sprite = Sprite.FromFile("icon.bmp", (ColorKeyOption.FromPixel(0,0)))
            let numSprites = 200
            let v = 3
            let x = Array.zeroCreate numSprites
            let y = Array.zeroCreate numSprites
            let vx = Array.zeroCreate numSprites
            let vy = Array.zeroCreate numSprites

            let r = Random()
            for i in 0..(numSprites - 1) do
                x.[i] <- r.Next(g.Size.X - sprite.Width)
                y.[i] <- r.Next(g.Size.Y - sprite.Height)
                let mutable tx = 0
                let mutable ty = 0
                while tx = 0 && ty = 0 do
                    tx <- (1 - r.Next(3)) * v
                    ty <- (1 - r.Next(3)) * v
                vx.[i] <- tx
                vy.[i] <- ty

            let view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f)
            let projection = Matrix4.CreateOrthographicOffCenter(0.0f, (float32 g.Size.X), (float32 g.Size.Y), 0.0f, 0.1f, 100.0f)
            fun (g:Game) req ->
                match req with
                | Update ->
                    for i in 0..(numSprites - 1) do
                        let mutable newX = x.[i] + vx.[i]
                        let mutable newY = y.[i] + vy.[i]
                        if (newX >= g.Size.X - sprite.Width / 2) || (newX <= 0) then
                            vx.[i] <- vx.[i]  * (-1)
                            newX <- newX + 2 * vx.[i]
                        if (newY >= g.Size.Y - sprite.Height / 2) || (newY <= 0) then
                            vy.[i] <- vy.[i]  * (-1)
                            newY <- newY + 2 * vy.[i]

                        x.[i] <- newX
                        y.[i] <- newY
                    Ok
                | Render ->
                    GL.Enable(EnableCap.Blend)
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)

                    sprite.Use(view, projection)

                    for i in 0..(numSprites - 1) do
                        sprite.Render(x.[i], y.[i])

                    GL.Disable(EnableCap.Blend)
                    Ok

                | Quit ->
                    (sprite :> IDisposable).Dispose()
                    Ok
                | _ -> Ok

    let root () = 
        let child = [|
            testsprite ()
            testFont ()
            |]
        makeEntity <|
        fun (g:Game) req ->
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

            let shader = Shader.FromFile("shader.vert", "shader.frag")
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
            texture2.Use(TextureUnit.Texture1)
            shader.SetInt("texture0", 0)
            shader.SetInt("texture1", 1)
            child |> Seq.iter (fun c -> c g req |> ignore)
            fun (g:Game) req ->
                child |> Seq.iter (fun c -> c g req |> ignore)
                match req with
                | Render ->
                    GL.BindVertexArray(vertexArrayObject)

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
    [<EntryPoint>]
    let main argv =
        Directory.CreateDirectory("output") |> ignore
        let nativeWindowSettings = 
            NativeWindowSettings()
            |> (fun it -> 
                    it.Size <- Vector2i(800, 600)
                    it.Title <- "Learn OpenTK"
                    it)
       
        use game = new Game(GameWindowSettings.Default, nativeWindowSettings, root () )
        game.Run()
        0

