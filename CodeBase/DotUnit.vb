Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

Class DotUnitInfo
    Protected team As Integer
    Protected radius As Integer
    Protected maxHealth As Integer
    Protected speed As Double

    Function GetTeam() As Integer
        GetTeam = team
    End Function
    Function GetRadius() As Integer
        GetRadius = radius
    End Function
    Function GetMaxHealth() As Integer
        GetMaxHealth = maxHealth
    End Function
    Function GetSpeed() As Double
        GetSpeed = speed
    End Function
End Class

Class DotUnit
    Inherits GameObject

    Private info As DotUnitInfo
    Private idle As Boolean = True ' if False, then dot is traveling to 'heading'
    Private speed As Double ' 1 unit is 5 screen pixels per update cycle
    Private veloc As DotVector
    Private accel As DotVector
    Private heading As DotLocation
    Private sel As Boolean = False

    Sub New(ByVal info As DotUnitInfo)
        Me.info = info
        Me.health = info.GetMaxHealth()
        Me.speed = info.GetSpeed()
        Me.bounds.width = info.GetRadius()
        Me.bounds.height = info.GetRadius()
    End Sub

    ' send the dot to a location
    Sub SendTo(ByVal location As DotLocation)
        Dim dir As Double
        Dim dx, dy As Integer

        heading = location

        ' compute direction
        dx = heading.px - bounds.location.px
        dy = heading.py - bounds.location.py
        veloc = New DotVector(dx, dy)

        ' use direction to get final vector
        dir = veloc.GetDirection()
        veloc = New DotVector(dir, speed, False)

        idle = False
    End Sub

    ' stop the dot in its tracks
    Sub Halt()
        idle = True
    End Sub

    Sub Touch()
        sel = True
    End Sub

    Sub Ignore()
        sel = False
    End Sub

    ReadOnly Property Selected As Boolean
        Get
            Return sel AndAlso Not IsDead()
        End Get
    End Property

    Protected Overrides Sub RenderObject(ByVal surface As DrawSurface)
        Dim e As New SlimDX.Direct2D.Ellipse
        Dim br = BRUSHES(info.GetTeam())
        e.Center = Location.GetRelativeLocation().ToPoint()
        e.RadiusX = bounds.width \ 2
        e.RadiusY = bounds.height \ 2

        If Selected Then
            Dim oldColor, newColor As SlimDX.Color4
            oldColor = br.Color
            newColor = oldColor

            newColor.Alpha = 0.5! * (CSng(health) / CSng(info.GetMaxHealth()))
            br.Color = newColor
            surface.FillEllipse(br, e)

            br.Color = oldColor
        End If

        surface.DrawEllipse(br, e)
    End Sub

    Protected Overrides Sub UpdateObject()
        If Not idle Then
            ' compute remaining distance to destination
            Dim remain As New DotVector(heading.px - bounds.location.px, heading.py - bounds.location.py)

            ' apply acceleration vector
            veloc += accel

            ' check to see if the dot has arrived at its destination
            If Math.Abs(remain.cx) < Math.Abs(veloc.cx) OrElse Math.Abs(remain.cy) < Math.Abs(veloc.cy) Then
                bounds.location = heading
                Halt()
                Exit Sub
            End If

            ' apply veloc vector to location
            bounds.location.px += CInt(Math.Round(veloc.cx))
            bounds.location.py += CInt(Math.Round(veloc.cy))

            ' if the dot is nearing the destination, then slow it down for effect
            If remain.GetMagnitude() < speed Then
                ' the distance left is less than the dot's speed, so apply acceleration
                ' in the opposite direction
                veloc += New DotVector(remain.GetDirection(), speed / 1.1, False)
            Else
                ' keep the dot on the straight and narrow by realigning it to its heading
                veloc = New DotVector(remain.GetDirection(), speed, False)
            End If
        ElseIf Not sel Then
            ' idle mode: apply a random motion to make the dot look like it is idleing

        End If
    End Sub

    Overrides Property Location As DotLocation
        ' we refer to the center of the dot for its location
        Get
            Return MyBase.Location + New DotLocation(bounds.width \ 2, bounds.height \ 2)
        End Get
        Set(value As DotLocation)
            MyBase.Location = value
        End Set
    End Property
End Class

Class DotTest
    Inherits DotUnit

    Private Class DotTestInfo
        Inherits DotUnitInfo
        Sub New(ByVal dotTeam As Integer)
            maxHealth = 10
            radius = 50
            speed = 2.55
            team = dotTeam
        End Sub
    End Class

    Sub New()
        MyBase.New(New DotTestInfo(1))
    End Sub

    Private Sub DotTest_OnClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation) Handles Me.OnClick
        Me.Touch()
    End Sub

    Private Sub DotTest_OnExternClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation) Handles Me.OnExternClick
        If kind = DotClickKind.Click1 Then
            Me.Ignore()
        ElseIf kind = DotClickKind.Click2 And Selected Then
            Me.SendTo(location)
        End If
    End Sub
End Class