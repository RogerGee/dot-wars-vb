Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

Class DotUnit
    Inherits GameObject

    Private team As Integer
    Private vector As DotVector
    Private sel As Boolean = False

    Sub New(ByVal team As Integer)
        Me.team = team
    End Sub

    Protected Overrides Sub RenderObject(ByVal surface As DrawSurface)
        Dim e As New SlimDX.Direct2D.Ellipse
        Dim br = BRUSHES(team)
        e.Center = loc.GetRelativeLocation().ToPoint()
        e.RadiusX = bound.Width \ 2
        e.RadiusY = bound.Height \ 2

        If sel Then
            Dim oldColor, newColor As SlimDX.Color4
            oldColor = br.Color
            newColor = oldColor

            newColor.Alpha = 0.25!
            br.Color = newColor
            surface.FillEllipse(br, e)

            br.Color = oldColor
        End If

        surface.DrawEllipse(br, e)
    End Sub

    Protected Overrides Sub ClickAction(ByVal kind As DotClickKind)

    End Sub
End Class
