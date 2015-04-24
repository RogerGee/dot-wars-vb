Option Strict On

Imports System
Imports System.Windows.Forms

Class StartForm
    Private Shared teamColors As String() = New String() {"Blue", "Green", "Yellow", "Orange", "Purple", "Pink", "Gray"}
    Private selections() As Integer

    Public ReadOnly Property DotWarsSelections As Integer()
        Get
            Return Me.selections
        End Get
    End Property

    Private Sub btnBegin_Click(sender As Object, e As EventArgs) Handles btnBegin.Click
        If cboxPlayerTeam.SelectedIndex = -1 Then
            ErrorMessage("Select team for player")
            Exit Sub
        End If
        If cboxNPC1.SelectedIndex = -1 Then
            ErrorMessage("Select team for computer player 1")
            Exit Sub
        End If

        If cboxPlayerTeam.SelectedIndex = cboxNPC1.SelectedIndex Then
            ErrorMessage("Player team cannot be same as computer team")
            Exit Sub
        End If

        ' cache the selections before the controls are destroyed
        ReDim selections(1)
        selections(0) = cboxPlayerTeam.SelectedIndex
        selections(1) = cboxNPC1.SelectedIndex

        Me.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        Me.DialogResult = Windows.Forms.DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub StartForm_Load(sender As Object, e As EventArgs) Handles Me.Load
        cboxNPC1.Items.AddRange(teamColors)
        cboxPlayerTeam.Items.AddRange(teamColors)
    End Sub

    Private Sub ErrorMessage(ByVal text As String)
        MessageBox.Show(text, "DotWars - error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
    End Sub
End Class