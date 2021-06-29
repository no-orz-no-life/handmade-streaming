namespace MovieToolkit

open System
open OpenTK.Mathematics

type Camera(pos:Vector3, aspect:float32) = 
    let mutable front = -Vector3.UnitZ
    let mutable up = Vector3.UnitY
    let mutable right = Vector3.UnitX
    let mutable pitch = 0.0f
    let mutable yaw = -MathHelper.PiOver2
    let mutable fov = MathHelper.PiOver2

    let mutable position = Vector3.Zero
    let mutable aspectRatio= 0.0f
    do
        position <- pos
        aspectRatio <- aspect
    member self.Position 
        with get() = position
        and set(v) = position <- v
    member self.AspectRatio
        with get() = aspectRatio
    member self.Front
        with get() = front
        and set(v) = front <- v
    member self.Up
        with get() = up
        and set(v) = up <- v
    member self.Right
        with get() = right
        and set(v) = right <- v
    member self.Pitch
        with get() = MathHelper.RadiansToDegrees(pitch)
        and set(v) = 
            let angle = MathHelper.Clamp(v, -89f, 89f)
            pitch <- MathHelper.DegreesToRadians(angle)
            self.UpdateVectors()
    member self.Yaw
        with get() = MathHelper.RadiansToDegrees(yaw)
        and set(v:float32) =
            yaw <- MathHelper.DegreesToRadians(v)
            self.UpdateVectors()
    member self.Fov
        with get() = MathHelper.RadiansToDegrees(fov)
        and set(v) =
            let angle = MathHelper.Clamp(v, 1f, 45f)
            fov <- MathHelper.DegreesToRadians(angle)
    member self.GetViewMatrix() =
        Matrix4.LookAt(position, position + front, up)
    member self.GetProjectionMatrix() =
        Matrix4.CreatePerspectiveFieldOfView(fov, aspectRatio, 0.01f, 100f)
    member self.UpdateVectors() =
        front.X <- MathF.Cos(pitch) * MathF.Cos(yaw)
        front.Y <- MathF.Sin(pitch)
        front.Z <- MathF.Cos(pitch) * MathF.Sin(yaw)
        front <- Vector3.Normalize(front)
        right <- Vector3.Normalize(Vector3.Cross(front, Vector3.UnitY))
        up <- Vector3.Normalize(Vector3.Cross(right, front))
