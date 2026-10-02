Imports System.IO
Imports Microsoft.Win32
Imports PGPKeyDesk.Models
Imports PGPKeyDesk.Services

Class FilesView
    Private Const MaxFileBytes As Long = 200L * 1024 * 1024

    Private _profile As PGPProfile
    Private _recipient As RecipientKey
    Private _sender As RecipientKey

    ''' <summary>Active profile (private key for decrypt/sign), set by the main window.</summary>
    Public Property Profile As PGPProfile
        Get
            Return _profile
        End Get
        Set(value As PGPProfile)
            _profile = value
            UpdateButtons()
        End Set
    End Property

    Public Event StatusChanged(message As String, colorHex As String)

    Private Sub Browse_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New OpenFileDialog() With {.Title = "Select File", .CheckFileExists = True}
        If dlg.ShowDialog() = True Then TxtFile.Text = dlg.FileName
    End Sub

    Private Sub Inputs_Changed(sender As Object, e As RoutedEventArgs)
        UpdateButtons()
    End Sub

    Private Sub UpdateButtons()
        If BtnEncryptFile Is Nothing Then Return
        Dim hasFile = File.Exists(TxtFile.Text.Trim())
        Dim hasPriv = _profile IsNot Nothing AndAlso Not String.IsNullOrEmpty(_profile.PrivateKey)
        BtnEncryptFile.IsEnabled = hasFile AndAlso _recipient IsNot Nothing
        BtnDecryptFile.IsEnabled = hasFile AndAlso hasPriv
        BtnSignFile.IsEnabled = hasFile AndAlso hasPriv
        BtnVerifyFile.IsEnabled = hasFile AndAlso _recipient IsNot Nothing
    End Sub

    Private Sub PickRecipient_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New KeyringWindow(True) With {.Owner = Window.GetWindow(Me)}
        If dlg.ShowDialog() = True Then
            _recipient = dlg.SelectedKey
            TxtRecipient.Text = KeyringWindow.Describe(_recipient, DateTime.UtcNow)
            TxtRecipient.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#CDD6F4"), Color))
            UpdateButtons()
        End If
    End Sub

    Private Sub PickSender_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New KeyringWindow(True) With {.Owner = Window.GetWindow(Me)}
        If dlg.ShowDialog() = True Then
            _sender = dlg.SelectedKey
            TxtSender.Text = _sender.Name
            TxtSender.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#CDD6F4"), Color))
        End If
    End Sub

    Private Function ValidInput() As String
        Dim path = TxtFile.Text.Trim()
        If Not File.Exists(path) Then
            ShowResult("File not found.", "#F38BA8")
            Return Nothing
        End If
        If New FileInfo(path).Length > MaxFileBytes Then
            ShowResult("File exceeds the 200 MB limit.", "#F38BA8")
            Return Nothing
        End If
        Return path
    End Function

    Private Function AskOutput(title As String, suggested As String, input As String) As String
        Dim dlg As New SaveFileDialog() With {
            .Title = title, .FileName = suggested, .OverwritePrompt = True,
            .InitialDirectory = Path.GetDirectoryName(input)}
        If dlg.ShowDialog() <> True Then Return Nothing
        If String.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(input), StringComparison.OrdinalIgnoreCase) Then
            ShowResult("Output must differ from the input file.", "#F38BA8")
            Return Nothing
        End If
        Return dlg.FileName
    End Function

    Private Sub ShowResult(text As String, colorHex As String)
        TxtFileResult.Text = text
        TxtFileResult.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString(colorHex), Color))
        RaiseEvent StatusChanged(text, colorHex)
    End Sub

    Private Async Function RunAsync(work As Func(Of Task), busy As String) As Task
        IsEnabled = False
        ShowResult(busy, "#FAB387")
        Try
            Await work()
        Catch ex As Exception
            ShowResult("Failed: " & ex.Message, "#F38BA8")
        Finally
            PwdFile.Password = String.Empty
            IsEnabled = True
        End Try
    End Function

    Private Async Sub EncryptFile_Click(sender As Object, e As RoutedEventArgs)
        Dim input = ValidInput()
        If input Is Nothing OrElse _recipient Is Nothing Then Return

        Dim check = PgpService.CheckRecipientKey(_recipient.PublicKey, DateTime.UtcNow)
        If check.Level = KeyCheckLevel.Blocked Then
            ShowResult(check.Message, "#F38BA8")
            Return
        End If
        Dim signKey As String = Nothing
        If ChkSign.IsChecked = True Then
            If _profile Is Nothing OrElse String.IsNullOrEmpty(_profile.PrivateKey) Then
                ShowResult("Select a profile to sign with.", "#F38BA8")
                Return
            End If
            signKey = _profile.PrivateKey
        End If

        Dim armor = ChkArmor.IsChecked = True
        Dim output = AskOutput("Save encrypted file", Path.GetFileName(input) & If(armor, ".asc", ".pgp"), input)
        If output Is Nothing Then Return
        Dim pass = PwdFile.Password
        Await RunAsync(Async Function()
                           Await PgpService.EncryptFileAsync(input, output, _recipient.PublicKey, signKey, pass, armor)
                           Dim msg = If(signKey Is Nothing, "Encrypted: ", "Signed and encrypted: ") & Path.GetFileName(output)
                           If check.Level = KeyCheckLevel.Warning Then msg &= "  (" & check.Message & ")"
                           ShowResult(msg, If(check.Level = KeyCheckLevel.Warning, "#F9E2AF", "#A6E3A1"))
                       End Function, "Encrypting...")
    End Sub

    Private Async Sub DecryptFile_Click(sender As Object, e As RoutedEventArgs)
        Dim input = ValidInput()
        If input Is Nothing OrElse _profile Is Nothing Then Return

        Dim ext = Path.GetExtension(input).ToLowerInvariant()
        Dim suggested = If(ext = ".pgp" OrElse ext = ".gpg" OrElse ext = ".asc",
                           Path.GetFileNameWithoutExtension(input), Path.GetFileName(input) & ".decrypted")
        Dim output = AskOutput("Save decrypted file", suggested, input)
        If output Is Nothing Then Return
        Dim pass = PwdFile.Password
        Dim senderKey = If(_sender Is Nothing, Nothing, _sender.PublicKey)
        Await RunAsync(Async Function()
                           Dim st = Await PgpService.DecryptFileAsync(input, output, _profile.PrivateKey, pass, senderKey)
                           Select Case st
                               Case SignatureStatus.Valid
                                   ShowResult("Decrypted: " & Path.GetFileName(output) & "  ·  " & ChrW(&H2714) & " valid signature from " & _sender.Name, "#A6E3A1")
                               Case SignatureStatus.Invalid
                                   ShowResult("Decrypted: " & Path.GetFileName(output) & "  ·  " & ChrW(&H26A0) & " SIGNATURE INVALID or from a different key than " & _sender.Name & ". Do not trust this file.", "#F38BA8")
                               Case SignatureStatus.NotSigned
                                   ShowResult("Decrypted: " & Path.GetFileName(output) & "  ·  not signed, sender cannot be verified.", "#F9E2AF")
                               Case Else
                                   ShowResult("Decrypted: " & Path.GetFileName(output) & "  ·  signature not checked (no sender key selected).", "#A6E3A1")
                           End Select
                       End Function, "Decrypting...")
    End Sub

    Private Async Sub SignFile_Click(sender As Object, e As RoutedEventArgs)
        Dim input = ValidInput()
        If input Is Nothing OrElse _profile Is Nothing Then Return
        Dim armor = ChkArmor.IsChecked = True
        Dim output = AskOutput("Save signed file", Path.GetFileName(input) & If(armor, ".asc", ".sig.pgp"), input)
        If output Is Nothing Then Return
        Dim pass = PwdFile.Password
        Await RunAsync(Async Function()
                           Await PgpService.SignFileAsync(input, output, _profile.PrivateKey, pass, armor)
                           ShowResult("Signed: " & Path.GetFileName(output) & " (signature embedded; use Verify to check it).", "#A6E3A1")
                       End Function, "Signing...")
    End Sub

    Private Async Sub VerifyFile_Click(sender As Object, e As RoutedEventArgs)
        Dim input = ValidInput()
        If input Is Nothing OrElse _recipient Is Nothing Then Return
        Await RunAsync(Async Function()
                           If Await PgpService.VerifyFileAsync(input, _recipient.PublicKey) Then
                               ShowResult(ChrW(&H2714) & " Valid signature from " & _recipient.Name & ".", "#A6E3A1")
                           Else
                               ShowResult(ChrW(&H2718) & " Signature NOT valid for the selected key (wrong key, tampered or unsigned file).", "#F38BA8")
                           End If
                       End Function, "Verifying...")
    End Sub
End Class
