Imports System.IO
Imports NUnit.Framework
Imports PGPKeyDesk.Services

<TestFixture>
Public Class RecipientKeyringTests
    Private _dir As String
    Private _file As String

    <SetUp>
    Public Sub SetUp()
        _dir = Path.Combine(Path.GetTempPath(), "pgpkd-ring-" & Guid.NewGuid().ToString("N"))
        _file = Path.Combine(_dir, "keyring.json")
    End Sub

    <TearDown>
    Public Sub TearDown()
        If Directory.Exists(_dir) Then Directory.Delete(_dir, True)
    End Sub

    <Test>
    Public Sub Add_PersistsAndReloads()
        Dim k = New RecipientKeyring(_file).AddOrUpdate("Bob", TestKeys.Bob.Public_)
        Dim reloaded = New RecipientKeyring(_file).Keys
        Assert.That(reloaded.Single().Name, [Is].EqualTo("Bob"))
        Assert.That(reloaded.Single().Fingerprint, [Is].EqualTo(k.Fingerprint))
    End Sub

    <Test>
    Public Sub Add_WithoutName_UsesUserId()
        Dim k = New RecipientKeyring(_file).AddOrUpdate("", TestKeys.Bob.Public_)
        Assert.That(k.Name, Does.Contain("bob@example.test"))
    End Sub

    <Test>
    Public Sub Add_SameFingerprintTwice_UpdatesInsteadOfDuplicating()
        Dim ring As New RecipientKeyring(_file)
        ring.AddOrUpdate("Bob", TestKeys.Bob.Public_)
        ring.AddOrUpdate("Robert", TestKeys.Bob.Public_)
        Assert.That(ring.Keys, Has.Count.EqualTo(1))
        Assert.That(ring.Keys.Single().Name, [Is].EqualTo("Robert"))
    End Sub

    <Test>
    Public Sub Add_InvalidKey_Throws_AndStoresNothing()
        Dim ring As New RecipientKeyring(_file)
        Assert.Throws(Of ArgumentException)(Sub() ring.AddOrUpdate("x", "not a key"))
        Assert.That(ring.Keys, [Is].Empty)
        Assert.That(File.Exists(_file), [Is].False)
    End Sub

    <Test>
    Public Sub Add_PrivateKey_IsRejected()
        Dim ring As New RecipientKeyring(_file)
        Assert.Throws(Of ArgumentException)(Sub() ring.AddOrUpdate("x", TestKeys.Alice.Private_))
    End Sub

    <Test>
    Public Sub Remove_DeletesAndPersists()
        Dim ring As New RecipientKeyring(_file)
        Dim a = ring.AddOrUpdate("A", TestKeys.Alice.Public_)
        ring.AddOrUpdate("B", TestKeys.Bob.Public_)
        Assert.That(ring.Remove(a.Id), [Is].True)
        Assert.That(New RecipientKeyring(_file).Keys.Select(Function(k) k.Name), [Is].EqualTo({"B"}))
        Assert.That(ring.Remove("unknown"), [Is].False)
    End Sub

    <Test>
    Public Sub FindByFingerprint_IsCaseInsensitive()
        Dim ring As New RecipientKeyring(_file)
        Dim a = ring.AddOrUpdate("A", TestKeys.Alice.Public_)
        Assert.That(ring.FindByFingerprint(a.Fingerprint.ToLowerInvariant()), [Is].SameAs(a))
        Assert.That(ring.FindByFingerprint("0000"), [Is].Null)
    End Sub

    <Test>
    Public Sub CorruptFile_WarnsAndKeepsOriginal()
        Directory.CreateDirectory(_dir)
        File.WriteAllText(_file, "{ not json")
        Dim ring As New RecipientKeyring(_file)
        Assert.That(ring.Keys, [Is].Empty)
        Assert.That(ring.LastLoadWarning, [Is].Not.Null)
        Assert.That(Directory.GetFiles(_dir, "keyring.json.unreadable-*"), Has.Length.EqualTo(1))
    End Sub
End Class
