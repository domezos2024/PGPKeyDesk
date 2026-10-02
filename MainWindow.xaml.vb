Imports System.IO
Imports PGPKeyDesk.Models
Imports PGPKeyDesk.Services
Imports PgpCore
Imports Microsoft.Win32
Imports System.Windows.Media
Imports System.Windows.Threading

Class MainWindow
    Private _profiles As List(Of PGPProfile)
    Private _selectedProfile As PGPProfile
    Private _statusTimer As DispatcherTimer
    Private _passphraseTimer As DispatcherTimer
    Private _clipboardTimer As DispatcherTimer
    Private _senderKey As RecipientKey
    Private Const MaxKeyFileBytes As Long = 1000000L
    Private Const ClipboardClearSeconds As Integer = 60

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        Try
            _profiles = ProfileStore.Load()
        Catch ex As Exception
            _profiles = New List(Of PGPProfile)
            MessageBox.Show("Profiles could not be loaded:" & vbCrLf & ex.Message, "Profiles", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
        RefreshProfileList()

        If ProfileStore.Default.LastLoadWarning IsNot Nothing Then
            MessageBox.Show(ProfileStore.Default.LastLoadWarning, "Profile file unreadable", MessageBoxButton.OK, MessageBoxImage.Warning)
        End If
        If RecipientKeyring.Default.LastLoadWarning IsNot Nothing Then
            MessageBox.Show(RecipientKeyring.Default.LastLoadWarning, "Keyring unreadable", MessageBoxButton.OK, MessageBoxImage.Warning)
        End If
        WarnAboutExpiringKeys()
    End Sub

    ' Startup check: own profile keys and keyring recipients that are expired or expire within 30 days.
    Private Sub WarnAboutExpiringKeys()
        Dim now = DateTime.UtcNow
        Dim problems As New List(Of String)

        For Each p In _profiles
            Dim v = KeyValidator.ValidatePrivateKey(p.PrivateKey)
            If Not v.IsValid Then Continue For
            Dim st = v.GetExpiryStatus(now)
            If st = KeyExpiryStatus.Expired OrElse st = KeyExpiryStatus.ExpiringSoon Then
                problems.Add("Profile """ & p.Name & """: " & KeyExpiry.Describe(v.ExpiresAtUtc, now))
            End If
        Next
        For Each k In RecipientKeyring.Default.Keys
            Dim v = KeyValidator.ValidatePublicKey(k.PublicKey)
            If Not v.IsValid Then Continue For
            Dim st = v.GetExpiryStatus(now)
            If v.IsRevoked Then
                problems.Add("Recipient """ & k.Name & """: key REVOKED")
            ElseIf st = KeyExpiryStatus.Expired OrElse st = KeyExpiryStatus.ExpiringSoon Then
                problems.Add("Recipient """ & k.Name & """: " & KeyExpiry.Describe(v.ExpiresAtUtc, now))
            End If
        Next

        If problems.Count > 0 Then
            SetStatus(problems.Count & " key warning(s) — open the Recipient Keyring or check your profiles.", "#F9E2AF", "")
            MessageBox.Show("Key expiry warnings:" & vbCrLf & vbCrLf & String.Join(vbCrLf, problems),
                            "Key expiry", MessageBoxButton.OK, MessageBoxImage.Warning)
        End If
    End Sub

    Private Sub Window_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Return AndAlso (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control Then
            If MainTabControl.SelectedIndex = 0 AndAlso BtnDecrypt.IsEnabled Then
                Decrypt_Click(Nothing, Nothing)
                e.Handled = True
            ElseIf MainTabControl.SelectedIndex = 1 AndAlso BtnEncrypt.IsEnabled Then
                Encrypt_Click(Nothing, Nothing)
                e.Handled = True
            End If
        End If
    End Sub

    Private Sub RefreshProfileList()
        ProfileListBox.ItemsSource = Nothing
        ProfileListBox.ItemsSource = _profiles
        TxtNoProfiles.Visibility = If(_profiles.Count = 0, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub AddProfile_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New AddProfileWindow()
        dlg.Owner = Me
        If dlg.ShowDialog() = True Then
            _profiles.Add(dlg.SavedProfile)
            ProfileStore.Save(_profiles)
            RefreshProfileList()
            ProfileListBox.SelectedItem = dlg.SavedProfile
            SetStatus("Profile """ & dlg.SavedProfile.Name & """ added.", "#A6E3A1", "󰄬")
        End If
    End Sub

    Private Sub GenerateKeyPair_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New GenerateKeyPairWindow()
        dlg.Owner = Me
        If dlg.ShowDialog() = True Then
            _profiles.Add(dlg.SavedProfile)
            ProfileStore.Save(_profiles)
            RefreshProfileList()
            ProfileListBox.SelectedItem = dlg.SavedProfile
            SetStatus("Key pair """ & dlg.SavedProfile.Name & """ generated and saved.", "#A6E3A1", "")
        End If
    End Sub

    Private Sub DeleteProfile_Click(sender As Object, e As RoutedEventArgs)
        If _selectedProfile IsNot Nothing Then
            If MessageBox.Show("Are you sure you want to delete the profile """ & _selectedProfile.Name & """?", "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes Then
                _profiles.Remove(_selectedProfile)
                ProfileStore.Save(_profiles)
                _selectedProfile = Nothing
                RefreshProfileList()
                ResetDecryptUI()
                SetStatus("Profile removed.", "#F38BA8", "󰆴")
            End If
        End If
    End Sub

    Private Sub ProfileListBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        _selectedProfile = TryCast(ProfileListBox.SelectedItem, PGPProfile)

        If _selectedProfile IsNot Nothing Then
            TxtActiveProfile.Text = "Active Profile:"
            TxtBadgeName.Text = _selectedProfile.Name
            ProfileBadge.Visibility = Visibility.Visible
            BtnDeleteProfile.IsEnabled = True
            BtnCopyPublicKey.Visibility = If(String.IsNullOrEmpty(_selectedProfile.PublicKey), Visibility.Collapsed, Visibility.Visible)
            BtnUseOwnPublicKey.Visibility = If(String.IsNullOrEmpty(_selectedProfile.PublicKey), Visibility.Collapsed, Visibility.Visible)
            BtnExportPublicKey.IsEnabled = Not String.IsNullOrEmpty(_selectedProfile.PublicKey)
            BtnExportPrivateKey.IsEnabled = Not String.IsNullOrEmpty(_selectedProfile.PrivateKey)
        Else
            TxtActiveProfile.Text = "No profile selected"
            ProfileBadge.Visibility = Visibility.Collapsed
            BtnDeleteProfile.IsEnabled = False
            BtnCopyPublicKey.Visibility = Visibility.Collapsed
            BtnUseOwnPublicKey.Visibility = Visibility.Collapsed
            BtnExportPublicKey.IsEnabled = False
            BtnExportPrivateKey.IsEnabled = False
        End If

        FilesTab.Profile = _selectedProfile
        UpdateDecryptButton()
        UpdateEncryptButton()
    End Sub

    Private Async Sub Decrypt_Click(sender As Object, e As RoutedEventArgs)
        If _selectedProfile Is Nothing Then
            SetStatus("Please select a profile first.", "#F38BA8", "󰀦")
            Return
        End If

        Dim encryptedText = TxtEncrypted.Text.Trim()
        Dim passphrase = PwdPassphrase.Password

        Try
            LoadingOverlay.Visibility = Visibility.Visible
            SetStatus("Decrypting...", "#FAB387", "󱐋")

            Dim senderPub = If(_senderKey Is Nothing, Nothing, _senderKey.PublicKey)
            Dim res = Await PgpService.DecryptTextAsync(encryptedText, _selectedProfile.PrivateKey, passphrase, senderPub)

            TxtDecrypted.Text = res.Text
            TxtDecrypted.Foreground = UiBrushes.FromHex("#A6E3A1")
            BtnCopyResult.Visibility = Visibility.Visible
            PwdPassphrase.Clear()
            ShowSignatureResult(res.Signature)
        Catch ex As Exception
            TxtSignatureResult.Text = ""
            TxtDecrypted.Text = "ERROR: " & ex.Message
            TxtDecrypted.Foreground = UiBrushes.FromHex("#F38BA8")
            BtnCopyResult.Visibility = Visibility.Collapsed
            SetStatus(GetFriendlyDecryptError(ex), "#F38BA8", "󰅙")
            Dim m = ex.Message.ToLower()
            If m.Contains("bad pass") OrElse m.Contains("password") OrElse m.Contains("passphrase") OrElse m.Contains("checksum") Then
                FlashPassphraseError()
            End If
        Finally
            LoadingOverlay.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Async Sub Encrypt_Click(sender As Object, e As RoutedEventArgs)
        Dim publicKey = TxtRecipientKey.Text.Trim()
        Dim plainText = TxtPlaintext.Text

        Try
            LoadingOverlayEncrypt.Visibility = Visibility.Visible
            SetStatus("Encrypting...", "#FAB387", "󱐋")

            Dim check = PgpService.CheckRecipientKey(publicKey, DateTime.UtcNow)
            If check.Level = KeyCheckLevel.Blocked Then
                SetStatus(check.Message, "#F38BA8", "󰅙")
                Return
            End If

            Dim sign = _selectedProfile IsNot Nothing AndAlso Not String.IsNullOrEmpty(_selectedProfile.PrivateKey)
            Dim encrypted = Await PgpService.EncryptTextAsync(plainText, publicKey,
                                If(sign, _selectedProfile.PrivateKey, Nothing),
                                If(sign, PwdEncryptPassphrase.Password, Nothing))
            If sign Then PwdEncryptPassphrase.Clear()

            If check.Level = KeyCheckLevel.Warning Then
                SetStatus((If(sign, "Signed and encrypted. ", "Encrypted. ")) & check.Message, "#F9E2AF", "")
            Else
                SetStatus(If(sign, "Signed and encrypted successfully.", "Successfully encrypted."), "#A6E3A1", "󰄬")
            End If

            TxtEncryptedOutput.Text = encrypted
            TxtEncryptedOutput.ScrollToHome()
            BtnCopyEncrypted.Visibility = Visibility.Visible
        Catch ex As Exception
            SetStatus(GetFriendlyEncryptError(ex), "#F38BA8", "󰅙")
        Finally
            LoadingOverlayEncrypt.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Sub SetStatus(message As String, colorHex As String, icon As String)
        _statusTimer?.Stop()

        TxtStatus.Text = message
        TxtStatus.Foreground = UiBrushes.FromHex(colorHex)
        TxtStatusIcon.Text = icon
        TxtStatusIcon.Foreground = TxtStatus.Foreground

        ' Auto-fade success (#A6E3A1) and info (#89B4FA) messages back to Ready after 3.5 s
        If colorHex = "#A6E3A1" OrElse colorHex = "#89B4FA" Then
            If _statusTimer Is Nothing Then
                _statusTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromSeconds(3.5)}
                AddHandler _statusTimer.Tick, Sub(s, ev)
                    _statusTimer.Stop()
                    TxtStatus.Text = "Ready"
                    Dim readyBrush = UiBrushes.FromHex("#6C7086")
                    TxtStatus.Foreground = readyBrush
                    TxtStatusIcon.Text = ""
                    TxtStatusIcon.Foreground = readyBrush
                End Sub
            End If
            _statusTimer.Start()
        End If
    End Sub

    Private Sub FlashPassphraseError()
        PwdPassphrase.BorderBrush = UiBrushes.FromHex("#F38BA8")
        If _passphraseTimer Is Nothing Then
            _passphraseTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromSeconds(1.5)}
            AddHandler _passphraseTimer.Tick, Sub(s, ev)
                _passphraseTimer.Stop()
                PwdPassphrase.BorderBrush = UiBrushes.FromHex("#45475A")
            End Sub
        End If
        _passphraseTimer.Stop()
        _passphraseTimer.Start()
    End Sub

    Private Sub ResetDecryptUI()
        TxtDecrypted.Text = "Decrypted content appears here..."
        TxtDecrypted.Foreground = UiBrushes.FromHex("#45475A")
        BtnCopyResult.Visibility = Visibility.Collapsed
        PwdPassphrase.Password = String.Empty
        SetStatus("Ready — select a profile to get started.", "#6C7086", "")
    End Sub

    Private Sub UpdateDecryptButton()
        BtnDecrypt.IsEnabled = (_selectedProfile IsNot Nothing AndAlso Not String.IsNullOrEmpty(TxtEncrypted.Text.Trim()))
    End Sub

    Private Sub UpdateEncryptButton()
        Dim hasKey = Not String.IsNullOrEmpty(TxtRecipientKey.Text.Trim())
        Dim hasText = Not String.IsNullOrEmpty(TxtPlaintext.Text)
        Dim profileActive = _selectedProfile IsNot Nothing AndAlso Not String.IsNullOrEmpty(_selectedProfile.PrivateKey)
        Dim hasPassphrase = Not String.IsNullOrEmpty(PwdEncryptPassphrase.Password)
        BtnEncrypt.IsEnabled = hasKey AndAlso hasText AndAlso (Not profileActive OrElse hasPassphrase)
    End Sub

    Private Sub ShowSignatureResult(status As SignatureStatus)
        Dim name = If(_senderKey Is Nothing, "", _senderKey.Name)
        Select Case status
            Case SignatureStatus.Valid
                TxtSignatureResult.Text = ChrW(&H2714) & " Valid signature from " & name
                TxtSignatureResult.Foreground = UiBrushes.FromHex("#A6E3A1")
                SetStatus("Successfully decrypted. Signature verified.", "#A6E3A1", "󰄬")
            Case SignatureStatus.Invalid
                TxtSignatureResult.Text = ChrW(&H26A0) & " Signature INVALID or from a different key than " & name
                TxtSignatureResult.Foreground = UiBrushes.FromHex("#F38BA8")
                SetStatus("Decrypted, but the signature could not be verified. Do not trust the sender.", "#F38BA8", "󰀦")
            Case SignatureStatus.NotSigned
                TxtSignatureResult.Text = "Not signed — sender cannot be verified"
                TxtSignatureResult.Foreground = UiBrushes.FromHex("#F9E2AF")
                SetStatus("Successfully decrypted (message is not signed).", "#A6E3A1", "󰄬")
            Case Else
                TxtSignatureResult.Text = "Signature not checked (no sender key selected)"
                TxtSignatureResult.Foreground = UiBrushes.FromHex("#6C7086")
                SetStatus("Successfully decrypted.", "#A6E3A1", "󰄬")
        End Select
    End Sub

    Private Sub OpenKeyring_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New KeyringWindow() With {.Owner = Me}
        dlg.ShowDialog()
    End Sub

    Private Sub PickSenderKey_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New KeyringWindow(True) With {.Owner = Me}
        If dlg.ShowDialog() = True Then
            _senderKey = dlg.SelectedKey
            TxtSenderKey.Text = "Sender: " & _senderKey.Name
            TxtSenderKey.Foreground = UiBrushes.FromHex("#CDD6F4")
        End If
    End Sub

    Private Sub PickRecipient_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New KeyringWindow(True) With {.Owner = Me}
        If dlg.ShowDialog() = True Then TxtRecipientKey.Text = dlg.SelectedKey.PublicKey
    End Sub

    Private Sub SaveRecipient_Click(sender As Object, e As RoutedEventArgs)
        Try
            Dim k = RecipientKeyring.Default.AddOrUpdate(Nothing, TxtRecipientKey.Text.Trim())
            SetStatus("Saved to keyring: " & k.Name, "#A6E3A1", "󰄬")
        Catch ex As Exception
            SetStatus(ex.Message, "#F38BA8", "󰅙")
        End Try
    End Sub

    Private Sub UpdateRecipientStatus()
        Dim text = TxtRecipientKey.Text.Trim()
        BtnSaveRecipient.IsEnabled = False
        If text.Length = 0 OrElse Not text.Contains("-----BEGIN PGP") Then
            TxtRecipientStatus.Visibility = Visibility.Collapsed
            Return
        End If

        Dim now = DateTime.UtcNow
        Dim v = KeyValidator.ValidatePublicKey(text)
        Dim check = PgpService.CheckRecipientKey(v, now)
        TxtRecipientStatus.Visibility = Visibility.Visible
        If check.Level = KeyCheckLevel.Blocked Then
            TxtRecipientStatus.Text = ChrW(&H2718) & "  " & check.Message
            TxtRecipientStatus.Foreground = UiBrushes.FromHex("#F38BA8")
        Else
            TxtRecipientStatus.Text = ChrW(&H2714) & "  " & v.KeyInfo & "  ·  " & KeyExpiry.Describe(v.ExpiresAtUtc, now)
            TxtRecipientStatus.Foreground = UiBrushes.FromHex(If(check.Level = KeyCheckLevel.Warning, "#F9E2AF", "#A6E3A1"))
            BtnSaveRecipient.IsEnabled = True
        End If
    End Sub

    Private Function GetFriendlyDecryptError(ex As Exception) As String
        Dim msg = ex.Message.ToLower()
        If msg.Contains("bad pass") OrElse msg.Contains("password") OrElse msg.Contains("passphrase") Then
            Return "Wrong passphrase."
        ElseIf msg.Contains("checksum mismatch") Then
            Return "Error: integrity or passphrase incorrect."
        ElseIf msg.Contains("no secret key") Then
            Return "No matching private key found."
        End If
        Return "Decryption failed."
    End Function

    Private Function GetFriendlyEncryptError(ex As Exception) As String
        Dim msg = ex.Message.ToLower()
        If msg.Contains("public key") Then
            Return "Invalid public key."
        End If
        Return "Encryption failed."
    End Function

    Private Sub TxtEncrypted_TextChanged(sender As Object, e As TextChangedEventArgs)
        HintEncrypted.Visibility = If(String.IsNullOrEmpty(TxtEncrypted.Text), Visibility.Visible, Visibility.Collapsed)
        UpdateDecryptButton()
    End Sub

    Private Sub PwdPassphrase_Changed(sender As Object, e As RoutedEventArgs)
        UpdateDecryptButton()
    End Sub

    Private Sub PwdEncryptPassphrase_Changed(sender As Object, e As RoutedEventArgs)
        UpdateEncryptButton()
    End Sub

    Private Sub TxtRecipientKey_Changed(sender As Object, e As TextChangedEventArgs)
        HintRecipientKey.Visibility = If(String.IsNullOrEmpty(TxtRecipientKey.Text), Visibility.Visible, Visibility.Collapsed)
        UpdateRecipientStatus()
        UpdateEncryptButton()
    End Sub

    Private Sub TxtPlaintext_Changed(sender As Object, e As TextChangedEventArgs)
        HintPlaintext.Visibility = If(String.IsNullOrEmpty(TxtPlaintext.Text), Visibility.Visible, Visibility.Collapsed)
        UpdateEncryptButton()
    End Sub

    Private Sub ClearAll_Click(sender As Object, e As RoutedEventArgs)
        TxtEncrypted.Text = ""
        PwdPassphrase.Password = ""
        TxtSignatureResult.Text = ""
        ResetDecryptUI()
    End Sub

    Private Sub ClearEncrypt_Click(sender As Object, e As RoutedEventArgs)
        TxtPlaintext.Text = ""
        PwdEncryptPassphrase.Password = ""
        HintPlaintext.Visibility = Visibility.Visible
        TxtEncryptedOutput.Text = "Encrypted output appears here..."
        TxtEncryptedOutput.Foreground = UiBrushes.FromHex("#89DCEB")
        BtnCopyEncrypted.Visibility = Visibility.Collapsed
    End Sub

    Private Sub CopyResult_Click(sender As Object, e As RoutedEventArgs)
        Dim text = TxtDecrypted.Text
        Clipboard.SetText(text)
        SetStatus("Copied! Clipboard is cleared in " & ClipboardClearSeconds & " s.", "#A6E3A1", "󰄬")
        ScheduleClipboardClear(text)
    End Sub

    ' Decrypted plaintext should not linger in the clipboard (clipboard history excluded by the OS).
    Private Sub ScheduleClipboardClear(copiedText As String)
        _clipboardTimer?.Stop()
        _clipboardTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromSeconds(ClipboardClearSeconds)}
        AddHandler _clipboardTimer.Tick, Sub(s, ev)
            _clipboardTimer.Stop()
            Try
                If Clipboard.ContainsText() AndAlso Clipboard.GetText() = copiedText Then Clipboard.Clear()
            Catch
                ' Clipboard may be locked by another process; nothing more to do.
            End Try
        End Sub
        _clipboardTimer.Start()
    End Sub

    Private Sub CopyEncrypted_Click(sender As Object, e As RoutedEventArgs)
        Clipboard.SetText(TxtEncryptedOutput.Text)
        SetStatus("Copied!", "#A6E3A1", "󰄬")
    End Sub

    Private Sub CopyPublicKey_Click(sender As Object, e As RoutedEventArgs)
        If _selectedProfile IsNot Nothing AndAlso Not String.IsNullOrEmpty(_selectedProfile.PublicKey) Then
            Clipboard.SetText(_selectedProfile.PublicKey)
            SetStatus("Public key copied!", "#A6E3A1", "󰄬")
        End If
    End Sub

    Private Sub UseOwnPublicKey_Click(sender As Object, e As RoutedEventArgs)
        If _selectedProfile IsNot Nothing AndAlso Not String.IsNullOrEmpty(_selectedProfile.PublicKey) Then
            TxtRecipientKey.Text = _selectedProfile.PublicKey
        End If
    End Sub

    Private Sub OnDecryptOpenFileClicked(sender As Object, e As RoutedEventArgs)
        Dim dlg As New OpenFileDialog() With {
            .Title = "Open Encrypted File",
            .Filter = "PGP Files (*.asc;*.pgp;*.gpg;*.txt)|*.asc;*.pgp;*.gpg;*.txt|All Files (*.*)|*.*",
            .Multiselect = False
        }
        If dlg.ShowDialog() <> True Then Return
        Try
            Dim info As New FileInfo(dlg.FileName)
            If info.Length > 10_000_000L Then
                MessageBox.Show("File exceeds 10 MB limit.", "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            TxtEncrypted.Text = File.ReadAllText(dlg.FileName)
            SetStatus("Loaded: " & Path.GetFileName(dlg.FileName), "#89B4FA", "")
        Catch ex As Exception
            MessageBox.Show("Could not read file:" & vbCrLf & ex.Message, "File Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub OnEncryptImportRecipientKeyClicked(sender As Object, e As RoutedEventArgs)
        Dim dlg As New OpenFileDialog() With {
            .Title = "Import Recipient Public Key",
            .Filter = "PGP Key Files (*.asc;*.pgp;*.gpg;*.txt)|*.asc;*.pgp;*.gpg;*.txt|All Files (*.*)|*.*",
            .Multiselect = False
        }
        If dlg.ShowDialog() <> True Then Return
        Try
            If New FileInfo(dlg.FileName).Length > MaxKeyFileBytes Then
                MessageBox.Show("File is too large for a public key.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            TxtRecipientKey.Text = File.ReadAllText(dlg.FileName).Trim()
            SetStatus("Recipient key imported: " & Path.GetFileName(dlg.FileName), "#89B4FA", "")
        Catch ex As Exception
            MessageBox.Show("Could not read key file:" & vbCrLf & ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub OnExportPublicKeyClicked(sender As Object, e As RoutedEventArgs)
        If _selectedProfile Is Nothing OrElse String.IsNullOrEmpty(_selectedProfile.PublicKey) Then Return
        Dim safeName = String.Concat(_selectedProfile.Name.Split(Path.GetInvalidFileNameChars()))
        Dim dlg As New SaveFileDialog() With {
            .Title = "Export Public Key",
            .FileName = safeName & "-public.asc",
            .Filter = "ASCII Armored Key (*.asc)|*.asc|PGP File (*.pgp)|*.pgp|All Files (*.*)|*.*",
            .DefaultExt = "asc"
        }
        If dlg.ShowDialog() <> True Then Return
        Try
            File.WriteAllText(dlg.FileName, _selectedProfile.PublicKey, System.Text.Encoding.UTF8)
            SetStatus("Public key exported: " & Path.GetFileName(dlg.FileName), "#A6E3A1", "")
        Catch ex As Exception
            MessageBox.Show("Could not save file:" & vbCrLf & ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub OnExportPrivateKeyClicked
        If _selectedProfile Is Nothing OrElse String.IsNullOrEmpty(_selectedProfile.PrivateKey) Then Return

        Dim warn = MessageBox.Show(
            "You are about to export your PRIVATE KEY." & vbCrLf & vbCrLf &
            "Anyone who obtains this file can decrypt your messages." & vbCrLf &
            "Store it in a secure location and never share it." & vbCrLf & vbCrLf &
            "Continue with the export?",
            "Security Warning — Export Private Key",
            MessageBoxButton.YesNo, MessageBoxImage.Warning)
        If warn <> MessageBoxResult.Yes Then Return

        Dim pwdDlg As New PassphraseDialog(_selectedProfile.PrivateKey) With {.Owner = Me}
        If pwdDlg.ShowDialog() <> True Then Return

        Dim safeName = String.Concat(_selectedProfile.Name.Split(Path.GetInvalidFileNameChars()))
        Dim dlg As New SaveFileDialog() With {
            .Title = "Export Private Key",
            .FileName = safeName & "-private.asc",
            .Filter = "ASCII Armored Key (*.asc)|*.asc|PGP File (*.pgp)|*.pgp|All Files (*.*)|*.*",
            .DefaultExt = "asc"
        }
        If dlg.ShowDialog() <> True Then Return
        Try
            File.WriteAllText(dlg.FileName, _selectedProfile.PrivateKey, System.Text.Encoding.UTF8)
            SetStatus("Private key exported: " & Path.GetFileName(dlg.FileName), "#A6E3A1", "")
        Catch ex As Exception
            MessageBox.Show("Could not save file:" & vbCrLf & ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class
