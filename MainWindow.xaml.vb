Imports System.IO
Imports OpenGPG.Models
Imports OpenGPG.Services
Imports PgpCore
Imports Microsoft.Win32
Imports System.Windows.Media
Imports System.Windows.Threading

Class MainWindow
    Private _profiles As List(Of PGPProfile)
    Private _selectedProfile As PGPProfile
    Private _statusTimer As DispatcherTimer
    Private _passphraseTimer As DispatcherTimer

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        _profiles = ProfileStore.Load()
        RefreshProfileList()
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

            Dim pgp As New PGP(New EncryptionKeys(_selectedProfile.PrivateKey, passphrase))
            Dim decrypted = Await pgp.DecryptArmoredStringAsync(encryptedText)

            TxtDecrypted.Text = decrypted
            TxtDecrypted.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#A6E3A1"), Color))
            BtnCopyResult.Visibility = Visibility.Visible
            SetStatus("Successfully decrypted.", "#A6E3A1", "󰄬")
        Catch ex As Exception
            TxtDecrypted.Text = "ERROR: " & ex.Message
            TxtDecrypted.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#F38BA8"), Color))
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

            Dim encrypted As String
            If _selectedProfile IsNot Nothing AndAlso Not String.IsNullOrEmpty(_selectedProfile.PrivateKey) Then
                Dim keys As New EncryptionKeys(publicKey, _selectedProfile.PrivateKey, PwdEncryptPassphrase.Password)
                Dim pgp As New PGP(keys)
                encrypted = Await pgp.EncryptArmoredStringAndSignAsync(plainText)
                SetStatus("Signed and encrypted successfully.", "#A6E3A1", "󰄬")
            Else
                Dim pgp As New PGP(New EncryptionKeys(publicKey))
                encrypted = Await pgp.EncryptArmoredStringAsync(plainText)
                SetStatus("Successfully encrypted.", "#A6E3A1", "󰄬")
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
        TxtStatus.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString(colorHex), Color))
        TxtStatusIcon.Text = icon
        TxtStatusIcon.Foreground = TxtStatus.Foreground

        ' Auto-fade success (#A6E3A1) and info (#89B4FA) messages back to Ready after 3.5 s
        If colorHex = "#A6E3A1" OrElse colorHex = "#89B4FA" Then
            If _statusTimer Is Nothing Then
                _statusTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromSeconds(3.5)}
                AddHandler _statusTimer.Tick, Sub(s, ev)
                    _statusTimer.Stop()
                    TxtStatus.Text = "Ready"
                    Dim readyBrush = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#6C7086"), Color))
                    TxtStatus.Foreground = readyBrush
                    TxtStatusIcon.Text = ""
                    TxtStatusIcon.Foreground = readyBrush
                End Sub
            End If
            _statusTimer.Start()
        End If
    End Sub

    Private Sub FlashPassphraseError()
        PwdPassphrase.BorderBrush = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#F38BA8"), Color))
        If _passphraseTimer Is Nothing Then
            _passphraseTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromSeconds(1.5)}
            AddHandler _passphraseTimer.Tick, Sub(s, ev)
                _passphraseTimer.Stop()
                PwdPassphrase.BorderBrush = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#45475A"), Color))
            End Sub
        End If
        _passphraseTimer.Stop()
        _passphraseTimer.Start()
    End Sub

    Private Sub ResetDecryptUI()
        TxtDecrypted.Text = "Decrypted content appears here..."
        TxtDecrypted.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#45475A"), Color))
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
        UpdateEncryptButton()
    End Sub

    Private Sub TxtPlaintext_Changed(sender As Object, e As TextChangedEventArgs)
        HintPlaintext.Visibility = If(String.IsNullOrEmpty(TxtPlaintext.Text), Visibility.Visible, Visibility.Collapsed)
        UpdateEncryptButton()
    End Sub

    Private Sub ClearAll_Click(sender As Object, e As RoutedEventArgs)
        TxtEncrypted.Text = ""
        PwdPassphrase.Password = ""
        ResetDecryptUI()
    End Sub

    Private Sub ClearEncrypt_Click(sender As Object, e As RoutedEventArgs)
        TxtPlaintext.Text = ""
        PwdEncryptPassphrase.Password = ""
        HintPlaintext.Visibility = Visibility.Visible
        TxtEncryptedOutput.Text = "Encrypted output appears here..."
        TxtEncryptedOutput.Foreground = New SolidColorBrush(Color.FromRgb(&H89, &HDC, &HEB))
        BtnCopyEncrypted.Visibility = Visibility.Collapsed
    End Sub

    Private Sub CopyResult_Click(sender As Object, e As RoutedEventArgs)
        Clipboard.SetText(TxtDecrypted.Text)
        SetStatus("Copied!", "#A6E3A1", "󰄬")
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
