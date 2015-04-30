Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

Structure DotStructureInfo
    Dim team As Integer
    Dim rect As DotRectangle
    Dim initialHealth As Integer
    Dim weaponType As Type
    Dim weaponInfo As DotWeaponInfo
End Structure

MustInherit Class DotStructure
    Inherits GameObject

    Private sel As Boolean = False

    Sub New(ByVal info As DotStructureInfo)
        MyBase.New(info.team)
        bounds = info.rect
        health = info.initialHealth
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

    Protected Overridable Sub RenderObject_derived(ByVal surface As DrawSurface, ByVal br As DrawBrush)
        ' do nothing unless overrided
        Exit Sub
    End Sub

    Protected Overrides Sub RenderObject(ByVal surface As DrawSurface, ByVal br As DrawBrush)
        ' draw the base rectangle
        Dim rect As Rectangle
        rect.Location = bounds.location.GetRelativeLocation().ToPoint()
        rect.Size = bounds.size

        If Selected Then
            Dim clr As SlimDX.Color4
            BrushAlpha(br, 0.5! * (CSng(health) / 1), clr)
            surface.FillRectangle(br, rect)
            br.Color = clr
        End If

        ' call derived implementation
        RenderObject_derived(surface, br)
    End Sub

    Protected Overridable Sub UpdateObject_derived()

    End Sub

    Protected Overrides Sub UpdateObject()

    End Sub
End Class

Structure DotUnitGeneratorInfo
    Dim base As DotStructureInfo

    ' what does the unit generator produce?
    Dim unitType As Type
    Dim weaponType As Type

    ' how does it behave when producing them?
    Dim generateTime As Integer ' how long does it take to generate a squad?
    Dim slots As Integer ' how many slots for each squad?
    Dim stacked As Boolean ' do the units come out stacked?
End Structure

Class DotUnitGenerator
    Inherits DotStructure

    Private unitType As Type
    Private weaponType As Type

    Sub New(ByVal info As DotUnitGeneratorInfo)
        MyBase.New(info.base)
        unitType = info.unitType
        weaponType = info.weaponType
    End Sub
End Class
