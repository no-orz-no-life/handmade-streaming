module Game
    open System.IO
    open OpenTK.Windowing.Desktop
    open OpenTK.Windowing.Common
    open OpenTK.Windowing.GraphicsLibraryFramework
    open OpenTK.Mathematics
    open SkiaSharp
    open MovieToolkit
    open OpenTK.Graphics.OpenGL4

    open FFMediaToolkit.Graphics
    open FFMediaToolkit.Encoding

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
            GL.ClearColor(0.2f, 0.3f, 0.3f, 0.0f)
            pointer <- System.Runtime.InteropServices.Marshal.AllocHGlobal(4 * self.Size.X * self.Size.Y)

            let aspectRatio = (float32 self.Size.X) / (float32 self.Size.Y)

            self.camera <- Camera(Vector3.UnitZ * 3.0f, aspectRatio)
            self.CursorGrabbed <- true

            let inline enumOfValue e = e |> int |> LanguagePrimitives.EnumOfValue

            let settings = VideoEncoderSettings(self.Size.X, self.Size.Y, 60, enumOfValue FFmpeg.AutoGen.AVCodecID.AV_CODEC_ID_PRORES)
            settings.EncoderPreset <- EncoderPreset.Fast
            settings.CRF <- 17
            settings.VideoFormat <- enumOfValue FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_YUVA444P10LE
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
