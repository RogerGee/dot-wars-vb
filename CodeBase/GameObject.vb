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
    Protected Shared BRUSHES(0 To 9) As DrawBrush
    Protected Shared BLACK_BRUSH As DrawBrush
    Protected Shared RED_BRUSH As DrawBrush
    Protected Shared BLUE_BRUSH As DrawBrush
    Protected Shared GREEN_BRUSH As DrawBrush

    Private Shared gameObjects As New Collection(Of GameObject)
    Private Shared playerTeam As Integer ' team that the player owns

    Private Shared updateThreadCond As Boolean
    Private Shared updateThread As Thread
    Private Shared updateLock As New Object

    Protected bounds As DotRectangle
    Protected teamv As Integer
    Protected health As Integer = 0
    Protected WithEvents weapon As DotWeapon

    Private decompose As Integer = 180 ' seconds till dead object is "decomposed"

    ' "virtual" functions
    Protected MustOverride Sub RenderObject(ByVal surface As DrawSurface)
    Protected MustOverride Sub UpdateObject()

    ' delegate types and events
    Public Delegate Sub GameObjectClickEventHandler(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation)
    Public Delegate Sub GameObjectTargetedEventHandler(ByVal gobj As GameObject, ByRef squad As DotSquad)
    Public Event OnClick As GameObjectClickEventHandler ' raised when player game object is clicked; gets ref to player object clicked
    Public Event OnExternClick As GameObjectClickEventHandler ' raised when player game object is not clicked; gets ref to foreign object if possible
    Public Event OnTargeted As GameObjectTargetedEventHandler ' raised when the game object is targeted by another object

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
        BRUSHES(0) = New DrawBrush(surface, New SlimDX.Color4(Color.Black)) ' BLACK
        BRUSHES(1) = New DrawBrush(surface, New SlimDX.Color4(Color.Red)) ' RED
        BRUSHES(2) = New DrawBrush(surface, New SlimDX.Color4(Color.Blue)) ' BLUE
        BRUSHES(3) = New DrawBrush(surface, New SlimDX.Color4(Color.Green)) ' GREEN
        BRUSHES(4) = New DrawBrush(surface, New SlimDX.Color4(Color.Yellow)) ' YELLOW

        BLACK_BRUSH = BRUSHES(0)
        RED_BRUSH = BRUSHES(1)
        BLUE_BRUSH = BRUSHES(2)
        GREEN_BRUSH = BRUSHES(3)

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
                If obj IsNot Nothing Then rendering.Add(obj)
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
                If obj IsNot Nothing AndAlso (playerTeam = obj.team OrElse obj.team = -1) Then clicking.Add(obj)
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

    Private Shared Sub GameObjects_UpdateThread()
        ' we handle the game logic here
        Dim timeout = 50
        Dim ticks As UInt64 = 0
        Dim has_time_elapsed = Function(seconds As Integer) _
                                  ticks * timeout / 1000 Mod seconds = 0

        While updateThreadCond
            For Each obj In gameObjects
                ' note: dead objects do not get their update actions invoked
                If obj IsNot Nothing AndAlso Not obj.IsDead() Then
                    obj.UpdateObject() ' let each object update itself in some way
                    If obj.weapon IsNot Nothing Then obj.weapon.Update(gameObjects, playerTeam) ' update object's weapon
                End If
            Next

            ' every second, update the dead objects' decompose counter
            If has_time_elapsed(1) Then
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

            ' update ticks; if, God forbid, we every exceed 2^64, we need to reset to avoid throwing an exception
            ticks = If(ticks + 1 = UInt64.MaxValue, 0UL, ticks + 1UL)

            Thread.Sleep(timeout)
        End While
    End Sub

    Function IsDead() As Boolean
        Return health <= 0
    End Function

    Sub Render(ByVal surface As DrawSurface)
        RenderObject(surface)

        ' draw red X of death if dead
        If IsDead() Then
            Dim localRect As New DotRectangle(bounds.location.GetRelativeLocation(), bounds.size)
            surface.DrawLine(RED_BRUSH, localRect.TopLeftCorner().ToPoint(), localRect.BotRightCorner().ToPoint())
            surface.DrawLine(RED_BRUSH, localRect.TopRightCorner().ToPoint(), localRect.BotLeftCorner().ToPoint())
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
            For Each obj In gameObjects
                If obj IsNot Nothing AndAlso obj.team <> playerTeam AndAlso location.IsWithin(obj.bounds) Then
                    o = obj
                    Exit For
                End If
            Next
            ' raise the OnExternClick event; leave the location alone
            RaiseEvent OnExternClick(o, kind, location)
        End If
    End Sub

    Sub Arm(ByVal weaponType As Type, ByVal weaponInfo As DotWeaponInfo)
        ' create a weapon of the specified type using the specified info
        weapon = CType(Activator.CreateInstance(weaponType, weaponInfo), DotWeapon)
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
        Me.health -= amt
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
End Class

' Represents an unrendered object that is used to hook into the object system
Class DummyObject
    Inherits GameObject

    Public Event OnUpdateHook As EventHandler

    Sub New()
        MyBase.New(-1)
        health = 1
    End Sub

    Protected Overrides Sub RenderObject(surface As SlimDX.Direct2D.RenderTarget)
        Exit Sub
    End Sub

    Protected Overrides Sub UpdateObject()
        RaiseEvent OnUpdateHook(Me, New EventArgs)
    End Sub
End Class