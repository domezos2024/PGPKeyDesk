Imports System.IO
Imports Microsoft.Win32
Imports PGPKeyDesk.Models
Imports PGPKeyDesk.Services

Class KeyringWindow
    Private Const MaxKeyFileBytes As Long = 1000000L

    ''' <summary>Set when the window was opened to pick a key (see New(pickMode)).</summary>
    Public Property SelectedKey As RecipientKey

    Private ReadOnly _pickMode As Boolean

    Public Sub New(Optional pickMode As Boolean = False)
        InitializeComponent()
        _pickMode = pickMode
        BtnSelect.Visibility = If(pickMode, Visibility.Visible, Visibility.Collapsed)
        RefreshList()
        If RecipientKeyring.Default.LastLoadWarning IsNot Nothing Then
            ShowMessage(RecipientKeyring.Default.LastLoadWarning, "#F38BA8")
        End If
    End Sub

    ''' <summary>One-line description (name, fingerprint, expiry) for list entries, shared with the main window.</summary>
    Public Shared Function Describe(k As RecipientKey, nowUtc As DateTime) As String
        Dim v = KeyValidator.ValidatePublicKey(k.PublicKey)
        Dim fp = If(k.Fingerprint.Length >= 16, k.Fingerprint.Substring(k.Fingerprint.Length - 16), k.Fingerprint)
        Dim expiry = If(v.IsValid, KeyExpiry.Describe(v.ExpiresAtUtc, nowUtc), "unreadable key")
        If v.IsValid AndAlso v.IsRevoked Then expiry = "REVOKED"
        Return k.Name & "   [" & fp & "]   " & expiry
    End Function

    Private Sub RefreshList()
        Dim now = DateTime.UtcNow
        KeyList.Items.Clear()
        For Each k In RecipientKeyring.Default.Keys
            Dim item As New ListBoxItem() With {.Content = Describe(k, now), .Tag = k}
            Dim v = KeyValidator.ValidatePublicKey(k.PublicKey)
            Dim st = If(v.IsValid, v.GetExpiryStatus(now), KeyExpiryStatus.Expired)
            If (v.IsValid AndAlso v.IsRevoked) OrElse st = KeyExpiryStatus.Expired Then
                item.Foreground = Brush("#F38BA8")
            ElseIf st = KeyExpiryStatus.ExpiringSoon Then
                item.Foreground = Brush("#F9E2AF")
            End If
            item.Style = CType(FindResource("ProfileItem"), Style)
            KeyList.Items.Add(item)
        Next
        TxtEmpty.Visibility = If(KeyList.Items.Count = 0, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Shared Function Brush(hex As String) As SolidColorBrush
        Return New SolidColorBrush(CType(ColorConverter.ConvertFromString(hex), Color))
    End Function

    Private Sub ShowMessage(text As String, colorHex As String)
        TxtMessage.Text = text
        TxtMessage.Foreground = Brush(colorHex)
    End Sub

    Private Function SelectedRecipient() As RecipientKey
        Dim item = TryCast(KeyList.SelectedItem, ListBoxItem)
        Return If(item Is Nothing, Nothing, TryCast(item.Tag, RecipientKey))
    End Function

    Private Sub KeyList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        BtnRemove.IsEnabled = SelectedRecipient() IsNot Nothing
        BtnSelect.IsEnabled = SelectedRecipient() IsNot Nothing
    End Sub

    Private Sub KeyList_DoubleClick(sender As Object, e As MouseButtonEventArgs)
        If _pickMode AndAlso SelectedRecipient() IsNot Nothing Then Select_Click(sender, Nothing)
    End Sub

    Private Sub TxtKey_Changed(sender As Object, e As TextChangedEventArgs)
        Dim text = TxtKey.Text.Trim()
        If text.Length = 0 Then
            TxtKeyStatus.Visibility = Visibility.Collapsed
            BtnAdd.IsEnabled = False
            Return
        End If
        Dim v = KeyValidator.ValidatePublicKey(text)
        TxtKeyStatus.Visibility = Visibility.Visible
        Dim now = DateTime.UtcNow
        If Not v.IsValid Then
            TxtKeyStatus.Text = ChrW(&H2718) & "  " & v.ErrorMessage
            TxtKeyStatus.Foreground = Brush("#F38BA8")
            BtnAdd.IsEnabled = False
        ElseIf v.IsRevoked OrElse Not v.HasEncryptionKey OrElse v.GetExpiryStatus(now) = KeyExpiryStatus.Expired Then
            TxtKeyStatus.Text = ChrW(&H2718) & "  " & v.KeyInfo & "  ·  " &
                If(v.IsRevoked, "revoked", If(Not v.HasEncryptionKey, "no encryption key", KeyExpiry.Describe(v.ExpiresAtUtc, now)))
            TxtKeyStatus.Foreground = Brush("#F38BA8")
            BtnAdd.IsEnabled = False
        Else
            Dim warn = v.GetExpiryStatus(now) = KeyExpiryStatus.ExpiringSoon
            TxtKeyStatus.Text = ChrW(&H2714) & "  " & v.KeyInfo & "  ·  " & KeyExpiry.Describe(v.ExpiresAtUtc, now)
            TxtKeyStatus.Foreground = Brush(If(warn, "#F9E2AF", "#A6E3A1"))
            BtnAdd.IsEnabled = True
        End If
    End Sub

    Private Sub LoadFile_Click(sender As Object, e As RoutedEventArgs)
        Dim dlg As New OpenFileDialog() With {
            .Title = "Load Public Key",
            .Filter = "PGP Key Files (*.asc;*.pgp;*.gpg;*.txt)|*.asc;*.pgp;*.gpg;*.txt|All Files (*.*)|*.*",
            .CheckFileExists = True}
        If dlg.ShowDialog() <> True Then Return
        Try
            If New FileInfo(dlg.FileName).Length > MaxKeyFileBytes Then
                ShowMessage("File is too large for a public key.", "#F38BA8")
                Return
            End If
            TxtKey.Text = File.ReadAllText(dlg.FileName).Trim()
        Catch ex As Exception
            ShowMessage("Could not read file: " & ex.Message, "#F38BA8")
        End Try
    End Sub

    Private Sub Add_Click(sender As Object, e As RoutedEventArgs)
        Try
            Dim k = RecipientKeyring.Default.AddOrUpdate(TxtName.Text, TxtKey.Text)
            TxtKey.Text = ""
            TxtName.Text = ""
            RefreshList()
            ShowMessage("Saved """ & k.Name & """.", "#A6E3A1")
        Catch ex As Exception
            ShowMessage(ex.Message, "#F38BA8")
        End Try
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        Dim k = SelectedRecipient()
        If k Is Nothing Then Return
        If MessageBox.Show("Remove """ & k.Name & """ from the keyring?", "Remove Key",
                           MessageBoxButton.YesNo, MessageBoxImage.Question) <> MessageBoxResult.Yes Then Return
        RecipientKeyring.Default.Remove(k.Id)
        RefreshList()
    End Sub

    Private Sub Select_Click(sender As Object, e As RoutedEventArgs)
        SelectedKey = SelectedRecipient()
        If SelectedKey IsNot Nothing Then DialogResult = True
    End Sub

    Private Sub Close_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub
End Class
