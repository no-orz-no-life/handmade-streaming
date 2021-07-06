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
open Sprite
open Game

module Main = 

    let makeEntity initial = 
        let mutable next = Unchecked.defaultof<Entity>

        fun (g:Game) req ->
            match req with
            | Initialize ->
                next <- initial g req
                Ok
            | req -> next g req

    let testFont () =
        makeEntity <|
        fun (g:Game) req -> 
            use bmp = new SKBitmap(700, 512, SKColorType.Rgba8888, SKAlphaType.Unpremul)
            use canvas = new SKCanvas(bmp)

            canvas.Clear(SKColors.Transparent)

            let height = 64.0f
            use paint = new SKPaint()
            paint.TextSize <- height
            paint.IsAntialias <- true

            use typeface = SKTypeface.FromFile("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 0)
            paint.Typeface <- typeface

            let message = "人生オワタ＼(^o^)／"

            paint.Style <- SKPaintStyle.Fill
            paint.Color <- SKColors.White
            paint.Shader <- SKShader.CreateLinearGradient(
                SKPoint(0f, 100f),
                SKPoint(0f, height + 100f),
                [| 
                    SKColor(200uy, 200uy, 200uy)
                    SKColor(150uy, 255uy, 255uy)
                    SKColor(200uy, 200uy, 200uy)  |],
                [| 0.45f; 0.7f; 0.85f|],
                SKShaderTileMode.Repeat
            )
            canvas.DrawText(message, 100.0f, 100.0f, paint)

            paint.Color <- SKColor(20uy, 20uy, 20uy)
            paint.Style <- SKPaintStyle.Stroke
            paint.StrokeWidth <- 2.0f
            paint.Shader <- null
            canvas.DrawText(message, 100.0f, 100.0f, paint)

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

