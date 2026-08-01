Imports System.IO
Imports Org.BouncyCastle.Bcpg.OpenPgp

Class PassphraseDialog
    Private ReadOnly _privateKey As String

    Public Sub New(privateKey As String)
        InitializeComponent()
        _privateKey = privateKey
    End Sub

    Public ReadOnly Property Passphrase As String
        Get
            Return PwdInput.Password
        End Get
    End Property

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        PwdInput.Focus()
    End Sub

    Private Sub BtnConfirm_Click(sender As Object, e As RoutedEventArgs)
        BtnConfirm.IsEnabled = False
        TxtError.Visibility = Visibility.Collapsed

        If IsPassphraseValid(_privateKey, PwdInput.Password) Then
            DialogResult = True
        Else
            TxtError.Text = "Incorrect passphrase. Please try again."
            TxtError.Visibility = Visibility.Visible
            BtnConfirm.IsEnabled = True
            PwdInput.Focus()
            PwdInput.SelectAll()
        End If
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub

    Private Sub PwdInput_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Enter Then BtnConfirm_Click(sender, e)
        If e.Key = Key.Escape Then BtnCancel_Click(sender, e)
    End Sub

    Private Shared Function IsPassphraseValid(privateKeyArmored As String, passphrase As String) As Boolean
        Try
            Dim data = System.Text.Encoding.UTF8.GetBytes(privateKeyArmored)
            Using ms As New MemoryStream(data)
                Dim decoderStream = PgpUtilities.GetDecoderStream(ms)
                Dim bundle As New PgpSecretKeyRingBundle(decoderStream)
                For Each ring As PgpSecretKeyRing In bundle.GetKeyRings()
                    For Each key As PgpSecretKey In ring.GetSecretKeys()
                        Dim pk = key.ExtractPrivateKey(passphrase.ToCharArray())
                        Return pk IsNot Nothing
                    Next
                Next
            End Using
            Return False
        Catch
            Return False
        End Try
    End Function
End Class
