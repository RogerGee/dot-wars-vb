Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

Structure DotStructureInfo
    Dim team As Integer
    Dim rect As DotRectangle
    Dim initialHealth As Integer
    Dim weaponType As Type ' if Nothing, then the structure will have no weapon
    Dim weaponInfo As DotWeaponInfo
End Structure

' Represents the base-class for any in-game structure object; each structure is drawn as a rectangle and generates
' an 'OnGenerate' event to handle the structure's main operation (e.g. generate units); each structure is equipped
' with a weapon (which may be Nothing)
MustInherit Class DotStructure
    Inherits GameObject

    Protected Delegate Sub DotStructureEventHandler(ByVal struct As DotStructure, ByVal obj As GameObject)
    Protected Event OnGenerate As DotStructureEventHandler

    Private maxHealth As Integer
    Private sel As Boolean = False
    Private waypoint As DotLocation = DotLocation.InvalidLocation
    Shared selcounter As Integer = 0 ' used to determine if a structure is selected (should rest at 1 or 0)
    Private tsquad As DotSquad = Nothing ' hold reference to targeted squad (structure may target other

    Sub New(ByVal info As DotStructureInfo)
        MyBase.New(info.team)
        bounds = info.rect
        health = info.initialHealth
        maxHealth = info.initialHealth
        If info.weaponType IsNot Nothing Then
            ' arm the structure if specified
            Arm(info.weaponType, info.weaponInfo)
        End If
    End Sub

    ReadOnly Property Selected As Boolean
        Get
            Return sel AndAlso Not IsDead()
        End Get
    End Property

    ReadOnly Property RallyPoint As DotLocation
        Get
            Return waypoint
        End Get
    End Property

    ' each derived structure can paint itself in some unique way
    Protected MustOverride Sub RenderObject_derived(ByVal surface As DrawSurface, ByVal br As DrawBrush)

    ' each derived structure can update itself in some unique way
    Protected MustOverride Sub UpdateObject_derived()

    Protected Overrides Sub RenderObject(ByVal surface As DrawSurface, ByVal br As DrawBrush)
        ' draw the base rectangle
        Dim rect As Rectangle
        rect.Location = bounds.location.GetRelativeLocation().ToPoint()
        rect.Size = bounds.size

        If Selected Then
            Dim clr As SlimDX.Color4
            BrushAlpha(br, 0.5! * (CSng(health) / maxHealth), clr)
            surface.FillRectangle(br, rect)
            br.Color = clr
        End If

        ' render the waypoint (it's a cross)
        If waypoint <> DotLocation.InvalidLocation AndAlso Selected Then
            Dim rel = waypoint.GetRelativeLocation().ToPoint()
            Dim pnts = New Point() {rel, rel, rel, rel}
            pnts(0).X -= 10
            pnts(1).X += 10
            pnts(2).Y -= 10
            pnts(3).Y += 10
            surface.DrawLine(br, pnts(0), pnts(1), 1.5!)
            surface.DrawLine(br, pnts(2), pnts(3), 1.5!)
        End If

        ' render the base rectangle
        surface.DrawRectangle(br, rect)

        ' call derived implementation
        RenderObject_derived(surface, br)
    End Sub

    Protected Overrides Sub UpdateObject()
        If Not weapon.IsActive Then
            Dim obj As GameObject
            obj = GetNearObject(weapon.Range)
            If obj IsNot Nothing Then AttackObject(obj)
        End If
        UpdateObject_derived()
    End Sub

    Private Sub DotStructure_OnClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation) Handles Me.OnClick
        If DotSquad.IsSquadSelected Then Exit Sub

        If kind = DotClickKind.Click1 Then
            sel = True
            selcounter += 1
        End If
    End Sub

    Private Sub DotStructure_OnExternClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation) Handles Me.OnExternClick

        If sel Then
            If kind = DotClickKind.Click1 Then
                sel = False
                selcounter -= 1
            ElseIf kind = DotClickKind.Click2 Then
                If Me.weapon IsNot Nothing AndAlso gobj IsNot Nothing Then
                    ' target the specified game object; first see if it is a dot squad; if so,
                    ' set target to fire on squad
                    gobj.Target(tsquad) ' see if object belongs to dot squad
                    Me.AttackObject(gobj)
                Else
                    RaiseEvent OnGenerate(Me, gobj)
                End If
            End If
        End If
    End Sub

    Private Sub OnOutOfRange(ByVal target As GameObject) Handles weapon.OnOutOfRange
        ' if we are out of range then cease fire (the building cannot move)
        weapon.CeaseFire()
    End Sub

    Private Sub OnTargetKilled(ByVal myself As GameObject) Handles weapon.OnTargetKilled
        If tsquad IsNot Nothing Then
            If tsquad.IsDead() Then
                tsquad = Nothing
                Exit Sub
            End If

            ' choose a new random unit to attack in the squad
            Dim enemies = tsquad.Units
            Dim n = enemies.Length
            Do
                Dim dex As Integer
                dex = CInt(Math.Floor(enemies.Length * Rnd()))
                If enemies(dex) IsNot Nothing AndAlso Not enemies(dex).IsDead() Then
                    Me.AttackObject(enemies(dex))
                    Exit Do
                End If
                n -= 1
                enemies(dex) = Nothing
            Loop Until n = 0
        End If
    End Sub
End Class

' make DotUnitGeneratorInfo a reference type since multiple structures will reference
' a single info structure
Class DotUnitGeneratorInfo
    Dim m_base As DotStructureInfo

    ' what does the unit generator produce?
    Dim m_unitType As Type
    Dim m_weaponType As Type
    Dim m_weaponInfo As Type

    ' how does it behave when producing them?
    Dim m_generateTime As Integer ' how long does it take to generate a squad?
    Dim m_slots As Integer ' how many slots for each squad?
    Dim m_stacked As Boolean ' do the units come out stacked?
    Dim m_cost As Integer ' how much does it cost to generate a single unit in the squad?

    Sub New(ByVal team As Integer, ByVal rect As DotRectangle, ByVal initialHealth As Integer, _
                Optional ByVal weaponType As Type = Nothing, Optional ByVal weaponInfo As DotWeaponInfo = Nothing)
        If weaponType IsNot Nothing AndAlso weaponInfo Is Nothing Then
            Throw New Exception("parameters 'weaponType' and 'weaponInfo' must both be either set or Nothing")
        End If
        m_base.team = team
        m_base.rect = rect
        m_base.initialHealth = initialHealth
        m_base.weaponType = weaponType
        m_base.weaponInfo = weaponInfo
    End Sub

    ReadOnly Property Base As DotStructureInfo
        Get
            Return m_base
        End Get
    End Property
    Property UnitType As Type
        Get
            Return m_unitType
        End Get
        Set(value As Type)
            m_unitType = value
        End Set
    End Property
    Property WeaponType As Type
        Get
            Return m_weaponType
        End Get
        Set(value As Type)
            m_weaponType = value
        End Set
    End Property
    Property WeaponInfo As Type
        Get
            Return m_weaponInfo
        End Get
        Set(value As Type)
            m_weaponInfo = value
        End Set
    End Property
    Property GenerateTime As Integer
        Get
            Return m_generateTime
        End Get
        Set(value As Integer)
            m_generateTime = value
        End Set
    End Property
    Property Slots As Integer
        Get
            Return m_slots
        End Get
        Set(value As Integer)
            m_slots = value
        End Set
    End Property
    Property Stacked As Boolean
        Get
            Return m_stacked
        End Get
        Set(value As Boolean)
            m_stacked = value
        End Set
    End Property
    Property UnitCost As Integer
        Get
            Return m_cost
        End Get
        Set(value As Integer)
            m_cost = value
        End Set
    End Property
    ReadOnly Property TotalCost As Integer
        Get
            Return m_cost * m_slots
        End Get
    End Property
End Class

Class DotUnitGenerator
    Inherits DotStructure

    Private Class GeneratorRequest
        Dim countdown As Integer

        Sub New(ByVal struct As DotUnitGenerator)
            countdown = struct.info.GenerateTime
        End Sub

        Function Done() As Boolean
            countdown -= 1
            If countdown <= 0 Then Return True
            Return False
        End Function
    End Class

    Dim info As DotUnitGeneratorInfo
    Dim load As New Queue(Of GeneratorRequest)
    Dim lock As New Object

    Sub New(ByVal info As DotUnitGeneratorInfo)
        MyBase.New(info.Base)
        Me.info = info
    End Sub

    Protected Overrides Sub RenderObject_derived(ByVal surface As SlimDX.Direct2D.RenderTarget, ByVal br As SlimDX.Direct2D.SolidColorBrush)

    End Sub

    Protected Overrides Sub UpdateObject_derived()
        Static ticks As UInt64 = 1UL
        Dim hd As GeneratorRequest = Nothing

        SyncLock lock
            If load.Count > 0 Then hd = load.Peek()
        End SyncLock

        If hd IsNot Nothing AndAlso has_time_elapsed(ticks, 1) AndAlso hd.Done() Then
            Dim newSquad = New DotSquad(info.UnitType, info.Slots, Me.Team)
            newSquad.StackUpAt(Me.Location)
            newSquad.SendTo(Me.Location + Me.BoundingRectangle.size)
            If info.WeaponType IsNot Nothing Then newSquad.Arm(info.WeaponType, info.WeaponInfo)
            SyncLock lock
                load.Dequeue()
            End SyncLock
        End If

        ' update ticks: avoid throwing exception on MaxValue
        ticks = If(ticks + 1UL = UInt64.MaxValue, 0UL, ticks + 1UL)
    End Sub

    Private Sub GenerateUnits(ByVal myself As DotStructure, ByVal obj As GameObject) Handles Me.OnGenerate
        If info.UnitType Is Nothing Then Exit Sub

        ' check money levels for faction

        ' add a new squad request to the production queue
        SyncLock lock
            load.Enqueue(New GeneratorRequest(Me))
        End SyncLock
    End Sub
End Class
