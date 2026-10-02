Imports System.IO
Imports NUnit.Framework
Imports PGPKeyDesk.Services

<TestFixture>
Public Class PgpServiceTests
    Private _dir As String

    <SetUp>
    Public Sub SetUp()
        _dir = Path.Combine(Path.GetTempPath(), "pgpkd-svc-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(_dir)
    End Sub

    <TearDown>
    Public Sub TearDown()
        If Directory.Exists(_dir) Then Directory.Delete(_dir, True)
    End Sub

    Private Function P(name As String) As String
        Return Path.Combine(_dir, name)
    End Function

    ' ------------------------------------------------------------ text

    <Test>
    Public Async Function Text_RoundTrip_Unsigned() As Task
        Dim c = Await PgpService.EncryptTextAsync("hello äöü", TestKeys.Bob.Public_)
        Assert.That(c, Does.StartWith("-----BEGIN PGP MESSAGE-----"))
        Dim r = Await PgpService.DecryptTextAsync(c, TestKeys.Bob.Private_, TestKeys.Bob.Passphrase)
        Assert.That(r.Text, [Is].EqualTo("hello äöü"))
        Assert.That(r.Signature, [Is].EqualTo(SignatureStatus.NotChecked))
    End Function

    <Test>
    Public Async Function Text_Signed_VerifiesWithSendersKey() As Task
        Dim c = Await PgpService.EncryptTextAsync("signed", TestKeys.Bob.Public_, TestKeys.Alice.Private_, TestKeys.Alice.Passphrase)
        Dim r = Await PgpService.DecryptTextAsync(c, TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Alice.Public_)
        Assert.That(r.Text, [Is].EqualTo("signed"))
        Assert.That(r.Signature, [Is].EqualTo(SignatureStatus.Valid))
    End Function

    <Test>
    Public Async Function Text_Signed_WrongSenderKey_IsInvalid_ButStillDecrypts() As Task
        Dim c = Await PgpService.EncryptTextAsync("signed", TestKeys.Bob.Public_, TestKeys.Alice.Private_, TestKeys.Alice.Passphrase)
        Dim r = Await PgpService.DecryptTextAsync(c, TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Eve.Public_)
        Assert.That(r.Text, [Is].EqualTo("signed"))
        Assert.That(r.Signature, [Is].EqualTo(SignatureStatus.Invalid))
    End Function

    <Test>
    Public Async Function Text_Unsigned_WithSenderKey_IsNotSigned() As Task
        Dim c = Await PgpService.EncryptTextAsync("plain", TestKeys.Bob.Public_)
        Dim r = Await PgpService.DecryptTextAsync(c, TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Alice.Public_)
        Assert.That(r.Signature, [Is].EqualTo(SignatureStatus.NotSigned))
    End Function

    <Test>
    Public Sub Text_WrongPassphrase_Throws()
        Dim c = PgpService.EncryptTextAsync("x", TestKeys.Bob.Public_).GetAwaiter().GetResult()
        Assert.That(Sub() PgpService.DecryptTextAsync(c, TestKeys.Bob.Private_, "wrong").GetAwaiter().GetResult(), Throws.Exception)
    End Sub

    <Test>
    Public Sub Text_WrongPrivateKey_Throws()
        Dim c = PgpService.EncryptTextAsync("x", TestKeys.Bob.Public_).GetAwaiter().GetResult()
        Assert.That(Sub() PgpService.DecryptTextAsync(c, TestKeys.Eve.Private_, TestKeys.Eve.Passphrase).GetAwaiter().GetResult(), Throws.Exception)
    End Sub

    <Test>
    Public Sub Encrypt_ToExpiredKey_IsBlocked()
        ' Check logic with an injected clock; the service itself uses UtcNow, so use the check API for the future date.
        Dim chk = PgpService.CheckRecipientKey(TestKeys.ShortLived.Public_, DateTime.UtcNow.AddDays(11))
        Assert.That(chk.Level, [Is].EqualTo(KeyCheckLevel.Blocked))
        Assert.That(chk.Message, Does.Contain("EXPIRED"))
    End Sub

    <Test>
    Public Sub CheckRecipient_ExpiringSoon_Warns()
        Dim chk = PgpService.CheckRecipientKey(TestKeys.ShortLived.Public_, DateTime.UtcNow)
        Assert.That(chk.Level, [Is].EqualTo(KeyCheckLevel.Warning))
        Assert.That(chk.Message, Does.Contain("expires in"))
    End Sub

    <Test>
    Public Sub CheckRecipient_NonExpiring_IsOk()
        Assert.That(PgpService.CheckRecipientKey(TestKeys.Bob.Public_, DateTime.UtcNow.AddYears(30)).Level, [Is].EqualTo(KeyCheckLevel.Ok))
    End Sub

    <Test>
    Public Sub CheckRecipient_Garbage_IsBlocked()
        Assert.That(PgpService.CheckRecipientKey("nope", DateTime.UtcNow).Level, [Is].EqualTo(KeyCheckLevel.Blocked))
        Assert.That(PgpService.CheckRecipientKey(TestKeys.Bob.Private_, DateTime.UtcNow).Level, [Is].EqualTo(KeyCheckLevel.Blocked))
    End Sub

    <Test>
    Public Sub Encrypt_ToInvalidKey_Throws()
        Assert.That(Sub() PgpService.EncryptTextAsync("x", "nope").GetAwaiter().GetResult(), Throws.InvalidOperationException)
    End Sub

    <Test>
    Public Async Function ClearSign_Verifies_And_DetectsTampering() As Task
        Dim s = Await PgpService.SignTextAsync("contract text", TestKeys.Alice.Private_, TestKeys.Alice.Passphrase)
        Assert.That((Await PgpService.VerifyTextAsync(s, TestKeys.Alice.Public_)).IsValid, [Is].True)
        Assert.That((Await PgpService.VerifyTextAsync(s, TestKeys.Eve.Public_)).IsValid, [Is].False)
        Assert.That((Await PgpService.VerifyTextAsync(s.Replace("contract", "c0ntract"), TestKeys.Alice.Public_)).IsValid, [Is].False)
    End Function

    ' ------------------------------------------------------------ files

    Private Shared ReadOnly Binary As Byte() = Enumerable.Range(0, 5000).Select(Function(i) CByte(i Mod 256)).ToArray()

    <Test>
    Public Async Function File_RoundTrip_BinaryAndArmored() As Task
        For Each armor In {False, True}
            Dim src = P("data.bin")
            Dim enc = P("data.bin." & armor & ".pgp")
            Dim dec = P("data.out." & armor)
            File.WriteAllBytes(src, Binary)
            Await PgpService.EncryptFileAsync(src, enc, TestKeys.Bob.Public_, armor:=armor)
            Assert.That(File.ReadAllBytes(enc), [Is].Not.EqualTo(Binary))
            Dim st = Await PgpService.DecryptFileAsync(enc, dec, TestKeys.Bob.Private_, TestKeys.Bob.Passphrase)
            Assert.That(st, [Is].EqualTo(SignatureStatus.NotChecked))
            Assert.That(File.ReadAllBytes(dec), [Is].EqualTo(Binary))
        Next
    End Function

    <Test>
    Public Async Function File_Signed_VerifiesAndDetectsWrongSender() As Task
        Dim src = P("doc.bin"), enc = P("doc.pgp")
        File.WriteAllBytes(src, Binary)
        Await PgpService.EncryptFileAsync(src, enc, TestKeys.Bob.Public_, TestKeys.Alice.Private_, TestKeys.Alice.Passphrase)
        Dim ok = Await PgpService.DecryptFileAsync(enc, P("o1"), TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Alice.Public_)
        Dim bad = Await PgpService.DecryptFileAsync(enc, P("o2"), TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Eve.Public_)
        Assert.That(ok, [Is].EqualTo(SignatureStatus.Valid))
        Assert.That(bad, [Is].EqualTo(SignatureStatus.Invalid))
        Assert.That(File.ReadAllBytes(P("o1")), [Is].EqualTo(Binary))
    End Function

    <Test>
    Public Async Function File_Unsigned_WithSenderKey_IsNotSigned() As Task
        File.WriteAllBytes(P("u"), Binary)
        Await PgpService.EncryptFileAsync(P("u"), P("u.pgp"), TestKeys.Bob.Public_)
        Dim st = Await PgpService.DecryptFileAsync(P("u.pgp"), P("u.out"), TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Alice.Public_)
        Assert.That(st, [Is].EqualTo(SignatureStatus.NotSigned))
    End Function

    <Test>
    Public Sub File_WrongPassphrase_Throws()
        File.WriteAllBytes(P("w"), Binary)
        PgpService.EncryptFileAsync(P("w"), P("w.pgp"), TestKeys.Bob.Public_).GetAwaiter().GetResult()
        Assert.That(Sub() PgpService.DecryptFileAsync(P("w.pgp"), P("w.out"), TestKeys.Bob.Private_, "wrong").GetAwaiter().GetResult(), Throws.Exception)
    End Sub

    <Test>
    Public Async Function File_SignOnly_Verifies() As Task
        File.WriteAllBytes(P("s"), Binary)
        Await PgpService.SignFileAsync(P("s"), P("s.asc"), TestKeys.Alice.Private_, TestKeys.Alice.Passphrase)
        Assert.That(Await PgpService.VerifyFileAsync(P("s.asc"), TestKeys.Alice.Public_), [Is].True)
        Assert.That(Await PgpService.VerifyFileAsync(P("s.asc"), TestKeys.Eve.Public_), [Is].False)
    End Function

    <Test>
    Public Async Function File_SignOnly_TamperedFile_FailsVerification() As Task
        File.WriteAllBytes(P("t"), Binary)
        Await PgpService.SignFileAsync(P("t"), P("t.pgp"), TestKeys.Alice.Private_, TestKeys.Alice.Passphrase, armor:=False)
        Dim bytes = File.ReadAllBytes(P("t.pgp"))
        bytes(bytes.Length \ 2) = CByte(bytes(bytes.Length \ 2) Xor &HFF)
        File.WriteAllBytes(P("t.pgp"), bytes)
        Assert.That(Await PgpService.VerifyFileAsync(P("t.pgp"), TestKeys.Alice.Public_), [Is].False)
    End Function

    <Test>
    Public Async Function File_WrongPassphrase_KeepsExistingOutputUntouched() As Task
        File.WriteAllBytes(P("k"), Binary)
        Await PgpService.EncryptFileAsync(P("k"), P("k.pgp"), TestKeys.Bob.Public_)
        File.WriteAllText(P("k.out"), "precious")
        Try
            Await PgpService.DecryptFileAsync(P("k.pgp"), P("k.out"), TestKeys.Bob.Private_, "wrong")
        Catch
        End Try
        Assert.That(File.ReadAllText(P("k.out")), [Is].EqualTo("precious"))
    End Function

    <Test>
    Public Async Function File_Failure_LeavesNoOutputOrTempFiles() As Task
        File.WriteAllBytes(P("n"), Binary)
        Await PgpService.EncryptFileAsync(P("n"), P("n.pgp"), TestKeys.Bob.Public_)
        Try
            Await PgpService.DecryptFileAsync(P("n.pgp"), P("n.out"), TestKeys.Bob.Private_, "wrong")
        Catch
        End Try
        Assert.That(File.Exists(P("n.out")), [Is].False)
        Assert.That(Directory.GetFiles(_dir, "*.tmp*"), [Is].Empty)
    End Function

    <Test>
    Public Async Function File_Decrypt_WithWrongSender_StillWritesPlaintext() As Task
        File.WriteAllBytes(P("m"), Binary)
        Await PgpService.EncryptFileAsync(P("m"), P("m.pgp"), TestKeys.Bob.Public_, TestKeys.Alice.Private_, TestKeys.Alice.Passphrase)
        Dim st = Await PgpService.DecryptFileAsync(P("m.pgp"), P("m.out"), TestKeys.Bob.Private_, TestKeys.Bob.Passphrase, TestKeys.Eve.Public_)
        Assert.That(st, [Is].EqualTo(SignatureStatus.Invalid))
        Assert.That(File.ReadAllBytes(P("m.out")), [Is].EqualTo(Binary))
    End Function

    <Test>
    Public Sub CheckRecipient_ParsedOverload_MatchesTextOverload()
        Dim now = DateTime.UtcNow
        For Each keyText In {TestKeys.Bob.Public_, "garbage"}
            Dim a = PgpService.CheckRecipientKey(keyText, now)
            Dim b = PgpService.CheckRecipientKey(KeyValidator.ValidatePublicKey(keyText), now)
            Assert.That(b.Level, [Is].EqualTo(a.Level))
            Assert.That(b.Message, [Is].EqualTo(a.Message))
        Next
    End Sub
End Class
