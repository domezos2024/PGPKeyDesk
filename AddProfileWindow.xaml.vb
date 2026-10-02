Imports PGPKeyDesk.Models
Imports PGPKeyDesk.Services
Imports Microsoft.Win32
Imports System.Windows.Media

Class AddProfileWindow
    Public Property SavedProfile As PGPProfile

    Private Sub TxtName_TextChanged(sender As Object, e As TextChangedEventArgs)
        HintName.Visibility = If(String.IsNullOrEmpty(TxtName.Text), Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub TxtPrivateKey_TextChanged(sender As Object, e As TextChangedEventArgs)
        HintPrivateKey.Visibility = If(String.IsNullOrEmpty(TxtPrivateKey.Text), Visibility.Visible, Visibility.Collapsed)
        ValidateAndShowPrivateKey()
    End Sub

    Private Sub TxtPublicKey_TextChanged(sender As Object, e As TextChangedEventArgs)
        HintPublicKey.Visibility = If(String.IsNullOrEmpty(TxtPublicKey.Text), Visibility.Visible, Visibility.Collapsed)
        ValidateAndShowPublicKey()
    End Sub

    Private Sub ValidateAndShowPrivateKey()
        Dim text = TxtPrivateKey.Text.Trim()
        If Not text.Contains("-----BEGIN PGP") Then
            TxtPrivateKeyStatus.Visibility = Visibility.Collapsed
            Return
        End If

        Dim r = KeyValidator.ValidatePrivateKey(text)
        ShowStatus(TxtPrivateKeyStatus, r)
    End Sub

    Private Sub ValidateAndShowPublicKey()
        Dim text = TxtPublicKey.Text.Trim()
        If Not text.Contains("-----BEGIN PGP") Then
            TxtPublicKeyStatus.Visibility = Visibility.Collapsed
            Return
        End If

        Dim r = KeyValidator.ValidatePublicKey(text)
        ShowStatus(TxtPublicKeyStatus, r)
    End Sub

    Private Shared Sub ShowStatus(label As System.Windows.Controls.TextBlock, r As KeyValidationResult)
        label.Visibility = Visibility.Visible
        If r.IsValid Then
            label.Text = ChrW(&H2714) & "  " & r.KeyInfo
            label.Foreground = UiBrushes.FromHex("#A6E3A1")
        Else
            label.Text = ChrW(&H2718) & "  " & r.ErrorMessage
            label.Foreground = UiBrushes.FromHex("#F38BA8")
        End If
    End Sub

    Private Sub BrowsePrivateKey_Click(sender As Object, e As RoutedEventArgs)
        Dim content = BrowseAscFile("Load Private Key")
        If content IsNot Nothing Then TxtPrivateKey.Text = content
    End Sub

    Private Sub BrowsePublicKey_Click(sender As Object, e As RoutedEventArgs)
        Dim content = BrowseAscFile("Load Public Key")
        If content IsNot Nothing Then TxtPublicKey.Text = content
    End Sub

    Private Function BrowseAscFile(title As String) As String
        Dim dlg As New OpenFileDialog() With {
            .Title = title,
            .Filter = "PGP Key Files (*.asc;*.pgp;*.gpg;*.txt)|*.asc;*.pgp;*.gpg;*.txt|All Files (*.*)|*.*",
            .CheckFileExists = True
        }
        If dlg.ShowDialog() = True Then Return IO.File.ReadAllText(dlg.FileName).Trim()
        Return Nothing
    End Function

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        Dim name As String = TxtName.Text.Trim()
        Dim privateKey As String = TxtPrivateKey.Text.Trim()
        Dim publicKey As String = TxtPublicKey.Text.Trim()

        If String.IsNullOrEmpty(name) Then
            MessageBox.Show("Please enter a profile name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
            TxtName.Focus()
            Return
        End If

        Dim privResult = KeyValidator.ValidatePrivateKey(privateKey)
        If Not privResult.IsValid Then
            MessageBox.Show("Invalid private key:" & Environment.NewLine & privResult.ErrorMessage,
                            "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
            TxtPrivateKey.Focus()
            Return
        End If

        If Not String.IsNullOrEmpty(publicKey) Then
            Dim pubResult = KeyValidator.ValidatePublicKey(publicKey)
            If Not pubResult.IsValid Then
                MessageBox.Show("Invalid public key:" & Environment.NewLine & pubResult.ErrorMessage,
                                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
                TxtPublicKey.Focus()
                Return
            End If
        End If

        SavedProfile = New PGPProfile With {
            .Name = name,
            .PrivateKey = privateKey,
            .PublicKey = publicKey
        }
        DialogResult = True
        Close()
    End Sub

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class
