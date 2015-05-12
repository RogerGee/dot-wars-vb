Option Strict On
Option Explicit On

Imports System
Imports System.Threading
Imports System.Drawing
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

' A GameObject is the base class for any rendered object in the game; it provides an
' abstract interface that child classes must implement; it provides shared functionality
' to manage all instances of the class that are instantiated, along with methods to render,
' click and update those objects
MustInherit Class GameObject
    Private Shared BRUSHES(0 To 9) As DrawBrush ' all colors
    Private Shared BLUE_BRUSH As DrawBrush ' team colors
    Private Shared GREEN_BRUSH As DrawBrush
    Private Shared YELLOW_BRUSH As DrawBrush
    Private Shared ORANGE_BRUSH As DrawBrush
    Private Shared PURPLE_BRUSH As DrawBrush
    Private Shared PINK_BRUSH As DrawBrush
    Private Shared GRAY_BRUSH As DrawBrush
    Private Shared RED_BRUSH As DrawBrush ' non-team colors
    Private Shared BLACK_BRUSH As DrawBrush

    Private Shared gameObjects As New Collection(Of GameObject)
    Private Shared playerTeam As Integer ' team that the player owns

    Private Shared updateThreadCond As Boolean
    Private Shared updateThread As Thread
    Private Shared updateLock As New Object

    Private Const DECOMP_SEC = 30
    Private Const DECOMP_RATE = 0.25

    Protected bounds As DotRectangle
    Protected teamv As Integer
    Protected health As Integer = 0
    Protected WithEvents weapon As DotWeapon

    Private decompose As Integer = DECOMP_SEC * CInt(1 / DECOMP_RATE) ' seconds till dead object is "decomposed"

    ' "virtual" functions
    Protected MustOverride Sub RenderObject(ByVal surface As DrawSurface, ByVal br As DrawBrush)
    Protected MustOverride Sub UpdateObject()

    ' delegate types and events
    Public Delegate Sub GameObjectClickEventHandler(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation)
    Public Delegate Sub GameObjectTargetedEventHandler(ByVal gobj As GameObject, ByRef squad As DotSquad)
    Public Event OnClick As GameObjectClickEventHandler ' raised when player game object is clicked; gets ref to player object clicked
    Public Event OnExternClick As GameObjectClickEventHandler ' raised when player game object is not clicked; gets ref to foreign object if possible
    Public Event OnTargeted As GameObjectTargetedEventHandler ' raised when the game object is targeted by another object
    Public Event OnDeath As EventHandler ' raised when the object dies

    Sub New(ByVal teamValue As Integer)
        teamv = teamValue

        ' store a reference to the object in a global collection
        SyncLock updateLock
            Dim i = 0
            While i < gameObjects.Count AndAlso gameObjects(i) IsNot Nothing
                i += 1
            End While
            If i < gameObjects.Count Then
                gameObjects(i) = Me
            Else
                gameObjects.Add(Me)
            End If
        End SyncLock
    End Sub

    Shared Sub BeginGameObjects(ByVal surface As DrawSurface, ByVal team As Integer)
        playerTeam = team

        ' load brush resources
        '  team colors
        BRUSHES(0) = New DrawBrush(surface, New SlimDX.Color4(Color.Blue)) ' BLUE
        BRUSHES(1) = New DrawBrush(surface, New SlimDX.Color4(Color.Green)) ' GREEN
        BRUSHES(2) = New DrawBrush(surface, New SlimDX.Color4(Color.Yellow)) ' YELLOW
        BRUSHES(3) = New DrawBrush(surface, New SlimDX.Color4(Color.Orange)) ' ORANGE
        BRUSHES(4) = New DrawBrush(surface, New SlimDX.Color4(Color.Purple)) ' PURPLE
        BRUSHES(5) = New DrawBrush(surface, New SlimDX.Color4(Color.Pink)) ' PINK
        BRUSHES(6) = New DrawBrush(surface, New SlimDX.Color4(Color.Gray)) ' GRAY
        '  non-team colors
        BRUSHES(8) = New DrawBrush(surface, New SlimDX.Color4(Color.Red)) ' RED
        BRUSHES(9) = New DrawBrush(surface, New SlimDX.Color4(Color.Black)) ' BLACK

        BLUE_BRUSH = BRUSHES(0)
        GREEN_BRUSH = BRUSHES(1)
        YELLOW_BRUSH = BRUSHES(2)
        ORANGE_BRUSH = BRUSHES(3)
        PURPLE_BRUSH = BRUSHES(4)
        PINK_BRUSH = BRUSHES(5)
        GRAY_BRUSH = BRUSHES(6)
        RED_BRUSH = BRUSHES(8)
        BLACK_BRUSH = BRUSHES(9)

        ' start update thread
        updateThread = New Thread(New ThreadStart(AddressOf GameObjects_UpdateThread))
        updateThreadCond = True
        updateThread.Start()
    End Sub

    ' this subroutine invokes the rendering operation for all game objects
    Shared Sub RenderGameObjects(ByVal surface As DrawSurface)
        ' obtain a collection of all objects that could possibly be rendered; this
        ' must be performed in a critical region; postpone the actual rendering until
        ' later so that we can return the lock sooner
        Dim rendering As New Collection(Of GameObject)
        SyncLock updateLock
            For Each obj In gameObjects
                If obj IsNot Nothing AndAlso obj.teamv >= 0 Then rendering.Add(obj)
            Next
        End SyncLock

        Dim viewableRect = DotRectangle.WorldViewRectangle
        For Each obj In rendering
            ' check to make sure at least one corner of the object's bounding rectangle is within
            ' the viewable screen space
            If obj.bounds.Overlaps(viewableRect) Then
                obj.Render(surface)
            End If

            ' render the object's weapon
            If obj.weapon IsNot Nothing AndAlso obj.weapon.IsFiring Then obj.weapon.Render(surface, BRUSHES(obj.teamv))
        Next
    End Sub

    Shared Sub ClickGameObjects(ByVal kind As DotClickKind, ByVal target As DotLocation)
        ' obtain a collection of objects to click in the critical region; these objects must
        ' be associated with the player team
        Dim clicking As New Collection(Of GameObject)
        SyncLock updateLock
            For Each obj In gameObjects
                If obj IsNot Nothing AndAlso Not obj.IsDead() AndAlso _
                    (playerTeam = obj.Team OrElse obj.Team = -1) Then clicking.Add(obj)
            Next
        End SyncLock

        ' perform their click actions outside the critical region
        For Each obj In clicking
            obj.Click(kind, target)
        Next
    End Sub

    Shared Sub EndGameObjects()
        ' delete the DirectX brush resources
        For Each br In BRUSHES
            If br IsNot Nothing Then
                br.Dispose()
            End If
        Next

        ' stop update thread
        updateThreadCond = False
        updateThread.Join()
        updateThread = Nothing

        ' destroy game objects
        gameObjects.Clear()
    End Sub

    Const timeout As Integer = 50
    Protected Delegate Function HasTimeElapsedDelegate(ByVal ticks As UInt64, ByVal seconds As Double) As Boolean
    Protected Shared ReadOnly has_time_elapsed As HasTimeElapsedDelegate = Function(ticks As UInt64, seconds As Double) _
                                                                            ticks * timeout / 1000.0 Mod seconds = 0.0
    Private Shared Sub GameObjects_UpdateThread()
        ' we handle the game logic here
        Dim ticks As UInt64 = 1

        While updateThreadCond
            SyncLock updateLock
                ' this thread may add game objects to the global collection; the SyncLock guareentees that we
                ' remain inside the same thread, so no deadlock occurs when an update call tries to obtain the lock
                ' on this same thread (you think it would, but it doesn't)
                Dim top = gameObjects.Count - 1
                For i = 0 To top
                    ' note: dead objects do not get their update actions invoked; their warheads
                    ' still need to be updated in case they are finishing
                    Dim obj = gameObjects(i)
                    If obj IsNot Nothing Then
                        If Not obj.IsDead() Then obj.UpdateObject() ' let each object update itself in some way
                        If obj.weapon IsNot Nothing Then obj.weapon.Update(gameObjects, obj.teamv) ' update object's weapon
                    End If
                Next
            End SyncLock

            ' every second, update the dead objects' decompose counter
            If has_time_elapsed(ticks, DECOMP_RATE) Then
                SyncLock updateLock
                    For i = 0 To gameObjects.Count - 1
                        If gameObjects(i) IsNot Nothing AndAlso gameObjects(i).IsDead() Then
                            If gameObjects(i).decompose <= 0 Then
                                gameObjects(i) = Nothing
                            Else
                                gameObjects(i).decompose -= 1
                            End If
                        End If
                    Next
                End SyncLock
            End If

            ' update ticks; if, God forbid, we ever exceed 2^64, we need to reset to avoid throwing an exception
            ticks = If(ticks + 1 = UInt64.MaxValue, 0UL, ticks + 1UL)

            Thread.Sleep(timeout)
        End While
    End Sub

    Protected Shared ReadOnly Property UpdateTimeout As Integer
        Get
            Return timeout
        End Get
    End Property

    Function IsDead() As Boolean
        Return health <= 0
    End Function

    Sub Render(ByVal surface As DrawSurface)
        Dim br As DrawBrush
        Dim dper As Single
        Dim clr As SlimDX.Color4
        br = BRUSHES(teamv)
        dper = CSng(decompose) / CSng(DECOMP_SEC)
        If IsDead() Then
            BrushAlpha(br, dper, clr)
            RenderObject(surface, br)
            br.Color = clr
        Else
            RenderObject(surface, br)
        End If

        ' draw red X of death if dead
        If IsDead() Then
            Dim localRect As New DotRectangle(bounds.location.GetRelativeLocation(), bounds.size)

            BrushAlpha(RED_BRUSH, dper, clr)
            surface.DrawLine(RED_BRUSH, localRect.TopLeftCorner().ToPoint(), localRect.BotRightCorner().ToPoint())
            surface.DrawLine(RED_BRUSH, localRect.TopRightCorner().ToPoint(), localRect.BotLeftCorner().ToPoint())
            RED_BRUSH.Color = clr
        End If
    End Sub

    Sub Click(ByVal kind As DotClickKind, ByVal location As DotLocation)
        ' only do click action if the object was clicked
        If location.IsWithin(bounds) Then
            ' raise the OnClick event; make the location relative to the bounding
            ' rectangle for the object
            RaiseEvent OnClick(Me, kind, location - bounds.location)
        Else
            ' see if the click occurred over a foreign game object
            Dim o As GameObject = Nothing
            SyncLock updateLock
                For Each obj In gameObjects
                    If obj IsNot Nothing AndAlso Not obj.IsDead() AndAlso obj.Team <> playerTeam _
                        AndAlso location.IsWithin(obj.bounds) Then
                        o = obj
                        Exit For
                    End If
                Next
            End SyncLock
            ' raise the OnExternClick event; leave the location alone
            RaiseEvent OnExternClick(o, kind, location)
        End If
    End Sub

    Sub Arm(ByVal weaponType As Type, ByVal weaponInfo As DotWeaponInfo, Optional ByVal killHandler As DotWeapon.DotWeaponEventHandler = Nothing)
        ' create a weapon of the specified type using the specified info; add
        ' a kill handler if specified
        weapon = CType(Activator.CreateInstance(weaponType, weaponInfo), DotWeapon)
        If killHandler IsNot Nothing Then AddHandler weapon.OnTargetKilled, killHandler
    End Sub

    Sub CeaseFire()
        If weapon IsNot Nothing Then weapon.CeaseFire()
    End Sub

    ' this means, target Me, which raises an event to obtain the DotSquad (if any)
    ' to which the object is attached
    Sub Target(ByRef squad As DotSquad)
        RaiseEvent OnTargeted(Me, squad)
    End Sub

    Overridable Sub AttackObject(ByVal obj As GameObject)
        If weapon Is Nothing Then Exit Sub

        ' the default behavior is to attempt to fire on
        ' the object from the current location; this will
        ' fail if the object is out of range
        weapon.FireOn(Me, obj)
    End Sub

    Sub DealDamage(ByVal amt As Integer)
        If Me.teamv < 0 Then Exit Sub ' not a "real" object
        Dim was = Me.IsDead()
        Me.health -= amt
        If Not was AndAlso Me.IsDead() Then RaiseEvent OnDeath(Me, New EventArgs)
    End Sub

    ReadOnly Property BoundingRectangle As DotRectangle
        Get
            Return Me.bounds
        End Get
    End Property

    Property Size As Size
        Get
            Return bounds.size
        End Get
        Set(value As Size)
            bounds.width = value.Width
            bounds.height = value.Height
        End Set
    End Property

    Property Width As Integer
        Get
            Return bounds.width
        End Get
        Set(value As Integer)
            bounds.width = value
        End Set
    End Property

    Property Height As Integer
        Get
            Return bounds.height
        End Get
        Set(value As Integer)
            bounds.height = value
        End Set
    End Property

    Overridable Property Location As DotLocation
        Get
            Return bounds.location
        End Get
        Set(value As DotLocation)
            bounds.location = value
        End Set
    End Property

    ReadOnly Property Team As Integer
        Get
            Return teamv
        End Get
    End Property

    Private Function GetNearObjectImpl(ByVal distance As Double) As GameObject
        For Each obj In gameObjects
            If obj IsNot Nothing AndAlso Not obj.IsDead() AndAlso obj.teamv <> Me.teamv AndAlso obj.teamv >= 0 AndAlso _
                New DotVector(Me.Location.px - obj.Location.px, Me.Location.py - obj.Location.py).GetMagnitude() <= distance Then
                Return obj
            End If
        Next

        Return Nothing
    End Function
    Protected Function GetNearObject(ByVal distance As Double, Optional ByVal DoMutualExclusion As Boolean = False) As GameObject
        ' note: DoMutualExclusion should be False if called from the update thread
        If DoMutualExclusion Then
            SyncLock updateLock
                GetNearObject = GetNearObjectImpl(distance)
            End SyncLock
        Else
            GetNearObject = GetNearObjectImpl(distance)
        End If
    End Function

    Private Sub IDied(ByVal sender As Object, ByVal e As EventArgs) Handles Me.OnDeath
        ' I can't fire if I'm dead...
        Me.CeaseFire()
    End Sub
End Class

' Represents an unrendered object that is used to hook into the object system
Class DummyObject
    Inherits GameObject

    Public Event OnUpdateHook As EventHandler

    Sub New()
        MyBase.New(-1)
        health = 1
    End Sub

    Protected Overrides Sub RenderObject(ByVal surface As DrawSurface, ByVal br As DrawBrush)
        Exit Sub
    End Sub

    Protected Overrides Sub UpdateObject()
        RaiseEvent OnUpdateHook(Me, New EventArgs)
    End Sub
End Class