Option Strict On
Option Explicit On

Imports System
Imports System.Collections.ObjectModel
Imports DrawSurface = SlimDX.Direct2D.RenderTarget
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

' Represents a weapon's basic configuration; use a reference type
' so that settings can be updated across multiple GameObject instances
MustInherit Class DotWeaponInfo
    Private damagev As Integer ' how much damage does the weapon warhead do?
    Private speedv As Double   ' how fast does a warhead travel?
    Private rangev As Integer  ' how far does the warhead travel? (pixels)
    Private ratev As Integer   ' how often does the weapon fire?
    Private clipv As Integer   ' how many warheads can the weapon fire in sequence?
    Private reloadv As Integer ' how long does it take the weapon to reload?

    Property Damage As Integer
        Get
            Return damagev
        End Get
        Set(value As Integer)
            damagev = value
        End Set
    End Property

    Property Speed As Double
        Get
            Return speedv
        End Get
        Set(value As Double)
            speedv = value
        End Set
    End Property

    Property Range As Integer
        Get
            Return rangev
        End Get
        Set(value As Integer)
            rangev = value
        End Set
    End Property

    Property FireRate As Integer
        Get
            Return ratev
        End Get
        Set(value As Integer)
            ratev = value
        End Set
    End Property

    Property Clip As Integer
        Get
            Return clipv
        End Get
        Set(value As Integer)
            clipv = value
        End Set
    End Property

    Property ReloadRate As Integer
        Get
            Return reloadv
        End Get
        Set(value As Integer)
            reloadv = value
        End Set
    End Property
End Class

' An abstract base class that represents a game object's weapon; handles
' its rendering and its updating
MustInherit Class DotWeapon
    Public Delegate Sub DotWeaponEventHandler(ByVal target As GameObject)

    Public Event OnInRange As EventHandler
    Public Event OnOutOfRange As DotWeaponEventHandler
    Public Event OnTargetKilled As DotWeaponEventHandler

    Private ReadOnly info As DotWeaponInfo
    Private warheads As New Collection(Of DotWarhead) ' warheads fired from the weapon
    Private mutex As New Object

    Private shooter As GameObject
    Private target As GameObject
    Private clip As Integer
    Private iters As UInt32
    Private inrange As Boolean

    Sub New(ByVal winfo As DotWeaponInfo)
        info = winfo
    End Sub

    Sub FireOn(ByVal attacker As GameObject, ByVal targetObject As GameObject)
        If target Is Nothing Then ' if not active
            iters = 0UI
            clip = info.Clip
            shooter = attacker
            target = targetObject
            inrange = False ' assume not in range to start off
        End If
    End Sub

    Sub CeaseFire()
        target = Nothing
        shooter = Nothing
    End Sub

    Sub Render(ByVal surface As DrawSurface, ByVal brush As DrawBrush)
        Dim iter = 0
        Dim heads(0 To warheads.Count - 1) As DotWarhead
        SyncLock mutex
            For Each whead In warheads
                If whead IsNot Nothing Then
                    heads(iter) = whead
                    iter += 1
                End If
            Next
        End SyncLock

        For i = 0 To iter - 1
            RenderWarhead(surface, brush, heads(i))
        Next
    End Sub

    Sub Update(ByVal objects As Collection(Of GameObject), ByVal team As Integer)
        ' update the warheads (this needs to happen even if the weapon is de-activated)
        UpdateWarheads()
        ApplyWarheads(objects, team)

        If IsActive Then
            ' stop the weapon if the target has died; if so, then we are done here
            If target.IsDead() Then
                Dim tmp = shooter
                CeaseFire() ' do this first
                ' inform an event handling context that the shooter killed its target
                RaiseEvent OnTargetKilled(tmp)
                ' if an object is out-of-range then it may be traveling to meet the enemy (which died);
                ' so tell it to stop by saying it is in range
                If Not inrange Then RaiseEvent OnInRange(Me, New EventArgs)
                Exit Sub
            End If

            ' check fire and reload
            If clip > 0 Then
                If IsInRange() AndAlso iters Mod info.FireRate = 0 Then
                    ' fire
                    FireNewWarhead()
                    clip -= 1
                End If
                If clip = 0 Then
                    ' restart counter to get correct reload duration
                    iters = 1UI
                Else
                    iters += 1UI
                End If
            Else
                If iters Mod info.ReloadRate = 0 Then
                    ' reload
                    clip = info.Clip
                    iters = 0UI
                End If
                iters += 1UI
            End If
        End If
    End Sub

    ReadOnly Property IsActive As Boolean
        Get
            Return target IsNot Nothing
        End Get
    End Property

    ReadOnly Property IsFiring As Boolean
        Get
            IsFiring = False

            For Each whead In warheads
                If whead IsNot Nothing Then
                    IsFiring = True
                    Exit Property
                End If
            Next
        End Get
    End Property

    Protected MustOverride Sub RenderWarhead(ByVal surface As DrawSurface, ByVal brush As DrawBrush, ByVal warhead As DotWarhead)

    Protected Class DotWarhead
        Private src As DotLocation
        Private loc As DotLocation
        Private dur As Integer
        Private veloc As DotVector
        Private defunct As Boolean = False

        Sub New(ByVal weapon As DotWeapon, ByVal source As DotLocation, ByVal dest As DotLocation)
            loc = source
            veloc = New DotVector(dest.px - source.px, dest.py - source.py)
            veloc.SetMagnitude(weapon.info.Speed)
            dur = If(weapon.info.Speed = 0.0, 1, CInt(Math.Round(weapon.info.Range / weapon.info.Speed)))
        End Sub

        Sub Update()
            ' apply the velocity vector to the warhead's position
            loc.px += CInt(Math.Round(veloc.cx))
            loc.py += CInt(Math.Round(veloc.cy))

            ' check <0 so that Range=0 is valid for melee warheads
            If dur < 0 Then
                ' the warhead expired
                defunct = True
            End If
            dur -= 1
        End Sub

        Sub Apply(ByVal weapon As DotWeapon, ByVal enemy As GameObject, Optional ByVal shooter As GameObject = Nothing)
            ' see if the warhead overlaps the enemy; if so, attack the enemy and become
            ' defunct (the warhead gets destroyed when it hits its target)
            If loc.IsWithin(enemy.BoundingRectangle) OrElse (shooter IsNot Nothing _
                    AndAlso weapon.shooter.BoundingRectangle().Overlaps(enemy.BoundingRectangle)) Then
                enemy.DealDamage(weapon.info.Damage)
                defunct = True
            End If
        End Sub

        ReadOnly Property Source As DotLocation
            Get
                Return src
            End Get
        End Property

        ReadOnly Property Location As DotLocation
            Get
                Return loc
            End Get
        End Property

        ReadOnly Property IsDefunct As Boolean
            Get
                Return defunct
            End Get
        End Property

        ReadOnly Property Vector As DotVector
            Get
                Return veloc
            End Get
        End Property
    End Class

    Private Sub UpdateWarheads()
        ' check for defunct warheads and remove them
        SyncLock mutex ' since we modify the list, this is a critical region
            Dim iter = 0
            Do Until iter = warheads.Count
                If warheads(iter) IsNot Nothing AndAlso warheads(iter).IsDefunct Then
                    warheads(iter) = Nothing
                End If
                iter += 1
            Loop
        End SyncLock

        ' call update on each active warhead
        For Each whead In warheads
            If whead IsNot Nothing AndAlso Not whead.IsDefunct Then
                whead.Update()
            End If
        Next
    End Sub

    Private Sub ApplyWarheads(ByVal objects As Collection(Of GameObject), ByVal team As Integer)
        ' check to see if the warheads hit any of the enemies (objects not marked on team 'team')
        For Each obj In objects
            If obj IsNot Nothing AndAlso Not obj.IsDead AndAlso obj.Team <> team Then
                For Each whead In warheads
                    If whead IsNot Nothing AndAlso Not whead.IsDefunct Then _
                        whead.Apply(Me, obj, If(info.Range = 0, shooter, Nothing))
                Next
            End If
        Next
    End Sub

    Private Function IsInRange() As Boolean
        ' We assume that this is called within a context that has checked
        ' whether this weapon 'IsActive'
        IsInRange = False

        ' check to see if we are in range
        If info.Range = 0 Then
            ' range 0 demands an overlap between the two objects
            If Not target.Location.IsWithin(shooter.BoundingRectangle) Then
                inrange = False
                RaiseEvent OnOutOfRange(target) ' we need to send these repeatedly
                Exit Function
            End If
        Else
            ' otherwise we compute the distance to check if in range
            Dim s, d As DotLocation
            Dim distance As DotVector
            s = shooter.Location ' cache these
            d = target.Location
            distance = New DotVector(d.px - s.px, d.py - s.py)
            If CInt(distance.GetMagnitude()) > info.Range Then
                inrange = False
                RaiseEvent OnOutOfRange(target) ' we need to send these repeatedly
                Exit Function
            End If
        End If

        If Not inrange Then RaiseEvent OnInRange(Me, New EventArgs)
        inrange = True

        Return True
    End Function

    Private Sub FireNewWarhead()
        ' We assume that this is called within a context that has checked
        ' whether this weapon 'IsActive'

        ' find a space to assign new warhead object
        Dim iter As Integer
        Dim whead As DotWarhead
        iter = 0
        While iter < warheads.Count
            If warheads(iter) Is Nothing Then
                Exit While
            End If

            iter += 1
        End While

        ' create the warhead
        whead = New DotWarhead(Me, shooter.Location, target.Location)

        SyncLock mutex
            If iter >= warheads.Count Then
                warheads.Add(whead)
            Else
                warheads(iter) = whead
            End If
        End SyncLock
    End Sub
End Class

Class DotMeleeWeapon
    Inherits DotWeapon

    Sub New(ByVal winfo As DotWeaponInfo)
        MyBase.New(winfo)
    End Sub

    Protected Overrides Sub RenderWarhead(ByVal surface As SlimDX.Direct2D.RenderTarget, ByVal brush As SlimDX.Direct2D.SolidColorBrush, ByVal warhead As DotWeapon.DotWarhead)
        ' this kind of weapon is not rendered
        Exit Sub
    End Sub
End Class

Class DotDashWeapon
    Inherits DotWeapon

    Sub New(ByVal winfo As DotWeaponInfo)
        MyBase.New(winfo)
    End Sub

    Protected Overrides Sub RenderWarhead(ByVal surface As SlimDX.Direct2D.RenderTarget, ByVal brush As SlimDX.Direct2D.SolidColorBrush, ByVal warhead As DotWeapon.DotWarhead)
        Dim v As New DotVector(warhead.Vector.GetDirection(), 10.0, False)
        Dim a, b As DotLocation

        a = warhead.Location + v.ToLocation()
        b = a + (-v).ToLocation()

        surface.DrawLine(brush, a.GetRelativeLocation().ToPoint(), b.GetRelativeLocation().ToPoint(), 1.5)
    End Sub
End Class

Class DotStarWeapon
    Inherits DotWeapon

    Sub New(ByVal winfo As DotWeaponInfo)
        MyBase.New(winfo)
    End Sub

    Protected Overrides Sub RenderWarhead(surface As SlimDX.Direct2D.RenderTarget, brush As SlimDX.Direct2D.SolidColorBrush, warhead As DotWeapon.DotWarhead)
        Dim dir = warhead.Vector.GetDirection()
        Dim vs(1) As DotVector
        vs(0) = New DotVector(dir, 5.0, False)
        vs(1) = New DotVector(dir + Math.PI / 4.0, 5.0, False)

        For i = 0 To 1
            For j = 0 To 1
                Dim a, b As DotLocation
                a = warhead.Location + vs(i).ToLocation()
                b = warhead.Location + (-vs(i)).ToLocation()
                surface.DrawLine(brush, a.GetRelativeLocation().ToPoint(), b.GetRelativeLocation().ToPoint())
                vs(i) = vs(i).GetOrthogonal()
            Next
        Next
    End Sub
End Class

' DotWeaponInfo subclasses
Class DotMeleeWeaponInfo
    Inherits DotWeaponInfo

    Sub New()
        Clip = 1
        ReloadRate = 5
        FireRate = 1
        Damage = 1
        Range = 0
    End Sub
End Class

Class DotCalvaryMeleeWeaponInfo
    Inherits DotWeaponInfo

    Sub New()
        Clip = 1
        ReloadRate = 2
        FireRate = 1
        Damage = 10
        Range = 0
    End Sub
End Class

Class DotRangedWeaponInfoC
    Inherits DotWeaponInfo

    Sub New()
        Clip = 5
        ReloadRate = 40
        FireRate = 20
        Damage = 5
        Range = 750
        Speed = 15.5
    End Sub
End Class