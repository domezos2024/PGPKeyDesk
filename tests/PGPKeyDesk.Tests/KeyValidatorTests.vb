Imports NUnit.Framework
Imports PGPKeyDesk.Services

<TestFixture>
Public Class KeyValidatorTests

    <Test>
    Public Sub PrivateKey_Valid()
        Dim r = KeyValidator.ValidatePrivateKey(TestKeys.Alice.Private_)
        Assert.That(r.IsValid, [Is].True, r.ErrorMessage)
        Assert.That(r.KeyInfo, Does.Contain("RSA 2048"))
        Assert.That(r.KeyInfo, Does.Contain("alice@example.test"))
        Assert.That(r.Fingerprint, Has.Length.EqualTo(40))
    End Sub

    <Test>
    Public Sub PublicKey_Valid()
        Dim r = KeyValidator.ValidatePublicKey(TestKeys.Alice.Public_)
        Assert.That(r.IsValid, [Is].True, r.ErrorMessage)
        Assert.That(r.UserId, Does.Contain("alice@example.test"))
        Assert.That(r.IsRevoked, [Is].False)
        Assert.That(r.HasEncryptionKey, [Is].True)
    End Sub

    <Test>
    Public Sub PublicAndPrivateKey_ShareFingerprint()
        Assert.That(KeyValidator.ValidatePrivateKey(TestKeys.Alice.Private_).Fingerprint,
                    [Is].EqualTo(KeyValidator.ValidatePublicKey(TestKeys.Alice.Public_).Fingerprint))
    End Sub

    <Test>
    Public Sub DifferentKeys_HaveDifferentFingerprints()
        Assert.That(KeyValidator.ValidatePublicKey(TestKeys.Alice.Public_).Fingerprint,
                    [Is].Not.EqualTo(KeyValidator.ValidatePublicKey(TestKeys.Bob.Public_).Fingerprint))
    End Sub

    <Test>
    Public Sub PrivateKey_PassedAsPublic_IsRejected()
        Dim r = KeyValidator.ValidatePublicKey(TestKeys.Alice.Private_)
        Assert.That(r.IsValid, [Is].False)
        Assert.That(r.ErrorMessage, Does.Contain("PUBLIC KEY BLOCK"))
    End Sub

    <Test>
    Public Sub PublicKey_PassedAsPrivate_IsRejected()
        Dim r = KeyValidator.ValidatePrivateKey(TestKeys.Alice.Public_)
        Assert.That(r.IsValid, [Is].False)
        Assert.That(r.ErrorMessage, Does.Contain("PRIVATE KEY BLOCK"))
    End Sub

    <TestCase("")>
    <TestCase("   ")>
    <TestCase("hello world")>
    Public Sub Garbage_IsRejected(text As String)
        Assert.That(KeyValidator.ValidatePrivateKey(text).IsValid, [Is].False)
        Assert.That(KeyValidator.ValidatePublicKey(text).IsValid, [Is].False)
    End Sub

    <Test>
    Public Sub Nothing_IsRejectedWithoutException()
        Assert.That(KeyValidator.ValidatePrivateKey(Nothing).IsValid, [Is].False)
        Assert.That(KeyValidator.ValidatePublicKey(Nothing).IsValid, [Is].False)
    End Sub

    <Test>
    Public Sub CorruptedArmor_IsRejectedWithMessage()
        Dim broken = "-----BEGIN PGP PUBLIC KEY BLOCK-----" & vbLf & vbLf & "AAAA!!!notbase64" & vbLf & "-----END PGP PUBLIC KEY BLOCK-----"
        Dim r = KeyValidator.ValidatePublicKey(broken)
        Assert.That(r.IsValid, [Is].False)
        Assert.That(r.ErrorMessage, [Is].Not.Empty)
    End Sub

    <Test>
    Public Sub NonExpiringKey_HasNoExpiry()
        Dim r = KeyValidator.ValidatePublicKey(TestKeys.Alice.Public_)
        Assert.That(r.ExpiresAtUtc.HasValue, [Is].False)
        Assert.That(r.GetExpiryStatus(DateTime.UtcNow.AddYears(50)), [Is].EqualTo(KeyExpiryStatus.NeverExpires))
    End Sub

    <Test>
    Public Sub ExpiringKey_ReportsExpiryAndStatusOverTime()
        Dim r = KeyValidator.ValidatePublicKey(TestKeys.ShortLived.Public_)
        Assert.That(r.IsValid, [Is].True, r.ErrorMessage)
        Assert.That(r.ExpiresAtUtc.HasValue, [Is].True)
        Dim exp = r.ExpiresAtUtc.Value
        Assert.That((exp - DateTime.UtcNow).TotalDays, [Is].InRange(9.0, 10.1))
        Assert.That(r.GetExpiryStatus(exp.AddDays(-40)), [Is].EqualTo(KeyExpiryStatus.Valid))
        Assert.That(r.GetExpiryStatus(exp.AddDays(-5)), [Is].EqualTo(KeyExpiryStatus.ExpiringSoon))
        Assert.That(r.GetExpiryStatus(exp.AddSeconds(1)), [Is].EqualTo(KeyExpiryStatus.Expired))
    End Sub

    <Test>
    Public Sub PrivateKey_AlsoReportsExpiry()
        Dim r = KeyValidator.ValidatePrivateKey(TestKeys.ShortLived.Private_)
        Assert.That(r.ExpiresAtUtc.HasValue, [Is].True)
    End Sub
End Class
