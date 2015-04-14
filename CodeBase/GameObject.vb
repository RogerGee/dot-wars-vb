Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

MustInherit Class GameObject
    Protected Shared BRUSHES(0 To 9) As DrawBrush
    Protected Shared BLACK_BRUSH As DrawBrush
    Protected Shared RED_BRUSH As DrawBrush
    Protected Shared BLUE_BRUSH As DrawBrush
    Protected Shared GREEN_BRUSH As DrawBrush

    Protected loc As DotLocation
    Protected bound As Size

    Protected health As Integer = 0

    ' "virtual" functions
    Protected MustOverride Sub RenderObject(ByVal surface As DrawSurface)
    Protected MustOverride Sub ClickAction(ByVal kind As DotClickKind)

    Shared Sub InitGameObject(ByVal surface As DrawSurface)
        BRUSHES(0) = New DrawBrush(surface, New SlimDX.Color4(Color.Black)) ' BLACK
        BRUSHES(1) = New DrawBrush(surface, New SlimDX.Color4(Color.Red)) ' RED
        BRUSHES(2) = New DrawBrush(surface, New SlimDX.Color4(Color.Blue)) ' BLUE
        BRUSHES(3) = New DrawBrush(surface, New SlimDX.Color4(Color.Green)) ' GREEN

        BLACK_BRUSH = BRUSHES(0)
        RED_BRUSH = BRUSHES(1)
        BLUE_BRUSH = BRUSHES(2)
        GREEN_BRUSH = BRUSHES(3)
    End Sub

    Shared Sub DestroyGameObject()
        For Each br In BRUSHES
            If Not br Is Nothing Then
                br.Dispose()
            End If
        Next
    End Sub

    Function IsDead() As Boolean
        Return health <= 0
    End Function

    Sub Render(ByVal surface As DrawSurface)
        RenderObject(surface)

        ' draw red X of death if dead
        If IsDead() Then
            Dim p = loc.ToPoint()
            p.X -= bound.Width \ 2
            p.Y -= bound.Height \ 2
            surface.DrawLine(RED_BRUSH, p, p + bound)
            surface.DrawLine(RED_BRUSH, p + New Size(bound.Width, 0), p + New Size(0, bound.Height))
        End If
    End Sub

    Sub Click(ByVal kind As DotClickKind)

        ClickAction()
    End Sub

    Property Size As Size
        Get
            Return bound
        End Get
        Set(value As Size)
            bound = value
        End Set
    End Property

    Property Location As DotLocation
        Get
            Return loc
        End Get
        Set(value As DotLocation)
            loc = value
        End Set
    End Property
End Class
