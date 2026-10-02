Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports NUnit.Framework
Imports PGPKeyDesk.Models
Imports PGPKeyDesk.Services

<TestFixture>
Public Class ProfileStoreTests
    ''' <summary>Reversible fake: XOR with a marker byte prefix; rejects anything else like DPAPI would.</summary>
    Private Class FakeProtector
        Implements ISecretProtector

        Public Function Protect(plain As Byte()) As Byte() Implements ISecretProtector.Protect
            Return New Byte() {&HAA}.Concat(plain.Select(Function(b) CByte(b Xor &H5A))).ToArray()
        End Function

        Public Function Unprotect(data As Byte()) As Byte() Implements ISecretProtector.Unprotect
            If data.Length = 0 OrElse data(0) <> &HAA Then Throw New CryptographicException("not protected")
            Return data.Skip(1).Select(Function(b) CByte(b Xor &H5A)).ToArray()
        End Function
    End Class

    Private _dir As String
    Private _store As String
    Private _legacy As String

    <SetUp>
    Public Sub SetUp()
        _dir = Path.Combine(Path.GetTempPath(), "pgpkd-tests-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(_dir)
        _store = Path.Combine(_dir, "new", "profiles.json")
        _legacy = Path.Combine(_dir, "old", "profiles.json")
    End Sub

    <TearDown>
    Public Sub TearDown()
        If Directory.Exists(_dir) Then Directory.Delete(_dir, True)
    End Sub

    Private Function NewStore() As ProfileStore
        Return New ProfileStore(_store, _legacy, New FakeProtector())
    End Function

    Private Shared Function Profile(name As String) As PGPProfile
        Return New PGPProfile With {.Name = name, .PrivateKey = "PRIV-" & name, .PublicKey = "PUB-" & name}
    End Function

    <Test>
    Public Sub Load_WithoutFile_ReturnsEmpty()
        Assert.That(NewStore().LoadProfiles(), [Is].Empty)
        Assert.That(NewStore().LastLoadWarning, [Is].Null)
    End Sub

    <Test>
    Public Sub SaveThenLoad_RoundTrips()
        Dim s = NewStore()
        s.SaveProfiles(New List(Of PGPProfile) From {Profile("a"), Profile("b")})
        Dim loaded = NewStore().LoadProfiles()
        Assert.That(loaded.Select(Function(p) p.Name), [Is].EqualTo({"a", "b"}))
        Assert.That(loaded(0).PrivateKey, [Is].EqualTo("PRIV-a"))
        Assert.That(loaded(1).PublicKey, [Is].EqualTo("PUB-b"))
    End Sub

    <Test>
    Public Sub Save_WritesEncryptedFile_NoPlaintextKeys()
        NewStore().SaveProfiles(New List(Of PGPProfile) From {Profile("secret")})
        Dim text = Encoding.UTF8.GetString(File.ReadAllBytes(_store))
        Assert.That(text, Does.Not.Contain("PRIV-secret"))
    End Sub

    <Test>
    Public Sub Save_LeavesNoTempFileBehind()
        NewStore().SaveProfiles(New List(Of PGPProfile) From {Profile("a")})
        Assert.That(File.Exists(_store & ".tmp"), [Is].False)
    End Sub

    <Test>
    Public Sub Save_Overwrites_PreviousContent()
        Dim s = NewStore()
        s.SaveProfiles(New List(Of PGPProfile) From {Profile("a"), Profile("b")})
        s.SaveProfiles(New List(Of PGPProfile) From {Profile("c")})
        Assert.That(NewStore().LoadProfiles().Select(Function(p) p.Name), [Is].EqualTo({"c"}))
    End Sub

    <Test>
    Public Sub Load_PlaintextLegacyFile_IsMigratedToEncrypted()
        Directory.CreateDirectory(Path.GetDirectoryName(_store))
        File.WriteAllText(_store, "[{""Id"":""1"",""Name"":""old"",""PrivateKey"":""P"",""PublicKey"":""U""}]")
        Dim loaded = NewStore().LoadProfiles()
        Assert.That(loaded.Single().Name, [Is].EqualTo("old"))
        Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(_store)), Does.Not.Contain("""Name"""))
        Assert.That(NewStore().LoadProfiles().Single().Name, [Is].EqualTo("old"))
    End Sub

    <Test>
    Public Sub Load_CopiesLegacyOpenGpgStore_WhenNewStoreMissing()
        Directory.CreateDirectory(Path.GetDirectoryName(_legacy))
        File.WriteAllBytes(_legacy, New FakeProtector().Protect(
            Encoding.UTF8.GetBytes("[{""Id"":""1"",""Name"":""legacy"",""PrivateKey"":""P"",""PublicKey"":""""}]")))
        Assert.That(NewStore().LoadProfiles().Single().Name, [Is].EqualTo("legacy"))
        Assert.That(File.Exists(_legacy), [Is].True, "legacy file must not be deleted")
    End Sub

    <Test>
    Public Sub Load_CorruptFile_ReturnsEmpty_WarnsAndKeepsFile()
        Directory.CreateDirectory(Path.GetDirectoryName(_store))
        File.WriteAllBytes(_store, {1, 2, 3, 4, 5})
        Dim s = NewStore()
        Assert.That(s.LoadProfiles(), [Is].Empty)
        Assert.That(s.LastLoadWarning, [Is].Not.Null.And.Contains("unreadable"))
        Dim kept = Directory.GetFiles(Path.GetDirectoryName(_store), "profiles.json.unreadable-*")
        Assert.That(kept, Has.Length.EqualTo(1))
        Assert.That(File.ReadAllBytes(kept(0)), [Is].EqualTo(New Byte() {1, 2, 3, 4, 5}))
    End Sub

    <Test>
    Public Sub Save_AfterCorruptLoad_DoesNotDestroyOriginal()
        Directory.CreateDirectory(Path.GetDirectoryName(_store))
        File.WriteAllBytes(_store, {9, 9, 9})
        Dim s = NewStore()
        s.LoadProfiles()
        s.SaveProfiles(New List(Of PGPProfile) From {Profile("new")})
        Dim kept = Directory.GetFiles(Path.GetDirectoryName(_store), "profiles.json.unreadable-*")
        Assert.That(File.ReadAllBytes(kept.Single()), [Is].EqualTo(New Byte() {9, 9, 9}))
    End Sub

    <Test>
    Public Sub Load_JsonNull_ReturnsEmptyList()
        Directory.CreateDirectory(Path.GetDirectoryName(_store))
        File.WriteAllBytes(_store, New FakeProtector().Protect(Encoding.UTF8.GetBytes("null")))
        Assert.That(NewStore().LoadProfiles(), [Is].Empty)
    End Sub

    <Test>
    Public Sub Load_UnicodeNames_Survive()
        NewStore().SaveProfiles(New List(Of PGPProfile) From {Profile("Müller ✓ 日本")})
        Assert.That(NewStore().LoadProfiles().Single().Name, [Is].EqualTo("Müller ✓ 日本"))
    End Sub

    <Test>
    Public Sub Dpapi_RoundTrip_OnWindows()
        If Not OperatingSystem.IsWindows() Then Assert.Ignore("DPAPI is Windows-only.")
        Dim p As New DpapiProtector()
        Dim data = Encoding.UTF8.GetBytes("secret")
        Dim enc = p.Protect(data)
        Assert.That(enc, [Is].Not.EqualTo(data))
        Assert.That(p.Unprotect(enc), [Is].EqualTo(data))
        Assert.Throws(Of CryptographicException)(Sub() p.Unprotect({1, 2, 3}))
    End Sub
End Class
