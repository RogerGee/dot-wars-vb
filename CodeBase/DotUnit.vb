Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

MustInherit Class DotUnitInfo
    Public Event OnUpdateInfo As EventHandler

    Private teamv As Integer
    Private radiusv As Integer
    Private maxHealthv As Integer
    Private speedv As Double

    ' getters and setters; some of these trigger an update event
    Property Team As Integer
        Get
            Return teamv
        End Get
        Set(value As Integer)
            teamv = value
        End Set
    End Property
    Property Radius As Integer
        Get
            Return radiusv
        End Get
        Set(value As Integer)
            radiusv = value
            RaiseEvent OnUpdateInfo(Me, New EventArgs)
        End Set
    End Property
    Property MaxHealth As Integer
        Get
            Return maxHealthv
        End Get
        Set(value As Integer)
            maxHealthv = value
        End Set
    End Property
    Property Speed As Double
        Get
            Return speedv
        End Get
        Set(value As Double)
            speedv = value
            RaiseEvent OnUpdateInfo(Me, New EventArgs)
        End Set
    End Property
End Class

Class DotUnit
    Inherits GameObject

    WithEvents info As DotUnitInfo
    Private idle As Boolean = True ' if False, then dot is traveling to 'heading'
    Private speed As Double ' 1 unit is 1 screen pixel per update cycle
    Private veloc As DotVector
    Private accel As DotVector
    Private idlev As DotVector
    Private heading As DotLocation
    Private sel As Boolean = False

    Sub New(ByVal info As DotUnitInfo)
        MyBase.New(info.Team)
        Me.info = info
        Me.health = info.MaxHealth
        Me.speed = info.Speed
        Me.bounds.width = info.Radius * 2
        Me.bounds.height = info.Radius * 2
    End Sub

    ' send the dot to a location
    Sub SendTo(ByVal location As DotLocation)
        Dim dir As Double
        Dim dx, dy As Integer

        heading = location - New DotLocation(bounds.width \ 2, bounds.height \ 2)
        speed = info.Speed

        ' compute direction
        dx = heading.px - bounds.location.px
        dy = heading.py - bounds.location.py
        veloc = New DotVector(dx, dy)

        ' use direction to get final vector
        dir = veloc.GetDirection()
        veloc = New DotVector(dir, speed, False)
        accel = New DotVector(dir, 0.5, False)

        idle = False
    End Sub

    ' stop the dot in its tracks
    Sub Halt()
        idle = True
    End Sub

    ' select the dot
    Sub Touch()
        sel = True
    End Sub

    ' de-select the dot
    Sub Ignore()
        sel = False
    End Sub

    Overrides Sub AttackObject(ByVal obj As GameObject)
        ' send the dot towards the target
        SendTo(obj.Location)

        ' call the base functionality
        MyBase.AttackObject(obj)
    End Sub

    ReadOnly Property Selected As Boolean
        Get
            Return sel AndAlso Not IsDead()
        End Get
    End Property

    Overrides Property Location As DotLocation
        ' we refer to the center of the dot for its location
        Get
            Return MyBase.Location + New DotLocation(bounds.width \ 2, bounds.height \ 2)
        End Get
        Set(value As DotLocation)
            MyBase.Location = value
        End Set
    End Property

    Protected Overrides Sub RenderObject(ByVal surface As DrawSurface)
        Dim e As New SlimDX.Direct2D.Ellipse
        Dim br = BRUSHES(team)
        e.Center = Location.GetRelativeLocation().ToPoint()
        e.RadiusX = bounds.width \ 2
        e.RadiusY = bounds.height \ 2

        If Selected Then
            Dim oldColor, newColor As SlimDX.Color4
            oldColor = br.Color
            newColor = oldColor

            newColor.Alpha = 0.5! * (CSng(health) / CSng(info.MaxHealth))
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
            speed = veloc.GetMagnitude()

            ' check to see if the dot has arrived at its destination
            If Math.Abs(remain.cx) < Math.Abs(veloc.cx) AndAlso Math.Abs(remain.cy) < Math.Abs(veloc.cy) Then
                bounds.location = heading
                Halt()
                Exit Sub
            End If

            ' apply veloc vector to location
            bounds.location.px += CInt(Math.Round(veloc.cx))
            bounds.location.py += CInt(Math.Round(veloc.cy))

            ' if the dot is nearing the destination, then slow it down for effect
            If remain.GetMagnitude() < info.Radius Then
                ' the distance left is less than the dot's speed, so apply acceleration
                ' in the opposite direction
                veloc += New DotVector(remain.GetDirection() + Math.PI, speed / 2, False)
            Else
                ' keep the dot on the straight and narrow by realigning it to its heading
                veloc = New DotVector(remain.GetDirection(), speed, False)
            End If
        ElseIf Not sel Then
            ' idle mode: apply a random motion to make the dot look like it is idleing

        End If
    End Sub

    Private Sub OnUpdateInfo(ByVal sender As Object, ByVal e As EventArgs) Handles info.OnUpdateInfo
        ' update radius
        bounds.width = info.Radius * 2
        bounds.height = info.Radius * 2

        ' update speed
        speed = info.Speed
    End Sub

    Private Sub OnInRange(ByVal sender As Object, ByVal e As EventArgs) Handles weapon.OnInRange
        ' we are targeting something and have gotten in range, so we can stop
        ' moving; this is mostly for ranged units so that we can see them firing
        ' from a distance
        Halt()
    End Sub
End Class

' This DotUnit derivation is for testing and should not be used with a squad
Class DotTest
    Inherits DotUnit

    Private Class DotTestInfo
        Inherits DotUnitInfo
        Sub New(ByVal dotTeam As Integer)
            MaxHealth = 10
            Radius = 50
            Speed = 20.55
            Team = dotTeam
        End Sub
    End Class

    Sub New()
        MyBase.New(New DotTestInfo(1))
    End Sub

    Private Sub DotTest_OnClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation) Handles Me.OnClick
        If kind = DotClickKind.Click1 Then
            Me.Touch()
        End If
    End Sub

    Private Sub DotTest_OnExternClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation) Handles Me.OnExternClick
        If kind = DotClickKind.Click1 Then
            Me.Ignore()
        ElseIf kind = DotClickKind.Click2 And Selected Then
            Me.SendTo(location)
        End If
    End Sub
End Class

' Unit info for common unit types
Class DotFodderInfo
    Inherits DotUnitInfo

    Sub New(ByVal teamValue As Integer)
        MaxHealth = 5
        Radius = 5
        Speed = 5.77
        Team = teamValue
    End Sub
End Class

Class DotInfantryInfo
    Inherits DotUnitInfo

    Sub New(ByVal teamValue As Integer)
        MaxHealth = 20
        Radius = 17
        Speed = 15.67
        Team = teamValue
    End Sub
End Class