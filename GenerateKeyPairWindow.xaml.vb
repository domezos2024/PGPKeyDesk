Imports PGPKeyDesk.Models
Imports PgpCore
Imports System.IO
Imports System.Text
Imports Microsoft.Win32

Class GenerateKeyPairWindow
    Public Property SavedProfile As PGPProfile

    Private _generatedPublicKey As String = String.Empty
    Private _generatedPrivateKey As String = String.Empty

    Private Sub TxtProfileName_Changed(sender As Object, e As TextChangedEventArgs)
        HintProfileName.Visibility = If(String.IsNullOrEmpty(TxtProfileName.Text), Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub TxtIdentity_Changed(sender As Object, e As TextChangedEventArgs)
        HintIdentity.Visibility = If(String.IsNullOrEmpty(TxtIdentity.Text), Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Async Sub BtnGenerate_Click(sender As Object, e As RoutedEventArgs)
        Dim profileName = TxtProfileName.Text.Trim()
        Dim identity = TxtIdentity.Text.Trim()
        Dim passphrase = PwdPassphrase.Password
        Dim confirm = PwdConfirm.Password

        If String.IsNullOrEmpty(profileName) Then
            MessageBox.Show("Please enter a profile name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
            TxtProfileName.Focus()
            Return
        End If

        If String.IsNullOrEmpty(identity) Then
            MessageBox.Show("Please enter a name or email address as the key identity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
            TxtIdentity.Focus()
            Return
        End If

        If String.IsNullOrEmpty(passphrase) Then
            MessageBox.Show("Please enter a passphrase.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
            PwdPassphrase.Focus()
            Return
        End If

        If passphrase <> confirm Then
            MessageBox.Show("The two passphrases do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning)
            PwdConfirm.Focus()
            Return
        End If

        Dim strength As Integer = If(Rb4096.IsChecked = True, 4096, 2048)

        BtnGenerate.IsEnabled = False
        BtnGenerate.Content = "Generating key pair..."
        ResultPanel.Visibility = Visibility.Collapsed
        BtnSave.IsEnabled = False

        Try
            Dim pgp As New PGP()
            Using pubStream As New MemoryStream()
                Using privStream As New MemoryStream()
                    Await pgp.GenerateKeyAsync(pubStream, privStream, identity, passphrase, strength)
                    _generatedPublicKey = Encoding.UTF8.GetString(pubStream.ToArray())
                    _generatedPrivateKey = Encoding.UTF8.GetString(privStream.ToArray())
                End Using
            End Using

            TxtPublicKeyResult.Text = _generatedPublicKey
            TxtPrivateKeyResult.Text = _generatedPrivateKey
            ResultPanel.Visibility = Visibility.Visible
            BtnSave.IsEnabled = True
            BtnGenerate.Content = "&#x2699;  Generate Again"
        Catch ex As Exception
            MessageBox.Show("Error generating key pair:" & Environment.NewLine & ex.Message,
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error)
            BtnGenerate.Content = "&#x2699;  Generate Key Pair"
        Finally
            BtnGenerate.IsEnabled = True
        End Try
    End Sub

    Private Sub BtnCopyPublicKey_Click(sender As Object, e As RoutedEventArgs)
        If Not String.IsNullOrEmpty(_generatedPublicKey) Then
            Clipboard.SetText(_generatedPublicKey)
        End If
    End Sub

    Private Sub BtnCopyPrivateKey_Click(sender As Object, e As RoutedEventArgs)
        If Not String.IsNullOrEmpty(_generatedPrivateKey) Then
            Clipboard.SetText(_generatedPrivateKey)
        End If
    End Sub

    Private Sub BtnExportPublicKey_Click(sender As Object, e As RoutedEventArgs)
        ExportKeyToFile(_generatedPublicKey, "Export Public Key", "public_key.asc")
    End Sub

    Private Sub BtnExportPrivateKey_Click(sender As Object, e As RoutedEventArgs)
        ExportKeyToFile(_generatedPrivateKey, "Export Private Key", "private_key.asc")
    End Sub

    Private Sub ExportKeyToFile(keyData As String, title As String, defaultName As String)
        If String.IsNullOrEmpty(keyData) Then Return
        Dim dlg As New SaveFileDialog() With {
            .Title = title,
            .FileName = defaultName,
            .Filter = "PGP Key Files (*.asc)|*.asc|All Files (*.*)|*.*"
        }
        If dlg.ShowDialog() = True Then
            IO.File.WriteAllText(dlg.FileName, keyData, Encoding.UTF8)
        End If
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        SavedProfile = New PGPProfile With {
            .Name = TxtProfileName.Text.Trim(),
            .PrivateKey = _generatedPrivateKey,
            .PublicKey = _generatedPublicKey
        }
        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class
