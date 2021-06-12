open System
open OpenTK
open OpenTK.Graphics
open OpenTK.Windowing.Desktop
open OpenTK.Windowing.Common
open OpenTK.Windowing.GraphicsLibraryFramework
open OpenTK.Graphics.OpenGL

type Game(width: int, height: int, title: string) as self =
    inherit GameWindow(GameWindowSettings.Default, NativeWindowSettings.Default)
    override self.OnUpdateFrame(e:FrameEventArgs) =
        let input = self.KeyboardState
        if input.IsKeyDown(Keys.Escape) = true then
            Environment.Exit(0)
        else
            base.OnUpdateFrame(e)
    override self.OnLoad() =
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f)
        base.OnLoad()
    override self.OnRenderFrame(e:FrameEventArgs) =
        GL.Clear(ClearBufferMask.ColorBufferBit)
        self.Context.SwapBuffers()
        base.OnRenderFrame(e)
    override self.OnResize(e:ResizeEventArgs) =
        GL.Viewport(0, 0, e.Width, e.Height)
        base.OnResize(e)


[<EntryPoint>]
let main argv =
    use game = new Game(800, 600, "Learn OpenTK")
    game.Run()
    0

